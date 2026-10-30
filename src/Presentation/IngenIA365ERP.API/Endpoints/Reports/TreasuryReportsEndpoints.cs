using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// El centro de informes de Tesorería (feature 015, US21, T0315; R-36; FR-153 parte T0, FR-154; api.md §17): <c>GET /api/reports/treasury</c> es el registro de
/// vistas y cada vista vive en <c>/api/reports/treasury/{vista}?format=json|xlsx|pdf|docx</c>, con <c>Treasury.Reports.View</c> para verla y
/// <c>Treasury.Reports.Export</c> para exportarla (<c>RequirePermissionWhenExporting</c>: sin él, el mismo 404 que lo inexistente). Cada exportación que sale bien
/// deja <c>Treasury.Report.Exported</c> por <see cref="AuditoriaDeTesoreria"/>; la consulta no la repite. Molde: <c>LendingReportsEndpoints</c>. (nuevo)
/// </summary>
public class TreasuryReportsEndpoints : ICarterModule
{
    public const string Ruta = "/api/reports/treasury";
    public const string PermisoDeVer = "Treasury.Reports.View";
    public const string PermisoDeExportar = "Treasury.Reports.Export";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta).WithTags("Treasury.Reports").RequireAuthorization();

        group.MapGet("/", () => Results.Ok(VistasDeTesoreria.Vistas))
            .WithName("Reportes_Tesoreria_Vistas")
            .RequirePermission(PermisoDeVer);

        foreach (var vista in VistasDeTesoreria.Vistas)
        {
            group.MapGet($"/{vista.Key}", async ([AsParameters] FiltrosDeInformeDeRecaudoYTesoreria filtros, HttpContext http, ISender sender, AuditoriaDeTesoreria auditoria,
                    CancellationToken ct) =>
                {
                    var resultado = await sender.Send(new TreasuryReportQuery(vista.Key, filtros), ct);
                    if (EntregaDeInformes.EsExportacion(filtros.Format) && EntregaDeInformes.EsFormatoValido(filtros.Format) && resultado.IsSuccess)
                        await auditoria.InformeExportadoAsync(vista.Key, EntregaDeInformes.Normalizar(filtros.Format), resultado.Value.Filas.Count, filtros.ParaAuditoria(), ct);
                    return await EntregaDeInformes.EntregarAsync(resultado, filtros.Format, vista.FileName, http);
                })
                .WithName($"Reportes_Tesoreria_{vista.Key}")
                .WithMetadata(vista)
                .RequirePermission(PermisoDeVer)
                .RequirePermissionWhenExporting(PermisoDeExportar);
        }
    }
}
