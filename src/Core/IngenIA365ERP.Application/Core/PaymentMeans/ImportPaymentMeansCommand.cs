using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;
using MedioDePago = IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans;
using M = IngenIA365ERP.Application.Core.PaymentMeans.PlantillaDeMediosDePago;

namespace IngenIA365ERP.Application.Core.PaymentMeans;

/// <summary>
/// Plantilla 11 — medios de pago, con franquicias, adquirentes y datáfonos (feature 012, I3, T592; contracts/plantillas.md §11).
/// Escribe <c>COR_CardNetworks</c>, <c>COR_CardAcquirers</c>, <c>COR_CardTerminals</c>, <c>COR_PaymentMeans</c> y la disponibilidad
/// del módulo (<c>INV_PaymentMeans*</c>) en el mismo <c>SaveChanges</c>; las columnas de disponibilidad exigen además
/// <c>Inventory.PointsOfSale.Manage</c> (§0.7). Reemplaza la definición que I1 publicaba sólo para descargar. (nuevo)
/// </summary>
public static class PlantillaDeMediosDePago
{
    public const string Clave = Inventory.Imports.CatalogoDePlantillas.MediosDePagoClave;
    public const string HojaFranquicias = "Franquicias";
    public const string HojaAdquirentes = "Adquirentes";
    public const string HojaDatafonos = "Datafonos";
    public const string HojaMedios = "MediosDePago";
    public const string PermisoDeDisponibilidad = "Inventory.PointsOfSale.Manage";

    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string TipoDeTarjeta = "tipoDeTarjeta";
    public const string Activa = "activa";
    public const string Activo = "activo";
    public const string Tercero = "tercero";
    public const string Adquirente = "adquirente";
    public const string Serial = "serial";
    public const string CajaPorDefecto = "cajaPorDefecto";
    public const string Orden = "orden";
    public const string TeclaRapida = "teclaRapida";
    public const string Clase = "clase";
    public const string Franquicia = "franquicia";
    public const string Banco = "banco";
    public const string CuentaDestino = "cuentaDestino";
    public const string TipoCuentaDestino = "tipoCuentaDestino";
    public const string ExigeReferencia = "exigeReferencia";
    public const string TipoReferencia = "tipoReferencia";
    public const string LargoMinimoReferencia = "largoMinimoReferencia";
    public const string LargoMaximoReferencia = "largoMaximoReferencia";
    public const string AdmiteVueltas = "admiteVueltas";
    public const string AdmitePagoParcial = "admitePagoParcial";
    public const string ReferenciaUnica = "referenciaUnica";
    public const string Arqueo = "arqueo";
    public const string Tolerancia = "tolerancia";
    public const string ComisionPorcentaje = "comisionEsperadaPorcentaje";
    public const string ComisionValor = "comisionEsperadaValor";
    public const string CodigoDian = "codigoDian";
    public const string PlazoDias = "plazoDias";
    public const string Cuotas = "cuotas";
    public const string Periodicidad = "periodicidad";
    public const string LineaSugerida = "lineaSugerida";
    public const string Puntos = "puntos";
    public const string Canales = "canales";
    public const string TiposDeDocumento = "tiposDeDocumento";
    public const string VigenteDesde = "vigenteDesde";
    public const string VigenteHasta = "vigenteHasta";

    public static IReadOnlyDictionary<string, CardKind> EtiquetasDeTarjeta { get; } = new Dictionary<string, CardKind>
    {
        ["credito"] = CardKind.Credit, ["debito"] = CardKind.Debit, ["ambas"] = CardKind.Both,
    };

    public static IReadOnlyDictionary<string, PaymentMeansClass> EtiquetasDeClase { get; } = new Dictionary<string, PaymentMeansClass>
    {
        ["efectivo"] = PaymentMeansClass.Cash, ["tarjetaCredito"] = PaymentMeansClass.CreditCard, ["tarjetaDebito"] = PaymentMeansClass.DebitCard,
        ["creditoAsociado"] = PaymentMeansClass.AssociateCredit, ["creditoComercial"] = PaymentMeansClass.CustomerCredit,
        ["consignacion"] = PaymentMeansClass.BankDeposit, ["transferencia"] = PaymentMeansClass.Transfer, ["bono"] = PaymentMeansClass.Voucher,
        ["cheque"] = PaymentMeansClass.Check, ["otro"] = PaymentMeansClass.Other,
    };

    public static IReadOnlyDictionary<string, CashCountMethod> EtiquetasDeArqueo { get; } = new Dictionary<string, CashCountMethod>
    {
        ["contadoFisico"] = CashCountMethod.PhysicalCount, ["totalDeComprobantes"] = CashCountMethod.VoucherTotal,
        ["porReferencias"] = CashCountMethod.ByReference, ["sinArqueo"] = CashCountMethod.None,
    };

    /// <summary>Periodicidad de las cuotas, en días (la columna <c>periodicidad</c>).</summary>
    public static IReadOnlyDictionary<string, short> Periodicidades { get; } = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
    {
        ["mensual"] = 30, ["quincenal"] = 15, ["semanal"] = 7,
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Medios de pago (con franquicias, adquirentes y datáfonos)", ModuloDeAuditoria.PaymentMeans,
    [
        new HojaDePlantilla(HojaFranquicias,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave", Ejemplo: "VISA"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDeTarjetas.LargoDeNombre, Ejemplo: "Visa"),
            new(TipoDeTarjeta, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Credit (crédito), Debit (débito), Both (ambas)", Ejemplo: "Both"),
            new(Activa, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ], Obligatoria: false),
        new HojaDePlantilla(HojaAdquirentes,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave", Ejemplo: "REDEBAN"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDeTarjetas.LargoDeNombre, Ejemplo: "Redeban Multicolor"),
            new(Tercero, TipoDeValor.Persona, Reglas: "documento de la persona del adquirente (la cuenta por cobrar lleva tercero)"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ], Obligatoria: false),
        new HojaDePlantilla(HojaDatafonos,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave con el adquirente; el terminal (TER) del comprobante", Ejemplo: "TER001"),
            new(Adquirente, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Ejemplo: "REDEBAN"),
            new(Serial, TipoDeValor.Texto, Largo: ReglasDeTarjetas.LargoDeSerial),
            new(CajaPorDefecto, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "se propone al cobrar en esa caja", Ejemplo: "CJ01"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ], Obligatoria: false),
        new HojaDePlantilla(HojaMedios,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave; no cambia nunca (la matriz contable lo usa)", Ejemplo: "EFECTIVO"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDeMedioDePago.LargoDeNombre, Ejemplo: "Efectivo"),
            new(Orden, TipoDeValor.Entero, Reglas: "vacío = 0", Ejemplo: "1"),
            new(TeclaRapida, TipoDeValor.Texto, Largo: 1, Reglas: "una letra o un dígito; única entre los medios activos", Ejemplo: "E"),
            new(Clase, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "Cash, CreditCard, DebitCard, AssociateCredit, CustomerCredit, BankDeposit, Transfer, Voucher, Check, Other; no cambia si el medio tiene pagos", Ejemplo: "Cash"),
            new(Franquicia, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "en tarjetas; débito no en CreditCard"),
            new(Adquirente, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "en tarjetas"),
            new(Banco, TipoDeValor.Banco, Reglas: "en consignación y transferencia; dato del medio, no cuenta contable"),
            new(CuentaDestino, TipoDeValor.Texto, Largo: ReglasDeMedioDePago.LargoDeCuenta, Reglas: "con banco"),
            new(TipoCuentaDestino, TipoDeValor.Enumeracion, Reglas: "Ahorros, Corriente"),
            new(ExigeReferencia, TipoDeValor.SiNo, Reglas: "vacío = según la clase"),
            new(TipoReferencia, TipoDeValor.Enumeracion, Reglas: "Approval, Receipt, Deposit, VoucherNumber, CheckNumber, Other; si exige referencia"),
            new(LargoMinimoReferencia, TipoDeValor.Entero),
            new(LargoMaximoReferencia, TipoDeValor.Entero),
            new(AdmiteVueltas, TipoDeValor.SiNo, Reglas: "vacío = no; sólo Cash"),
            new(AdmitePagoParcial, TipoDeValor.SiNo, Reglas: "vacío = sí"),
            new(ReferenciaUnica, TipoDeValor.SiNo, Reglas: "vacío = sí en Voucher"),
            new(Arqueo, TipoDeValor.Enumeracion, Reglas: "PhysicalCount, VoucherTotal, ByReference, None; vacío = según la clase"),
            new(Tolerancia, TipoDeValor.Monto, Reglas: "≥ 0; cambiarla en un medio existente pide motivo", Ejemplo: "500"),
            new(ComisionPorcentaje, TipoDeValor.Porcentaje, Reglas: "informativa; cambiarla pide motivo"),
            new(ComisionValor, TipoDeValor.Monto, Reglas: "informativa; cambiarla pide motivo"),
            new(CodigoDian, TipoDeValor.Texto, Obligatoria: true, Largo: 3, Reglas: "medio de pago del anexo DIAN vigente; pendiente de validar por la contadora", Ejemplo: "10"),
            new(PlazoDias, TipoDeValor.Entero, Reglas: "en clases de crédito"),
            new(Cuotas, TipoDeValor.Entero, Reglas: "en clases de crédito"),
            new(Periodicidad, TipoDeValor.Enumeracion, Reglas: "Mensual, Quincenal, Semanal; vacío = Mensual"),
            new(LineaSugerida, TipoDeValor.Texto, Largo: ReglasDeMedioDePago.LargoDeLinea, Reglas: "línea de Cartera sugerida, sin llave"),
            new(Puntos, TipoDeValor.Lista, Permiso: PermisoDeDisponibilidad, Reglas: "códigos separados por coma; * = todos; vacío = todos en un medio nuevo, sin cambio en uno existente", Ejemplo: "*"),
            new(Canales, TipoDeValor.Lista, Permiso: PermisoDeDisponibilidad, Reglas: "códigos separados por coma; * = todos", Ejemplo: "*"),
            new(TiposDeDocumento, TipoDeValor.Lista, Permiso: PermisoDeDisponibilidad, Reglas: "códigos separados por coma; * = todos", Ejemplo: "*"),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "AAAA-MM-01"),
            new(VigenteHasta, TipoDeValor.Fecha),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí; un medio con pagos no se borra: se inactiva", Ejemplo: "sí"),
        ]),
    ]);

    /// <summary>La referencia que exige cada clase cuando la columna viene vacía.</summary>
    public static (bool Exige, PaymentReferenceKind? Tipo) ReferenciaPorDefecto(PaymentMeansClass clase) => clase switch
    {
        PaymentMeansClass.CreditCard or PaymentMeansClass.DebitCard => (true, PaymentReferenceKind.Approval),
        PaymentMeansClass.BankDeposit => (true, PaymentReferenceKind.Deposit),
        PaymentMeansClass.Transfer => (true, PaymentReferenceKind.Receipt),
        PaymentMeansClass.Voucher => (true, PaymentReferenceKind.VoucherNumber),
        PaymentMeansClass.Check => (true, PaymentReferenceKind.CheckNumber),
        _ => (false, null),
    };
}

/// <summary>
/// La plantilla 11 (feature 012, I3, T592; contracts/plantillas.md §11, §0.5, §0.7; <c>POST /api/core/payment-means/import</c>):
/// franquicias, adquirentes, datáfonos y medios con <b>las mismas reglas que el alta una a una</b>
/// (<see cref="ReglasDeTarjetas"/>, <see cref="ReglasDeMedioDePago"/>) y la disponibilidad del módulo en el mismo guardado; todo o
/// nada con <c>Import.Invalid</c>. Un medio nuevo, un cambio de vigencia, de tolerancia o de comisión pide motivo. (nuevo)
/// </summary>
public sealed record ImportPaymentMeansCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportPaymentMeansCommandValidator : AbstractValidator<ImportPaymentMeansCommand>
{
    public ImportPaymentMeansCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportPaymentMeansCommand>.LargoMaximo);
    }
}

public sealed class ImportPaymentMeansCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IDateTimeService reloj)
    : IRequestHandler<ImportPaymentMeansCommand, Result<ImportResultDto>>
{
    private readonly List<(CashRegister Caja, CardTerminal Datafono)> _cajasPorGuardar = [];

    public Task<Result<ImportResultDto>> Handle(ImportPaymentMeansCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(M.Definicion, request, ProcesarAsync, ct, DespuesDeGuardarAsync);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var redes = (await db.CardNetworks.Where(n => !n.IsDeleted).ToListAsync(ct)).ToDictionary(n => n.Code, StringComparer.OrdinalIgnoreCase);
        var adquirentes = (await db.CardAcquirers.Where(a => !a.IsDeleted).ToListAsync(ct)).ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);
        await FranquiciasAsync(ctx, redes, ct);
        await AdquirentesAsync(ctx, adquirentes, ct);
        await DatafonosAsync(ctx, adquirentes, ct);
        await MediosAsync(ctx, redes, adquirentes, ct);
    }

    /// <summary>El datáfono nuevo de una caja: su Id existe sólo después del primer guardado.</summary>
    private Task DespuesDeGuardarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        foreach (var (caja, datafono) in _cajasPorGuardar) caja.DefaultCardTerminalId = datafono.Id;
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------- Franquicias --

    private async Task FranquiciasAsync(ContextoDeImportacion ctx, Dictionary<string, CardNetwork> redes, CancellationToken ct)
    {
        var hoja = ctx.Hoja(M.HojaFranquicias);
        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(M.Codigo);
            var nombre = fila.Texto(M.Nombre);
            var tipo = fila.Enumeracion(M.TipoDeTarjeta, M.EtiquetasDeTarjeta);
            var activa = fila.SiNo(M.Activa, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, M.Codigo) || codigo is null || nombre is null || tipo is null || fila.TieneErrores) continue;

            if (!redes.TryGetValue(codigo, out var red))
            {
                if (await ReglasDeTarjetas.RedDuplicadaAsync(db, codigo, ct) is { } dup) { fila.Error(M.Codigo, dup.Code, dup.Message); continue; }
                red = new CardNetwork { Code = codigo, Name = nombre, CardKind = tipo.Value, IsActive = activa };
                db.CardNetworks.Add(red);
                redes[codigo] = red;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, [new(M.Nombre, null, nombre), new(M.TipoDeTarjeta, null, tipo.Value.ToString())]);
                continue;
            }
            var campos = new List<CampoCambiadoDto>();
            Diferencia(campos, M.Nombre, red.Name, nombre);
            Diferencia(campos, M.TipoDeTarjeta, red.CardKind.ToString(), tipo.Value.ToString());
            Diferencia(campos, M.Activa, SiNo(red.IsActive), SiNo(activa));
            red.Name = nombre;
            red.CardKind = tipo.Value;
            red.IsActive = activa;
            ctx.Registrar(fila, codigo, Accion(campos), campos);
        }
    }

    // ------------------------------------------------------------------------------------------- Adquirentes --

    private async Task AdquirentesAsync(ContextoDeImportacion ctx, Dictionary<string, CardAcquirer> adquirentes, CancellationToken ct)
    {
        var hoja = ctx.Hoja(M.HojaAdquirentes);
        if (hoja.Filas.Count == 0) return;
        var documentos = hoja.Filas.Select(f => f.Crudo(M.Tercero)).Where(d => d is not null).Select(d => d!).Distinct().ToList();
        var personas = CatalogoCitado<(int Id, string TaxId)>.Desde(
            await db.People.Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted).Select(p => new { p.Id, p.TaxId }).ToListAsync(ct),
            p => (p.Id, p.TaxId), p => p.TaxId);
        var documentoDe = await db.People.Where(p => adquirentes.Values.Select(a => a.PersonId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.TaxId, ct);

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(M.Codigo);
            var nombre = fila.Texto(M.Nombre);
            var tercero = fila.EstaVacia(M.Tercero) ? ((int Id, string TaxId)?)null
                : fila.Referencia(M.Tercero, personas, "una persona con el documento", "en Maestros → Personas");
            var activo = fila.SiNo(M.Activo, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, M.Codigo) || codigo is null || nombre is null || fila.TieneErrores) continue;
            if (!fila.EstaVacia(M.Tercero) && tercero is null) continue;

            if (!adquirentes.TryGetValue(codigo, out var adquirente))
            {
                if (await ReglasDeTarjetas.AdquirenteDuplicadoAsync(db, codigo, ct) is { } dup) { fila.Error(M.Codigo, dup.Code, dup.Message); continue; }
                adquirente = new CardAcquirer { Code = codigo, Name = nombre, PersonId = tercero?.Id, IsActive = activo };
                db.CardAcquirers.Add(adquirente);
                adquirentes[codigo] = adquirente;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, [new(M.Nombre, null, nombre), new(M.Tercero, null, tercero?.TaxId)]);
                continue;
            }
            var campos = new List<CampoCambiadoDto>();
            Diferencia(campos, M.Nombre, adquirente.Name, nombre);
            Diferencia(campos, M.Tercero, adquirente.PersonId is { } p ? documentoDe.GetValueOrDefault(p) : null, tercero?.TaxId);
            Diferencia(campos, M.Activo, SiNo(adquirente.IsActive), SiNo(activo));
            adquirente.Name = nombre;
            adquirente.PersonId = tercero?.Id;
            adquirente.IsActive = activo;
            ctx.Registrar(fila, codigo, Accion(campos), campos);
        }
    }

    // ------------------------------------------------------------------------------------------- Datafonos --

    private async Task DatafonosAsync(ContextoDeImportacion ctx, Dictionary<string, CardAcquirer> adquirentes, CancellationToken ct)
    {
        var hoja = ctx.Hoja(M.HojaDatafonos);
        if (hoja.Filas.Count == 0) return;
        var catalogo = CatalogoCitado<CardAcquirer>.Desde(adquirentes.Values, a => a, a => a.Code);
        var datafonos = await db.CardTerminals.Where(t => !t.IsDeleted).ToListAsync(ct);
        var cajas = CatalogoCitado<CashRegister>.Desde(await db.CashRegisters.Where(c => !c.IsDeleted).ToListAsync(ct), c => c, c => c.Code);

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(M.Codigo);
            var adquirente = fila.Referencia(M.Adquirente, catalogo, "un adquirente", "en la hoja Adquirentes o en Maestros → Medios de pago");
            var serial = fila.Texto(M.Serial);
            var caja = fila.EstaVacia(M.CajaPorDefecto) ? null
                : fila.Referencia(M.CajaPorDefecto, cajas, "una caja", "en la plantilla de puntos de venta y cajas o en Ventas → Puntos de venta");
            var activo = fila.SiNo(M.Activo, porDefecto: true);
            if (codigo is null || adquirente is null || fila.TieneErrores || !hoja.LlaveUnica(fila, $"{adquirente.Code}|{codigo}", M.Codigo)) continue;
            if (!fila.EstaVacia(M.CajaPorDefecto) && caja is null) continue;

            var llave = $"{adquirente.Code} {codigo}";
            var datafono = adquirente.Id == 0 ? null : datafonos.FirstOrDefault(t => t.CardAcquirerId == adquirente.Id && string.Equals(t.Code, codigo, StringComparison.OrdinalIgnoreCase));
            if (datafono is null)
            {
                if (await ReglasDeTarjetas.DatafonoDuplicadoAsync(db, adquirente, codigo, ct) is { } dup) { fila.Error(M.Codigo, dup.Code, dup.Message); continue; }
                datafono = new CardTerminal { CardAcquirer = adquirente, Code = codigo, Serial = serial, IsActive = activo };
                if (adquirente.Id != 0) datafono.CardAcquirerId = adquirente.Id;
                db.CardTerminals.Add(datafono);
                ctx.Registrar(fila, llave, AccionDeImportacion.Create, [new(M.Serial, null, serial), new(M.CajaPorDefecto, null, caja?.Code)]);
            }
            else
            {
                var campos = new List<CampoCambiadoDto>();
                Diferencia(campos, M.Serial, datafono.Serial, serial);
                Diferencia(campos, M.Activo, SiNo(datafono.IsActive), SiNo(activo));
                if (caja is not null && caja.DefaultCardTerminalId != datafono.Id) Diferencia(campos, M.CajaPorDefecto, null, caja.Code);
                datafono.Serial = serial;
                datafono.IsActive = activo;
                ctx.Registrar(fila, llave, Accion(campos), campos);
            }

            if (caja is null) continue;
            if (datafono.Id != 0 && db.CardTerminals.Entry(datafono).State != EntityState.Added) caja.DefaultCardTerminalId = datafono.Id;
            else _cajasPorGuardar.Add((caja, datafono));
        }
    }

    // ------------------------------------------------------------------------------------------- MediosDePago --

    private async Task MediosAsync(ContextoDeImportacion ctx, Dictionary<string, CardNetwork> redes, Dictionary<string, CardAcquirer> adquirentes, CancellationToken ct)
    {
        var hoja = ctx.Hoja(M.HojaMedios);
        if (hoja.Filas.Count == 0) return;
        var hoy = reloj.HoyLocal;
        var catRedes = CatalogoCitado<CardNetwork>.Desde(redes.Values, n => n, n => n.Code);
        var catAdq = CatalogoCitado<CardAcquirer>.Desde(adquirentes.Values, a => a, a => a.Code);
        var bancos = CatalogoCitado<Bank>.Desde(await db.Banks.Where(b => !b.IsDeleted).ToListAsync(ct), b => b, b => b.Name, b => b.LegacyCode);
        var puntos = CatalogoCitado<PointOfSale>.Desde(await db.PointsOfSale.Where(p => !p.IsDeleted).ToListAsync(ct), p => p, p => p.Code);
        var canales = CatalogoCitado<SalesChannel>.Desde(await db.SalesChannels.Where(c => !c.IsDeleted).ToListAsync(ct), c => c, c => c.Code);
        var tipos = CatalogoCitado<InventoryDocumentType>.Desde(await db.InventoryDocumentTypes.Where(t => !t.IsDeleted).ToListAsync(ct), t => t, t => t.Code);
        var medios = (await db.PaymentMeans.Where(m => !m.IsDeleted).ToListAsync(ct)).ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(M.Codigo);
            var nombre = fila.Texto(M.Nombre);
            var orden = fila.Entero(M.Orden);
            var tecla = fila.Texto(M.TeclaRapida);
            var clase = fila.Enumeracion(M.Clase, M.EtiquetasDeClase);
            var red = fila.EstaVacia(M.Franquicia) ? null : fila.Referencia(M.Franquicia, catRedes, "una franquicia", "en la hoja Franquicias");
            var adq = fila.EstaVacia(M.Adquirente) ? null : fila.Referencia(M.Adquirente, catAdq, "un adquirente", "en la hoja Adquirentes");
            var banco = fila.EstaVacia(M.Banco) ? null : fila.Referencia(M.Banco, bancos, "un banco", "en Maestros → Bancos");
            var cuenta = fila.Texto(M.CuentaDestino);
            var tipoCuenta = TipoDeCuenta(fila);
            var exige = fila.SiNoIndiferente(M.ExigeReferencia);
            var tipoRef = fila.Enumeracion<PaymentReferenceKind>(M.TipoReferencia);
            var min = fila.Entero(M.LargoMinimoReferencia);
            var max = fila.Entero(M.LargoMaximoReferencia);
            var vueltas = fila.SiNo(M.AdmiteVueltas);
            var parcial = fila.SiNo(M.AdmitePagoParcial, porDefecto: true);
            var unica = fila.SiNoIndiferente(M.ReferenciaUnica);
            var arqueo = fila.Enumeracion(M.Arqueo, M.EtiquetasDeArqueo);
            var tolerancia = fila.Monto(M.Tolerancia) ?? 0m;
            var comisionTasa = fila.Porcentaje(M.ComisionPorcentaje);
            var comisionFija = fila.Monto(M.ComisionValor);
            var dian = fila.Texto(M.CodigoDian);
            var plazo = fila.Entero(M.PlazoDias);
            var cuotas = fila.Entero(M.Cuotas);
            var periodicidad = Periodicidad(fila);
            var linea = fila.Texto(M.LineaSugerida);
            var listaPuntos = fila.Lista(M.Puntos);
            var listaCanales = fila.Lista(M.Canales);
            var listaTipos = fila.Lista(M.TiposDeDocumento);
            var desde = fila.Fecha(M.VigenteDesde);
            var hasta = fila.Fecha(M.VigenteHasta);
            var activo = fila.SiNo(M.Activo, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, M.Codigo) || codigo is null || nombre is null || clase is null || dian is null || desde is null || fila.TieneErrores) continue;
            if (!fila.EstaVacia(M.Franquicia) && red is null || !fila.EstaVacia(M.Adquirente) && adq is null || !fila.EstaVacia(M.Banco) && banco is null) continue;

            // I3 (T660; plantillas.md §11): las columnas de crédito sólo en las clases de crédito, y un crédito no se arquea (None); el
            // error cae en la columna que sobra, no en el plazo.
            if (!ClasesDeMedio.EsCredito(clase.Value))
            {
                foreach (var columna in new[] { M.PlazoDias, M.Cuotas, M.Periodicidad, M.LineaSugerida })
                    if (!fila.EstaVacia(columna))
                        fila.Error(columna, PaymentMeansErrors.InvalidCode, $"«{columna}» sólo va en los medios de crédito (AssociateCredit, CustomerCredit); la clase {clase.Value} no la admite.");
            }
            else if (arqueo is { } metodo && metodo != CashCountMethod.None)
            {
                fila.Error(M.Arqueo, PaymentMeansErrors.InvalidCode, "Un medio de crédito no se arquea: su arqueo es «None».");
            }
            if (fila.TieneErrores) continue;
            if (min is < 0 or > 255 || max is < 0 or > 255)
            {
                fila.Error(min is < 0 or > 255 ? M.LargoMinimoReferencia : M.LargoMaximoReferencia, ImportErrors.CellFormat, "El largo de la referencia va de 0 a 255.");
                continue;
            }

            var idsPuntos = Resolver(fila, M.Puntos, listaPuntos, puntos, p => p.Id, "un punto de venta", "en la plantilla de puntos de venta");
            var idsCanales = Resolver(fila, M.Canales, listaCanales, canales, c => c.Id, "un canal de venta", "en Ventas → Canales");
            var idsTipos = Resolver(fila, M.TiposDeDocumento, listaTipos, tipos, t => t.Id, "un tipo de documento", "en la plantilla de tipos de documento");
            if (fila.TieneErrores) continue;

            medios.TryGetValue(codigo, out var existente);
            var (exigePorClase, tipoPorClase) = M.ReferenciaPorDefecto(clase.Value);
            var input = new PaymentMeansInput
            {
                Code = codigo, Name = nombre, DisplayOrder = (short)(orden ?? 0), QuickKey = tecla, Class = clase.Value,
                DestinationAccountNumber = cuenta, DestinationAccountType = tipoCuenta,
                RequiresReference = exige ?? exigePorClase, ReferenceKind = tipoRef ?? (exige ?? exigePorClase ? tipoPorClase : null),
                ReferenceMinLength = (byte?)min, ReferenceMaxLength = (byte?)max,
                AllowsChange = vueltas, AllowsPartial = parcial, UniqueReference = unica, CountMethod = arqueo,
                RequiresTerminalBatchAtClose = existente?.RequiresTerminalBatchAtClose ?? false,
                ToleranceAmount = tolerancia, ExpectedCommissionRate = comisionTasa, ExpectedCommissionFixed = comisionFija,
                DianPaymentMeansCode = dian,
                CreditDefaults = plazo is null && cuotas is null
                    ? null
                    : new CreditDefaultsInput((short)(plazo ?? 0), (short)(cuotas ?? 0), periodicidad ?? M.Periodicidades["mensual"], linea),
                OfferedAtAllPoints = Todos(listaPuntos, existente?.OfferedAtAllPointsOfSale),
                OfferedOnAllChannels = Todos(listaCanales, existente?.OfferedInAllChannels),
                OfferedForAllDocumentTypes = Todos(listaTipos, existente?.OfferedForAllDocumentTypes),
                IsActive = activo, ValidFrom = desde.Value, ValidTo = hasta, Notes = existente?.Notes,
            };

            var antes = existente is null ? null : Resumen(existente);
            var r = await ReglasDeMedioDePago.AplicarAsync(db, existente, new DatosDeMedio(input, red, adq, banco?.Id), hoy, ct, comprobarConjuntos: false);
            if (r.IsFailure)
            {
                fila.Error(ColumnaDe(r.Error), r.Error.Code, r.Error.Message);
                continue;
            }
            var medio = r.Value;
            medios[codigo] = medio;

            // La disponibilidad: «*» fija la marca «todos» y vacía el conjunto; una lista, la marca en falso y el conjunto; vacío en un
            // medio existente, sin cambio.
            if (listaPuntos is not null || listaCanales is not null || listaTipos is not null || existente is null)
            {
                await DisponibilidadDeMedioEnBase.ReemplazarAsync(db, medio,
                    idsPuntos ?? await IdsVigentesAsync(db.PaymentMeansPointsOfSale.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).Select(x => x.PointOfSaleId), medio, ct),
                    idsCanales ?? await IdsVigentesAsync(db.PaymentMeansChannels.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).Select(x => x.SalesChannelId), medio, ct),
                    idsTipos ?? await IdsVigentesAsync(db.PaymentMeansDocumentTypes.Where(x => x.PaymentMeansId == medio.Id && !x.IsDeleted).Select(x => x.DocumentTypeId), medio, ct),
                    _ => true, reloj.UtcNow, ct);
            }

            if (antes is null)
            {
                ctx.PedirMotivo();
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create,
                    [new(M.Nombre, null, nombre), new(M.Clase, null, clase.Value.ToString()), new(M.CodigoDian, null, dian), new(M.VigenteDesde, null, desde.Value.ToString("yyyy-MM-dd"))]);
                continue;
            }
            var despues = Resumen(medio);
            var campos = antes.Where(a => !string.Equals(a.Value, despues[a.Key], StringComparison.Ordinal))
                .Select(a => new CampoCambiadoDto(a.Key, a.Value, despues[a.Key])).ToList();
            if (listaPuntos is not null) campos.Add(new(M.Puntos, null, string.Join(", ", listaPuntos)));
            if (listaCanales is not null) campos.Add(new(M.Canales, null, string.Join(", ", listaCanales)));
            if (listaTipos is not null) campos.Add(new(M.TiposDeDocumento, null, string.Join(", ", listaTipos)));
            if (campos.Any(c => c.Column is M.Tolerancia or M.ComisionPorcentaje or M.ComisionValor or M.VigenteDesde or M.VigenteHasta)) ctx.PedirMotivo();
            ctx.Registrar(fila, codigo, Accion(campos), campos);
        }
    }

    private static async Task<IReadOnlyCollection<int>> IdsVigentesAsync(IQueryable<int> consulta, MedioDePago medio, CancellationToken ct) =>
        medio.Id == 0 ? [] : await consulta.ToListAsync(ct);

    /// <summary>
    /// Los Id de una lista de la fila: nulo si la celda vino vacía o con <c>*</c> (el conjunto no se toca o se vacía según
    /// <see cref="Todos"/>), la lista si vino explícita. Un código que no existe es <c>Import.Cell.NotFound</c>.
    /// </summary>
    private static IReadOnlyCollection<int>? Resolver<T>(FilaDeImportacion fila, string columna, IReadOnlyList<string>? lista, CatalogoCitado<T> catalogo,
        Func<T, int> id, string que, string dondeSeCrea)
    {
        if (lista is null) return null;
        if (FilaDeImportacion.EsTodos(lista)) return [];
        var ids = new List<int>();
        foreach (var codigo in lista)
        {
            if (catalogo.Buscar(codigo, out var valor)) ids.Add(id(valor));
            else fila.Error(columna, ImportErrors.CellNotFound, $"No hay {que} «{codigo}». Créelo {dondeSeCrea}.");
        }
        return ids;
    }

    /// <summary>La marca «todos»: <c>*</c> la prende, una lista la apaga, vacío la deja (en un medio nuevo, prendida).</summary>
    private static bool Todos(IReadOnlyList<string>? lista, bool? actual) =>
        lista is null ? actual ?? true : FilaDeImportacion.EsTodos(lista);

    private static byte? TipoDeCuenta(FilaDeImportacion fila)
    {
        var crudo = fila.Crudo(M.TipoCuentaDestino);
        if (crudo is null) return null;
        switch (TablaLeida.Normalizar(crudo))
        {
            case "ahorros" or "1": return 1;
            case "corriente" or "2": return 2;
            default:
                fila.Error(M.TipoCuentaDestino, ImportErrors.CellFormat, $"«{crudo}» no es válido en «{M.TipoCuentaDestino}». Admite: Ahorros, Corriente.");
                return null;
        }
    }

    private static short? Periodicidad(FilaDeImportacion fila)
    {
        var crudo = fila.Crudo(M.Periodicidad);
        if (crudo is null) return null;
        if (M.Periodicidades.TryGetValue(TablaLeida.Normalizar(crudo), out var dias)) return dias;
        fila.Error(M.Periodicidad, ImportErrors.CellFormat, $"«{crudo}» no es válido en «{M.Periodicidad}». Admite: Mensual, Quincenal, Semanal.");
        return null;
    }

    /// <summary>La columna de la plantilla donde cae un error de la regla del medio (<c>data.field</c> del alta una a una).</summary>
    private static string ColumnaDe(Error error)
    {
        if (error.Code == "Catalogo.CodigoDuplicado") return M.Codigo;
        if (error.Code == PaymentMeansErrors.InUseCode) return M.Clase;
        var campo = error is ErrorConDatos { Data: { } data } ? data.GetType().GetProperty("field")?.GetValue(data) as string : null;
        return campo switch
        {
            "allowsChange" => M.AdmiteVueltas,
            "countMethod" => M.Arqueo,
            "cardNetworkPublicId" => M.Franquicia,
            "cardAcquirerPublicId" => M.Adquirente,
            "referenceKind" => M.TipoReferencia,
            "referenceMaxLength" => M.LargoMaximoReferencia,
            "bankPublicId" => M.Banco,
            "creditDefaults" => M.PlazoDias,
            "dianPaymentMeansCode" => M.CodigoDian,
            "validTo" => M.VigenteHasta,
            "quickKey" => M.TeclaRapida,
            "toleranceAmount" => M.Tolerancia,
            "expectedCommissionRate" => M.ComisionPorcentaje,
            _ => M.Codigo,
        };
    }

    /// <summary>Lo que la revisión compara de un medio, por columna.</summary>
    private static Dictionary<string, string?> Resumen(MedioDePago m)
    {
        var inv = CultureInfo.InvariantCulture;
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [M.Nombre] = m.Name,
            [M.Orden] = m.DisplayOrder.ToString(inv),
            [M.TeclaRapida] = m.QuickKey,
            [M.Clase] = m.Class.ToString(),
            [M.Arqueo] = m.CountMethod.ToString(),
            [M.Tolerancia] = m.ToleranceAmount.ToString("0.##", inv),
            [M.ComisionPorcentaje] = m.ExpectedCommissionRate?.ToString(inv),
            [M.ComisionValor] = m.ExpectedCommissionFixed?.ToString("0.##", inv),
            [M.CodigoDian] = m.DianPaymentMeansCode,
            [M.ExigeReferencia] = SiNo(m.RequiresReference),
            [M.AdmiteVueltas] = SiNo(m.AllowsChange),
            [M.AdmitePagoParcial] = SiNo(m.AllowsPartial),
            [M.VigenteDesde] = m.ValidFrom.ToString("yyyy-MM-dd", inv),
            [M.VigenteHasta] = m.ValidTo?.ToString("yyyy-MM-dd", inv),
            [M.Activo] = SiNo(m.IsActive),
        };
    }

    private static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        if (!string.Equals(antes ?? string.Empty, despues ?? string.Empty, StringComparison.Ordinal)) campos.Add(new(columna, antes, despues));
    }

    private static AccionDeImportacion Accion(List<CampoCambiadoDto> campos) => campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update;

    private static string SiNo(bool valor) => valor ? "sí" : "no";
}
