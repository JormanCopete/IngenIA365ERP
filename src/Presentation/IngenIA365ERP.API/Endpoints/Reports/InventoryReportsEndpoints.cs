using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Reports;
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

        MapVistas(group);
    }

    /// <summary>
    /// Aquí registra cada historia sus vistas (T263 <c>kardex</c>/<c>stock</c>, T318, T531, T751, T809, T957, T966…), una
    /// línea por vista: <c>group.MapVistaDeInventario(new VistaDeInformeDeInventario(…), (f, q) =&gt; new …Query(f, …));</c>.
    /// La base arranca sin ninguna.
    /// </summary>
    private static void MapVistas(RouteGroupBuilder group)
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
    }
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
