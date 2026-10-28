using System.Collections;
using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Rates;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.ColumnasDeLineas;
using H = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.HojasDeLineas;

namespace IngenIA365ERP.Application.Lending.Imports;

/// <summary>
/// Plantilla 1 — líneas de crédito (feature 013, US1, T0356; plantillas.md §1; <c>POST /api/lending/credit-lines/import?mode=review|apply</c>,
/// <c>Lending.CreditLines.Manage</c>). (nuevo)
/// </summary>
public sealed record ImportCreditLinesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportCreditLinesCommandValidator : AbstractValidator<ImportCreditLinesCommand>
{
    public ImportCreditLinesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportCreditLinesCommand>.LargoMaximo);
    }
}

/// <summary>
/// Cada fila de <c>Lineas</c> es una versión (llave <c>codigo</c> + <c>vigenteDesde</c>) con <b>las mismas reglas que el alta unitaria</b>
/// (<see cref="ReglasDeLineaDeCredito"/>; como <c>ImportAccountsCommand</c> de la 009): los errores de negocio llevan el mismo código
/// (<c>Lending.Usury.Exceeded</c>, <c>Lending.CreditLine.ModalityUnknown</c>, <c>NationalValues.Missing</c>…) en la hoja, fila y columna que
/// los produce; <c>VIVIENDA</c> es <c>Import.Cell.NotYetAvailable</c> (D18). Las hojas <c>Tasas</c>, <c>Documentos</c>, <c>Seguros</c> y
/// <c>Garantias</c> cuelgan de una versión de la hoja <c>Lineas</c> y la reemplazan entera (lo que no traen, la versión no lo tiene). Una
/// línea nueva se crea; una existente recibe una versión nueva (la anterior rige hasta la víspera); una versión igual a la registrada queda
/// «sin cambio». Una versión que empieza antes de hoy sólo entra en una línea sin solicitudes ni créditos. La aseguradora se cita por
/// documento y debe existir viva (la plantilla no crea personas). <c>activa = no</c> cierra la línea a créditos nuevos. <c>Equivalencias</c>
/// registra versiones de las equivalencias de Comercial. Crear o cerrar vigencias pide motivo; todo o nada.
/// </summary>
public sealed class ImportCreditLinesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    ReglasDeLineaDeCredito reglas,
    AltaDeVersionDeLinea alta,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ImportCreditLinesCommand, Result<ImportResultDto>>
{
    private const string MotivoPorDefecto = "Importación de líneas de crédito";

    private static readonly IReadOnlyDictionary<string, RepaymentPeriodicity> EtiquetasDePeriodicidad = new Dictionary<string, RepaymentPeriodicity>
    {
        ["pago unico"] = RepaymentPeriodicity.SinglePayment, ["semanal"] = RepaymentPeriodicity.Weekly, ["decadal"] = RepaymentPeriodicity.TenDay,
        ["quincenal"] = RepaymentPeriodicity.Biweekly, ["mensual"] = RepaymentPeriodicity.Monthly, ["bimestral"] = RepaymentPeriodicity.Bimonthly,
        ["trimestral"] = RepaymentPeriodicity.Quarterly, ["semestral"] = RepaymentPeriodicity.SemiAnnual, ["anual"] = RepaymentPeriodicity.Annual,
    };

    private static readonly IReadOnlyDictionary<string, RepaymentChannel> EtiquetasDeFormaDePago = new Dictionary<string, RepaymentChannel>
    {
        ["caja"] = RepaymentChannel.Cashier, ["nomina propia"] = RepaymentChannel.OwnPayroll, ["libranza"] = RepaymentChannel.PaymasterDeduction,
        ["debito automatico"] = RepaymentChannel.DirectDebit,
    };

    private static readonly IReadOnlyDictionary<string, DayCountConvention> EtiquetasDeConvencion = new Dictionary<string, DayCountConvention>
    {
        ["30/360"] = DayCountConvention.Thirty360, ["real/365"] = DayCountConvention.Actual365, ["real/360"] = DayCountConvention.Actual360,
    };

    /// <summary>Una versión leída de la hoja <c>Lineas</c> con sus hijas y lo que la rodea.</summary>
    private sealed class VersionLeida(FilaDeImportacion fila, string codigo, DateOnly desde)
    {
        public FilaDeImportacion Fila { get; } = fila;
        public string Codigo { get; } = codigo;
        public DateOnly Desde { get; } = desde;
        public string Nombre { get; set; } = string.Empty;
        public string? Destino { get; set; }
        public bool Activa { get; set; } = true;
        public BorradorDeVersionDeLinea? Borrador { get; set; }
        public List<(FilaDeImportacion Fila, BorradorDeBanda Banda)> Bandas { get; } = [];
        public List<(FilaDeImportacion Fila, BorradorDeDocumento Documento)> Documentos { get; } = [];
        public List<(FilaDeImportacion Fila, BorradorDeSeguro Seguro)> Seguros { get; } = [];
        public List<(FilaDeImportacion Fila, BorradorDeGarantia Garantia)> Garantias { get; } = [];
        public bool AdmitirDesde { get; set; }
        public AccionDeImportacion? Accion { get; set; }
        public string Llave => $"{Codigo}|{Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }

    public Task<Result<ImportResultDto>> Handle(ImportCreditLinesCommand request, CancellationToken ct)
    {
        var pendientes = new List<VersionLeida>();
        var equivalencias = new List<(DateOnly Desde, CommercialTermEquivalenceInput Item)>();
        return ejecutor.EjecutarAsync(CatalogoDePlantillasDeCartera.LineasDeCredito, request,
            (ctx, c) => ProcesarAsync(ctx, pendientes, equivalencias, c), ct,
            (ctx, c) => AplicarAsync(ctx, pendientes, equivalencias, c));
    }

    // ---------------------------------------------------------------------------------------------------------- revisar --

    private async Task ProcesarAsync(ContextoDeImportacion ctx, List<VersionLeida> pendientes,
        List<(DateOnly, CommercialTermEquivalenceInput)> equivalencias, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var versiones = LeerLineas(ctx.Hoja(H.Lineas), ctx);
        var porLlave = versiones.ToDictionary(v => v.Llave, StringComparer.Ordinal);

        // Las aseguradoras citadas, en bloque: nunca una consulta por fila.
        var documentos = ctx.Hoja(H.Seguros).Filas.Select(f => f.Crudo(C.Aseguradora)).OfType<string>().Select(d => d.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Feature 017: el número solo puede ser de dos personas (CC y CE); la plantilla no trae el tipo y la fila ambigua se rechaza.
        documentos = documentos.Concat(documentos.Select(d => UsuarioDeDocumento.NormalizarNumero(d))).Distinct().ToList();
        var aseguradoras = ResolutorDePersonaPorDocumento.Indexar(
            await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted).Select(p => new PersonaCitada(p.Id, p.TaxId, p.Username)).ToListAsync(ct),
            p => p.Usuario, p => p.Numero);

        LeerHijas(ctx.Hoja(H.Tasas), porLlave, (v, f) => LeerBanda(v, f));
        LeerHijas(ctx.Hoja(H.Documentos), porLlave, (v, f) => LeerDocumento(v, f));
        LeerHijas(ctx.Hoja(H.Seguros), porLlave, (v, f) => LeerSeguro(v, f, aseguradoras));
        LeerHijas(ctx.Hoja(H.Garantias), porLlave, (v, f) => LeerGarantia(v, f));
        LeerEquivalencias(ctx.Hoja(H.Equivalencias), equivalencias);

        var codigos = versiones.Select(v => v.Codigo).Distinct().ToArray();
        var lineas = await db.CreditLines.Where(l => codigos.Contains(l.Code) && !l.IsDeleted).ToListAsync(ct);
        var idsDeLinea = lineas.Select(l => l.Id).ToArray();
        var registradas = await db.CreditLineVersions.AsNoTracking().Where(v => idsDeLinea.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
        var usadas = (await db.CreditApplications.IgnoreQueryFilters().Where(a => idsDeLinea.Contains(a.CreditLineId)).Select(a => a.CreditLineId).ToListAsync(ct))
            .Concat(await db.Loans.IgnoreQueryFilters().Where(l => idsDeLinea.Contains(l.CreditLineId)).Select(l => l.CreditLineId).ToListAsync(ct))
            .ToHashSet();

        foreach (var v in versiones.OrderBy(v => v.Codigo, StringComparer.Ordinal).ThenBy(v => v.Desde))
        {
            if (v.Fila.TieneErrores || v.Borrador is null || HijasConErrores(v)) continue;
            var linea = lineas.FirstOrDefault(l => l.Code == v.Codigo);
            var borrador = Completo(v, ctx);
            v.AdmitirDesde = linea is null || !usadas.Contains(linea.Id);

            if (linea is not null)
            {
                var suyas = registradas.Where(r => r.CreditLineId == linea.Id).ToList();
                var igual = suyas.FirstOrDefault(r => r.ValidFrom == v.Desde && r.ContentSha256 == CreditLineVersion.Huella(borrador));
                if (igual is not null)
                {
                    v.Accion = AccionDeImportacion.Unchanged;
                    var cambiaLaLinea = linea.Name != v.Nombre || linea.Purpose != v.Destino || (linea.Status == CreditLineStatus.Closed) == v.Activa;
                    if (cambiaLaLinea) { v.Accion = AccionDeImportacion.Update; pendientes.Add(v); ctx.PedirMotivo(); }
                    ctx.Registrar(v.Fila, v.Llave, v.Accion.Value);
                    continue;
                }
                if (linea.Status == CreditLineStatus.Closed && v.Activa is false)
                {
                    // Una línea cerrada que sigue cerrada no recibe versiones (como el alta unitaria).
                    v.Fila.Error(C.Activa, CreditLine.CodigoCerrada, $"La línea {linea.Code} está cerrada: reábrala antes de registrarle una versión.");
                    continue;
                }
                var ultima = suyas.Select(r => (DateOnly?)r.ValidFrom).Max();
                if (ultima is { } u && v.Desde < u)
                {
                    Error(v, ctx, ErroresDeLineasDeCredito.VersionCruzada(u));
                    continue;
                }
                if (!v.AdmitirDesde && v.Desde < hoy)
                {
                    v.Fila.Error(C.VigenteDesde, CreditLineVersion.CodigoRetroactiva,
                        $"La línea {linea.Code} ya tiene solicitudes o créditos: una versión no puede empezar antes de hoy ({hoy:yyyy-MM-dd}). Lo pactado no se reescribe.");
                    continue;
                }
            }
            var verificada = await reglas.VerificarAsync(borrador, hoy, ct, v.AdmitirDesde);
            if (verificada.IsFailure)
            {
                Error(v, ctx, verificada.Error);
                continue;
            }
            var contraLoRegistrado = await alta.ValidarContraLoRegistradoAsync(linea?.Id, borrador, ct);
            if (contraLoRegistrado.IsFailure)
            {
                Error(v, ctx, contraLoRegistrado.Error);
                continue;
            }

            v.Borrador = borrador;
            v.Accion = linea is null ? AccionDeImportacion.Create : AccionDeImportacion.Update;
            pendientes.Add(v);
            ctx.PedirMotivo();
            ctx.Registrar(v.Fila, v.Llave, v.Accion.Value,
            [
                new(C.Modalidad, null, borrador.ModalityCode),
                new(C.VigenteDesde, null, v.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ]);
            foreach (var (fila, _) in v.Bandas) ctx.Registrar(fila, v.Llave, AccionDeImportacion.Create);
            foreach (var (fila, _) in v.Documentos) ctx.Registrar(fila, v.Llave, AccionDeImportacion.Create);
            foreach (var (fila, _) in v.Seguros) ctx.Registrar(fila, v.Llave, AccionDeImportacion.Create);
            foreach (var (fila, _) in v.Garantias) ctx.Registrar(fila, v.Llave, AccionDeImportacion.Create);

            if (linea is null && lineas.All(l => l.Code != v.Codigo))
            {
                var nueva = CreditLine.Nueva(v.Codigo, v.Nombre, v.Destino);
                db.CreditLines.Add(nueva);
                lineas.Add(nueva);
            }
        }

        if (equivalencias.Count > 0)
        {
            foreach (var grupo in equivalencias.GroupBy(e => e.Item1))
                if (RegistroDeEquivalencias.Validar(grupo.Select(g => g.Item2).ToList()) is { } problema)
                    ctx.ErrorSinFila(H.Equivalencias, C.PeriodicidadComercial, problema.Codigo, problema.Mensaje);
            ctx.PedirMotivo();
        }
    }

    // ---------------------------------------------------------------------------------------------------------- aplicar --

    private async Task AplicarAsync(ContextoDeImportacion ctx, List<VersionLeida> pendientes,
        List<(DateOnly Desde, CommercialTermEquivalenceInput Item)> equivalencias, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var motivo = ctx.Motivo ?? MotivoPorDefecto;
        foreach (var v in pendientes)
        {
            var linea = await db.CreditLines.FirstAsync(l => l.Code == v.Codigo && !l.IsDeleted, ct);
            linea.Name = v.Nombre;
            linea.Purpose = v.Destino;
            if (v.Accion != AccionDeImportacion.Unchanged && v.Borrador is not null && (v.Accion == AccionDeImportacion.Create
                || !await db.CreditLineVersions.AnyAsync(r => r.CreditLineId == linea.Id && r.ValidFrom == v.Desde && r.ContentSha256 == CreditLineVersion.Huella(v.Borrador), ct)))
            {
                if (linea.Status == CreditLineStatus.Closed && v.Activa) linea.Reabrir();
                await alta.RegistrarAsync(linea, v.Borrador, ct, v.AdmitirDesde);
            }
            if (!v.Activa && linea.Status != CreditLineStatus.Closed) linea.Cerrar(CreditLineClosedReason.Manual, motivo, actor.UserId, reloj.UtcNow);
            else if (v.Activa && linea.Status == CreditLineStatus.Closed && linea.ClosedReason == CreditLineClosedReason.Manual) linea.Reabrir();
        }
        foreach (var grupo in equivalencias.GroupBy(e => e.Desde))
            await RegistroDeEquivalencias.AgregarAsync(db, grupo.Select(g => g.Item).ToList(), grupo.Key, reloj.UtcNow, ct);
    }

    // ------------------------------------------------------------------------------------------------------ lectura --

    private static List<VersionLeida> LeerLineas(HojaDeImportacion hoja, ContextoDeImportacion ctx)
    {
        var versiones = new List<VersionLeida>();
        foreach (var f in hoja.Filas)
        {
            var codigo = f.Codigo(C.Codigo, 10);
            var desde = f.Fecha(C.VigenteDesde);
            if (codigo is null || desde is null) continue;
            var llave = $"{codigo}|{desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            if (!hoja.LlaveUnica(f, llave, C.Codigo)) continue;

            var v = new VersionLeida(f, codigo, desde.Value)
            {
                Nombre = f.Texto(C.Nombre, 120) ?? string.Empty,
                Destino = f.Texto(C.Destino, 200),
                Activa = f.SiNo(C.Activa, porDefecto: true),
            };
            var modalidad = f.Codigo(C.Modalidad, 40);
            // D18: hasta K6b la vivienda no se carga; desde K6b (T1055) la línea pasa las reglas de la Ley 546 al registrarse (ReglasDeLineaDeCredito).
            if (modalidad is not null && string.Equals(modalidad, ReglasDeLineaDeCredito.ModalidadDeVivienda, StringComparison.OrdinalIgnoreCase)
                && !Housing.LectorDeVivienda.Disponible())
                f.TodaviaNoDisponible(C.Modalidad, ErroresDeLineasDeCredito.EntregaDeVivienda);

            var tipoDeTasa = f.Enumeracion<RateKind>(C.TipoDeTasa, new Dictionary<string, RateKind> { ["fija"] = RateKind.Fixed, ["variable"] = RateKind.Variable });
            var tipoDeMora = f.Enumeracion<DefaultRateKind>(C.TipoDeMora,
                new Dictionary<string, DefaultRateKind> { ["fija"] = DefaultRateKind.Fixed, ["relativa"] = DefaultRateKind.RelativeToCurrent });
            var convencion = f.Enumeracion(C.ConvencionDeDias, EtiquetasDeConvencion);
            var sistema = f.Enumeracion<AmortizationSystem>(C.Sistema, new Dictionary<string, AmortizationSystem>
            {
                ["cuota fija"] = AmortizationSystem.LevelPayment, ["abono constante"] = AmortizationSystem.ConstantPrincipal, ["pago unico"] = AmortizationSystem.Bullet,
            });
            var periodicidades = Lista(f, C.Periodicidades, EtiquetasDePeriodicidad);
            var periodicidad = UnValor(f, C.PeriodicidadPorDefecto, EtiquetasDePeriodicidad);
            var formas = Lista(f, C.FormasDePago, EtiquetasDeFormaDePago);
            var forma = UnValor(f, C.FormaDePagoPorDefecto, EtiquetasDeFormaDePago);
            var meses = f.Entero(C.MesesDeReajuste);
            RepaymentPeriodicity? reajuste = meses switch
            {
                null => null,
                1 => RepaymentPeriodicity.Monthly,
                2 => RepaymentPeriodicity.Bimonthly,
                3 => RepaymentPeriodicity.Quarterly,
                6 => RepaymentPeriodicity.SemiAnnual,
                12 => RepaymentPeriodicity.Annual,
                _ => Formato<RepaymentPeriodicity>(f, C.MesesDeReajuste, "El reajuste es cada 1, 2, 3, 6 o 12 meses."),
            };
            Guid? soporte = null;
            if (f.Texto(C.SoporteDelConcepto, 200) is { } textoDeSoporte)
            {
                if (Guid.TryParse(textoDeSoporte, out var g)) soporte = g;
                else f.Error(C.SoporteDelConcepto, ImportErrors.CellFormat, "Escriba el identificador del adjunto con el concepto de la contadora (súbalo primero en la línea).");
            }
            var concepto = f.Enumeracion<InterestTaxTreatment>(C.ConceptoTributario,
                new Dictionary<string, InterestTaxTreatment> { ["no gravado"] = InterestTaxTreatment.NotTaxed, ["gravado"] = InterestTaxTreatment.Taxed });
            var diaDePago = f.Entero(C.DiaDePago);
            var gracia = f.Enumeracion<GraceKind>(C.Gracia, new Dictionary<string, GraceKind> { ["sin gracia"] = GraceKind.None, ["a capital"] = GraceKind.Principal });

            var montoMinimo = f.Monto(C.MontoMinimo);
            var montoMaximo = f.Monto(C.MontoMaximo);
            var plazoMinimo = f.Entero(C.PlazoMinimo);
            var plazoMaximo = f.Entero(C.PlazoMaximo);
            var unidad = f.Monto(C.RedondeoUnidad);
            if (f.TieneErrores || tipoDeTasa is null || tipoDeMora is null || convencion is null || sistema is null || periodicidades is null
                || periodicidad is null || formas is null || forma is null || montoMinimo is null || montoMaximo is null || plazoMinimo is null
                || plazoMaximo is null || modalidad is null)
            {
                versiones.Add(v);
                continue;
            }

            v.Borrador = new BorradorDeVersionDeLinea(desde.Value, f.Texto(C.Notas, 300) ?? ctx.Motivo ?? MotivoPorDefecto, modalidad)
            {
                SubsegmentCode = f.Texto(C.Subsegmento, 30),
                ForEmployees = f.SiNo(C.ParaEmpleados),
                AllowsNonAssociates = f.SiNo(C.AdmiteNoAsociados),
                AllowsCommercialOrigin = f.SiNo(C.AdmiteOrigenComercial),
                InterestTaxTreatment = concepto,
                InterestTaxSupportAttachmentPublicId = soporte,
                RateKind = tipoDeTasa.Value,
                AnnualRate = f.Porcentaje(C.TasaCorriente),
                IndexCode = f.Codigo(C.Indice, 20),
                IndexKey = f.Texto(C.ClaveDelIndice, 20),
                Margin = f.Porcentaje(C.Margen),
                RateFloor = f.Porcentaje(C.TasaPiso),
                RateCeiling = f.Porcentaje(C.TasaTecho),
                ResetPeriodicity = reajuste,
                DefaultRateKind = tipoDeMora.Value,
                DefaultRate = f.Porcentaje(C.TasaDeMora),
                DefaultRateFactor = f.Costo(C.FactorDeMora),
                MinAmount = montoMinimo.Value,
                MaxAmount = montoMaximo.Value,
                MinTermMonths = plazoMinimo.Value,
                MaxTermMonths = plazoMaximo.Value,
                Periodicities = periodicidades,
                DefaultPeriodicity = periodicidad.Value,
                PaymentDay = diaDePago is >= 1 and <= 31 ? (byte)diaDePago.Value : diaDePago is null ? null : Formato<byte>(f, C.DiaDePago, "El día de pago va de 1 a 31."),
                AmortizationSystems = [sistema.Value],
                DefaultAmortizationSystem = sistema.Value,
                AllowsExtraordinaryInstallments = f.SiNo(C.AdmiteExtraordinarias),
                AllowedGraceKind = gracia ?? GraceKind.None,
                MaxGraceInstallments = f.Entero(C.GraciaMaxima),
                GraceInterestTreatment = f.Enumeracion<GraceInterestTreatment>(C.InteresEnGracia),
                AllowsTranches = f.SiNo(C.AdmiteTramos),
                DayCount = convencion.Value,
                RateEquivalence = f.Enumeracion<RateEquivalence>(C.Equivalencia,
                    new Dictionary<string, RateEquivalence> { ["compuesta"] = RateEquivalence.Compound, ["lineal"] = RateEquivalence.Linear }) ?? RateEquivalence.Compound,
                RoundingUnit = unidad ?? RedondeoDeCartera.Centavo,
                RoundingMode = f.Enumeracion<RoundingMode>(C.RedondeoModo) ?? RoundingMode.AwayFromZero,
                RoundingMoment = f.Enumeracion<RoundingMoment>(C.RedondeoMomento) ?? RoundingMoment.PerInstallment,
                AllocationOrder = f.Enumeracion<AllocationOrder>(C.Prelacion) ?? AllocationOrder.OldestInstallmentFirst,
                NoteRecalculation = f.Enumeracion<PrepaymentChoice>(C.RecalculoPorNotas,
                    new Dictionary<string, PrepaymentChoice> { ["reducir plazo"] = PrepaymentChoice.ReduceTerm, ["reducir cuota"] = PrepaymentChoice.ReduceInstallment })
                    ?? PrepaymentChoice.ReduceInstallment,
                RepaymentChannels = formas,
                DefaultRepaymentChannel = forma.Value,
                ContributionsMultiple = f.Costo(C.RelacionCupoAportes),
                MaxIndebtedness = f.Monto(C.EndeudamientoMaximo),
                MinContributionsBalance = f.Monto(C.SaldoMinimoDeAportes),
                RequiresBureauQuery = f.SiNo(C.ExigeCentrales),
            };
            versiones.Add(v);
        }
        return versiones;
    }

    private static void LeerHijas(HojaDeImportacion hoja, IReadOnlyDictionary<string, VersionLeida> porLlave, Action<VersionLeida, FilaDeImportacion> leer)
    {
        foreach (var f in hoja.Filas)
        {
            var linea = f.Codigo(C.Linea, 10);
            var desde = f.Fecha(C.VigenteDesde);
            if (linea is null || desde is null) continue;
            if (!porLlave.TryGetValue($"{linea}|{desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", out var v))
            {
                f.Error(C.Linea, ImportErrors.CellNotFound,
                    $"La versión {linea} desde {desde.Value:yyyy-MM-dd} no está en la hoja Lineas. Las versiones registradas no cambian: súbala en Lineas con una vigencia nueva.");
                continue;
            }
            leer(v, f);
        }
    }

    private static void LeerBanda(VersionLeida v, FilaDeImportacion f)
    {
        var banda = new BorradorDeBanda(v.Bandas.Count + 1)
        {
            MinAmount = f.Monto(C.MontoDesde),
            MaxAmount = f.Monto(C.MontoHasta),
            MinTermMonths = f.Entero(C.PlazoDesde),
            MaxTermMonths = f.Entero(C.PlazoHasta),
            DebtorKind = f.Enumeracion<DebtorKind>(C.TipoDeSolicitante, new Dictionary<string, DebtorKind> { ["asociado"] = DebtorKind.Associate, ["cliente"] = DebtorKind.Customer }),
            ForEmployees = f.SiNoIndiferente(C.ParaEmpleados),
            MinSeniorityMonths = f.Entero(C.AntiguedadMinima),
            AnnualRate = f.Porcentaje(C.TasaCorriente),
            Margin = f.Porcentaje(C.Margen),
        };
        v.Bandas.Add((f, banda));
    }

    private static void LeerDocumento(VersionLeida v, FilaDeImportacion f)
    {
        var codigo = f.Codigo(C.Codigo, 10);
        var nombre = f.Texto(C.Nombre, 120);
        var formas = Lista(f, C.FormasDePago, EtiquetasDeFormaDePago);
        if (formas is { Count: > 1 })
        {
            f.Error(C.FormasDePago, ImportErrors.CellFormat, "Un documento se condiciona a una sola forma de pago; para varias, repita la fila con otro código.");
            return;
        }
        if (codigo is null || nombre is null) return;
        v.Documentos.Add((f, new BorradorDeDocumento(codigo, nombre, f.SiNo(C.Obligatorio, porDefecto: true), v.Documentos.Count + 1)
        {
            MinAmount = f.Monto(C.MontoDesde),
            MaxAmount = f.Monto(C.MontoHasta),
            DebtorKind = f.Enumeracion<DebtorKind>(C.TipoDeSolicitante, new Dictionary<string, DebtorKind> { ["asociado"] = DebtorKind.Associate, ["cliente"] = DebtorKind.Customer }),
            RepaymentChannel = formas is [var unica] ? unica : null,
        }));
    }

    /// <summary>Una persona citada por documento en la plantilla (feature 017).</summary>
    private sealed record PersonaCitada(int Id, string Numero, string Usuario);

    private static void LeerSeguro(VersionLeida v, FilaDeImportacion f, IndiceDePersonas<PersonaCitada> aseguradoras)
    {
        var codigo = f.Codigo(C.Codigo, 10);
        var nombre = f.Texto(C.Nombre, 120);
        var documento = f.Texto(C.Aseguradora, 20);
        var @base = f.Enumeracion<InsuranceBase>(C.Base, new Dictionary<string, InsuranceBase> { ["saldo"] = InsuranceBase.OutstandingBalance, ["monto inicial"] = InsuranceBase.InitialAmount });
        var tarifa = f.Porcentaje(C.Tarifa);
        var periodicidad = UnValor(f, C.Periodicidad, EtiquetasDePeriodicidad);
        var retiene = f.SiNo(C.RetieneParteDeLaPrima);
        var porcentaje = f.Porcentaje(C.PorcentajeRetenido);
        if (retiene && porcentaje is null) f.Requerido(C.PorcentajeRetenido);
        PersonaCitada? aseguradora = null;
        if (documento is not null && !aseguradoras.TryGet(null, documento, out aseguradora, out var ambigua))
        {
            f.Error(C.Aseguradora, ambigua ? ResolutorDePersonaPorDocumento.CodigoAmbiguo : ImportErrors.CellNotFound, ambigua
                ? $"Hay varias personas con el documento «{documento}» y esta plantilla no trae el tipo de documento: no se puede saber a cuál corresponde."
                : $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
            return;
        }
        if (codigo is null || nombre is null || documento is null || @base is null || tarifa is null) return;
        v.Seguros.Add((f, new BorradorDeSeguro(codigo, nombre, aseguradora!.Id, @base.Value, tarifa.Value,
            periodicidad ?? v.Borrador?.DefaultPeriodicity ?? RepaymentPeriodicity.Monthly)
        {
            IsMandatory = f.SiNo(C.Obligatorio, porDefecto: true),
            RetainedFraction = retiene ? porcentaje : null,
        }));
    }

    private static void LeerGarantia(VersionLeida v, FilaDeImportacion f)
    {
        var tipos = Lista<CollateralKind>(f, C.TiposAdmitidos, new Dictionary<string, CollateralKind>
        {
            ["pagare"] = CollateralKind.PromissoryNote, ["codeudor"] = CollateralKind.CoDebtor, ["prenda"] = CollateralKind.Pledge,
            ["hipoteca"] = CollateralKind.Mortgage, ["fondo de garantias"] = CollateralKind.GuaranteeFund, ["aportes"] = CollateralKind.Contributions,
        });
        if (tipos is null) return;
        v.Garantias.Add((f, new BorradorDeGarantia(v.Garantias.Count + 1, f.Entero(C.CodeudoresMinimos) ?? 0)
        {
            MinAmount = f.Monto(C.MontoDesde),
            MaxAmount = f.Monto(C.MontoHasta),
            MinTermMonths = f.Entero(C.PlazoDesde),
            MaxTermMonths = f.Entero(C.PlazoHasta),
            AdmittedKinds = tipos,
            MinimumCoverage = f.Porcentaje(C.CoberturaMinima),
            ContributionsCountAsCollateral = f.SiNo(C.AportesCuentan),
        }));
    }

    private static void LeerEquivalencias(HojaDeImportacion hoja, List<(DateOnly, CommercialTermEquivalenceInput)> equivalencias)
    {
        foreach (var f in hoja.Filas)
        {
            var desde = f.Fecha(C.VigenteDesde);
            var periodicidadComercial = f.Texto(C.PeriodicidadComercial, CommercialTermEquivalence.LargoDelCodigo);
            var periodicidad = UnValor(f, C.Periodicidad, EtiquetasDePeriodicidad);
            var unidad = f.Texto(C.UnidadDePlazo, CommercialTermEquivalence.LargoDelCodigo);
            var dias = f.Entero(C.DiasPorUnidad);
            if (desde is null || f.TieneErrores) continue;
            if (periodicidadComercial is null && unidad is null)
            {
                f.Error(C.PeriodicidadComercial, ImportErrors.CellRequired, "La fila traduce una periodicidad o una unidad de plazo de Comercial.");
                continue;
            }
            if (periodicidadComercial is not null)
            {
                var item = new CommercialTermEquivalenceInput(CommercialTermKind.Periodicity, periodicidadComercial, periodicidad);
                if (Problema(f, C.Periodicidad, item)) continue;
                equivalencias.Add((desde.Value, item));
            }
            if (unidad is not null)
            {
                var item = new CommercialTermEquivalenceInput(CommercialTermKind.TermUnit, unidad, null, dias);
                if (Problema(f, C.DiasPorUnidad, item)) continue;
                equivalencias.Add((desde.Value, item));
            }
        }
    }

    private static bool Problema(FilaDeImportacion f, string columna, CommercialTermEquivalenceInput item)
    {
        if (CommercialTermEquivalence.Validar(item.Kind, item.CommercialCode, item.Periodicity, item.DaysPerUnit) is not { } problema) return false;
        f.Error(columna, problema.Codigo, problema.Mensaje);
        return true;
    }

    // --------------------------------------------------------------------------------------------------- composición --

    private static bool HijasConErrores(VersionLeida v) =>
        v.Bandas.Any(b => b.Fila.TieneErrores) || v.Documentos.Any(d => d.Fila.TieneErrores) || v.Seguros.Any(s => s.Fila.TieneErrores)
        || v.Garantias.Any(g => g.Fila.TieneErrores);

    private static BorradorDeVersionDeLinea Completo(VersionLeida v, ContextoDeImportacion ctx) => v.Borrador! with
    {
        Reason = string.IsNullOrWhiteSpace(v.Fila.Crudo(C.Notas)) ? ctx.Motivo ?? MotivoPorDefecto : v.Borrador!.Reason,
        RateBands = v.Bandas.Select(b => b.Banda).ToList(),
        DocumentRequirements = v.Documentos.Select(d => d.Documento).ToList(),
        Insurances = v.Seguros.Select(s => s.Seguro).ToList(),
        CollateralRules = v.Garantias.Select(g => g.Garantia).ToList(),
    };

    /// <summary>Un error de las reglas en la hoja, fila y columna que lo produce (el mismo código que el alta unitaria).</summary>
    private static void Error(VersionLeida v, ContextoDeImportacion ctx, Error error)
    {
        var campo = CampoDelError(error);
        var codigo = error.Code == ErroresDeLineasDeCredito.CodigoViviendaNoDisponible ? ImportErrors.CellNotYetAvailable : error.Code;

        if (campo is not null && campo.StartsWith("rateBands[", StringComparison.Ordinal) && Orden(campo) is { } orden
            && v.Bandas.FirstOrDefault(b => b.Banda.Order == orden) is { Fila: { } filaDeBanda })
        {
            filaDeBanda.Error(campo.EndsWith(".margin", StringComparison.Ordinal) ? C.Margen : C.TasaCorriente, codigo, error.Message);
            return;
        }
        if (error is ErrorConDatos { Data: IReadOnlyDictionary<string, object?> datos } && datos.TryGetValue("orders", out var ordenes)
            && ordenes is IEnumerable<int> lista && v.Bandas.FirstOrDefault(b => b.Banda.Order == lista.First()) is { Fila: { } filaCruzada })
        {
            filaCruzada.Error(C.MontoDesde, codigo, error.Message);
            return;
        }
        var (hoja, columna) = campo switch
        {
            "annualRate" => (H.Lineas, C.TasaCorriente),
            "margin" => (H.Lineas, C.Margen),
            "indexCode" => (H.Lineas, C.Indice),
            "rateFloor" or "rateCeiling" => (H.Lineas, C.TasaPiso),
            "defaultRate" => (H.Lineas, C.TasaDeMora),
            "defaultRateFactor" => (H.Lineas, C.FactorDeMora),
            "modalityCode" => (H.Lineas, C.Modalidad),
            "allowsNonAssociates" => (H.Lineas, C.AdmiteNoAsociados),
            "interestTaxTreatment" => (H.Lineas, C.ConceptoTributario),
            "validFrom" => (H.Lineas, C.VigenteDesde),
            "minAmount" => (H.Lineas, C.MontoMinimo),
            "minTermMonths" => (H.Lineas, C.PlazoMinimo),
            "maxInstallments" => (H.Lineas, C.PlazoMaximo),
            "paymentDay" => (H.Lineas, C.DiaDePago),
            "defaultPeriodicity" => (H.Lineas, C.PeriodicidadPorDefecto),
            "defaultAmortizationSystem" => (H.Lineas, C.Sistema),
            "defaultRepaymentChannel" => (H.Lineas, C.FormaDePagoPorDefecto),
            "maxExtraordinaryFraction" => (H.Lineas, C.AdmiteExtraordinarias),
            "maxGraceInstallments" or "allowedGraceKind" => (H.Lineas, C.GraciaMaxima),
            "graceInterestTreatment" or "graceDistributionInstallments" => (H.Lineas, C.InteresEnGracia),
            "roundingUnit" => (H.Lineas, C.RedondeoUnidad),
            "conceptPriority" => (H.Lineas, C.Prelacion),
            "commercialDefaultFor" => (H.Lineas, C.AdmiteOrigenComercial),
            "documentRequirements" => (H.Documentos, C.Codigo),
            "collateralRules" => (H.Garantias, C.TiposAdmitidos),
            _ when campo?.StartsWith("insurances", StringComparison.Ordinal) == true => (H.Seguros, C.Tarifa),
            _ when error.Code == ErroresDeLineasDeCredito.CodigoModalidadDesconocida || codigo == ImportErrors.CellNotYetAvailable => (H.Lineas, C.Modalidad),
            _ when error.Code == ControlDeUsura.CodigoExcedida => (H.Lineas, C.TasaCorriente),
            _ when error.Code == ErroresDeLineasDeCredito.CodigoNoAsociadosNoPermitidos => (H.Lineas, C.AdmiteNoAsociados),
            _ => (H.Lineas, C.VigenteDesde),
        };
        var fila = hoja switch
        {
            H.Documentos => v.Documentos.FirstOrDefault().Fila,
            H.Seguros => v.Seguros.FirstOrDefault().Fila,
            H.Garantias => v.Garantias.FirstOrDefault().Fila,
            _ => null,
        } ?? v.Fila;
        fila.Error(fila == v.Fila && hoja != H.Lineas ? C.Codigo : columna, codigo, error.Message);
    }

    /// <summary>El campo del error: el <c>field</c> del dominio o el primero de <c>data.errors</c>.</summary>
    private static string? CampoDelError(Error error)
    {
        if (error is not ErrorConDatos { Data: { } datos }) return null;
        if (datos is IReadOnlyDictionary<string, object?> d) return d.TryGetValue("field", out var f) ? f?.ToString() : null;
        if (datos.GetType().GetProperty("errors")?.GetValue(datos) is IEnumerable errores)
            foreach (var e in errores)
                if (e is ErrorDeCampo campo) return campo.Field;
        return null;
    }

    private static int? Orden(string campo)
    {
        var inicio = campo.IndexOf('[', StringComparison.Ordinal) + 1;
        var fin = campo.IndexOf(']', StringComparison.Ordinal);
        return int.TryParse(campo[inicio..fin], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    // -------------------------------------------------------------------------------------------------- celdas de enums --

    /// <summary>Un enum por su nombre en inglés, su número o una etiqueta en español (plantillas.md §0.1).</summary>
    private static TEnum? UnValor<TEnum>(FilaDeImportacion f, string columna, IReadOnlyDictionary<string, TEnum> etiquetas) where TEnum : struct, Enum
    {
        var crudo = f.Crudo(columna);
        if (crudo is null)
        {
            if (f.Hoja.Columna(columna)?.Obligatoria == true) f.Requerido(columna);
            return null;
        }
        if (Valor(crudo, etiquetas) is { } valor) return valor;
        f.Error(columna, ImportErrors.CellFormat, $"«{crudo}» no es un valor admitido. Admite: {string.Join(", ", Enum.GetNames<TEnum>())}.");
        return null;
    }

    /// <summary>Una lista de enums separados por coma (nombre, número o etiqueta); <c>*</c> = todos.</summary>
    private static IReadOnlyList<TEnum>? Lista<TEnum>(FilaDeImportacion f, string columna, IReadOnlyDictionary<string, TEnum> etiquetas) where TEnum : struct, Enum
    {
        var crudo = f.Crudo(columna);
        if (crudo is null)
        {
            if (f.Hoja.Columna(columna)?.Obligatoria == true) f.Requerido(columna);
            return null;
        }
        var partes = crudo.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes is ["*"]) return Enum.GetValues<TEnum>();
        var valores = new List<TEnum>();
        foreach (var parte in partes)
        {
            if (Valor(parte, etiquetas) is not { } valor)
            {
                f.Error(columna, ImportErrors.CellFormat, $"«{parte}» no es un valor admitido. Admite: {string.Join(", ", Enum.GetNames<TEnum>())}.");
                return null;
            }
            if (!valores.Contains(valor)) valores.Add(valor);
        }
        return valores;
    }

    private static TEnum? Valor<TEnum>(string crudo, IReadOnlyDictionary<string, TEnum> etiquetas) where TEnum : struct, Enum
    {
        var buscado = TablaLeida.Normalizar(crudo);
        foreach (var valor in Enum.GetValues<TEnum>())
            if (TablaLeida.Normalizar(valor.ToString()) == buscado) return valor;
        if (int.TryParse(crudo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) && Enum.IsDefined(typeof(TEnum), numero))
            return (TEnum)Enum.ToObject(typeof(TEnum), numero);
        foreach (var (etiqueta, valor) in etiquetas)
            if (TablaLeida.Normalizar(etiqueta) == buscado) return valor;
        return null;
    }

    private static T? Formato<T>(FilaDeImportacion f, string columna, string mensaje) where T : struct
    {
        f.Error(columna, ImportErrors.CellFormat, mensaje);
        return null;
    }
}
