using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Analytics;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Dashboard;

/// <summary>A dónde lleva una ficha con un clic: la página y su query (contracts/api.md §28). (nuevo)</summary>
public sealed record DashboardLinkDto(string Page, IReadOnlyDictionary<string, string> Query);

/// <summary>
/// Una ficha del tablero (§28): <c>unit</c> es <c>Money | Quantity | Days | Percent | Count</c> y <c>severity</c> usa los nombres de
/// <see cref="AlertSeverity"/>; los dos son textos de presentación. <c>value</c> es nulo cuando no hay dato (una rotación sin inventario). (nuevo)
/// </summary>
public sealed record DashboardTileDto(
    string Key,
    string Label,
    decimal? Value,
    decimal? PreviousValue,
    string Unit,
    string? Severity,
    DashboardLinkDto Link);

/// <summary>Una sucursal o bodega que el tablero puede filtrar. (nuevo)</summary>
public sealed record DashboardScopeItemDto(Guid PublicId, string Code, string Name);

/// <summary>Las sucursales y bodegas del alcance de quien consulta, para los filtros de la pantalla. (nuevo)</summary>
public sealed record DashboardScopeDto(IReadOnlyList<DashboardScopeItemDto> Branches, IReadOnlyList<DashboardScopeItemDto> Warehouses);

/// <summary>Un tipo de alerta levantado sin destinatario activo, que se enrutó a <c>CompanyAdmin</c> (SC-022). (nuevo)</summary>
public sealed record DashboardWithoutRecipientDto(string AlertTypeCode);

/// <summary>El tablero de inventario (contracts/api.md §28, <c>InventoryDashboardDto</c>). (nuevo)</summary>
public sealed record InventoryDashboardDto(
    DateOnly AsOf,
    DashboardScopeDto Scope,
    IReadOnlyList<DashboardTileDto> Tiles,
    IReadOnlyList<DashboardWithoutRecipientDto> WithoutRecipient);

/// <summary>
/// <c>GET /api/inventory/dashboard?branch=&amp;warehouse=&amp;asOf=</c> (feature 012, I6, US17, T967; FR-088, SC-022; contracts/api.md §28),
/// <c>Inventory.Dashboard.View</c>. <paramref name="LateToleranceMinutes"/> es la tolerancia técnica de los lotes programados
/// (<c>Integration:Dispatcher:LateToleranceMinutes</c>), la misma de la vista <c>accounting-batches</c>. (nuevo)
/// </summary>
public sealed record GetInventoryDashboardQuery(
    Guid? Branch = null,
    Guid? Warehouse = null,
    DateOnly? AsOf = null,
    int LateToleranceMinutes = AccountingBatchesReportQueryHandler.ToleranciaPorDefecto) : IRequest<Result<InventoryDashboardDto>>;

/// <summary>
/// El tablero (T967; FR-088): las fichas de §28 a <c>asOf</c> (hoy por defecto), por sucursal y bodega, con el alcance por bodega de quien
/// consulta (<see cref="IAlcanceDeInventario"/>). <b>No calcula nada propio</b>: cada ficha sale de la consulta de su vista o de su área —valor,
/// rotación, margen y ventas de <see cref="AnaliticaDeInventario"/> (las de <c>valuation</c>, <c>turnover</c>, <c>margin</c>), reorden y quiebres
/// de <see cref="EvaluacionDeReposicion"/> (<c>reorder-alerts</c>), por vencer de <c>expiring</c>, mensajes y lotes de la bandeja
/// (<see cref="VistaDeMensajes"/>, <see cref="AccountingBatchesReportQueryHandler.Tarde"/>), DIAN de la bandeja electrónica
/// (<see cref="ConsultaDeDocumentosElectronicos"/>), alertas de <see cref="VisibilidadDeAlertas"/> y los tipos sin paso de
/// <c>Contabilidad.ModoDePaso</c> por <see cref="ILectorDeParametros"/>—.
/// <list type="bullet">
/// <item>Las de valor y margen (<c>inventoryValue</c>, <c>turnover</c>, <c>inventoryDays</c>, <c>grossMargin</c>) exigen <c>Inventory.Costs.Read</c>;
/// sin él no salen.</item>
/// <item>Las de un área salen sólo con el permiso que abre esa área: mensajes y lotes con <c>Inventory.Messages.View</c>, DIAN con
/// <c>ElectronicInvoicing.Documents.View</c>, alertas y <c>withoutRecipient</c> con <c>Inventory.Alerts.View</c>, tipos sin paso con
/// <c>Inventory.DocumentTypes.View</c>.</item>
/// <item>Rotación, días y margen son del mes de <c>asOf</c> hasta esa fecha; las ventas del día comparan con el día anterior y las del mes con el
/// mismo tramo del mes anterior.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class GetInventoryDashboardQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    AnaliticaDeInventario analitica,
    EvaluacionDeReposicion evaluacion,
    VisibilidadDeAlertas alertas,
    IEnumerable<IConsultaDeFuenteElectronica> fuentesElectronicas,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
    : IRequestHandler<GetInventoryDashboardQuery, Result<InventoryDashboardDto>>
{
    public const string PaginaDeInformes = "/inventario/informes";
    public const string PaginaDeMensajes = "/inventario/bandeja-de-mensajes";
    public const string PaginaDian = "/ventas/documentos-electronicos";
    public const string PaginaDeParametros = "/inventario/parametros";
    public const string PaginaDeAlertas = "/inventario/alertas";

    public const string PermisoDeMensajes = "Inventory.Messages.View";
    public const string PermisoDian = "ElectronicInvoicing.Documents.View";
    public const string PermisoDeAlertas = "Inventory.Alerts.View";
    public const string PermisoDeTipos = "Inventory.DocumentTypes.View";

    private const string Dinero = "Money";
    private const string Cantidad = "Quantity";
    private const string Dias = "Days";
    private const string Porcentaje = "Percent";
    private const string Conteo = "Count";

    public async Task<Result<InventoryDashboardDto>> Handle(GetInventoryDashboardQuery request, CancellationToken ct)
    {
        var fecha = request.AsOf ?? reloj.HoyLocal;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(new FiltrosDeInformeDeInventario { Branch = request.Branch, Warehouse = request.Warehouse }, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<InventoryDashboardDto>(ambito.Error);
        var a = ambito.Value;
        var inicioDelMes = new DateOnly(fecha.Year, fecha.Month, 1);
        var fichas = new List<DashboardTileDto>();

        // Ventas del mes (la misma consulta de margin): también dan el margen y las ventas del día.
        var delMes = await analitica.LineasDeVentaAsync(a, inicioDelMes, fecha, ct);

        if (await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct))
        {
            var valor = (await analitica.ExistenciasAsync(a, fecha, sinTransito: false, ct)).Sum(x => x.Value);
            fichas.Add(new("inventoryValue", "Valor del inventario", valor, null, Dinero, null,
                Informe("valuation", ("includeTransit", "true"))));

            var costo = (await analitica.CostoDeVentaPorProductoAsync(a, inicioDelMes, fecha, ct)).Values.Sum();
            var promedio = (await analitica.InventarioPromedioPorProductoAsync(a, inicioDelMes, fecha, ct)).Values.Sum();
            var rotacion = IndicadoresDeRotacion.Calcular(costo, promedio, IndicadoresDeRotacion.DiasDelPeriodo(inicioDelMes, fecha));
            fichas.Add(new("turnover", "Rotación del mes", rotacion.Rotacion, null, Cantidad, null, Informe("turnover")));
            fichas.Add(new("inventoryDays", "Días de inventario", rotacion.DiasDeInventario, null, Dias, null, Informe("turnover")));

            var margen = AnaliticaDeInventario.PorcentajeDeMargen(delMes.Sum(l => l.VentaNeta), delMes.Sum(l => l.CostoDeVenta));
            fichas.Add(new("grossMargin", "Margen bruto del mes", margen, null, Porcentaje, margen < 0m ? nameof(AlertSeverity.Critical) : null,
                Informe("margin", ("by", "category"))));
        }

        var hoy = delMes.Where(l => l.OperationDate == fecha).Sum(l => l.VentaNeta);
        var ayer = (await analitica.LineasDeVentaAsync(a, fecha.AddDays(-1), fecha.AddDays(-1), ct)).Sum(l => l.VentaNeta);
        var mesAnterior = (await analitica.LineasDeVentaAsync(a, inicioDelMes.AddMonths(-1), fecha.AddMonths(-1), ct)).Sum(l => l.VentaNeta);
        fichas.Add(new("salesToday", "Ventas del día", hoy, ayer, Dinero, null, Informe("sales-by-register", ("from", Fecha(fecha)), ("to", Fecha(fecha)))));
        fichas.Add(new("salesMonth", "Ventas del mes", delMes.Sum(l => l.VentaNeta), mesAnterior, Dinero, null,
            Informe("sales-by-register", ("from", Fecha(inicioDelMes)), ("to", Fecha(fecha)))));

        // Reorden y quiebres (la evaluación de reorder-alerts) y por vencer (la de expiring).
        var politicas = db.ReorderPolicies.AsNoTracking().PorBodega(alcance, r => r.WarehouseId);
        if (a.Bodegas is not null)
        {
            var bodegas = a.Bodegas.ToArray();
            politicas = politicas.Where(r => bodegas.Contains(r.WarehouseId));
        }
        var reposicion = await evaluacion.EvaluarAsync(politicas, ct);
        var bajoElPunto = reposicion.Count(x => x.Resultado.RequiereReorden);
        var quiebres = reposicion.Count(x => x.Resultado.Quiebre);
        fichas.Add(new("belowReorder", "Bajo punto de reorden", bajoElPunto, null, Conteo, bajoElPunto > 0 ? nameof(AlertSeverity.Warning) : null,
            Informe("reorder-alerts")));
        fichas.Add(new("stockouts", "Quiebres", quiebres, null, Conteo, quiebres > 0 ? nameof(AlertSeverity.Critical) : null, Informe("reorder-alerts")));
        var diasParaVencer = await Vistas.NoMovementReportQueryHandler.DiasAsync(parametros, ParametrosDeInventario.InformesDiasProximoAVencer, fecha, ct);
        var porVencer = (await analitica.LotesPorVencerAsync(a, fecha, diasParaVencer, ct)).Count;
        fichas.Add(new("expiringSoon", "Próximos a vencer", porVencer, null, Conteo, porVencer > 0 ? nameof(AlertSeverity.Warning) : null, Informe("expiring")));

        // Mensajes y lotes: las consultas de la bandeja.
        if (await permisos.HasPermissionAsync(PermisoDeMensajes, ct))
        {
            var entregas = VistaDeMensajes.Visibles(db, alcance);
            if (a.Bodegas is not null)
            {
                var ids = a.Bodegas.ToList();
                var codigos = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => ids.Contains(w.Id)).Select(w => w.Code).ToListAsync(ct);
                entregas = entregas.Where(d => d.Message!.WarehouseCode != null && codigos.Contains(d.Message.WarehouseCode));
            }
            var pendientes = await entregas.CountAsync(d => d.Status == DeliveryStatus.Pending, ct);
            var rechazados = await entregas.CountAsync(d => d.Status == DeliveryStatus.Rejected, ct);
            fichas.Add(new("messagesPending", "Mensajes pendientes", pendientes, null, Conteo, null, Pagina(PaginaDeMensajes, ("status", nameof(DeliveryStatus.Pending)))));
            fichas.Add(new("messagesRejected", "Mensajes rechazados", rechazados, null, Conteo, rechazados > 0 ? nameof(AlertSeverity.Critical) : null,
                Pagina(PaginaDeMensajes, ("status", nameof(DeliveryStatus.Rejected)))));

            var lotes = AccountingBatchesReportQueryHandler.Visibles(db, db.IntegrationBatches.AsNoTracking(), alcance);
            var tarde = await AccountingBatchesReportQueryHandler.Tarde(lotes, AccountingBatchesReportQueryHandler.Limite(reloj, request.LateToleranceMinutes)).CountAsync(ct);
            fichas.Add(new("lateBatches", "Lotes que no corrieron", tarde, null, Conteo, tarde > 0 ? nameof(AlertSeverity.Warning) : null, Informe("accounting-batches")));
        }

        // DIAN: la bandeja de documentos electrónicos con el alcance de cada módulo fuente.
        if (await permisos.HasPermissionAsync(PermisoDian, ct))
        {
            var electronicos = await ConsultaDeDocumentosElectronicos.ConAlcanceAsync(db.ElectronicDocuments.AsNoTracking(), fuentesElectronicas, ct);
            var pendientes = await electronicos.CountAsync(d => d.Status == ElectronicDocumentStatus.Pending, ct);
            var rechazados = await electronicos.CountAsync(d => d.Status == ElectronicDocumentStatus.Rejected, ct);
            fichas.Add(new("dianPending", "DIAN pendientes", pendientes, null, Conteo, pendientes > 0 ? nameof(AlertSeverity.Warning) : null,
                Pagina(PaginaDian, ("status", nameof(ElectronicDocumentStatus.Pending)))));
            fichas.Add(new("dianRejected", "DIAN rechazados", rechazados, null, Conteo, rechazados > 0 ? nameof(AlertSeverity.Critical) : null,
                Pagina(PaginaDian, ("status", nameof(ElectronicDocumentStatus.Rejected)))));
        }

        // Tipos fiscales configurados para no pasar a contabilidad (FR-075, FR-088).
        if (await permisos.HasPermissionAsync(PermisoDeTipos, ct))
        {
            var tipos = (await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.IsActive).Select(t => new { t.Id, t.Class }).ToListAsync(ct))
                .Where(t => ClasesDeDocumento.De(t.Class).IsFiscal).ToList();
            var sinPaso = 0;
            foreach (var t in tipos)
            {
                var modo = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, fecha,
                    ParameterScopeKind.DocumentType, t.Id, ct);
                if (modo.IsSuccess && modo.Value.Texto == "NoPasa") sinPaso++;
            }
            fichas.Add(new("fiscalTypesNotPosted", "Tipos fiscales sin paso a contabilidad", sinPaso, null, Conteo, sinPaso > 0 ? nameof(AlertSeverity.Warning) : null,
                Pagina(PaginaDeParametros, ("clave", ParametrosDeInventario.ContabilidadModoDePaso))));
        }

        // Alertas: las que alcanzan a quien consulta.
        IReadOnlyList<DashboardWithoutRecipientDto> sinDestinatario = [];
        if (await permisos.HasPermissionAsync(PermisoDeAlertas, ct))
        {
            var visibles = (await alertas.VisiblesAsync(ct)).AsNoTracking().Where(x => x.Status == AlertStatus.Pending);
            if (a.Bodegas is not null)
            {
                var ids = a.Bodegas.ToList();
                var publicas = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => ids.Contains(w.Id)).Select(w => w.PublicId).ToListAsync(ct);
                visibles = visibles.Where(x => x.ScopeWarehousePublicId == null || publicas.Contains(x.ScopeWarehousePublicId.Value));
            }
            var pendientes = await visibles.CountAsync(ct);
            fichas.Add(new("alertsPending", "Alertas pendientes", pendientes, null, Conteo, null, Pagina(PaginaDeAlertas, ("status", nameof(AlertStatus.Pending)))));
            sinDestinatario = (await visibles.Where(x => x.WithoutRecipient).Select(x => x.TypeCode).Distinct().ToListAsync(ct))
                .Order(StringComparer.Ordinal).Select(c => new DashboardWithoutRecipientDto(c)).ToList();
        }

        return Result.Success(new InventoryDashboardDto(fecha, await AlcanceAsync(alcance, ct), fichas, sinDestinatario));
    }

    /// <summary>Las sucursales y bodegas activas del alcance (sin las de tránsito), para los filtros.</summary>
    private async Task<DashboardScopeDto> AlcanceAsync(AlcanceDeInventario alcance, CancellationToken ct)
    {
        var bodegas = (await db.Warehouses.AsNoTracking().Where(w => w.IsActive && w.Behavior != Domain.Enums.Inventory.WarehouseBehavior.Transit)
                .Select(w => new { w.Id, w.PublicId, w.Code, w.Name, w.BranchId }).ToListAsync(ct))
            .Where(w => alcance.IncluyeBodega(w.Id)).OrderBy(w => w.Code, StringComparer.Ordinal).ToList();
        var sucursalIds = bodegas.Select(w => w.BranchId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().Where(b => sucursalIds.Contains(b.Id))
            .Select(b => new { b.PublicId, b.LegacyCode, b.Name }).ToListAsync(ct);
        return new DashboardScopeDto(
            sucursales.OrderBy(b => b.Name, StringComparer.CurrentCulture).Select(b => new DashboardScopeItemDto(b.PublicId, b.LegacyCode ?? b.Name, b.Name)).ToList(),
            bodegas.Select(w => new DashboardScopeItemDto(w.PublicId, w.Code, w.Name)).ToList());
    }

    private static DashboardLinkDto Informe(string vista, params (string Clave, string Valor)[] mas)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["vista"] = vista };
        foreach (var (clave, valor) in mas) query[clave] = valor;
        return new DashboardLinkDto(PaginaDeInformes, query);
    }

    private static DashboardLinkDto Pagina(string pagina, params (string Clave, string Valor)[] query) =>
        new(pagina, query.ToDictionary(q => q.Clave, q => q.Valor, StringComparer.Ordinal));

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
