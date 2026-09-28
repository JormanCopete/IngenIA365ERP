using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Reports.Vistas;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.AspNetCore.Routing;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// El centro de informes de Inventario (feature 012, T44, T182; contracts/api.md §27, decisiones-transversales §2.12):
/// una ruta por vista bajo <c>/api/reports/inventory/{vista}</c>, todas con los filtros comunes de
/// <see cref="FiltrosDeInformeDeInventario"/> y <c>format=json|xlsx|pdf|docx</c>, entregadas por
/// <see cref="EntregaDeInformes.EntregarAsync"/>. Reescrito como <b>base vacía</b>: el módulo heredado se retiró en la
/// fase 2 (sus rutas <c>/valuation/pdf</c> e <c>/invoice/{id}/pdf</c> no vuelven, ni con alias) y cada historia registra
/// sus vistas en <see cref="MapVistas"/> con <see cref="InventoryReportsRoutes.MapVistaDeInventario"/>. (reescrito)
///
/// <para>
/// <c>GET /api/reports/inventory</c> es el <b>registro de vistas</b>: las que están publicadas, leídas de la metadata de
/// cada ruta, sin las que exigen un permiso adicional que quien pregunta no tiene. De ahí arma su selector la pantalla
/// <c>/inventario/informes</c>; una vista nueva aparece sola en cuanto su historia la registra.
/// </para>
/// </summary>
public class InventoryReportsEndpoints : ICarterModule
{
    public const string Ruta = "/api/reports/inventory";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta)
            .WithTags("Inventory Reports")
            .RequireAuthorization();

        group.MapGet("/", async (HttpContext http, CancellationToken ct) =>
            {
                var publicadas = http.RequestServices.GetRequiredService<EndpointDataSource>().Endpoints
                    .Select(e => e.Metadata.GetMetadata<VistaDeInformeDeInventario>())
                    .OfType<VistaDeInformeDeInventario>()
                    .DistinctBy(v => v.Key)
                    .OrderBy(v => v.Name, StringComparer.CurrentCulture)
                    .ToList();
                var visibles = new List<VistaDeInformeDeInventario>(publicadas.Count);
                foreach (var vista in publicadas)
                {
                    ct.ThrowIfCancellationRequested();
                    if (vista.RequiredPermission is null || await PermissionAuthorizationFilter.TieneAsync(http, vista.RequiredPermission))
                        visibles.Add(vista);
                }
                return Results.Ok(visibles);
            })
            .WithName("Reportes_Inventario_Vistas")
            .RequirePermission("Inventory.Reports.View");

        // La tolerancia técnica de un lote programado (Integration:Dispatcher:LateToleranceMinutes, T526) para la vista
        // accounting-batches; sin la sección, la de la consulta.
        var tolerancia = app.ServiceProvider.GetService<Microsoft.Extensions.Options.IOptions<IngenIA365ERP.API.Integration.IntegrationOptions>>()
            ?.Value.Dispatcher.LateToleranceMinutes ?? AccountingBatchesReportQueryHandler.ToleranciaPorDefecto;
        MapVistas(group, tolerancia);
    }

    /// <summary>
    /// Aquí registra cada historia sus vistas (T263 <c>kardex</c>/<c>stock</c>, T318, T531, T751, T809, T957, T966…), una
    /// línea por vista: <c>group.MapVistaDeInventario(new VistaDeInformeDeInventario(…), (f, q) =&gt; new …Query(f, …));</c>.
    /// La base arranca sin ninguna.
    /// </summary>
    private static void MapVistas(RouteGroupBuilder group, int toleranciaDeLotes)
    {
        // US2 (T263): el kardex de un producto y la existencia por bodega.
        group.MapVistaDeInventario(
            new VistaDeInformeDeInventario("kardex", "Kardex", "Movimientos de un producto con saldo en cantidad y valor y costo promedio.",
                "kardex", ["from", "to", "product", "warehouse"], ["location", "includeCostAdjustments"]),
            (f, q) => new KardexReportQuery(f,
                Guid.TryParse(q["location"].ToString(), out var ubicacion) ? ubicacion : null,
                !bool.TryParse(q["includeCostAdjustments"].ToString(), out var ajustes) || ajustes));
        group.MapVistaDeInventario(
            new VistaDeInformeDeInventario("stock", "Existencias", "Físico, reservado, disponible y en tránsito por bodega, con mínimos y máximos.",
                "existencias", ["warehouse", "category", "product"], ["onlyWithStock"]),
            (f, q) => new StockReportQuery(f, bool.TryParse(q["onlyWithStock"].ToString(), out var conExistencia) && conExistencia));

        // US3 (T293): el valorizado a una fecha por grupo contable, bodega y producto. Exige además Inventory.Costs.Read.
        group.MapVistaDeInventario(
            new VistaDeInformeDeInventario("valuation", "Valorizado", "Cantidad, costo promedio y valor por grupo contable, bodega y producto a una fecha.",
                "valorizado", ["asOf", "warehouse", "accountingGroup", "category", "product"], ["includeTransit"],
                RequiredPermission: ValuationReportQueryHandler.PermisoDeCostos),
            (f, q) => new ValuationReportQuery(f, bool.TryParse(q["includeTransit"].ToString(), out var conTransito) && conTransito));

        // US4 (T318): los comparativos con las cifras de referencia (US4-6). El de valorizado exige además Inventory.Costs.Read; el de kardex
        // exige la bodega y deja vacíos los valores sin ese permiso.
        group.MapVistaDeInventario(
            new VistaDeInformeDeInventario("legacy-comparison-kardex", "Comparativo de kardex con cifras de referencia",
                "Por producto, en cada fecha con cifras de referencia de la bodega: cantidad y valor de referencia contra el módulo.",
                "comparativo-kardex-referencia", ["warehouse", "product", "from", "to"], []),
            (f, _) => new LegacyComparisonKardexQuery(f));
        group.MapVistaDeInventario(
            new VistaDeInformeDeInventario("legacy-comparison-valuation", "Comparativo de valorizado con cifras de referencia",
                "Por grupo contable, bodega y producto a una fecha: cantidad y valor de referencia contra el valorizado del módulo.",
                "comparativo-valorizado-referencia", ["asOf", "warehouse", "accountingGroup"], [],
                RequiredPermission: ValuationReportQueryHandler.PermisoDeCostos),
            (f, _) => new LegacyComparisonValuationQuery(f));

        // US9 (T351): los eventos RADIAN de las facturas del proveedor (acuse 030 y recibo del bien 032).
        group.MapVistaDeInventario(
            new VistaDeInformeDeInventario("radian-events", "Eventos RADIAN",
                "Por factura del proveedor: CUFE, emisión, vencimiento, forma de pago y el estado, la fecha y la fuente del 030 y del 032.",
                "eventos-radian", ["from", "to", "person"], ["onlyPending"]),
            (f, q) => new RadianEventsReportQuery(f, bool.TryParse(q["onlyPending"].ToString(), out var pendientes) && pendientes));

        // US11 (T402): las diferencias de los conteos cerrados (valores con Inventory.Costs.Read, dentro de la consulta).
        group.MapVistaDeInventario(
            new VistaDeInformeDeInventario("count-differences", "Diferencias de conteo",
                "Por línea de cada conteo cerrado: teórico, contado, reconteo, diferencia en cantidad y valor, y el ajuste que la llevó a la existencia.",
                "diferencias-de-conteo", ["from", "to", "warehouse"], ["count", "onlyWithDifference"]),
            (f, q) => new CountDifferencesReportQuery(f,
                Guid.TryParse(q["count"].ToString(), out var conteo) ? conteo : null,
                bool.TryParse(q["onlyWithDifference"].ToString(), out var conDiferencia) && conDiferencia));

        // US17 (T957): los documentos de inventario (exportarlos con contraparte exige además ExportPersonalData) y la vista de
        // reorden y quiebres. class y status entran por nombre o por número.
        group.MapVistaDeInventario(DocumentsReportQueryHandler.Vista,
            (f, q) => new DocumentsReportQuery(f,
                Enum.TryParse<DocumentClass>(q["class"].ToString(), ignoreCase: true, out var clase) && Enum.IsDefined(clase) ? clase : null,
                Enum.TryParse<DocumentStatus>(q["status"].ToString(), ignoreCase: true, out var estado) && Enum.IsDefined(estado) ? estado : null));
        group.MapVistaDeInventario(ReorderAlertsReportQueryHandler.Vista, (f, _) => new ReorderAlertsReportQuery(f));

        // US7 (I2, T531): los mensajes a otros módulos, los lotes de contabilización y la conciliación con Contabilidad (ésta exige
        // además Inventory.Reconciliation.View, que la vista declara). status, trigger y el destino entran por nombre o número.
        group.MapVistaDeInventario(MessagesReportQueryHandler.Vista,
            (f, q) => new MessagesReportQuery(f,
                Enum.TryParse<IngenIA365ERP.Domain.Enums.Integration.DeliveryStatus>(q["status"].ToString(), ignoreCase: true, out var estado) && Enum.IsDefined(estado) ? estado : null,
                string.IsNullOrWhiteSpace(q["destination"].ToString()) ? null : q["destination"].ToString(),
                string.IsNullOrWhiteSpace(q["type"].ToString()) ? null : q["type"].ToString(),
                Guid.TryParse(q["batch"].ToString(), out var lote) ? lote : null));
        group.MapVistaDeInventario(AccountingBatchesReportQueryHandler.Vista,
            (f, q) => new AccountingBatchesReportQuery(f,
                Enum.TryParse<IngenIA365ERP.Domain.Enums.Integration.BatchStatus>(q["status"].ToString(), ignoreCase: true, out var estado) && Enum.IsDefined(estado) ? estado : null,
                Enum.TryParse<IngenIA365ERP.Domain.Enums.Integration.BatchTrigger>(q["trigger"].ToString(), ignoreCase: true, out var disparador) && Enum.IsDefined(disparador) ? disparador : null,
                toleranciaDeLotes));
        group.MapVistaDeInventario(ConciliacionReportQueryHandler.Vista, (f, _) => new ConciliacionReportQuery(f));

        // US5 (I3, T625): las diez vistas de ventas y caja (T623) y los indicios de deterioro (T624, que exige además
        // Inventory.Costs.Read). card-payments y voucher-redemptions traen datos de clientes siempre; las sales-by-* con groupBy=customer.
        // Los enums propios (class, kind, treatment, status) entran por nombre o por número.
        group.MapVistaDeInventario(SalesBySessionReportQueryHandler.Vista,
            (f, q) => new SalesBySessionReportQuery(f, Id(q, "cashier"), Texto(q, "groupBy")));
        group.MapVistaDeInventario(SalesByRegisterReportQueryHandler.Vista, (f, q) => new SalesByRegisterReportQuery(f, Texto(q, "groupBy")));
        group.MapVistaDeInventario(SalesByPaymentMeansReportQueryHandler.Vista,
            (f, q) => new SalesByPaymentMeansReportQuery(f, Id(q, "paymentMeans"), Enumerado<IngenIA365ERP.Domain.Enums.Core.PaymentMeansClass>(q, "class"), Texto(q, "groupBy")));
        group.MapVistaDeInventario(CashSessionReportQueryHandler.Vista, (f, _) => new CashSessionReportQuery(f));
        group.MapVistaDeInventario(DayCloseReportQueryHandler.Vista,
            (f, q) => new DayCloseReportQuery(f, DateOnly.TryParse(q["operatingDate"].ToString(), System.Globalization.CultureInfo.InvariantCulture, out var dia) ? dia : null,
                Id(q, "dayClose")));
        group.MapVistaDeInventario(CardPaymentsReportQueryHandler.Vista,
            (f, q) => new CardPaymentsReportQuery(f, Id(q, "acquirer"), Id(q, "terminal"), Id(q, "network")));
        group.MapVistaDeInventario(CashMovementsReportQueryHandler.Vista, (f, q) => new CashMovementsReportQuery(f, Enumerado<CashMovementKind>(q, "kind")));
        group.MapVistaDeInventario(CashDifferencesReportQueryHandler.Vista,
            (f, q) => new CashDifferencesReportQuery(f, Id(q, "cashier"), Enumerado<CashDifferenceTreatment>(q, "treatment")));
        group.MapVistaDeInventario(VoucherRedemptionsReportQueryHandler.Vista,
            (f, q) => new VoucherRedemptionsReportQuery(f, Id(q, "paymentMeans"), Enumerado<VoucherRedemptionStatus>(q, "status")));
        group.MapVistaDeInventario(DiscountApprovalsReportQueryHandler.Vista, (f, q) => new DiscountApprovalsReportQuery(f, Id(q, "approver")));
        group.MapVistaDeInventario(ImpairmentReportQueryHandler.Vista, (f, _) => new ImpairmentReportQuery(f));

        // US8 (I4, T751): los documentos electrónicos ante la DIAN. status y kind entran por nombre o por número; contingency es true/false.
        group.MapVistaDeInventario(DianDocumentsReportQueryHandler.Vista,
            (f, q) => new DianDocumentsReportQuery(f,
                Enumerado<IngenIA365ERP.Domain.Enums.ElectronicInvoicing.ElectronicDocumentStatus>(q, "status"),
                Enumerado<IngenIA365ERP.Domain.Enums.ElectronicInvoicing.ElectronicDocumentKind>(q, "kind"),
                bool.TryParse(q["contingency"].ToString(), out var conContingencia) ? conContingencia : null));
    }

    private static Guid? Id(IQueryCollection q, string clave) => Guid.TryParse(q[clave].ToString(), out var id) ? id : null;

    private static string? Texto(IQueryCollection q, string clave) => string.IsNullOrWhiteSpace(q[clave].ToString()) ? null : q[clave].ToString().Trim();

    private static T? Enumerado<T>(IQueryCollection q, string clave) where T : struct, Enum =>
        Enum.TryParse<T>(q[clave].ToString(), ignoreCase: true, out var valor) && Enum.IsDefined(valor) ? valor : null;
}

/// <summary>La extensión con la que se publica cada vista de inventario (feature 012, T182). (nuevo)</summary>
public static class InventoryReportsRoutes
{
    public const string PermisoDeVer = "Inventory.Reports.View";
    public const string PermisoDeExportar = "Inventory.Reports.Export";
    public const string PermisoDeDatosPersonales = "Inventory.Reports.ExportPersonalData";

    /// <summary>
    /// Publica <c>GET {grupo}/{vista.Key}</c> con todo lo que §27 pide de cada vista, en un solo sitio para que ninguna
    /// historia lo olvide:
    /// <list type="bullet">
    /// <item><c>.RequirePermission("Inventory.Reports.View")</c> y, si la vista lo declara, su permiso adicional
    /// (<see cref="VistaDeInformeDeInventario.RequiredPermission"/>);</item>
    /// <item><c>.RequirePermissionWhenExporting("Inventory.Reports.Export")</c>, más
    /// <c>Inventory.Reports.ExportPersonalData</c> en las vistas (PD) —siempre o con el filtro que las vuelve personales—;
    /// sin ellos, el mismo 404 que lo inexistente;</item>
    /// <item>con <see cref="VistaDeInformeDeInventario.PersonalDataColumn"/>, <c>Inventory.Reports.ExportPersonalData</c> al exportar
    /// una tabla que trae valor en esa columna (se decide después de consultar);</item>
    /// <item>el rango de los filtros comunes (hasta cinco años, no al revés) antes de consultar;</item>
    /// <item>la auditoría <c>Inventory.Report.Exported</c> por <see cref="InventoryAuditEmitter"/> en cada exportación a
    /// archivo que sale bien, con las filas de la tabla: la emite esta ruta, y las consultas no la repiten;</item>
    /// <item>la entrega por <see cref="EntregaDeInformes.EntregarAsync"/> (tabla o archivo; el error con el sobre).</item>
    /// </list>
    /// </summary>
    /// <param name="grupo">El grupo <c>/api/reports/inventory</c> (ya con <c>RequireAuthorization()</c>).</param>
    /// <param name="vista">Lo que la vista declara; va también como metadata de la ruta (el registro de vistas).</param>
    /// <param name="consulta">Arma la consulta con los filtros comunes y la query (para los filtros propios).</param>
    public static RouteHandlerBuilder MapVistaDeInventario(
        this RouteGroupBuilder grupo,
        VistaDeInformeDeInventario vista,
        Func<FiltrosDeInformeDeInventario, IQueryCollection, IRequest<Result<TablaExportable>>> consulta)
    {
        ArgumentNullException.ThrowIfNull(vista);
        ArgumentNullException.ThrowIfNull(consulta);

        var ruta = grupo.MapGet($"/{vista.Key}", async (
                [AsParameters] FiltrosDeInformeDeInventario filtros, HttpContext http, ISender sender, InventoryAuditEmitter auditoria,
                IngenIA365ERP.Application.Common.Interfaces.IDateTimeService reloj, CancellationToken ct) =>
            {
                var rango = filtros.ValidarRango(reloj.HoyLocal);
                if (rango.IsFailure)
                    return await EntregaDeInformes.EntregarAsync(Result.Failure<TablaExportable>(rango.Error), filtros.Format, vista.FileName, http);

                var exporta = EntregaDeInformes.EsExportacion(filtros.Format) && EntregaDeInformes.EsFormatoValido(filtros.Format);
                if (exporta && vista.PersonalDataWhen is not null
                    && vista.TraeDatosPersonales(p => http.Request.Query[p].ToString())
                    && !await PermissionAuthorizationFilter.TieneAsync(http, PermisoDeDatosPersonales))
                    return PermissionAuthorizationFilter.NotFoundEnvelope(http);

                var resultado = await sender.Send(consulta(filtros, http.Request.Query), ct);
                // Datos personales según lo que trajo la tabla (documents con contraparte, T957): sin el permiso, el mismo 404 y
                // sin auditar, porque no salió nada.
                if (exporta && resultado.IsSuccess && vista.TablaTraeDatosPersonales(resultado.Value)
                    && !await PermissionAuthorizationFilter.TieneAsync(http, PermisoDeDatosPersonales))
                    return PermissionAuthorizationFilter.NotFoundEnvelope(http);
                if (exporta && resultado.IsSuccess)
                    await auditoria.EmitirExportacionAsync(vista.Key, filtros.ParaAuditoria(), EntregaDeInformes.Normalizar(filtros.Format),
                        resultado.Value.Filas.Count, ct);
                return await EntregaDeInformes.EntregarAsync(resultado, filtros.Format, vista.FileName, http);
            })
            .WithName($"Reportes_Inventario_{vista.Key}")
            .WithMetadata(vista)
            .RequirePermission(PermisoDeVer)
            .RequirePermissionWhenExporting(PermisoDeExportar);

        if (vista.RequiredPermission is { } adicional) ruta.RequirePermission(adicional);
        if (vista.PersonalData) ruta.WithMetadata(new RequirePermissionWhenExportingAttribute(PermisoDeDatosPersonales));
        return ruta;
    }
}
