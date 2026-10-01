using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

// =====================================================================================================================
// Las diez vistas de ventas y caja de I3 (feature 012, T623; contracts/api.md §27; decisiones-transversales §2.12). Todas
// devuelven TablaExportable con las columnas de §27 (las ocultas `_documento` y `_sesion` para profundizar), aplican el alcance
// por punto de venta (IAlcanceDeInventario; los documentos sin punto, por su bodega) y, las que leen sesiones de caja, sólo las
// del cajero salvo Inventory.CashSessions.ViewAll (SesionesDeCaja). La auditoría Inventory.Report.Exported la emite la ruta
// (MapVistaDeInventario), no estas consultas. (nuevo)
// =====================================================================================================================

/// <summary>Lo común de las vistas de ventas y caja: clases, filtros resueltos con el alcance y nombres. (nuevo)</summary>
public sealed class DatosDeVentasYCaja(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion)
{
    /// <summary>Las clases que suman como venta: facturas, POS, recibos no electrónicos y notas débito.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesDeVenta =
    [
        DocumentClass.SalesInvoice, DocumentClass.SalesInvoiceFromShipments, DocumentClass.PosEquivalentDocument,
        DocumentClass.NonElectronicSalesReceipt, DocumentClass.DebitNote,
    ];

    /// <summary>Las clases que restan como devolución: notas crédito, notas de venta no electrónicas y notas de ajuste POS.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesDeDevolucion =
    [
        DocumentClass.CreditNote, DocumentClass.NonElectronicSalesNote, DocumentClass.PosAdjustmentNote,
    ];

    public const string AgruparPorCliente = "customer";
    public const string AgruparPorDia = "day";

    private AlcanceDeInventario? _alcance;

    public async Task<AlcanceDeInventario> AlcanceAsync(CancellationToken ct) => _alcance ??= await alcanceDeLaPeticion.ObtenerAsync(ct);

    /// <summary>Los filtros de punto, caja y sesión resueltos a Ids internos, dentro del alcance; fuera, el 404 de cada uno.</summary>
    public async Task<Result<FiltrosDeCaja>> FiltrosAsync(FiltrosDeInformeDeInventario f, CancellationToken ct)
    {
        var alcance = await AlcanceAsync(ct);
        int? punto = null, caja = null, sesion = null;
        if (f.PointOfSale is { } pp)
        {
            punto = await db.PointsOfSale.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (punto is null || !alcance.IncluyePunto(punto.Value)) return Result.Failure<FiltrosDeCaja>(ErroresDeAlcance.PuntoInexistente());
        }
        if (f.CashRegister is { } cp)
        {
            var c = await db.CashRegisters.AsNoTracking().Where(x => x.PublicId == cp).Select(x => new { x.Id, x.PointOfSaleId }).FirstOrDefaultAsync(ct);
            if (c is null || !alcance.IncluyePunto(c.PointOfSaleId)) return Result.Failure<FiltrosDeCaja>(ErroresDePuntoDeVenta.CashRegisterNotFound());
            caja = c.Id;
        }
        if (f.Session is { } sp)
        {
            var s = await db.CashSessions.AsNoTracking().Where(x => x.PublicId == sp).Select(x => new { x.Id, x.PointOfSaleId }).FirstOrDefaultAsync(ct);
            if (s is null || !alcance.IncluyePunto(s.PointOfSaleId)) return Result.Failure<FiltrosDeCaja>(ErroresDeCaja.SessionNotFound());
            sesion = s.Id;
        }
        return Result.Success(new FiltrosDeCaja(punto, caja, sesion));
    }

    /// <summary>Los documentos que quien pregunta ve (por su punto o por su bodega), en el rango y con los filtros de caja.</summary>
    public async Task<IQueryable<InventoryDocument>> DocumentosAsync(FiltrosDeInformeDeInventario f, FiltrosDeCaja c, DateOnly hoy, CancellationToken ct)
    {
        var alcance = await AlcanceAsync(ct);
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var q = db.InventoryDocuments.AsNoTracking().DocumentosVisibles(alcance, db.DocumentLinks, db.InventoryDocuments)
            .Where(d => d.OperationDate >= desde && d.OperationDate <= hasta);
        if (c.PuntoId is int p) q = q.Where(d => d.PointOfSaleId == p);
        if (c.CajaId is int k) q = q.Where(d => d.CashRegisterId == k);
        if (c.SesionId is int s) q = q.Where(d => d.CashSessionId == s);
        return q;
    }

    /// <summary>Los códigos de puntos, cajas y medios, y los nombres de usuarios, de una vez.</summary>
    public async Task<Dictionary<int, string>> PuntosAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.PointsOfSale.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
    }

    public async Task<Dictionary<int, string>> CajasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
    }

    public async Task<Dictionary<int, string>> UsuariosAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => lista.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
    }

    /// <summary>El cliente de cada documento: la copia fiscal de mayor versión; sin copia, la persona; sin persona, nada.</summary>
    public async Task<Dictionary<int, string>> ClientesAsync(IReadOnlyCollection<(int DocumentId, int? PersonId)> documentos, CancellationToken ct)
    {
        var ids = documentos.Select(d => d.DocumentId).Distinct().ToList();
        var copias = (await db.DocumentPartySnapshots.AsNoTracking().Where(s => ids.Contains(s.DocumentId))
                .Select(s => new { s.DocumentId, s.Version, s.TaxId, s.LegalName }).ToListAsync(ct))
            .GroupBy(s => s.DocumentId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Version).First());
        var personaIds = documentos.Where(d => !copias.ContainsKey(d.DocumentId) && d.PersonId is not null).Select(d => d.PersonId!.Value).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id))
            .Select(p => new { p.Id, p.TaxId, p.BusinessName, p.FirstName, p.LastName }).ToDictionaryAsync(p => p.Id, ct);
        var resultado = new Dictionary<int, string>();
        foreach (var (documento, persona) in documentos)
        {
            if (copias.TryGetValue(documento, out var c)) resultado[documento] = $"{c.TaxId} {c.LegalName}".Trim();
            else if (persona is int pid && personas.TryGetValue(pid, out var p))
                resultado[documento] = $"{p.TaxId} {(string.IsNullOrWhiteSpace(p.BusinessName) ? $"{p.FirstName} {p.LastName}".Trim() : p.BusinessName)}".Trim();
        }
        return resultado;
    }

    /// <summary>Quién aprobó cada fuente (documento o conteo) según las decisiones de aprobación: la última que aprobó.</summary>
    public async Task<Dictionary<Guid, string>> AprobadoresAsync(IReadOnlyCollection<Guid> fuentes, CancellationToken ct)
    {
        if (fuentes.Count == 0) return [];
        var lista = fuentes.Distinct().ToList();
        var decisiones = await (from d in db.ApprovalDecisions.AsNoTracking()
                                join r in db.ApprovalRequests.AsNoTracking() on d.RequestId equals r.Id
                                where lista.Contains(r.SourcePublicId) && d.Decision == ApprovalDecisionKind.Approve
                                select new { r.SourcePublicId, d.DecidedByName, d.DecidedAt }).ToListAsync(ct);
        return decisiones.GroupBy(d => d.SourcePublicId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.DecidedAt).First().DecidedByName);
    }

    /// <summary>El texto de un estado de documento en pantalla.</summary>
    public static string Estado(DocumentStatus estado) => estado switch
    {
        DocumentStatus.Draft => "Borrador",
        DocumentStatus.PendingApproval => "En aprobación",
        DocumentStatus.Confirmed => "Confirmado",
        DocumentStatus.Voided => "Anulado",
        DocumentStatus.Discarded => "Descartado",
        _ => estado.ToString(),
    };

    public static string Tratamiento(CashDifferenceTreatment? t) => t switch
    {
        CashDifferenceTreatment.Surplus => "Sobrante",
        CashDifferenceTreatment.ShortageToCashier => "Faltante a cargo del cajero",
        CashDifferenceTreatment.ShortageToExpense => "Faltante al gasto",
        _ => string.Empty,
    };

    public static string TipoDeMovimiento(CashMovementKind k) => k switch
    {
        CashMovementKind.WithdrawalToSafe => "Retiro a caja fuerte",
        CashMovementKind.WithdrawalToRegister => "Retiro a otra caja",
        CashMovementKind.WithdrawalForDeposit => "Retiro para consignar",
        CashMovementKind.BaseIncome => "Ingreso de base",
        CashMovementKind.ReclassificationBetweenMeans => "Reclasificación entre medios",
        _ => k.ToString(),
    };

    public static string Destino(CashMovementDestination? d) => d switch
    {
        CashMovementDestination.Safe => "Caja fuerte",
        CashMovementDestination.Register => "Otra caja",
        CashMovementDestination.Deposit => "Consignación",
        _ => string.Empty,
    };

    /// <summary>El subtítulo con el rango de fechas.</summary>
    public static string Rango(FiltrosDeInformeDeInventario f, DateOnly hoy) =>
        $"Del {f.Desde(hoy).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} al {f.Hasta(hoy).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
}

/// <summary>Los filtros de punto, caja y sesión ya resueltos (Ids internos). (nuevo)</summary>
public sealed record FiltrosDeCaja(int? PuntoId, int? CajaId, int? SesionId);

/// <summary>Las cifras de ventas de un grupo de documentos (bruto, descuentos, impuestos, devoluciones y neto). (nuevo)</summary>
internal sealed record CifrasDeVenta(int Documentos, decimal Bruto, decimal Descuentos, decimal Impuestos, decimal Devoluciones)
{
    public decimal Neto => Bruto - Descuentos + Impuestos - Devoluciones;

    public static CifrasDeVenta De(IEnumerable<FilaDeVenta> filas)
    {
        var l = filas.ToList();
        var ventas = l.Where(x => !x.EsDevolucion).ToList();
        return new CifrasDeVenta(l.Count, ventas.Sum(x => x.Subtotal), ventas.Sum(x => x.DiscountTotal), ventas.Sum(x => x.TaxTotal),
            l.Where(x => x.EsDevolucion).Sum(x => x.Total));
    }

    public object?[] Valores() => [Documentos, Bruto, Descuentos, Impuestos, Devoluciones, Neto];
}

/// <summary>Un documento de venta o devolución confirmado, tal como lo leen las vistas de ventas. (nuevo)</summary>
internal sealed record FilaDeVenta(
    int Id, int? PointOfSaleId, int? CashRegisterId, int? CashSessionId, int? PersonId, DateOnly OperationDate,
    DocumentClass Class, decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal Total)
{
    public bool EsDevolucion => DatosDeVentasYCaja.ClasesDeDevolucion.Contains(Class);

    public static async Task<List<FilaDeVenta>> LeerAsync(IQueryable<InventoryDocument> documentos, CancellationToken ct)
    {
        var clases = DatosDeVentasYCaja.ClasesDeVenta.Concat(DatosDeVentasYCaja.ClasesDeDevolucion).ToList();
        return await documentos.Where(d => d.Status == DocumentStatus.Confirmed && clases.Contains(d.Class))
            .Select(d => new FilaDeVenta(d.Id, d.PointOfSaleId, d.CashRegisterId, d.CashSessionId, d.CounterpartyPersonId, d.OperationDate, d.Class,
                d.Subtotal, d.DiscountTotal, d.TaxTotal, d.Total))
            .ToListAsync(ct);
    }
}

// ------------------------------------------------------------------------------------------------ sales-by-session --

/// <summary>
/// <c>sales-by-session</c>: por sesión de caja, documentos, ventas brutas, descuentos, impuestos, devoluciones y neto (confirmados). Sólo
/// las sesiones visibles (alcance por punto; ajenas con <c>CashSessions.ViewAll</c>). Filtros <c>from</c>/<c>to</c> (fecha operativa),
/// <c>pointOfSale</c>, <c>cashRegister</c>, <c>session</c> y los propios <c>cashier</c> (usuario) y <c>groupBy=customer</c>, que abre
/// cada sesión por cliente (datos personales). (nuevo)
/// </summary>
public sealed record SalesBySessionReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Cashier = null, string? GroupBy = null) : IRequest<Result<TablaExportable>>;

public sealed class SalesBySessionReportQueryHandler(
    IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, SesionesDeCaja sesiones, IDateTimeService reloj)
    : IRequestHandler<SalesBySessionReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "sales-by-session", "Ventas por sesión de caja", "Por sesión: documentos, ventas brutas, descuentos, impuestos, devoluciones y neto.",
        "ventas-por-sesion", ["from", "to", "pointOfSale", "cashRegister", "session"], ["cashier", "groupBy"],
        PersonalDataWhen: "groupBy=customer");

    public async Task<Result<TablaExportable>> Handle(SalesBySessionReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);
        var c = filtros.Value;
        var porCliente = string.Equals(request.GroupBy, DatosDeVentasYCaja.AgruparPorCliente, StringComparison.OrdinalIgnoreCase);

        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var q = (await sesiones.VisiblesAsync(ct)).Where(s => s.OperatingDate >= desde && s.OperatingDate <= hasta);
        if (c.PuntoId is int p) q = q.Where(s => s.PointOfSaleId == p);
        if (c.CajaId is int k) q = q.Where(s => s.CashRegisterId == k);
        if (c.SesionId is int si) q = q.Where(s => s.Id == si);
        if (request.Cashier is { } cajero)
        {
            var usuario = await db.Users.AsNoTracking().Where(u => u.PublicId == cajero).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(s => s.CashierUserId == usuario);
        }
        var lista = await q.OrderBy(s => s.OperatingDate).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.PublicId, s.PointOfSaleId, s.CashRegisterId, s.CashierName, s.OperatingDate }).ToListAsync(ct);
        var ids = lista.Select(s => s.Id).ToList();
        // Los documentos de la sesión: los del POS la llevan en el encabezado; los de oficina, en los pagos que se arquean en ella.
        var porPago = await db.DocumentPayments.AsNoTracking()
            .Where(x => x.CashSessionId != null && ids.Contains(x.CashSessionId.Value) && !x.IsDeleted)
            .Select(x => new { x.DocumentId, Sesion = x.CashSessionId!.Value }).Distinct().ToListAsync(ct);
        var docsPorPago = porPago.Select(x => x.DocumentId).Distinct().ToList();
        var ventas = (await FilaDeVenta.LeerAsync(db.InventoryDocuments.AsNoTracking()
                .Where(d => (d.CashSessionId != null && ids.Contains(d.CashSessionId.Value)) || docsPorPago.Contains(d.Id)), ct))
            .Select(v => v.CashSessionId is not null ? v : v with { CashSessionId = porPago.Where(x => x.DocumentId == v.Id).Min(x => x.Sesion) })
            .ToList();
        var puntos = await datos.PuntosAsync(lista.Select(s => s.PointOfSaleId), ct);
        var cajas = await datos.CajasAsync(lista.Select(s => s.CashRegisterId), ct);
        var clientes = porCliente ? await datos.ClientesAsync(ventas.Select(v => (v.Id, v.PersonId)).ToList(), ct) : [];

        var columnas = new List<ColumnaExportable>
        {
            new("Sesión"), new("Punto"), new("Caja"), new("Cajero"), new("Fecha operativa", TipoDeColumna.Fecha),
        };
        if (porCliente) columnas.Add(new("Cliente"));
        columnas.AddRange(ColumnasDeCifras());
        columnas.Add(new("Sesión", TipoDeColumna.Texto, "_sesion"));

        var filas = new List<FilaExportable>();
        foreach (var s in lista)
        {
            var suyas = ventas.Where(v => v.CashSessionId == s.Id).ToList();
            var cabeza = new object?[] { s.PublicId.ToString("N")[..8].ToUpperInvariant(), puntos.GetValueOrDefault(s.PointOfSaleId), cajas.GetValueOrDefault(s.CashRegisterId), s.CashierName, s.OperatingDate };
            if (!porCliente)
            {
                filas.Add(new FilaExportable([.. cabeza, .. CifrasDeVenta.De(suyas).Valores(), s.PublicId.ToString()]));
                continue;
            }
            foreach (var g in suyas.GroupBy(v => clientes.GetValueOrDefault(v.Id) ?? "Sin cliente").OrderBy(g => g.Key, StringComparer.CurrentCulture))
                filas.Add(new FilaExportable([.. cabeza, g.Key, .. CifrasDeVenta.De(g).Valores(), s.PublicId.ToString()]));
        }
        var total = CifrasDeVenta.De(ventas).Valores();
        var vacios = Enumerable.Repeat<object?>(null, porCliente ? 5 : 4);
        var totales = new FilaExportable(["Total", .. vacios, .. total, null]);
        return Result.Success(new TablaExportable("Ventas por sesión de caja", DatosDeVentasYCaja.Rango(f, hoy), columnas, filas, totales,
            ["Documentos confirmados de la sesión. Neto = ventas brutas − descuentos + impuestos − devoluciones."]));
    }

    internal static IEnumerable<ColumnaExportable> ColumnasDeCifras() =>
    [
        new("Documentos", TipoDeColumna.Entero), new("Ventas brutas", TipoDeColumna.Moneda), new("Descuentos", TipoDeColumna.Moneda),
        new("Impuestos", TipoDeColumna.Moneda), new("Devoluciones", TipoDeColumna.Moneda), new("Neto", TipoDeColumna.Moneda),
    ];
}

// ----------------------------------------------------------------------------------------------- sales-by-register --

/// <summary>
/// <c>sales-by-register</c>: por punto y caja, por día (<c>groupBy=day</c>, por defecto) o por cliente (<c>groupBy=customer</c>, datos
/// personales): documentos, ventas brutas, descuentos, impuestos, devoluciones y neto. Alcance por punto. (nuevo)
/// </summary>
public sealed record SalesByRegisterReportQuery(FiltrosDeInformeDeInventario Filtros, string? GroupBy = null) : IRequest<Result<TablaExportable>>;

public sealed class SalesByRegisterReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<SalesByRegisterReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "sales-by-register", "Ventas por caja", "Por punto y caja, por día o por cliente: documentos, ventas brutas, descuentos, impuestos, devoluciones y neto.",
        "ventas-por-caja", ["from", "to", "pointOfSale", "cashRegister"], ["groupBy"], PersonalDataWhen: "groupBy=customer");

    public async Task<Result<TablaExportable>> Handle(SalesByRegisterReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);
        var porCliente = string.Equals(request.GroupBy, DatosDeVentasYCaja.AgruparPorCliente, StringComparison.OrdinalIgnoreCase);

        var alcance = await datos.AlcanceAsync(ct);
        var documentos = (await datos.DocumentosAsync(f, filtros.Value, hoy, ct)).Where(d => d.CashRegisterId != null && d.PointOfSaleId != null);
        var ventas = (await FilaDeVenta.LeerAsync(documentos, ct)).Where(v => alcance.IncluyePunto(v.PointOfSaleId!.Value)).ToList();
        var puntos = await datos.PuntosAsync(ventas.Select(v => v.PointOfSaleId!.Value), ct);
        var cajas = await datos.CajasAsync(ventas.Select(v => v.CashRegisterId!.Value), ct);
        var clientes = porCliente ? await datos.ClientesAsync(ventas.Select(v => (v.Id, v.PersonId)).ToList(), ct) : [];

        var columnas = new List<ColumnaExportable> { new("Punto"), new("Caja"), porCliente ? new("Cliente") : new("Día", TipoDeColumna.Fecha) };
        columnas.AddRange(SalesBySessionReportQueryHandler.ColumnasDeCifras());
        var filas = ventas
            .GroupBy(v => (Punto: puntos.GetValueOrDefault(v.PointOfSaleId!.Value) ?? string.Empty, Caja: cajas.GetValueOrDefault(v.CashRegisterId!.Value) ?? string.Empty,
                Clave: porCliente ? (object)(clientes.GetValueOrDefault(v.Id) ?? "Sin cliente") : v.OperationDate))
            .OrderBy(g => g.Key.Punto, StringComparer.Ordinal).ThenBy(g => g.Key.Caja, StringComparer.Ordinal).ThenBy(g => g.Key.Clave.ToString(), StringComparer.Ordinal)
            .Select(g => new FilaExportable([g.Key.Punto, g.Key.Caja, g.Key.Clave, .. CifrasDeVenta.De(g).Valores()]))
            .ToList();
        var totales = new FilaExportable(["Total", null, null, .. CifrasDeVenta.De(ventas).Valores()]);
        return Result.Success(new TablaExportable("Ventas por caja", DatosDeVentasYCaja.Rango(f, hoy) + (porCliente ? " · por cliente" : " · por día"),
            columnas, filas, totales, ["Documentos confirmados de cada caja. Neto = ventas brutas − descuentos + impuestos − devoluciones."]));
    }
}

// ------------------------------------------------------------------------------------------ sales-by-payment-means --

/// <summary>
/// <c>sales-by-payment-means</c>: por medio, clase, punto y caja: pagos, recibido, reintegrado y neto de los documentos confirmados.
/// Filtros propios <c>paymentMeans</c> (PublicId), <c>class</c> y <c>groupBy=customer</c> (datos personales). (nuevo)
/// </summary>
public sealed record SalesByPaymentMeansReportQuery(
    FiltrosDeInformeDeInventario Filtros, Guid? PaymentMeans = null, PaymentMeansClass? Class = null, string? GroupBy = null) : IRequest<Result<TablaExportable>>;

public sealed class SalesByPaymentMeansReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<SalesByPaymentMeansReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "sales-by-payment-means", "Ventas por medio de pago", "Por medio, punto y caja: pagos recibidos, reintegrados y neto.",
        "ventas-por-medio-de-pago", ["from", "to", "pointOfSale", "cashRegister", "session"], ["paymentMeans", "class", "groupBy"],
        PersonalDataWhen: "groupBy=customer");

    public async Task<Result<TablaExportable>> Handle(SalesByPaymentMeansReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);
        var porCliente = string.Equals(request.GroupBy, DatosDeVentasYCaja.AgruparPorCliente, StringComparison.OrdinalIgnoreCase);

        var documentos = (await datos.DocumentosAsync(f, filtros.Value, hoy, ct)).Where(d => d.Status == DocumentStatus.Confirmed);
        var pagos = from p in db.DocumentPayments.AsNoTracking()
                    join d in documentos on p.DocumentId equals d.Id
                    where !p.IsDeleted
                    select new { p.PaymentMeansId, p.MeansCode, p.MeansName, p.MeansClass, p.Direction, p.Amount, d.Id, d.PointOfSaleId, d.CashRegisterId, d.CounterpartyPersonId };
        if (request.PaymentMeans is { } mp)
        {
            var medio = await db.PaymentMeans.AsNoTracking().Where(m => m.PublicId == mp).Select(m => (int?)m.Id).FirstOrDefaultAsync(ct) ?? -1;
            pagos = pagos.Where(p => p.PaymentMeansId == medio);
        }
        if (request.Class is { } clase) pagos = pagos.Where(p => p.MeansClass == clase);
        var lista = await pagos.ToListAsync(ct);
        var puntos = await datos.PuntosAsync(lista.Where(p => p.PointOfSaleId != null).Select(p => p.PointOfSaleId!.Value), ct);
        var cajas = await datos.CajasAsync(lista.Where(p => p.CashRegisterId != null).Select(p => p.CashRegisterId!.Value), ct);
        var clientes = porCliente ? await datos.ClientesAsync(lista.Select(p => (p.Id, p.CounterpartyPersonId)).Distinct().ToList(), ct) : [];

        var columnas = new List<ColumnaExportable> { new("Medio"), new("Clase"), new("Punto"), new("Caja") };
        if (porCliente) columnas.Add(new("Cliente"));
        columnas.AddRange([new("Pagos", TipoDeColumna.Entero), new("Recibido", TipoDeColumna.Moneda), new("Reintegrado", TipoDeColumna.Moneda), new("Neto", TipoDeColumna.Moneda)]);

        var filas = lista
            .GroupBy(p => (Medio: $"{p.MeansCode} · {p.MeansName}", p.MeansClass,
                Punto: p.PointOfSaleId is int pp ? puntos.GetValueOrDefault(pp) ?? string.Empty : string.Empty,
                Caja: p.CashRegisterId is int cc ? cajas.GetValueOrDefault(cc) ?? string.Empty : string.Empty,
                Cliente: porCliente ? clientes.GetValueOrDefault(p.Id) ?? "Sin cliente" : string.Empty))
            .OrderBy(g => g.Key.Medio, StringComparer.Ordinal).ThenBy(g => g.Key.Punto, StringComparer.Ordinal).ThenBy(g => g.Key.Caja, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Cliente, StringComparer.CurrentCulture)
            .Select(g =>
            {
                var recibido = g.Where(p => p.Direction == PaymentDirection.Received).Sum(p => p.Amount);
                var reintegrado = g.Where(p => p.Direction == PaymentDirection.Refunded).Sum(p => p.Amount);
                var cabeza = new List<object?> { g.Key.Medio, g.Key.MeansClass.ToString(), g.Key.Punto, g.Key.Caja };
                if (porCliente) cabeza.Add(g.Key.Cliente);
                return new FilaExportable([.. cabeza, g.Count(), recibido, reintegrado, recibido - reintegrado]);
            })
            .ToList();
        var recibidoTotal = lista.Where(p => p.Direction == PaymentDirection.Received).Sum(p => p.Amount);
        var reintegradoTotal = lista.Where(p => p.Direction == PaymentDirection.Refunded).Sum(p => p.Amount);
        var totales = new FilaExportable(["Total", .. Enumerable.Repeat<object?>(null, porCliente ? 4 : 3), lista.Count, recibidoTotal, reintegradoTotal, recibidoTotal - reintegradoTotal]);
        return Result.Success(new TablaExportable("Ventas por medio de pago", DatosDeVentasYCaja.Rango(f, hoy), columnas, filas, totales,
            ["Pagos de los documentos confirmados: recibidos en ventas y notas débito, reintegrados en notas crédito."]));
    }
}

// ----------------------------------------------------------------------------------------------------- cash-session --

/// <summary>
/// <c>cash-session</c> (sesión obligatoria): tres secciones. «Por medio»: base, ventas, devoluciones, movimientos y reclasificaciones
/// netos, esperado, contado, diferencia, tolerancia, tratamiento y motivo (el último arqueo); con arqueo ciego y sin
/// <c>CashSessions.ViewAll</c>, las cifras esperadas salen vacías mientras la sesión esté abierta. «Tarjetas»: por datáfono y lote, pagos y
/// total esperado y el total del lote contado. «Movimientos»: cada movimiento de caja que la toca. Una sesión ajena sin
/// <c>ViewAll</c> es el 404 de la sesión. (nuevo)
/// </summary>
public sealed record CashSessionReportQuery(FiltrosDeInformeDeInventario Filtros) : IRequest<Result<TablaExportable>>;

public sealed class CashSessionReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, SesionesDeCaja sesiones)
    : IRequestHandler<CashSessionReportQuery, Result<TablaExportable>>
{
    public const string SeccionPorMedio = "Por medio de pago";
    public const string SeccionTarjetas = "Tarjetas";
    public const string SeccionMovimientos = "Movimientos";

    public static readonly VistaDeInformeDeInventario Vista = new(
        "cash-session", "Sesión de caja", "Una sesión: el esperado y el contado por medio, las tarjetas por datáfono y lote, y los movimientos de caja.",
        "sesion-de-caja", ["session"], []);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Medio / detalle"), new("Base", TipoDeColumna.Moneda), new("Ventas", TipoDeColumna.Moneda), new("Devoluciones", TipoDeColumna.Moneda),
        new("Movimientos", TipoDeColumna.Moneda), new("Reclasificaciones", TipoDeColumna.Moneda), new("Esperado", TipoDeColumna.Moneda),
        new("Contado", TipoDeColumna.Moneda), new("Diferencia", TipoDeColumna.Moneda), new("Tolerancia", TipoDeColumna.Moneda),
        new("Tratamiento"), new("Motivo"),
        new("Documento", TipoDeColumna.Texto, "_documento"), new("Sesión", TipoDeColumna.Texto, "_sesion"),
    ];

    public async Task<Result<TablaExportable>> Handle(CashSessionReportQuery request, CancellationToken ct)
    {
        _ = alcanceDeLaPeticion; // el alcance por punto lo aplica SesionesDeCaja.VisibleAsync
        if (request.Filtros.Session is not { } sp) return Result.Failure<TablaExportable>(new ErrorConDatos("Validation.Invalid", "Indique la sesión.", new { field = "session" }));
        var sesion = await sesiones.VisibleAsync(sp, seguir: false, ct);
        if (sesion is null) return Result.Failure<TablaExportable>(ErroresDeCaja.SessionNotFound());
        var clave = sesion.PublicId.ToString();

        var esperado = await sesiones.EsperadoDtoAsync(sesion, ct);
        var conteo = await db.CashCounts.AsNoTracking().Where(c => c.CashSessionId == sesion.Id && !c.IsDeleted).OrderByDescending(c => c.Id)
            .Select(c => new { c.Id }).FirstOrDefaultAsync(ct);
        var contado = conteo is null
            ? []
            : await db.CashCountLines.AsNoTracking().Where(l => l.CashCountId == conteo.Id && !l.IsDeleted).ToListAsync(ct);
        var mediosIds = contado.Select(l => l.PaymentMeansId).ToList();
        var codigos = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => mediosIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code, ct);
        var contadoPorMedio = contado.ToDictionary(l => codigos.GetValueOrDefault(l.PaymentMeansId) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var ciego = esperado.Blind && sesion.EstaAbierta;

        var filas = new List<FilaExportable>();
        foreach (var l in esperado.Lines)
        {
            contadoPorMedio.TryGetValue(l.PaymentMeans.Code, out var c);
            decimal? Cifra(decimal? v) => ciego ? null : v;
            filas.Add(new FilaExportable(
            [
                $"{l.PaymentMeans.Code} · {l.PaymentMeans.Name}", Cifra(l.OpeningBase), Cifra(l.Sales), Cifra(l.Refunds),
                Cifra((l.MovementsIn ?? 0m) - (l.MovementsOut ?? 0m)), Cifra((l.ReclassificationsIn ?? 0m) - (l.ReclassificationsOut ?? 0m)),
                Cifra(l.Expected), c?.CountedAmount, c?.DifferenceAmount, l.Tolerance, DatosDeVentasYCaja.Tratamiento(c?.Treatment), c?.Reason,
                null, clave,
            ], SeccionPorMedio, Resaltada: c is { WithinTolerance: false }));
        }

        var lotes = conteo is null
            ? []
            : await (from b in db.CashCountTerminalBatches.AsNoTracking()
                     join l in db.CashCountLines.AsNoTracking() on b.CashCountLineId equals l.Id
                     where l.CashCountId == conteo.Id
                     select new { b.CardTerminalId, b.BatchNumber, b.BatchTotal, b.VoucherCount }).ToListAsync(ct);
        foreach (var l in esperado.Lines.Where(x => x.Terminals is { Count: > 0 }))
        {
            foreach (var t in l.Terminals!)
            {
                var terminalId = await db.CardTerminals.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == t.CardTerminalPublicId).Select(x => x.Id).FirstOrDefaultAsync(ct);
                var suyos = lotes.Where(b => b.CardTerminalId == terminalId).ToList();
                var lote = string.Join(", ", suyos.Select(b => b.BatchNumber));
                filas.Add(new FilaExportable(
                [
                    $"{l.PaymentMeans.Code} · {t.AcquirerCode} · datáfono {t.Code}{(lote.Length > 0 ? $" · lote {lote}" : string.Empty)} · {t.PaymentsCount} pagos",
                    null, null, null, null, null, ciego ? null : t.Expected, suyos.Count == 0 ? null : suyos.Sum(b => b.BatchTotal),
                    suyos.Count == 0 || ciego || t.Expected is null ? null : suyos.Sum(b => b.BatchTotal) - t.Expected, null, null, null, null, clave,
                ], SeccionTarjetas));
            }
        }

        var movimientos = await (from m in db.CashMovementDetails.AsNoTracking()
                                 join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
                                 where !m.IsDeleted && (m.CashSessionId == sesion.Id || m.DestinationCashSessionId == sesion.Id)
                                 orderby m.Id
                                 select new { Detalle = m, Documento = d }).ToListAsync(ct);
        var dtos = await VistaDeMovimientosDeCaja.DtosAsync(db, movimientos.Select(x => (x.Detalle, x.Documento)).ToList(), ct);
        foreach (var m in dtos)
        {
            var entra = movimientos.First(x => x.Documento.PublicId == m.DocumentPublicId).Detalle.DestinationCashSessionId == sesion.Id
                        || m.Kind == CashMovementKind.BaseIncome;
            filas.Add(new FilaExportable(
            [
                $"{DatosDeVentasYCaja.TipoDeMovimiento(m.Kind)} {m.Number} · {m.SourcePaymentMeansCode}{(m.TargetPaymentMeansCode is null ? string.Empty : $" → {m.TargetPaymentMeansCode}")}"
                    + $" · {DatosDeVentasYCaja.Estado(m.Status)}",
                null, null, null, entra ? m.Amount : -m.Amount, null, null, null, null, null, null, m.Reason, m.DocumentPublicId.ToString(), clave,
            ], SeccionMovimientos));
        }

        var punto = await db.PointsOfSale.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == sesion.PointOfSaleId).Select(p => p.Code).FirstOrDefaultAsync(ct);
        var caja = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == sesion.CashRegisterId).Select(c => c.Code).FirstOrDefaultAsync(ct);
        var subtitulo = $"Punto {punto} · caja {caja} · {sesion.CashierName} · {sesion.OperatingDate:yyyy-MM-dd}" + (sesion.EstaAbierta ? " · abierta" : " · cerrada");
        var notas = new List<string> { "Movimientos: positivo entra a la sesión, negativo sale. Contado y diferencia son los del último arqueo." };
        if (ciego) notas.Add("Arqueo ciego: el esperado no se muestra mientras la sesión esté abierta.");
        return Result.Success(new TablaExportable("Sesión de caja", subtitulo, Columnas, filas, null, notas));
    }
}

// ------------------------------------------------------------------------------------------------------- day-close --

/// <summary>
/// <c>day-close</c>: el cierre del día de un punto, por <c>dayClose</c> (PublicId) o por <c>pointOfSale</c> + <c>operatingDate</c> (el
/// vigente). Secciones: por medio (esperado, contado, diferencia, pagos), tarjetas (adquirente, datáfono, lotes, total) y sesiones; las
/// sesiones de otros cajeros sólo con <c>CashSessions.ViewAll</c>. (nuevo)
/// </summary>
public sealed record DayCloseReportQuery(FiltrosDeInformeDeInventario Filtros, DateOnly? OperatingDate = null, Guid? DayClose = null) : IRequest<Result<TablaExportable>>;

public sealed class DayCloseReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, SesionesDeCaja sesiones)
    : IRequestHandler<DayCloseReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "day-close", "Cierre del día", "El consolidado de un punto en un día: por medio, tarjetas por adquirente y datáfono, y las sesiones.",
        "cierre-del-dia", ["pointOfSale"], ["operatingDate", "dayClose"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Medio / detalle"), new("Adquirente"), new("Datáfono"), new("Lote"), new("Esperado", TipoDeColumna.Moneda), new("Contado", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda), new("Pagos", TipoDeColumna.Entero), new("Sesión", TipoDeColumna.Texto, "_sesion"),
    ];

    public async Task<Result<TablaExportable>> Handle(DayCloseReportQuery request, CancellationToken ct)
    {
        Guid? id = request.DayClose;
        if (id is null)
        {
            if (request.Filtros.PointOfSale is not { } pp || request.OperatingDate is not { } fecha)
                return Result.Failure<TablaExportable>(new ErrorConDatos("Validation.Invalid", "Indique el cierre o el punto de venta y la fecha operativa.", new { field = "dayClose" }));
            var punto = await db.PointsOfSale.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (punto is null) return Result.Failure<TablaExportable>(ErroresDeAlcance.PuntoInexistente());
            id = await db.DayCloses.AsNoTracking().Where(d => d.PointOfSaleId == punto && d.OperatingDate == fecha && d.Status == DayCloseStatus.Closed)
                .OrderByDescending(d => d.Version).Select(d => (Guid?)d.PublicId).FirstOrDefaultAsync(ct);
            if (id is null) return Result.Failure<TablaExportable>(ErroresDeCaja.DayCloseNotFound());
        }

        var detalle = await new GetDayCloseQueryHandler(db, alcanceDeLaPeticion).Handle(new GetDayCloseQuery(id.Value), ct);
        if (detalle.IsFailure) return Result.Failure<TablaExportable>(detalle.Error);
        var d = detalle.Value;

        var filas = new List<FilaExportable>();
        filas.AddRange(d.Lines.Select(l => new FilaExportable([l.PaymentMeansCode, null, null, null, l.Expected, l.Counted, l.Difference, l.PaymentsCount, null],
            "Por medio de pago", Resaltada: l.Difference != 0m)));
        filas.AddRange(d.Cards.Select(c => new FilaExportable([c.PaymentMeansCode, c.AcquirerCode, c.CardTerminalCode, string.Join(", ", c.BatchNumbers),
            c.Expected, c.Counted, c.Counted - c.Expected, c.Count, null], "Tarjetas")));

        var veTodas = await sesiones.VeTodasAsync(ct);
        var usuario = await sesiones.UsuarioAsync(ct);
        var propias = veTodas
            ? null
            : (await db.CashSessions.AsNoTracking().Where(s => s.CashierUserId == usuario).Select(s => s.PublicId).ToListAsync(ct)).ToHashSet();
        foreach (var s in d.Sessions.Where(s => propias is null || propias.Contains(s.CashSessionPublicId)))
            filas.Add(new FilaExportable([$"{s.CashRegisterCode} · {s.CashierName} · {s.OpenedAt:yyyy-MM-dd HH:mm}–{s.ClosedAt:HH:mm}", null, null, null, null, null, null, null,
                s.CashSessionPublicId.ToString()], "Sesiones"));

        var subtitulo = $"Punto {d.DayClose.PointOfSale.Code} · {d.DayClose.OperatingDate:yyyy-MM-dd} · versión {d.DayClose.Version}";
        var totales = new FilaExportable(["Total", null, null, null, d.Lines.Sum(l => l.Expected), d.Lines.Sum(l => l.Counted), d.Lines.Sum(l => l.Difference),
            d.Lines.Sum(l => l.PaymentsCount), null]);
        var notas = new List<string> { "Consolidado de todas las sesiones del punto en el día." };
        if (propias is not null && propias.Count < d.Sessions.Count) notas.Add("Sólo se listan sus sesiones: las de otros cajeros exigen ver todas las sesiones.");
        return Result.Success(new TablaExportable("Cierre del día", subtitulo, Columnas, filas, totales, notas));
    }
}

// --------------------------------------------------------------------------------------------------- card-payments --

/// <summary>
/// <c>card-payments</c> (datos personales): cada pago con tarjeta crédito o débito de los documentos confirmados, con franquicia,
/// adquirente, datáfono, lote, aprobación y los últimos cuatro —nunca el número—, valor y comisión esperada. Filtros propios
/// <c>acquirer</c>, <c>terminal</c> y <c>network</c> (PublicId). (nuevo)
/// </summary>
public sealed record CardPaymentsReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Acquirer = null, Guid? Terminal = null, Guid? Network = null)
    : IRequest<Result<TablaExportable>>;

public sealed class CardPaymentsReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<CardPaymentsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "card-payments", "Pagos con tarjeta", "Cada pago con tarjeta: franquicia, adquirente, datáfono, lote, aprobación, últimos 4, valor y comisión esperada.",
        "pagos-con-tarjeta", ["from", "to", "pointOfSale", "cashRegister", "session"], ["acquirer", "terminal", "network"], PersonalData: true);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha), new("Documento"), new("Punto"), new("Caja"), new("Cliente"), new("Medio"), new("Franquicia"), new("Adquirente"),
        new("Datáfono"), new("Lote"), new("Aprobación"), new("Últimos 4"), new("Valor", TipoDeColumna.Moneda), new("Comisión esperada", TipoDeColumna.Moneda),
        new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(CardPaymentsReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);

        var documentos = (await datos.DocumentosAsync(f, filtros.Value, hoy, ct)).Where(d => d.Status == DocumentStatus.Confirmed);
        var q = from p in db.DocumentPayments.AsNoTracking()
                join d in documentos on p.DocumentId equals d.Id
                where !p.IsDeleted && (p.MeansClass == PaymentMeansClass.CreditCard || p.MeansClass == PaymentMeansClass.DebitCard)
                select new { Pago = p, d.Id, d.PublicId, d.Prefix, d.Number, d.OperationDate, d.PointOfSaleId, d.CashRegisterId, d.CounterpartyPersonId };
        if (request.Terminal is { } tp)
        {
            var terminal = await db.CardTerminals.AsNoTracking().Where(t => t.PublicId == tp).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(x => x.Pago.CardTerminalId == terminal);
        }
        if (request.Acquirer is { } ap)
        {
            var codigo = await db.CardAcquirers.AsNoTracking().Where(a => a.PublicId == ap).Select(a => a.Code).FirstOrDefaultAsync(ct) ?? "\u0000";
            q = q.Where(x => x.Pago.CardAcquirerCode == codigo);
        }
        if (request.Network is { } np)
        {
            var codigo = await db.CardNetworks.AsNoTracking().Where(n => n.PublicId == np).Select(n => n.Code).FirstOrDefaultAsync(ct) ?? "\u0000";
            q = q.Where(x => x.Pago.CardNetworkCode == codigo);
        }
        var lista = await q.OrderBy(x => x.OperationDate).ThenBy(x => x.Id).ThenBy(x => x.Pago.LineNumber).ToListAsync(ct);
        var puntos = await datos.PuntosAsync(lista.Where(x => x.PointOfSaleId != null).Select(x => x.PointOfSaleId!.Value), ct);
        var cajas = await datos.CajasAsync(lista.Where(x => x.CashRegisterId != null).Select(x => x.CashRegisterId!.Value), ct);
        var terminalIds = lista.Select(x => x.Pago.CardTerminalId).OfType<int>().Distinct().ToList();
        var terminales = await db.CardTerminals.AsNoTracking().IgnoreQueryFilters().Where(t => terminalIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code, ct);
        var clientes = await datos.ClientesAsync(lista.Select(x => (x.Id, x.CounterpartyPersonId)).Distinct().ToList(), ct);

        var filas = lista.Select(x =>
        {
            var p = x.Pago;
            var signo = p.Direction == PaymentDirection.Refunded ? -1m : 1m;
            return new FilaExportable(
            [
                x.OperationDate, VistaDeDocumentos.NumeroVisible(x.Prefix, x.Number), x.PointOfSaleId is int pp ? puntos.GetValueOrDefault(pp) : null,
                x.CashRegisterId is int cc ? cajas.GetValueOrDefault(cc) : null, clientes.GetValueOrDefault(x.Id), $"{p.MeansCode} · {p.MeansName}",
                p.CardNetworkCode, p.CardAcquirerCode, p.CardTerminalId is int t ? terminales.GetValueOrDefault(t) : null, p.TerminalBatchNumber,
                p.AuthorizationCode, p.Last4, signo * p.Amount, p.ExpectedCommissionAmount is { } c ? signo * c : null, x.PublicId.ToString(),
            ]);
        }).ToList();
        var totales = new FilaExportable(["Total", .. Enumerable.Repeat<object?>(null, 11), filas.Sum(r => (decimal)r.Valores[12]!),
            filas.Sum(r => r.Valores[13] is decimal d ? d : 0m), null]);
        return Result.Success(new TablaExportable("Pagos con tarjeta", DatosDeVentasYCaja.Rango(f, hoy), Columnas, filas, totales,
            ["Nunca se guarda el número de la tarjeta: sólo los últimos cuatro. Los reintegros van en negativo."]));
    }
}

// -------------------------------------------------------------------------------------------------- cash-movements --

/// <summary>
/// <c>cash-movements</c>: los movimientos de caja de las sesiones visibles (retiros, ingresos de base, reclasificaciones), con su
/// sesión, medios, destino, valor, motivo, estado y quién los aprobó. Filtro propio <c>kind</c>. (nuevo)
/// </summary>
public sealed record CashMovementsReportQuery(FiltrosDeInformeDeInventario Filtros, CashMovementKind? Kind = null) : IRequest<Result<TablaExportable>>;

public sealed class CashMovementsReportQueryHandler(
    IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, SesionesDeCaja sesiones, IDateTimeService reloj)
    : IRequestHandler<CashMovementsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "cash-movements", "Movimientos de caja", "Retiros, ingresos de base y reclasificaciones, con sus medios, destino, valor, motivo y aprobación.",
        "movimientos-de-caja", ["from", "to", "pointOfSale", "cashRegister", "session"], ["kind"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha), new("Documento"), new("Sesión"), new("Tipo"), new("Medio origen"), new("Medio destino"), new("Destino"),
        new("Caja destino"), new("Valor", TipoDeColumna.Moneda), new("Motivo"), new("Estado"), new("Aprobado por"),
        new("Documento", TipoDeColumna.Texto, "_documento"), new("Sesión", TipoDeColumna.Texto, "_sesion"),
    ];

    public async Task<Result<TablaExportable>> Handle(CashMovementsReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);
        var c = filtros.Value;

        var visibles = await sesiones.VisiblesAsync(ct);
        if (c.PuntoId is int p) visibles = visibles.Where(s => s.PointOfSaleId == p);
        if (c.CajaId is int k) visibles = visibles.Where(s => s.CashRegisterId == k);
        if (c.SesionId is int si) visibles = visibles.Where(s => s.Id == si);
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var q = from m in db.CashMovementDetails.AsNoTracking()
                join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
                join s in visibles on m.CashSessionId equals s.Id
                where !m.IsDeleted && d.OperationDate >= desde && d.OperationDate <= hasta && d.Status != DocumentStatus.Discarded
                select new { Detalle = m, Documento = d };
        if (request.Kind is { } tipo) q = q.Where(x => x.Detalle.Kind == tipo);
        var lista = await q.OrderBy(x => x.Documento.OperationDate).ThenBy(x => x.Detalle.Id).ToListAsync(ct);
        var dtos = await VistaDeMovimientosDeCaja.DtosAsync(db, lista.Select(x => (x.Detalle, x.Documento)).ToList(), ct);
        var aprobadores = await datos.AprobadoresAsync(dtos.Select(d => d.DocumentPublicId).ToList(), ct);

        var filas = dtos.Select((m, i) => new FilaExportable(
        [
            lista[i].Documento.OperationDate, m.Number, m.CashSessionPublicId.ToString("N")[..8].ToUpperInvariant(), DatosDeVentasYCaja.TipoDeMovimiento(m.Kind),
            m.SourcePaymentMeansCode, m.TargetPaymentMeansCode, DatosDeVentasYCaja.Destino(m.Destination), m.DestinationCashRegisterCode, m.Amount, m.Reason,
            DatosDeVentasYCaja.Estado(m.Status), aprobadores.GetValueOrDefault(m.DocumentPublicId), m.DocumentPublicId.ToString(), m.CashSessionPublicId.ToString(),
        ])).ToList();
        var totales = new FilaExportable(["Total", .. Enumerable.Repeat<object?>(null, 7), dtos.Where(m => m.Status == DocumentStatus.Confirmed).Sum(m => m.Amount),
            null, null, null, null, null]);
        return Result.Success(new TablaExportable("Movimientos de caja", DatosDeVentasYCaja.Rango(f, hoy), Columnas, filas, totales,
            ["El total suma sólo los movimientos confirmados."]));
    }
}

// ------------------------------------------------------------------------------------------------ cash-differences --

/// <summary>
/// <c>cash-differences</c>: las diferencias de arqueo (líneas con diferencia distinta de cero del último arqueo de cada sesión visible), con
/// cajero, medio, esperado, contado, diferencia, tolerancia, tratamiento, motivo, quién aprobó y el documento de diferencia con su estado.
/// Filtros propios <c>cashier</c> (usuario) y <c>treatment</c>. (nuevo)
/// </summary>
public sealed record CashDifferencesReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Cashier = null, CashDifferenceTreatment? Treatment = null)
    : IRequest<Result<TablaExportable>>;

public sealed class CashDifferencesReportQueryHandler(
    IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, SesionesDeCaja sesiones, IDateTimeService reloj)
    : IRequestHandler<CashDifferencesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "cash-differences", "Diferencias de arqueo", "Por sesión y medio: esperado, contado, diferencia, tolerancia, tratamiento, motivo y aprobación.",
        "diferencias-de-arqueo", ["from", "to", "pointOfSale", "cashRegister", "session"], ["cashier", "treatment"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Sesión"), new("Cajero"), new("Medio"), new("Esperado", TipoDeColumna.Moneda), new("Contado", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda), new("Tolerancia", TipoDeColumna.Moneda), new("Tratamiento"), new("Motivo"), new("Aprobado por"),
        new("Documento"), new("Estado"), new("Documento", TipoDeColumna.Texto, "_documento"), new("Sesión", TipoDeColumna.Texto, "_sesion"),
    ];

    public async Task<Result<TablaExportable>> Handle(CashDifferencesReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);
        var c = filtros.Value;

        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var visibles = (await sesiones.VisiblesAsync(ct)).Where(s => s.OperatingDate >= desde && s.OperatingDate <= hasta);
        if (c.PuntoId is int p) visibles = visibles.Where(s => s.PointOfSaleId == p);
        if (c.CajaId is int k) visibles = visibles.Where(s => s.CashRegisterId == k);
        if (c.SesionId is int si) visibles = visibles.Where(s => s.Id == si);
        if (request.Cashier is { } cajero)
        {
            var usuario = await db.Users.AsNoTracking().Where(u => u.PublicId == cajero).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct) ?? -1;
            visibles = visibles.Where(s => s.CashierUserId == usuario);
        }
        var lista = await (from l in db.CashCountLines.AsNoTracking()
                           join cc in db.CashCounts.AsNoTracking() on l.CashCountId equals cc.Id
                           join s in visibles on cc.CashSessionId equals s.Id
                           where !l.IsDeleted && !cc.IsDeleted && l.DifferenceAmount != 0m
                                 && cc.Id == db.CashCounts.Where(o => o.CashSessionId == s.Id && !o.IsDeleted).Max(o => o.Id)
                           orderby s.OperatingDate, s.Id, l.Id
                           select new { Linea = l, cc.DifferenceDocumentId, Sesion = s.PublicId, s.CashierName }).ToListAsync(ct);
        if (request.Treatment is { } t) lista = lista.Where(x => x.Linea.Treatment == t).ToList();
        var medioIds = lista.Select(x => x.Linea.PaymentMeansId).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => medioIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code, ct);
        var docIds = lista.Select(x => x.DifferenceDocumentId).OfType<int>().Distinct().ToList();
        var documentos = await db.InventoryDocuments.AsNoTracking().Where(d => docIds.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Prefix, d.Number, d.Status }).ToDictionaryAsync(d => d.Id, ct);
        var aprobadores = await datos.AprobadoresAsync(documentos.Values.Select(d => d.PublicId).ToList(), ct);

        var filas = lista.Select(x =>
        {
            var l = x.Linea;
            var d = x.DifferenceDocumentId is int id ? documentos.GetValueOrDefault(id) : null;
            return new FilaExportable(
            [
                x.Sesion.ToString("N")[..8].ToUpperInvariant(), x.CashierName, medios.GetValueOrDefault(l.PaymentMeansId), l.ExpectedAmount, l.CountedAmount,
                l.DifferenceAmount, l.ToleranceAmount, DatosDeVentasYCaja.Tratamiento(l.Treatment), l.Reason,
                d is null ? (l.WithinTolerance ? "Dentro de la tolerancia" : null) : aprobadores.GetValueOrDefault(d.PublicId),
                d is null ? null : VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), d is null ? null : DatosDeVentasYCaja.Estado(d.Status),
                d?.PublicId.ToString(), x.Sesion.ToString(),
            ], Resaltada: !l.WithinTolerance);
        }).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, lista.Sum(x => x.Linea.DifferenceAmount), null, null, null, null, null, null, null, null]);
        return Result.Success(new TablaExportable("Diferencias de arqueo", DatosDeVentasYCaja.Rango(f, hoy), Columnas, filas, totales,
            ["Del último arqueo de cada sesión. Positivo = sobrante; negativo = faltante."]));
    }
}

// --------------------------------------------------------------------------------------------- voucher-redemptions --

/// <summary>
/// <c>voucher-redemptions</c> (datos personales): cada bono redimido, con su medio, número, documento, fecha, cliente, valor, estado y el
/// documento que lo liberó. Filtros propios <c>paymentMeans</c> y <c>status</c>. (nuevo)
/// </summary>
public sealed record VoucherRedemptionsReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? PaymentMeans = null, VoucherRedemptionStatus? Status = null)
    : IRequest<Result<TablaExportable>>;

public sealed class VoucherRedemptionsReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<VoucherRedemptionsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "voucher-redemptions", "Bonos redimidos", "Cada bono: medio, número, documento, fecha, cliente, valor, estado y quién lo liberó.",
        "bonos-redimidos", ["from", "to", "pointOfSale", "cashRegister", "session"], ["paymentMeans", "status"], PersonalData: true);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Medio"), new("Número de bono"), new("Documento"), new("Fecha", TipoDeColumna.Fecha), new("Cliente"), new("Valor", TipoDeColumna.Moneda),
        new("Estado"), new("Liberado por"), new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(VoucherRedemptionsReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);

        var documentos = await datos.DocumentosAsync(f, filtros.Value, hoy, ct);
        var q = from v in db.VoucherRedemptions.AsNoTracking()
                join p in db.DocumentPayments.AsNoTracking() on v.DocumentPaymentId equals p.Id
                join d in documentos on v.DocumentId equals d.Id
                select new { v.NormalizedNumber, v.Status, v.ReleasedByDocumentId, v.PaymentMeansId, p.MeansCode, p.MeansName, p.Amount, p.Reference,
                    d.Id, d.PublicId, d.Prefix, d.Number, d.OperationDate, d.CounterpartyPersonId };
        if (request.PaymentMeans is { } mp)
        {
            var medio = await db.PaymentMeans.AsNoTracking().Where(m => m.PublicId == mp).Select(m => (int?)m.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(x => x.PaymentMeansId == medio);
        }
        if (request.Status is { } estado) q = q.Where(x => x.Status == estado);
        var lista = await q.OrderBy(x => x.OperationDate).ThenBy(x => x.Id).ToListAsync(ct);
        var liberadores = lista.Select(x => x.ReleasedByDocumentId).OfType<int>().Distinct().ToList();
        var liberados = await db.InventoryDocuments.AsNoTracking().Where(d => liberadores.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number), ct);
        var clientes = await datos.ClientesAsync(lista.Select(x => (x.Id, x.CounterpartyPersonId)).Distinct().ToList(), ct);

        var filas = lista.Select(x => new FilaExportable(
        [
            $"{x.MeansCode} · {x.MeansName}", x.Reference ?? x.NormalizedNumber, VistaDeDocumentos.NumeroVisible(x.Prefix, x.Number), x.OperationDate,
            clientes.GetValueOrDefault(x.Id), x.Amount, x.Status == VoucherRedemptionStatus.Active ? "Redimido" : "Liberado",
            x.ReleasedByDocumentId is int r ? liberados.GetValueOrDefault(r) : null, x.PublicId.ToString(),
        ])).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, lista.Where(x => x.Status == VoucherRedemptionStatus.Active).Sum(x => x.Amount), null, null, null]);
        return Result.Success(new TablaExportable("Bonos redimidos", DatosDeVentasYCaja.Rango(f, hoy), Columnas, filas, totales,
            ["El total suma los bonos redimidos que siguen vigentes; un bono liberado por una nota o una anulación vuelve a servir."]));
    }
}

// ---------------------------------------------------------------------------------------------- discount-approvals --

/// <summary>
/// <c>discount-approvals</c>: los descuentos que pasaron el tope de quien los pidió, con producto, precio de lista, porcentaje y valor,
/// tope aplicado, quién pidió, quién aprobó, método y estado. Filtro propio <c>approver</c> (usuario). (nuevo)
/// </summary>
public sealed record DiscountApprovalsReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Approver = null) : IRequest<Result<TablaExportable>>;

public sealed class DiscountApprovalsReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DiscountApprovalsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "discount-approvals", "Aprobaciones de descuento", "Los descuentos sobre el tope de quien los pidió: quién pidió, quién aprobó, cómo y en qué quedó.",
        "aprobaciones-de-descuento", ["from", "to", "pointOfSale", "cashRegister", "session", "product"], ["approver"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha", TipoDeColumna.Fecha), new("Documento"), new("Producto"), new("Precio de lista", TipoDeColumna.Moneda),
        new("Descuento (%)", TipoDeColumna.Porcentaje), new("Descuento (valor)", TipoDeColumna.Moneda), new("Tope de quien pidió", TipoDeColumna.Porcentaje),
        new("Pidió"), new("Aprobó"), new("Método"), new("Estado"), new("Documento", TipoDeColumna.Texto, "_documento"),
    ];

    public async Task<Result<TablaExportable>> Handle(DiscountApprovalsReportQuery request, CancellationToken ct)
    {
        var datos = new DatosDeVentasYCaja(db, alcanceDeLaPeticion);
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var filtros = await datos.FiltrosAsync(f, ct);
        if (filtros.IsFailure) return Result.Failure<TablaExportable>(filtros.Error);

        var documentos = await datos.DocumentosAsync(f, filtros.Value, hoy, ct);
        var q = from x in db.DocumentLineDiscounts.AsNoTracking()
                join l in db.InventoryDocumentLines.AsNoTracking() on x.DocumentLineId equals l.Id
                join d in documentos on x.DocumentId equals d.Id
                where !x.IsDeleted && x.RequiresApproval
                select new { Descuento = x, l.ProductId, l.ListPrice, l.UnitPrice, d.PublicId, d.Prefix, d.Number, d.OperationDate, d.Status, d.CreatedByUserId };
        if (f.Product is { } pp) q = q.Where(x => db.Products.Any(p => p.Id == x.ProductId && p.PublicId == pp));
        if (request.Approver is { } ap)
        {
            var aprobador = await db.Users.AsNoTracking().Where(u => u.PublicId == ap).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct) ?? -1;
            q = q.Where(x => x.Descuento.ApprovedByUserId == aprobador);
        }
        var lista = await q.OrderBy(x => x.OperationDate).ThenBy(x => x.Descuento.Id).ToListAsync(ct);
        var productoIds = lista.Select(x => x.ProductId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => $"{p.Code} · {p.Name}", ct);
        var usuarios = await datos.UsuariosAsync(lista.Select(x => x.CreatedByUserId).Concat(lista.Select(x => x.Descuento.ApprovedByUserId).OfType<int>()), ct);

        var filas = lista.Select(x =>
        {
            var d = x.Descuento;
            var estado = d.ApprovedByUserId is not null ? "Aprobado"
                : x.Status == DocumentStatus.PendingApproval ? "En aprobación"
                : x.Status is DocumentStatus.Discarded ? "Descartado"
                : DatosDeVentasYCaja.Estado(x.Status);
            return new FilaExportable(
            [
                x.OperationDate, VistaDeDocumentos.NumeroVisible(x.Prefix, x.Number) ?? "Borrador", productos.GetValueOrDefault(x.ProductId), x.ListPrice ?? x.UnitPrice,
                d.Rate is { } tasa ? tasa * 100m : null, d.Amount, d.CapRateApplied * 100m, usuarios.GetValueOrDefault(x.CreatedByUserId),
                d.ApprovedByUserId is int a ? usuarios.GetValueOrDefault(a) : null, d.ApprovalMethod?.ToString(), estado, x.PublicId.ToString(),
            ]);
        }).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, lista.Sum(x => x.Descuento.Amount), null, null, null, null, null, null]);
        return Result.Success(new TablaExportable("Aprobaciones de descuento", DatosDeVentasYCaja.Rango(f, hoy), Columnas, filas, totales,
            ["Sólo los descuentos que superaron el tope de quien los pidió (FR-054)."]));
    }
}
