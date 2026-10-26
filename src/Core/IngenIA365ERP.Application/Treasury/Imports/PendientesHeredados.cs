using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Application.Treasury.Payables;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Imports;

/// <summary>
/// La plantilla <c>tesoreria.pendientes-heredados</c> (feature 015, fase 46, T0472; contracts/archivos-y-plantillas.md §5; data-model §24; FR-161): lo pendiente del módulo
/// heredado de Tesorería que tiene que seguir vivo después de su retiro. Cada fila es un <b>cheque girado sin cobrar</b> —nace como cheque girado en tránsito de la cuenta,
/// como los de la puesta en marcha (<c>IsOpeningLoad</c>), dentro de una chequera registrada— o una <b>factura por pagar</b> —nace como orden de la bandeja contra la cuenta
/// por pagar contable de su tercero y su documento cruce (la que dejó la apertura de Contabilidad), con el mismo camino de
/// <see cref="ConvertAccountingPayableCommand"/>—, y queda anotada en <c>TES_LegacyPendingLoads</c> con la referencia de la fila heredada (su <c>Id</c> en la exportación) y
/// lo que nació (<c>MigratedToPublicId</c>). Todo o nada (el motor de plantillas); sin hechos contables: lo heredado ya está en los saldos de apertura. (nuevo)
/// </summary>
public static class PlantillaDePendientesHeredados
{
    public const string Clave = "tesoreria.pendientes-heredados";

    public const string ClaseDePendiente = "clase";
    public const string Referencia = "referencia";
    public const string Cuenta = "cuenta";
    public const string Numero = "numero";
    public const string Beneficiario = "beneficiario";
    public const string Valor = "valor";
    public const string Fecha = "fecha";
    public const string Factura = "factura";
    public const string Entregado = "entregado";

    /// <summary>El concepto de las cifras de referencia con el que se coteja el total de cada clase (dimensión libre; cuenta la última cargada).</summary>
    public const string ConceptoDeCheques = "PendientesHeredadosCheques";

    public const string ConceptoDeFacturas = "PendientesHeredadosFacturas";

    /// <summary>Las clases por su nombre en la plantilla.</summary>
    public static readonly IReadOnlyDictionary<string, LegacyPendingKind> Clases = new Dictionary<string, LegacyPendingKind>(StringComparer.OrdinalIgnoreCase)
    {
        ["ChequeSinCobrar"] = LegacyPendingKind.UncashedCheck,
        ["FacturaPorPagar"] = LegacyPendingKind.PayableInvoice,
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Pendientes de la Tesorería anterior", ModuloDeAuditoria.Treasury,
    [
        new HojaDePlantilla("Datos",
        [
            new(ClaseDePendiente, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "ChequeSinCobrar o FacturaPorPagar", Ejemplo: "ChequeSinCobrar"),
            new(Referencia, TipoDeValor.Texto, Obligatoria: true, Largo: 60, Reglas: "el Id de la fila en la exportación de la Tesorería anterior; no se repite por clase"),
            new(Cuenta, TipoDeValor.Codigo, Largo: 10, Reglas: "cheques: el código de la cuenta bancaria propia"),
            new(Numero, TipoDeValor.Entero, Reglas: "cheques: el número, dentro de una chequera registrada de la cuenta"),
            new(Beneficiario, TipoDeValor.Persona, Obligatoria: true, Largo: 20, Reglas: "documento del beneficiario del cheque o del proveedor de la factura"),
            new(Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "lo pendiente: el valor del cheque o el saldo por pagar de la factura"),
            new(Fecha, TipoDeValor.Fecha, Obligatoria: true, Reglas: "cheques: la fecha de giro; facturas: el vencimiento (la orden se paga desde hoy si ya venció)"),
            new(Factura, TipoDeValor.Texto, Largo: 40, Reglas: "facturas: el documento cruce de la cuenta por pagar contable del proveedor"),
            new(Entregado, TipoDeValor.SiNo, Reglas: "cheques: vacío = no"),
        ]),
    ]);
}

/// <summary>
/// <c>POST /api/treasury/imports/tesoreria.pendientes-heredados/import?mode=review|apply</c> (<c>Treasury.Imports.Run</c>, con clave; fase 46, T0472): carga los pendientes
/// de la Tesorería anterior antes de su retiro. (nuevo)
/// </summary>
public sealed record ImportLegacyTreasuryPendingCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportLegacyTreasuryPendingCommandValidator : AbstractValidator<ImportLegacyTreasuryPendingCommand>
{
    public ImportLegacyTreasuryPendingCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportLegacyTreasuryPendingCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    IContabilidadParaTesoreria contabilidad,
    IActorActual actorActual,
    NumeradorDeTesoreria numerador,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    IDateTimeService reloj) : IRequestHandler<ImportLegacyTreasuryPendingCommand, Result<ImportResultDto>>
{
    /// <summary>La factura no tiene cuenta por pagar contable con ese documento cruce, o lo pendiente no alcanza (nuevo, DT §2.7).</summary>
    public const string CodigoSinCuentaPorPagar = "Treasury.Imports.LegacyPayableNotFound";

    /// <summary>El total cargado de una clase no coincide con su cifra de referencia (aviso, no bloquea; nuevo, DT §2.7).</summary>
    public const string CodigoDiferenciaConReferencia = "Treasury.Imports.LegacyTotalDiffers";

    public Task<Result<ImportResultDto>> Handle(ImportLegacyTreasuryPendingCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(PlantillaDePendientesHeredados.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        const string P = PlantillaDePendientesHeredados.Referencia;
        var actor = await actorActual.ObtenerAsync(ct);
        var hoy = reloj.HoyLocal;
        var aplicar = ctx.Modo == ModoDeImportacion.Apply;
        var cuentas = CatalogoCitado<OwnBankAccount>.Desde(await db.OwnBankAccounts.AsNoTracking().ToListAsync(ct), c => c, c => c.Code);
        var chequeras = await db.Checkbooks.AsNoTracking().Where(k => k.Status != CheckbookState.Voided).ToListAsync(ct);
        var usados = await db.IssuedChecks.AsNoTracking().Select(c => new { c.CheckbookId, c.CheckNumber }).ToListAsync(ct);
        var documentos = ctx.Datos.Filas.Select(f => f.Crudo(PlantillaDePendientesHeredados.Beneficiario)).OfType<string>().Distinct().ToList();
        var gente = await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId)).Select(p => new { p.Id, p.PublicId, p.TaxId }).ToListAsync(ct);
        var personas = CatalogoCitado<int>.Desde(gente, p => p.Id, p => p.TaxId);
        var cargadas = (await db.TreasuryLegacyPendingLoads.AsNoTracking().Select(l => new { l.Kind, l.LegacyReference }).ToListAsync(ct))
            .Select(l => (l.Kind, l.LegacyReference)).ToHashSet();
        var porPagar = new Dictionary<int, IReadOnlyList<CuentaPorPagarDto>>();
        var tomado = new Dictionary<(int, string), decimal>();
        var totales = new Dictionary<LegacyPendingKind, (int Filas, decimal Valor)>();

        foreach (var fila in ctx.Datos.Filas)
        {
            var clase = fila.Enumeracion(PlantillaDePendientesHeredados.ClaseDePendiente, PlantillaDePendientesHeredados.Clases);
            var referencia = fila.Texto(P, 60)?.Trim();
            var beneficiario = fila.Referencia(PlantillaDePendientesHeredados.Beneficiario, personas, "una persona con documento", "en Personas");
            var valor = fila.Monto(PlantillaDePendientesHeredados.Valor);
            var fecha = fila.Fecha(PlantillaDePendientesHeredados.Fecha);
            if (valor is <= 0m) fila.Error(PlantillaDePendientesHeredados.Valor, ImportErrors.CellFormat, "El valor tiene que ser mayor que cero.");
            if (clase is { } k && referencia is not null)
            {
                ctx.Datos.LlaveUnica(fila, $"{k}|{referencia}", P);
                if (cargadas.Contains((k, referencia)))
                    fila.Error(P, ImportErrors.RowDuplicate, $"El pendiente heredado «{referencia}» ya se cargó.");
            }

            LegacyPendingLoad? carga = null;
            if (clase == LegacyPendingKind.UncashedCheck)
                carga = Cheque(ctx, fila, cuentas, chequeras, usados.Select(u => (u.CheckbookId, u.CheckNumber)).ToList(), referencia, beneficiario, valor, fecha);
            else if (clase == LegacyPendingKind.PayableInvoice)
                carga = await FacturaAsync(fila, gente.FirstOrDefault(g => g.Id == beneficiario)?.PublicId, porPagar, tomado, referencia, beneficiario, valor, fecha, hoy, aplicar, ct);

            if (fila.TieneErrores || carga is null || clase is not { } kind || actor.UserId is not { } usuario) continue;
            carga.LoadedByUserId = usuario;
            carga.LoadedAt = reloj.UtcNow;
            db.TreasuryLegacyPendingLoads.Add(carga);
            totales[kind] = totales.TryGetValue(kind, out var t) ? (t.Filas + 1, t.Valor + carga.Amount) : (1, carga.Amount);
            ctx.Registrar(fila, $"{kind}|{referencia}", AccionDeImportacion.Create);
        }

        // El cotejo: el total de cada clase contra su cifra de referencia (la última cargada), si la hay. Avisa; no bloquea.
        var cifras = await db.TreasuryReferenceFigures.AsNoTracking()
            .Where(c => c.Concept == PlantillaDePendientesHeredados.ConceptoDeCheques || c.Concept == PlantillaDePendientesHeredados.ConceptoDeFacturas)
            .ToListAsync(ct);
        foreach (var (kind, concepto) in new[] { (LegacyPendingKind.UncashedCheck, PlantillaDePendientesHeredados.ConceptoDeCheques),
                     (LegacyPendingKind.PayableInvoice, PlantillaDePendientesHeredados.ConceptoDeFacturas) })
        {
            var cargado = totales.TryGetValue(kind, out var t) ? t.Valor : 0m;
            var ultima = cifras.Where(c => c.Concept == concepto).OrderByDescending(c => c.CutoffDate).ThenByDescending(c => c.Id).Select(c => c.CutoffDate).FirstOrDefault();
            if (cifras.All(c => c.Concept != concepto)) continue;
            var referencia = cifras.Where(c => c.Concept == concepto && c.CutoffDate == ultima).Sum(c => c.Amount);
            if (referencia != cargado && ctx.Datos.Filas.FirstOrDefault() is { } primera)
                ctx.Aviso(primera, PlantillaDePendientesHeredados.Valor, CodigoDiferenciaConReferencia,
                    string.Create(CultureInfo.InvariantCulture, $"Lo cargado de {kind} suma {cargado:N0} y la cifra de referencia {concepto} al {ultima:yyyy-MM-dd} dice {referencia:N0}."));
        }
        ctx.Extra["totals"] = totales.ToDictionary(t => t.Key.ToString(), t => (object)new { rows = t.Value.Filas, amount = t.Value.Valor });
    }

    private LegacyPendingLoad? Cheque(ContextoDeImportacion ctx, FilaDeImportacion fila, CatalogoCitado<OwnBankAccount> cuentas, List<Checkbook> chequeras,
        List<(int CheckbookId, long CheckNumber)> usados, string? referencia, int beneficiario, decimal? valor, DateOnly? fecha)
    {
        if (fila.EstaVacia(PlantillaDePendientesHeredados.Cuenta))
            fila.Error(PlantillaDePendientesHeredados.Cuenta, ImportErrors.CellRequired, "Un cheque sin cobrar lleva la cuenta bancaria propia.");
        if (fila.EstaVacia(PlantillaDePendientesHeredados.Numero))
            fila.Error(PlantillaDePendientesHeredados.Numero, ImportErrors.CellRequired, "Un cheque sin cobrar lleva su número.");
        var cuenta = fila.EstaVacia(PlantillaDePendientesHeredados.Cuenta) ? null
            : fila.Referencia(PlantillaDePendientesHeredados.Cuenta, cuentas, "una cuenta bancaria propia", "con la plantilla tesoreria.cuentas-bancarias");
        var numero = fila.Entero(PlantillaDePendientesHeredados.Numero);
        var entregado = fila.SiNo(PlantillaDePendientesHeredados.Entregado);
        var chequera = cuenta is null || numero is null ? null : chequeras.FirstOrDefault(k => k.BankAccountId == cuenta.Id && k.FromNumber <= numero && numero <= k.ToNumber);
        if (cuenta is not null && numero is not null && chequera is null)
            fila.Error(PlantillaDePendientesHeredados.Numero, ImportTreasuryGoLiveTemplateCommandHandler.CodigoSinChequera,
                "El número no está en ninguna chequera registrada de la cuenta: cargue antes la chequera (tesoreria.chequeras).");
        if (chequera is not null && usados.Any(u => u.CheckbookId == chequera.Id && u.CheckNumber == numero))
            fila.Error(PlantillaDePendientesHeredados.Numero, ImportErrors.RowDuplicate, "Ese cheque ya está registrado en la Tesorería.");
        if (cuenta is not null && numero is not null) ctx.Datos.LlaveUnica(fila, $"cheque|{cuenta.Code}|{numero}", PlantillaDePendientesHeredados.Numero);
        if (fila.TieneErrores || cuenta is null || chequera is null || numero is null || beneficiario == 0 || valor is null || fecha is null || referencia is null) return null;

        var cheque = new IssuedCheck
        {
            CheckbookId = chequera.Id, CheckNumber = numero.Value, BeneficiaryPersonId = beneficiario, Amount = valor.Value, IssuedOn = fecha.Value,
            Status = entregado ? IssuedCheckState.Delivered : IssuedCheckState.Issued, DeliveredOn = entregado ? fecha : null, IsOpeningLoad = true,
        };
        db.IssuedChecks.Add(cheque);
        usados.Add((chequera.Id, numero.Value));
        return new LegacyPendingLoad
        {
            Kind = LegacyPendingKind.UncashedCheck, LegacyReference = referencia, BankAccountId = cuenta.Id, CheckNumber = numero.Value, BeneficiaryPersonId = beneficiario,
            Amount = valor.Value, Date = fecha.Value, Status = LegacyPendingLoadState.Migrated, MigratedToPublicId = cheque.PublicId,
        };
    }

    private async Task<LegacyPendingLoad?> FacturaAsync(FilaDeImportacion fila, Guid? personaPublicId, Dictionary<int, IReadOnlyList<CuentaPorPagarDto>> porPagar,
        Dictionary<(int, string), decimal> tomado, string? referencia, int beneficiario, decimal? valor, DateOnly? fecha, DateOnly hoy, bool aplicar, CancellationToken ct)
    {
        var factura = fila.Texto(PlantillaDePendientesHeredados.Factura, 40)?.Trim();
        if (factura is null)
            fila.Error(PlantillaDePendientesHeredados.Factura, ImportErrors.CellRequired, "Una factura por pagar lleva el documento cruce de su cuenta por pagar contable.");
        if (fila.TieneErrores || personaPublicId is not { } persona || factura is null || valor is null || fecha is null || referencia is null) return null;

        if (!porPagar.TryGetValue(beneficiario, out var cuentasDelTercero))
            porPagar[beneficiario] = cuentasDelTercero = await contabilidad.CuentasPorPagarAsync(new FiltroDeCuentasPorPagar(persona, null), ct);
        var cuenta = cuentasDelTercero.FirstOrDefault(c => c.Number == factura);
        var abiertas = CuentasPorPagarDeTesoreria.Abiertas.ToList();
        var enOrdenes = cuenta is null ? 0m
            : await db.PaymentOrders.AsNoTracking().Where(o => o.Origin == PaymentOrderOrigin.AccountingPayable && o.BeneficiaryPersonId == beneficiario
                                                               && o.OriginOrderNumber == factura && abiertas.Contains(o.Status)).SumAsync(o => o.NetAmount, ct);
        var yaTomado = tomado.GetValueOrDefault((beneficiario, factura));
        if (cuenta is null || valor.Value > cuenta.Pending - enOrdenes - yaTomado)
        {
            fila.Error(PlantillaDePendientesHeredados.Factura, CodigoSinCuentaPorPagar, cuenta is null
                ? $"Contabilidad no tiene una cuenta por pagar de este proveedor con el documento cruce «{factura}»: regístrela en los saldos de apertura antes de cargar el pendiente."
                : string.Create(CultureInfo.InvariantCulture, $"Lo pendiente de «{factura}» en Contabilidad, menos lo que ya está en órdenes, es {cuenta.Pending - enOrdenes - yaTomado:N0}."));
            return null;
        }
        tomado[(beneficiario, factura)] = yaTomado + valor.Value;

        Guid? orden = null;
        if (aplicar)
        {
            // Las órdenes nacen sólo al aplicar: la revisión no consume números de la bandeja.
            var nueva = await CuentasPorPagarDeTesoreria.NuevaOrdenAsync(db, numerador, validador, parametros, PaymentOrderOrigin.AccountingPayable,
                CuentasPorPagarDeTesoreria.ClaseContable, cuenta.DocumentPublicId, factura, beneficiario, valor.Value, valor.Value, fecha.Value < hoy ? hoy : fecha.Value,
                $"Factura por pagar {factura}", cuenta.DocumentPublicId, null, reloj.UtcNow, ct);
            orden = nueva.PublicId;
        }
        return new LegacyPendingLoad
        {
            Kind = LegacyPendingKind.PayableInvoice, LegacyReference = referencia, BeneficiaryPersonId = beneficiario, Amount = valor.Value, Date = fecha.Value,
            InvoiceNumber = factura, Status = aplicar ? LegacyPendingLoadState.Migrated : LegacyPendingLoadState.Loaded, MigratedToPublicId = orden,
        };
    }
}
