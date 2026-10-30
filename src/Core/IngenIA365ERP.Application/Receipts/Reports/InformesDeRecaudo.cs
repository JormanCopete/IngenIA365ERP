using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Reports;

/// <summary>
/// El registro de las vistas de R0 del centro de informes de Recaudo (feature 015, US21, T0314; R-36; FR-152 parte R0; api.md §17). Cada vista es una
/// <see cref="TablaExportable"/> con alcance por oficina, punto y caja (<see cref="IAlcanceDeRecaudo"/>) y columnas ocultas con <c>_</c> para profundizar. Las
/// de R1 y R2 llegan con su entrega; <c>parallel-comparison</c> la sumó la fase 21 (T0323). (nuevo)
/// </summary>
public static class VistasDeRecaudo
{
    public const string CierreDeSesion = "session-close";
    public const string CierreDelDia = "day-close";
    public const string RecaudoDelDia = "daily-collection";
    public const string RecibosPorCajero = "receipts-by-cashier";
    public const string RecibosAnulados = "voided-receipts";
    public const string Excepciones = "exceptions";
    public const string PartidasPorIdentificar = "unidentified-items";
    public const string SaldosAFavor = "payer-credit-balances";
    public const string Pagadurias = "paymasters";
    public const string ChequesDevueltos = "returned-checks";
    public const string ContingenciaPorDigitar = "contingency-pending";

    /// <summary>Feature 015, US24 (T0323; FR-226; SC-024): el informe del paralelo en QA, lo registrado contra las cifras de referencia por día, medio y producto. (nuevo)</summary>
    public const string ComparacionDelParalelo = "parallel-comparison";

    /// <summary>Feature 015, R1 (T0401; FR-081, FR-087, FR-152): las vistas de R1. (nuevo)</summary>
    public const string Consolidado = "consolidated-collection";
    public const string AnulacionesConReemision = "voidings-with-reissue";
    public const string PagaduriasMorosas = "delinquent-paymasters";
    public const string ChequesRecibidos = "received-checks";
    public const string TransaccionesEnEfectivo = "cash-transactions";
    public const string CargaDelPendiente = "pending-application-load";

    /// <summary>Feature 015, R2 (fase 39, T0437; FR-152 parte R2): las vistas de R2. (nuevo)</summary>
    public const string Canales = "channels";
    public const string Convenios = "agreements";

    /// <summary>
    /// Los conceptos de las cifras de referencia del paralelo, cargados con la plantilla <c>recaudo.cifras-de-referencia</c> con la fecha de corte = el día:
    /// <c>PARALELO.MEDIO</c> con el código del medio de pago como dimensión, y <c>PARALELO.PRODUCTO</c> con <see cref="ProductosDelParalelo"/>. (nuevo)
    /// </summary>
    public const string ConceptoParaleloMedio = "PARALELO.MEDIO";
    public const string ConceptoParaleloProducto = "PARALELO.PRODUCTO";

    /// <summary>Las dimensiones de <see cref="ConceptoParaleloProducto"/>: créditos, cuentas (aportes y ahorros), conceptos varios, cuentas por cobrar y por aplicar.</summary>
    public static readonly IReadOnlyList<string> ProductosDelParalelo = ["CREDITOS", "CUENTAS", "CONCEPTOS", "CXC", "POR-APLICAR"];

    /// <summary>Las agrupaciones de <c>daily-collection</c> (FR-152): punto, caja, cajero, medio, canal, módulo y producto o línea.</summary>
    public static readonly IReadOnlyList<string> AgrupacionesDelRecaudo = ["point", "cashBox", "cashier", "means", "channel", "module", "product"];

    private static readonly string[] Rango = ["from", "to", "branch", "point", "cashBox"];
    private static readonly string[] Corte = ["asOf", "branch"];

    public static IReadOnlyList<VistaDeInformeDeRecaudoYTesoreria> Todas { get; } =
    [
        new(CierreDeSesion, "Cierre de sesiones", "Cada sesión de caja del rango: caja, cajero, apertura, cierre, base, recibos, anulaciones y estado.", "cierre-de-sesiones", Rango),
        new(CierreDelDia, "Cierre del día por punto", "El cierre del día de cada punto, medio por medio: esperado, contado y diferencia.", "cierre-del-dia", ["from", "to", "branch", "point"]),
        new(RecaudoDelDia, "Recaudo del día", "Lo recaudado por recibos vigentes, agrupado por punto, caja, cajero, medio, canal, módulo o producto. Por medio cuadra con los cierres de sesión.",
            "recaudo-del-dia", Rango, ["groupBy"]),
        new(RecibosPorCajero, "Recibos por cajero", "Por cajero y día: recibos, valor aplicado, vueltas y anulaciones.", "recibos-por-cajero", Rango),
        new(RecibosAnulados, "Recibos anulados", "Las anulaciones pedidas en el rango, con su motivo, quién la pidió y quién la decidió.", "recibos-anulados", Rango),
        new(Excepciones, "Excepciones del recibo", "Recibos con fecha valor anterior, fecha valor corregida o digitados por contingencia.", "excepciones-de-recibos", Rango),
        new(PartidasPorIdentificar, "Partidas por identificar", "Las partidas abiertas al corte con su antigüedad y su escalón.", "partidas-por-identificar", Corte),
        new(SaldosAFavor, "Saldos a favor de pagadores", "Los saldos a favor abiertos al corte con su antigüedad.", "saldos-a-favor", Corte),
        new(Pagadurias, "Pagadurías", "Por pagaduría: archivos de descuentos del rango, valor enviado y lo girado.", "pagadurias", ["from", "to", "branch"]),
        new(ChequesDevueltos, "Cheques devueltos", "Los cheques recibidos devueltos en el rango con su causal y el girador.", "cheques-devueltos", ["from", "to", "branch"]),
        new(ContingenciaPorDigitar, "Contingencia por digitar", "Por talonario activo: digitados, números por digitar antes del último y sin usar.", "contingencia-por-digitar", ["point"]),
        new(ComparacionDelParalelo, "Comparación del paralelo", "Lo registrado en el paralelo contra las cifras de referencia, por día, medio y producto, con la explicación de cada diferencia.",
            "comparacion-del-paralelo", ["from", "to"]),
        // Feature 015, R1 (T0401).
        new(Consolidado, "Recaudo consolidado", "Por día: caja, bancos, pagadurías y, de sólo lectura, lo descontado por Nómina y el débito automático de Cartera.",
            "recaudo-consolidado", Rango),
        new(AnulacionesConReemision, "Anulaciones con reemisión", "Los recibos anulados del rango y el recibo que los reemplazó (mismo pagador, desde la fecha de la anulación).",
            "anulaciones-con-reemision", Rango),
        new(PagaduriasMorosas, "Pagadurías morosas", "Lo descontado por las pagadurías y no girado pasada la fecha legal de giro del calendario (informativo).",
            "pagadurias-morosas", Corte),
        new(ChequesRecibidos, "Cheques recibidos", "Los cheques recibidos del rango por estado: recibidos, en custodia, consignados, en canje, pagados y devueltos.",
            "cheques-recibidos", ["from", "to", "branch"]),
        new(TransaccionesEnEfectivo, "Transacciones en efectivo", "El registro único de efectivo del rango: recibos, egresos y punto de venta, por persona.",
            "transacciones-en-efectivo", ["from", "to", "branch"]),
        new(CargaDelPendiente, "Carga del recaudo pendiente", "El recaudo pendiente de aplicar por módulo y persona, para aplicarlo el día de la activación.",
            "recaudo-pendiente-de-aplicar", ["branch"]),
        // Feature 015, R2 (fase 39, T0437).
        new(Canales, "Recaudo por canal", "Los recibos vigentes del rango por canal: caja, banco, pagaduría, convenio, pasarela y contingencia; cuadra con los recibos.",
            "recaudo-por-canal", Rango),
        new(Convenios, "Convenios de recaudo", "Por archivo cargado: lo informado, lo aplicado, lo que fue a partidas y su conciliación con el abono (comisión, IVA, retenciones).",
            "convenios-de-recaudo", ["from", "to", "branch"]),
    ];

    public static VistaDeInformeDeRecaudoYTesoreria? Buscar(string clave) => Todas.FirstOrDefault(v => v.Key == clave);
}

/// <summary>Una vista de R0 de Recaudo con sus filtros (<c>GET /api/reports/receipts/{vista}</c>, <c>Receipts.Reports.View</c>). (nuevo)</summary>
public sealed record ReceiptsReportQuery(string Vista, FiltrosDeInformeDeRecaudoYTesoreria Filtros) : IRequest<Result<TablaExportable>>;

public sealed partial class ReceiptsReportQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj,
    IngenIA365ERP.Application.Common.Integration.Payroll.IRecaudoDeNominaParaConsolidado? nomina = null,
    IngenIA365ERP.Application.Common.Integration.Lending.IDebitoAutomaticoParaConsolidado? debitoAutomatico = null,
    IngenIA365ERP.Application.Common.Integration.Inventory.IEfectivoDelPuntoDeVenta? puntoDeVenta = null)
    : IRequestHandler<ReceiptsReportQuery, Result<TablaExportable>>
{
    public const string CodigoVistaDesconocida = "Receipts.Report.UnknownView";

    public async Task<Result<TablaExportable>> Handle(ReceiptsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var rango = f.ValidarRango(hoy);
        if (rango.IsFailure) return Result.Failure<TablaExportable>(rango.Error);
        var ambito = await AmbitoAsync(f, ct);
        var (desde, hasta, corte) = (f.Desde(hoy), f.Hasta(hoy), f.ALaFecha(hoy));
        return request.Vista switch
        {
            VistasDeRecaudo.CierreDeSesion => Result.Success(await CierreDeSesionAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.CierreDelDia => Result.Success(await CierreDelDiaAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.RecaudoDelDia => await RecaudoDelDiaAsync(ambito, desde, hasta, f.GroupBy, ct),
            VistasDeRecaudo.RecibosPorCajero => Result.Success(await RecibosPorCajeroAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.RecibosAnulados => Result.Success(await AnuladosAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.Excepciones => Result.Success(await ExcepcionesAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.PartidasPorIdentificar => Result.Success(await PartidasAsync(ambito, corte, ct)),
            VistasDeRecaudo.SaldosAFavor => Result.Success(await SaldosAFavorAsync(ambito, corte, ct)),
            VistasDeRecaudo.Pagadurias => Result.Success(await PagaduriasAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.ChequesDevueltos => Result.Success(await ChequesDevueltosAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.ContingenciaPorDigitar => Result.Success(await ContingenciaAsync(ambito, ct)),
            VistasDeRecaudo.ComparacionDelParalelo => Result.Success(await ComparacionDelParaleloAsync(ambito, desde, hasta, ct)),
            // Feature 015, R1 (T0401): las vistas de R1.
            VistasDeRecaudo.Consolidado => Result.Success(await ConsolidadoAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.AnulacionesConReemision => Result.Success(await AnulacionesConReemisionAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.PagaduriasMorosas => Result.Success(await PagaduriasMorosasAsync(ambito, corte, ct)),
            VistasDeRecaudo.ChequesRecibidos => Result.Success(await ChequesRecibidosAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.TransaccionesEnEfectivo => Result.Success(await TransaccionesEnEfectivoAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.CargaDelPendiente => Result.Success(await CargaDelPendienteAsync(ambito, ct)),
            // Feature 015, R2 (fase 39, T0437): las vistas de R2.
            VistasDeRecaudo.Canales => Result.Success(await CanalesAsync(ambito, desde, hasta, ct)),
            VistasDeRecaudo.Convenios => Result.Success(await ConveniosAsync(ambito, desde, hasta, ct)),
            _ => Result.Failure<TablaExportable>(new Error(CodigoVistaDesconocida, $"«{request.Vista}» no es una vista de Recaudo.")),
        };
    }

    // ------------------------------------------------------------------------------------------------------------- ámbito --

    /// <summary>El alcance de la persona más los filtros de oficina, punto y caja, resueltos a sus Id internos.</summary>
    internal sealed record Ambito(AlcanceDeRecaudo Alcance, int? Oficina, int? Punto, int? Caja, Dictionary<int, (int Punto, int Oficina, string Codigo)> Cajas,
        Dictionary<int, (int Oficina, string Codigo)> Puntos)
    {
        public bool IncluyeCaja(int caja)
        {
            if (!Cajas.TryGetValue(caja, out var c)) return false;
            return Alcance.IncluyeCaja(caja, c.Punto) && (Caja is null || Caja == caja) && (Punto is null || Punto == c.Punto) && (Oficina is null || Oficina == c.Oficina);
        }

        public bool IncluyePunto(int punto) =>
            Puntos.TryGetValue(punto, out var p) && Alcance.IncluyePunto(punto) && (Punto is null || Punto == punto) && (Oficina is null || Oficina == p.Oficina);

        public bool IncluyeOficina(int oficina) => Alcance.IncluyeOficina(oficina) && (Oficina is null || Oficina == oficina) && Punto is null && Caja is null;

        /// <summary>Un recibo: por su caja si tiene, si no por su oficina (canal banco, pagaduría).</summary>
        public bool IncluyeRecibo(int? caja, int oficina) => caja is { } c ? IncluyeCaja(c) : IncluyeOficina(oficina);
    }

    private async Task<Ambito> AmbitoAsync(FiltrosDeInformeDeRecaudoYTesoreria f, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var puntos = await db.CollectionPoints.AsNoTracking().Select(p => new { p.Id, p.PublicId, p.BranchId, p.Code }).ToListAsync(ct);
        var cajas = await db.CashBoxes.AsNoTracking().Select(c => new { c.Id, c.PublicId, c.CollectionPointId, c.Code }).ToListAsync(ct);
        int? oficina = f.Branch is { } b
            ? await db.Branches.AsNoTracking().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1
            : null;
        int? punto = f.Point is { } p0 ? puntos.FirstOrDefault(x => x.PublicId == p0)?.Id ?? -1 : null;
        int? caja = f.CashBox is { } c0 ? cajas.FirstOrDefault(x => x.PublicId == c0)?.Id ?? -1 : null;
        var deLosPuntos = puntos.ToDictionary(x => x.Id, x => (x.BranchId, x.Code));
        return new Ambito(mio, oficina, punto, caja,
            cajas.ToDictionary(x => x.Id, x => (x.CollectionPointId, deLosPuntos.TryGetValue(x.CollectionPointId, out var pp) ? pp.BranchId : 0, x.Code)),
            deLosPuntos);
    }

    private async Task<Dictionary<int, string>> UsuariosAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => lista.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
    }

    private async Task<Dictionary<int, (Guid PublicId, string Nombre, string Documento)>> PersonasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, Nombre = (p.BusinessName ?? (p.FirstName + " " + p.LastName)).Trim(), p.TaxId })
            .ToDictionaryAsync(p => p.Id, p => (p.PublicId, p.Nombre, p.TaxId), ct);
    }

    private static string Periodo(DateOnly desde, DateOnly hasta) => $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}";

    // ------------------------------------------------------------------------------------------------------------- vistas --

    private async Task<TablaExportable> CierreDeSesionAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var sesiones = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                              join d in db.CollectionPointDays.AsNoTracking() on s.CollectionPointDayId equals d.Id
                              where d.BusinessDate >= desde && d.BusinessDate <= hasta
                              orderby d.BusinessDate, s.OpenedAt
                              select new { s.Id, s.PublicId, s.CashBoxId, s.CashierUserId, d.BusinessDate, s.OpenedAt, s.ClosedAt, s.OpeningBase, s.Status, s.IsForgotten })
            .ToListAsync(ct);
        sesiones = sesiones.Where(s => a.IncluyeCaja(s.CashBoxId)).ToList();
        var ids = sesiones.Select(s => s.Id).ToList();
        var recibos = await db.IssuedReceipts.AsNoTracking().Where(r => r.CashSessionId != null && ids.Contains(r.CashSessionId.Value))
            .GroupBy(r => r.CashSessionId!.Value)
            .Select(g => new
            {
                Sesion = g.Key, Recibos = g.Count(r => r.Status != ReceiptState.Voided), Total = g.Where(r => r.Status != ReceiptState.Voided).Sum(r => r.TotalApplied),
                Anulados = g.Count(r => r.Status == ReceiptState.Voided),
            }).ToDictionaryAsync(x => x.Sesion, ct);
        var usuarios = await UsuariosAsync(sesiones.Select(s => s.CashierUserId), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Día", TipoDeColumna.Fecha), new("Punto"), new("Caja"), new("Cajero"), new("Apertura"), new("Cierre"), new("Base", TipoDeColumna.Moneda),
            new("Recibos", TipoDeColumna.Entero), new("Recaudado", TipoDeColumna.Moneda), new("Anulados", TipoDeColumna.Entero), new("Estado"), new("_sesion", Clave: "_sesion"),
        };
        var filas = sesiones.Select(s =>
        {
            var caja = a.Cajas[s.CashBoxId];
            var r = recibos.GetValueOrDefault(s.Id);
            return new FilaExportable(
            [
                s.BusinessDate, a.Puntos.TryGetValue(caja.Punto, out var p) ? p.Codigo : string.Empty, caja.Codigo, usuarios.GetValueOrDefault(s.CashierUserId),
                s.OpenedAt.ToString("yyyy-MM-dd HH:mm"), s.ClosedAt?.ToString("yyyy-MM-dd HH:mm"), s.OpeningBase, r?.Recibos ?? 0, r?.Total ?? 0m, r?.Anulados ?? 0,
                s.IsForgotten ? $"{s.Status} (olvidada)" : s.Status.ToString(), s.PublicId,
            ], Resaltada: s.Status is CashSessionState.ClosedWithDifferencePending or CashSessionState.ForgottenOpen);
        }).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, null, sesiones.Sum(s => s.OpeningBase), filas.Sum(x => (int)x.Valores[7]!),
            filas.Sum(x => (decimal)x.Valores[8]!), filas.Sum(x => (int)x.Valores[9]!), null, null]);
        return new TablaExportable("Cierre de sesiones de caja", Periodo(desde, hasta), columnas, filas, totales, []);
    }

    private async Task<TablaExportable> CierreDelDiaAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var cierres = await (from c in db.ReceiptsDayCloses.AsNoTracking()
                             join d in db.CollectionPointDays.AsNoTracking() on c.CollectionPointDayId equals d.Id
                             join l in db.ReceiptsDayCloseLines.AsNoTracking() on c.Id equals l.DayCloseId
                             join m in db.PaymentMeans.AsNoTracking() on l.PaymentMeansId equals m.Id
                             where d.BusinessDate >= desde && d.BusinessDate <= hasta
                             orderby d.BusinessDate, d.CollectionPointId, m.DisplayOrder
                             select new
                             {
                                 c.PublicId, d.CollectionPointId, d.BusinessDate, c.SessionCount, c.ReceiptCount, c.VoidingCount, c.ClosedAt, c.ClosedByUserId,
                                 Medio = m.Name, l.Expected, l.Counted, l.Difference,
                             }).ToListAsync(ct);
        cierres = cierres.Where(c => a.IncluyePunto(c.CollectionPointId)).ToList();
        var usuarios = await UsuariosAsync(cierres.Select(c => c.ClosedByUserId), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Día", TipoDeColumna.Fecha), new("Punto"), new("Sesiones", TipoDeColumna.Entero), new("Recibos", TipoDeColumna.Entero), new("Anulaciones", TipoDeColumna.Entero),
            new("Medio"), new("Esperado", TipoDeColumna.Moneda), new("Contado", TipoDeColumna.Moneda), new("Diferencia", TipoDeColumna.Moneda), new("Cerró"),
            new("_cierre", Clave: "_cierre"),
        };
        var filas = cierres.Select(c => new FilaExportable(
        [
            c.BusinessDate, a.Puntos[c.CollectionPointId].Codigo, c.SessionCount, c.ReceiptCount, c.VoidingCount, c.Medio, c.Expected, c.Counted, c.Difference,
            usuarios.GetValueOrDefault(c.ClosedByUserId), c.PublicId,
        ], Seccion: $"{c.BusinessDate:dd/MM/yyyy} · {a.Puntos[c.CollectionPointId].Codigo}", Resaltada: c.Difference != 0m)).ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, null, cierres.Sum(c => c.Expected), cierres.Sum(c => c.Counted), cierres.Sum(c => c.Difference), null, null]);
        return new TablaExportable("Cierre del día por punto", Periodo(desde, hasta), columnas, filas, totales, []);
    }

    private async Task<Result<TablaExportable>> RecaudoDelDiaAsync(Ambito a, DateOnly desde, DateOnly hasta, string? groupBy, CancellationToken ct)
    {
        var por = string.IsNullOrWhiteSpace(groupBy) ? "means" : VistasDeRecaudo.AgrupacionesDelRecaudo.FirstOrDefault(g => string.Equals(g, groupBy.Trim(), StringComparison.OrdinalIgnoreCase));
        if (por is null) return Result.Failure<TablaExportable>(FiltrosDeInformeDeRecaudoYTesoreria.FiltroInvalido("groupBy", groupBy, VistasDeRecaudo.AgrupacionesDelRecaudo));
        var recibos = await db.IssuedReceipts.AsNoTracking().Where(r => r.Status != ReceiptState.Voided && r.ValueDate >= desde && r.ValueDate <= hasta)
            .Select(r => new { r.Id, r.ValueDate, r.CashBoxId, r.CollectionPointId, r.BranchId, r.CashierUserId, r.Channel, r.TotalApplied, r.ChangeGiven }).ToListAsync(ct);
        recibos = recibos.Where(r => a.IncluyeRecibo(r.CashBoxId, r.BranchId)).ToList();
        var ids = recibos.Select(r => r.Id).ToList();
        var porRecibo = recibos.ToDictionary(r => r.Id);
        List<(DateOnly Dia, string Grupo, int Recibos, decimal Valor)> grupos;
        string titulo;
        switch (por)
        {
            case "means":
            {
                titulo = "Medio";
                var medios = await (from m in db.ReceiptMeans.AsNoTracking()
                                    join pm in db.PaymentMeans.AsNoTracking() on m.PaymentMeansId equals pm.Id
                                    where ids.Contains(m.ReceiptId)
                                    select new { m.ReceiptId, pm.Name, pm.Class, m.Amount }).ToListAsync(ct);
                // Las vueltas salen del efectivo: el medio neto cuadra con lo esperado de la sesión.
                grupos = medios.GroupBy(m => (porRecibo[m.ReceiptId].ValueDate, m.Name))
                    .Select(g => (g.Key.ValueDate, g.Key.Name, g.Select(x => x.ReceiptId).Distinct().Count(),
                        g.Sum(x => x.Amount) - g.Where(x => x.Class == Domain.Enums.Core.PaymentMeansClass.Cash).Select(x => x.ReceiptId).Distinct()
                            .Sum(id => porRecibo[id].ChangeGiven)))
                    .ToList();
                break;
            }
            case "module":
            case "product":
            {
                titulo = por == "module" ? "Módulo" : "Producto o línea";
                var aplicaciones = await db.ReceiptApplications.AsNoTracking().Where(x => ids.Contains(x.ReceiptId) && x.Status != ApplicationState.Voided)
                    .Select(x => new { x.ReceiptId, x.Target, x.ProductNumber, x.MiscConceptId, x.Amount }).ToListAsync(ct);
                string Grupo(ApplicationTarget t, string? producto) => por == "module"
                    ? t switch
                    {
                        ApplicationTarget.Loan => "Cartera (créditos)",
                        ApplicationTarget.Account => "Captaciones y aportes",
                        ApplicationTarget.MiscConcept => "Conceptos varios",
                        ApplicationTarget.CommercialReceivable => "Cuentas por cobrar comerciales",
                        _ => "Recaudo por aplicar",
                    }
                    : $"{(t == ApplicationTarget.Loan ? "Crédito" : t == ApplicationTarget.Account ? "Cuenta" : t.ToString())} {producto}".Trim();
                grupos = aplicaciones.GroupBy(x => (porRecibo[x.ReceiptId].ValueDate, Grupo(x.Target, x.ProductNumber)))
                    .Select(g => (g.Key.ValueDate, g.Key.Item2, g.Select(x => x.ReceiptId).Distinct().Count(), g.Sum(x => x.Amount))).ToList();
                break;
            }
            default:
            {
                var usuarios = por == "cashier" ? await UsuariosAsync(recibos.Where(r => r.CashierUserId != null).Select(r => r.CashierUserId!.Value), ct) : new Dictionary<int, string>();
                titulo = por switch { "point" => "Punto", "cashBox" => "Caja", "cashier" => "Cajero", _ => "Canal" };
                string Grupo(int? punto, int? caja, int? cajero, ReceiptChannel canal) => por switch
                {
                    "point" => punto is int p && a.Puntos.TryGetValue(p, out var pp) ? pp.Codigo : "Sin punto",
                    "cashBox" => caja is int c && a.Cajas.TryGetValue(c, out var cc) ? cc.Codigo : "Sin caja",
                    "cashier" => cajero is int u ? usuarios.GetValueOrDefault(u) ?? "—" : "Sin cajero",
                    _ => canal.ToString(),
                };
                grupos = recibos.GroupBy(r => (r.ValueDate, Grupo(r.CollectionPointId, r.CashBoxId, r.CashierUserId, r.Channel)))
                    .Select(g => (g.Key.ValueDate, g.Key.Item2, g.Count(), g.Sum(r => r.TotalApplied))).ToList();
                break;
            }
        }
        var columnas = new List<ColumnaExportable> { new("Día", TipoDeColumna.Fecha), new(titulo), new("Recibos", TipoDeColumna.Entero), new("Valor", TipoDeColumna.Moneda) };
        var filas = grupos.OrderBy(g => g.Dia).ThenBy(g => g.Grupo, StringComparer.CurrentCulture)
            .Select(g => new FilaExportable([g.Dia, g.Grupo, g.Recibos, g.Valor])).ToList();
        var totales = new FilaExportable(["Total", null, recibos.Count, grupos.Sum(g => g.Valor)]);
        return Result.Success(new TablaExportable("Recaudo del día", Periodo(desde, hasta), columnas, filas, totales,
            ["Sólo recibos vigentes (los anulados no suman). Por medio, el efectivo va neto de las vueltas y cuadra con lo esperado en los cierres de sesión."]));
    }

    /// <summary>El código de producto del paralelo de una aplicación (<see cref="VistasDeRecaudo.ProductosDelParalelo"/>).</summary>
    internal static string ProductoDelParalelo(ApplicationTarget t) => t switch
    {
        ApplicationTarget.Loan => "CREDITOS",
        ApplicationTarget.Account => "CUENTAS",
        ApplicationTarget.MiscConcept => "CONCEPTOS",
        ApplicationTarget.CommercialReceivable => "CXC",
        _ => "POR-APLICAR",
    };

    /// <summary>
    /// Feature 015, US24 (T0323; FR-226; SC-024): por día del rango, lo registrado (recibos vigentes por fecha valor: por medio, el efectivo neto de las vueltas; por
    /// producto, lo aplicado) contra las cifras de referencia <c>PARALELO.MEDIO</c> y <c>PARALELO.PRODUCTO</c> de ese día. Cada diferencia lleva su explicación: falta la
    /// cifra, falta el registro, o los hechos del día que la pueden explicar (anulaciones, recibos con fecha valor distinta del día de registro, partidas por identificar
    /// abiertas ese día); si ninguno la explica, «por explicar». Con el alcance de la persona. (nuevo)
    /// </summary>
    private async Task<TablaExportable> ComparacionDelParaleloAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var recibos = await db.IssuedReceipts.AsNoTracking().Where(r => r.Status != ReceiptState.Voided && r.ValueDate >= desde && r.ValueDate <= hasta)
            .Select(r => new { r.Id, r.ValueDate, r.CashBoxId, r.BranchId, r.ChangeGiven, r.RegisteredAt }).ToListAsync(ct);
        recibos = recibos.Where(r => a.IncluyeRecibo(r.CashBoxId, r.BranchId)).ToList();
        var porRecibo = recibos.ToDictionary(r => r.Id);
        var ids = porRecibo.Keys.ToList();
        var medios = await (from m in db.ReceiptMeans.AsNoTracking()
                            join pm in db.PaymentMeans.AsNoTracking() on m.PaymentMeansId equals pm.Id
                            where ids.Contains(m.ReceiptId)
                            select new { m.ReceiptId, pm.Code, pm.Class, m.Amount }).ToListAsync(ct);
        var aplicaciones = await db.ReceiptApplications.AsNoTracking().Where(x => ids.Contains(x.ReceiptId) && x.Status != ApplicationState.Voided)
            .Select(x => new { x.ReceiptId, x.Target, x.Amount }).ToListAsync(ct);
        var registrado = new Dictionary<(DateOnly Dia, string Concepto, string Codigo), decimal>();
        void Sumar(DateOnly d, string c, string k, decimal v) => registrado[(d, c, k)] = registrado.GetValueOrDefault((d, c, k)) + v;
        foreach (var m in medios)
            Sumar(porRecibo[m.ReceiptId].ValueDate, VistasDeRecaudo.ConceptoParaleloMedio, m.Code.ToUpperInvariant(), m.Amount);
        foreach (var g in medios.Where(m => m.Class == Domain.Enums.Core.PaymentMeansClass.Cash).GroupBy(m => m.ReceiptId))
            Sumar(porRecibo[g.Key].ValueDate, VistasDeRecaudo.ConceptoParaleloMedio, g.First().Code.ToUpperInvariant(), -porRecibo[g.Key].ChangeGiven);
        foreach (var x in aplicaciones)
            Sumar(porRecibo[x.ReceiptId].ValueDate, VistasDeRecaudo.ConceptoParaleloProducto, ProductoDelParalelo(x.Target), x.Amount);

        var conceptos = new[] { VistasDeRecaudo.ConceptoParaleloMedio, VistasDeRecaudo.ConceptoParaleloProducto };
        var referencia = (await db.ReceiptsReferenceFigures.AsNoTracking()
                .Where(c => c.CutoffDate >= desde && c.CutoffDate <= hasta && conceptos.Contains(c.Concept))
                .Select(c => new { c.CutoffDate, c.Concept, c.Dimension, c.Amount }).ToListAsync(ct))
            .GroupBy(c => (Dia: c.CutoffDate, Concepto: c.Concept, Codigo: c.Dimension.ToUpperInvariant())).ToDictionary(g => g.Key, g => g.Sum(c => c.Amount));

        // Lo que puede explicar una diferencia del día.
        var anulados = (await (from v in db.ReceiptVoidings.AsNoTracking()
                               join r in db.IssuedReceipts.AsNoTracking() on v.ReceiptId equals r.Id
                               where r.ValueDate >= desde && r.ValueDate <= hasta
                               select new { r.ValueDate, r.CashBoxId, r.BranchId }).ToListAsync(ct))
            .Where(x => a.IncluyeRecibo(x.CashBoxId, x.BranchId)).GroupBy(x => x.ValueDate).ToDictionary(g => g.Key, g => g.Count());
        var fechaValorDistinta = recibos.Where(r => DateOnly.FromDateTime(r.RegisteredAt.ToLocalTime()) != r.ValueDate).GroupBy(r => r.ValueDate)
            .ToDictionary(g => g.Key, g => g.Count());
        var partidas = (await db.UnidentifiedItems.AsNoTracking().Where(p => p.OpenedOn >= desde && p.OpenedOn <= hasta).Select(p => p.OpenedOn).ToListAsync(ct))
            .GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

        string Explicar(DateOnly dia, string concepto, decimal? reg, decimal? refe)
        {
            if (refe is null) return $"Sin cifra de referencia del día: cárguela con la plantilla recaudo.cifras-de-referencia (concepto {concepto}).";
            if ((reg ?? 0m) == 0m && refe != 0m) return "La referencia lo trae y el paralelo no lo registró: falta digitar el recaudo del día.";
            if (reg == refe) return "Cuadra.";
            var motivos = new List<string>();
            if (anulados.TryGetValue(dia, out var n)) motivos.Add($"{n} anulación(es) de recibos de ese día");
            if (fechaValorDistinta.TryGetValue(dia, out var f)) motivos.Add($"{f} recibo(s) con fecha valor distinta del día en que se registraron");
            if (partidas.TryGetValue(dia, out var p)) motivos.Add($"{p} partida(s) por identificar abiertas ese día");
            return motivos.Count == 0 ? "Por explicar: ningún hecho del día la explica." : "Puede explicarla: " + string.Join("; ", motivos) + ".";
        }

        var llaves = registrado.Keys.Union(referencia.Keys).Distinct()
            .OrderBy(k => k.Dia).ThenBy(k => k.Concepto, StringComparer.Ordinal).ThenBy(k => k.Codigo, StringComparer.Ordinal).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Día", TipoDeColumna.Fecha), new("Comparación"), new("Medio o producto"), new("Registrado", TipoDeColumna.Moneda), new("Referencia", TipoDeColumna.Moneda),
            new("Diferencia", TipoDeColumna.Moneda), new("Explicación"),
        };
        var filas = llaves.Select(k =>
        {
            decimal? reg = registrado.TryGetValue(k, out var x) ? x : null;
            decimal? refe = referencia.TryGetValue(k, out var y) ? y : null;
            var diferencia = (reg ?? 0m) - (refe ?? 0m);
            return new FilaExportable(
            [
                k.Dia, k.Concepto == VistasDeRecaudo.ConceptoParaleloMedio ? "Medio" : "Producto", k.Codigo, reg ?? 0m, refe, diferencia, Explicar(k.Dia, k.Concepto, reg, refe),
            ], Seccion: $"{k.Dia:dd/MM/yyyy}", Resaltada: diferencia != 0m || refe is null);
        }).ToList();
        var porMedio = llaves.Where(k => k.Concepto == VistasDeRecaudo.ConceptoParaleloMedio).ToList();
        var totales = new FilaExportable(["Total por medio", null, null, porMedio.Sum(k => registrado.GetValueOrDefault(k)), porMedio.Sum(k => referencia.GetValueOrDefault(k)),
            porMedio.Sum(k => registrado.GetValueOrDefault(k) - referencia.GetValueOrDefault(k)), null]);
        return new TablaExportable("Comparación del paralelo", Periodo(desde, hasta), columnas, filas, totales,
        [
            "Cifras de referencia con la plantilla recaudo.cifras-de-referencia: fecha de corte = el día; concepto PARALELO.MEDIO con el código del medio, o PARALELO.PRODUCTO con CREDITOS, CUENTAS, CONCEPTOS, CXC o POR-APLICAR.",
            "Lo registrado son los recibos vigentes por fecha valor; por medio, el efectivo neto de las vueltas.",
        ]);
    }

    private async Task<TablaExportable> RecibosPorCajeroAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var recibos = await db.IssuedReceipts.AsNoTracking().Where(r => r.CashierUserId != null && r.ValueDate >= desde && r.ValueDate <= hasta)
            .Select(r => new { r.CashierUserId, r.ValueDate, r.CashBoxId, r.BranchId, r.Status, r.TotalApplied, r.ChangeGiven }).ToListAsync(ct);
        recibos = recibos.Where(r => a.IncluyeRecibo(r.CashBoxId, r.BranchId)).ToList();
        var usuarios = await UsuariosAsync(recibos.Select(r => r.CashierUserId!.Value), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Cajero"), new("Día", TipoDeColumna.Fecha), new("Recibos", TipoDeColumna.Entero), new("Aplicado", TipoDeColumna.Moneda), new("Vueltas", TipoDeColumna.Moneda),
            new("Anulados", TipoDeColumna.Entero),
        };
        var filas = recibos.GroupBy(r => (r.CashierUserId!.Value, r.ValueDate)).OrderBy(g => usuarios.GetValueOrDefault(g.Key.Value)).ThenBy(g => g.Key.ValueDate)
            .Select(g => new FilaExportable(
            [
                usuarios.GetValueOrDefault(g.Key.Value), g.Key.ValueDate, g.Count(r => r.Status != ReceiptState.Voided),
                g.Where(r => r.Status != ReceiptState.Voided).Sum(r => r.TotalApplied), g.Where(r => r.Status != ReceiptState.Voided).Sum(r => r.ChangeGiven),
                g.Count(r => r.Status == ReceiptState.Voided),
            ])).ToList();
        return new TablaExportable("Recibos por cajero", Periodo(desde, hasta), columnas, filas, null, []);
    }

    private async Task<TablaExportable> AnuladosAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var filas0 = await (from v in db.ReceiptVoidings.AsNoTracking()
                            join r in db.IssuedReceipts.AsNoTracking() on v.ReceiptId equals r.Id
                            where v.RequestedAt >= inicio && v.RequestedAt < fin
                            orderby v.RequestedAt
                            select new
                            {
                                r.PublicId, Recibo = r.Number, r.ValueDate, r.CashBoxId, r.BranchId, r.TotalApplied, Anulacion = v.Number, v.Reason, v.Status, v.InSameSession,
                                v.RequestedByUserId, v.RequestedAt, v.ApprovedByUserId, v.DecisionReason,
                            }).ToListAsync(ct);
        filas0 = filas0.Where(x => a.IncluyeRecibo(x.CashBoxId, x.BranchId)).ToList();
        var usuarios = await UsuariosAsync(filas0.Select(x => x.RequestedByUserId).Concat(filas0.Where(x => x.ApprovedByUserId != null).Select(x => x.ApprovedByUserId!.Value)), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Recibo"), new("Fecha valor", TipoDeColumna.Fecha), new("Valor", TipoDeColumna.Moneda), new("Anulación"), new("Motivo"), new("En la misma sesión"),
            new("Pidió"), new("Pedida"), new("Decidió"), new("Estado"), new("Motivo de la decisión"), new("_recibo", Clave: "_recibo"),
        };
        var filas = filas0.Select(x => new FilaExportable(
        [
            x.Recibo, x.ValueDate, x.TotalApplied, x.Anulacion, x.Reason, x.InSameSession ? "Sí" : "No", usuarios.GetValueOrDefault(x.RequestedByUserId),
            x.RequestedAt.ToString("yyyy-MM-dd HH:mm"), x.ApprovedByUserId is int u ? usuarios.GetValueOrDefault(u) : null, x.Status.ToString(), x.DecisionReason, x.PublicId,
        ], Resaltada: x.Status == ReceiptVoidingState.PendingApproval)).ToList();
        return new TablaExportable("Recibos anulados", Periodo(desde, hasta), columnas, filas, null, []);
    }

    private async Task<TablaExportable> ExcepcionesAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var recibos = await db.IssuedReceipts.AsNoTracking()
            .Where(r => r.ValueDate >= desde && r.ValueDate <= hasta && (r.BackdatedReason != null || r.OriginalValueDate != null || r.IsContingency))
            .OrderBy(r => r.ValueDate).ThenBy(r => r.Number)
            .Select(r => new
            {
                r.PublicId, r.Number, r.ValueDate, r.RegisteredAt, r.CashBoxId, r.BranchId, r.TotalApplied, r.BackdatedReason, r.OriginalValueDate, r.IsContingency,
                r.ContingencyNumber, r.Channel,
            }).ToListAsync(ct);
        recibos = recibos.Where(r => a.IncluyeRecibo(r.CashBoxId, r.BranchId)).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Excepción"), new("Recibo"), new("Fecha valor", TipoDeColumna.Fecha), new("Registrado"), new("Valor", TipoDeColumna.Moneda), new("Canal"), new("Detalle"),
            new("_recibo", Clave: "_recibo"),
        };
        var filas = new List<FilaExportable>();
        foreach (var r in recibos)
        {
            if (r.BackdatedReason is not null)
                filas.Add(new FilaExportable(["Fecha valor anterior", r.Number, r.ValueDate, r.RegisteredAt.ToString("yyyy-MM-dd HH:mm"), r.TotalApplied, r.Channel.ToString(),
                    r.BackdatedReason, r.PublicId]));
            if (r.OriginalValueDate is { } original)
                filas.Add(new FilaExportable(["Fecha valor reclasificada", r.Number, r.ValueDate, r.RegisteredAt.ToString("yyyy-MM-dd HH:mm"), r.TotalApplied, r.Channel.ToString(),
                    $"Fecha valor original {original:dd/MM/yyyy}", r.PublicId]));
            if (r.IsContingency)
                filas.Add(new FilaExportable(["Contingencia", r.Number, r.ValueDate, r.RegisteredAt.ToString("yyyy-MM-dd HH:mm"), r.TotalApplied, r.Channel.ToString(),
                    $"Talonario n.º {r.ContingencyNumber}", r.PublicId]));
        }
        return new TablaExportable("Excepciones del recibo", Periodo(desde, hasta), columnas, filas, null, []);
    }

    private async Task<TablaExportable> PartidasAsync(Ambito a, DateOnly corte, CancellationToken ct)
    {
        var partidas = await db.UnidentifiedItems.AsNoTracking()
            .Where(p => p.OpenedOn <= corte && (p.Status == UnidentifiedItemState.Open || p.Status == UnidentifiedItemState.PartiallyIdentified))
            .OrderBy(p => p.OpenedOn).ThenBy(p => p.Number)
            .Select(p => new { p.PublicId, p.Number, p.BranchId, p.MovementDate, p.OpenedOn, p.Amount, p.Balance, p.Reference, p.DepositorName, p.DepositorTaxId, p.AgingStep, p.Status })
            .ToListAsync(ct);
        partidas = partidas.Where(p => a.Alcance.IncluyeOficina(p.BranchId) && (a.Oficina is null || a.Oficina == p.BranchId)).ToList();
        var columnas = new List<ColumnaExportable>
        {
            new("Partida"), new("Consignada", TipoDeColumna.Fecha), new("Abierta", TipoDeColumna.Fecha), new("Días", TipoDeColumna.Entero), new("Escalón", TipoDeColumna.Entero),
            new("Valor", TipoDeColumna.Moneda), new("Saldo", TipoDeColumna.Moneda), new("Referencia"), new("Consignante"), new("Documento"), new("Estado"),
            new("_partida", Clave: "_partida"),
        };
        var filas = partidas.Select(p => new FilaExportable(
        [
            p.Number, p.MovementDate, p.OpenedOn, corte.DayNumber - p.OpenedOn.DayNumber, (int)p.AgingStep, p.Amount, p.Balance, p.Reference, p.DepositorName, p.DepositorTaxId,
            p.Status.ToString(), p.PublicId,
        ], Resaltada: p.AgingStep > 0)).ToList();
        return new TablaExportable("Partidas por identificar", $"Al {corte:dd/MM/yyyy}", columnas, filas,
            new FilaExportable(["Total", null, null, null, null, partidas.Sum(p => p.Amount), partidas.Sum(p => p.Balance), null, null, null, null, null]), []);
    }

    private async Task<TablaExportable> SaldosAFavorAsync(Ambito a, DateOnly corte, CancellationToken ct)
    {
        var saldos = await db.PayerCreditBalances.AsNoTracking().Where(s => s.Status == PayerCreditBalanceState.Open && s.OpenedOn <= corte && s.Balance > 0m)
            .OrderBy(s => s.OpenedOn)
            .Select(s => new { s.PublicId, s.PersonId, s.Kind, s.Origin, s.OriginDocumentNumber, s.BranchId, s.Amount, s.Balance, s.OpenedOn }).ToListAsync(ct);
        saldos = saldos.Where(s => a.Alcance.IncluyeOficina(s.BranchId) && (a.Oficina is null || a.Oficina == s.BranchId)).ToList();
        var personas = await PersonasAsync(saldos.Select(s => s.PersonId), ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Pagador"), new("Documento"), new("Clase"), new("Origen"), new("Documento de origen"), new("Abierto", TipoDeColumna.Fecha), new("Días", TipoDeColumna.Entero),
            new("Valor", TipoDeColumna.Moneda), new("Saldo", TipoDeColumna.Moneda), new("_persona", Clave: "_persona"), new("_saldo", Clave: "_saldo"),
        };
        var filas = saldos.Select(s =>
        {
            var p = personas.GetValueOrDefault(s.PersonId);
            return new FilaExportable([p.Nombre, p.Documento, s.Kind.ToString(), s.Origin.ToString(), s.OriginDocumentNumber, s.OpenedOn, corte.DayNumber - s.OpenedOn.DayNumber,
                s.Amount, s.Balance, p.PublicId, s.PublicId]);
        }).ToList();
        return new TablaExportable("Saldos a favor de pagadores", $"Al {corte:dd/MM/yyyy}", columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, null, saldos.Sum(s => s.Amount), saldos.Sum(s => s.Balance), null, null]), []);
    }

    private async Task<TablaExportable> PagaduriasAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var archivos = await db.ReceiptsPayrollDeductionFiles.AsNoTracking()
            .Where(x => x.Status != PayrollDeductionFileState.Superseded && x.SendBy >= desde && x.SendBy <= hasta)
            .Select(x => new { x.PaymasterPersonId, x.BranchId, x.Amount, x.LineCount, x.Status }).ToListAsync(ct);
        var giros = await db.PaymasterRemittances.AsNoTracking().Where(x => x.RemittedOn >= desde && x.RemittedOn <= hasta)
            .Select(x => new { x.PaymasterPersonId, x.BranchId, x.Amount, x.RemittedOn }).ToListAsync(ct);
        bool Ve(int pagaduria, int oficina) => a.Alcance.IncluyePagaduria(pagaduria) || a.Alcance.IncluyeOficina(oficina);
        archivos = archivos.Where(x => Ve(x.PaymasterPersonId, x.BranchId) && (a.Oficina is null || a.Oficina == x.BranchId)).ToList();
        giros = giros.Where(x => Ve(x.PaymasterPersonId, x.BranchId) && (a.Oficina is null || a.Oficina == x.BranchId)).ToList();
        var pagadurias = archivos.Select(x => x.PaymasterPersonId).Concat(giros.Select(x => x.PaymasterPersonId)).Distinct().ToList();
        var personas = await PersonasAsync(pagadurias, ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Pagaduría"), new("Documento"), new("Archivos", TipoDeColumna.Entero), new("Líneas", TipoDeColumna.Entero), new("Enviado", TipoDeColumna.Moneda),
            new("Girado", TipoDeColumna.Moneda), new("Diferencia", TipoDeColumna.Moneda), new("Último giro"), new("_persona", Clave: "_persona"),
        };
        var filas = pagadurias.Select(id =>
        {
            var p = personas.GetValueOrDefault(id);
            var enviado = archivos.Where(x => x.PaymasterPersonId == id).Sum(x => x.Amount);
            var girado = giros.Where(x => x.PaymasterPersonId == id).Sum(x => x.Amount);
            var ultimo = giros.Where(x => x.PaymasterPersonId == id).Select(x => (DateOnly?)x.RemittedOn).Max();
            return new FilaExportable([p.Nombre, p.Documento, archivos.Count(x => x.PaymasterPersonId == id), archivos.Where(x => x.PaymasterPersonId == id).Sum(x => x.LineCount),
                enviado, girado, enviado - girado, ultimo?.ToString("yyyy-MM-dd"), p.PublicId], Resaltada: enviado != girado);
        }).OrderBy(f => f.Valores[0]?.ToString(), StringComparer.CurrentCulture).ToList();
        return new TablaExportable("Pagadurías", Periodo(desde, hasta), columnas, filas, null,
            ["Enviado: los archivos de descuentos con fecha de envío en el rango (sin los reemplazados). Girado: los giros de la pagaduría con fecha en el rango."]);
    }

    private async Task<TablaExportable> ChequesDevueltosAsync(Ambito a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var cheques = await (from e in db.ReceivedCheckEvents.AsNoTracking()
                             join c in db.ReceivedChecks.AsNoTracking() on e.ReceivedCheckId equals c.Id
                             join b in db.Banks.AsNoTracking() on c.DraweeBankId equals b.Id
                             where e.ToStatus == ReceivedCheckState.Returned && e.At >= inicio && e.At < fin
                             orderby e.At
                             select new { c.PublicId, c.CheckNumber, Banco = b.Name, c.DrawerName, c.PayerPersonId, c.Amount, c.IssueDate, c.BranchId, e.At, Causa = e.CauseCode ?? c.ReturnCauseCode })
            .ToListAsync(ct);
        cheques = cheques.Where(c => a.Alcance.IncluyeOficina(c.BranchId) && (a.Oficina is null || a.Oficina == c.BranchId)).ToList();
        var personas = await PersonasAsync(cheques.Select(c => c.PayerPersonId), ct);
        var causas = await db.CheckReturnCauses.AsNoTracking().ToDictionaryAsync(c => c.Code, c => c.Description, ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Devuelto"), new("Cheque"), new("Banco"), new("Girador"), new("Pagador"), new("Fecha del cheque", TipoDeColumna.Fecha), new("Valor", TipoDeColumna.Moneda),
            new("Causal"), new("_cheque", Clave: "_cheque"),
        };
        var filas = cheques.Select(c => new FilaExportable(
        [
            c.At.ToString("yyyy-MM-dd"), c.CheckNumber, c.Banco, c.DrawerName, personas.GetValueOrDefault(c.PayerPersonId).Nombre, c.IssueDate, c.Amount,
            c.Causa is null ? null : $"{c.Causa} · {causas.GetValueOrDefault(c.Causa)}", c.PublicId,
        ])).ToList();
        return new TablaExportable("Cheques devueltos", Periodo(desde, hasta), columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, cheques.Sum(c => c.Amount), null, null]), []);
    }

    private async Task<TablaExportable> ContingenciaAsync(Ambito a, CancellationToken ct)
    {
        var talonarios = await db.ContingencyBooks.AsNoTracking().Where(b => b.Status == ContingencyBookState.Active)
            .Select(b => new { b.Id, b.PublicId, b.CollectionPointId, b.FromNumber, b.ToNumber }).ToListAsync(ct);
        talonarios = talonarios.Where(t => a.IncluyePunto(t.CollectionPointId)).ToList();
        var ids = talonarios.Select(t => t.Id).ToList();
        var usados = await db.IssuedReceipts.AsNoTracking().Where(r => r.ContingencyBookId != null && ids.Contains(r.ContingencyBookId.Value) && r.ContingencyNumber != null)
            .Select(r => new { Talonario = r.ContingencyBookId!.Value, Numero = r.ContingencyNumber!.Value }).ToListAsync(ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Punto"), new("Talonario"), new("Digitados", TipoDeColumna.Entero), new("Por digitar", TipoDeColumna.Entero), new("Números por digitar"),
            new("Sin usar", TipoDeColumna.Entero), new("_talonario", Clave: "_talonario"),
        };
        var filas = talonarios.Select(t =>
        {
            var digitados = usados.Where(u => u.Talonario == t.Id).Select(u => u.Numero).Distinct().ToList();
            var ultimo = digitados.Count == 0 ? t.FromNumber - 1 : digitados.Max();
            var faltan = digitados.Count == 0 ? [] : Enumerable.Range(t.FromNumber, ultimo - t.FromNumber + 1).Except(digitados).ToList();
            return new FilaExportable([a.Puntos[t.CollectionPointId].Codigo, $"{t.FromNumber}–{t.ToNumber}", digitados.Count, faltan.Count,
                string.Join(", ", faltan.Take(50)), t.ToNumber - ultimo, t.PublicId], Resaltada: faltan.Count > 0);
        }).ToList();
        return new TablaExportable("Contingencia por digitar", $"Al {reloj.HoyLocal:dd/MM/yyyy}", columnas, filas, null,
            ["«Por digitar» son los números del papel por debajo del último digitado que todavía no entraron al sistema."]);
    }
}
