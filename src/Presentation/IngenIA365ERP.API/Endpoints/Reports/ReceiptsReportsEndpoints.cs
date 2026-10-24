using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// El centro de informes de Recaudo (feature 015, US21, T0315; R-36; FR-152 parte R0, FR-154; api.md §17): <c>GET /api/reports/receipts</c> es el registro de
/// vistas y cada vista vive en <c>/api/reports/receipts/{vista}?format=json|xlsx|pdf|docx</c>, con <c>Receipts.Reports.View</c> para verla y
/// <c>Receipts.Reports.Export</c> para exportarla (<c>RequirePermissionWhenExporting</c>: sin él, el mismo 404 que lo inexistente). Cada exportación que sale bien
/// deja <c>Receipts.Report.Exported</c> por <see cref="AuditoriaDeRecaudo"/>; la consulta no la repite. Molde: <c>LendingReportsEndpoints</c>. (nuevo)
/// </summary>
public class ReceiptsReportsEndpoints : ICarterModule
{
    public const string Ruta = "/api/reports/receipts";
    public const string PermisoDeVer = "Receipts.Reports.View";
    public const string PermisoDeExportar = "Receipts.Reports.Export";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta).WithTags("Receipts Reports").RequireAuthorization();

        group.MapGet("/", () => Results.Ok(VistasDeRecaudo.Todas))
            .WithName("Reportes_Recaudo_Vistas")
            .RequirePermission(PermisoDeVer);

        foreach (var vista in VistasDeRecaudo.Todas)
        {
            group.MapGet($"/{vista.Key}", async ([AsParameters] FiltrosDeInformeDeRecaudoYTesoreria filtros, HttpContext http, ISender sender, AuditoriaDeRecaudo auditoria,
                    CancellationToken ct) =>
                {
                    var resultado = await sender.Send(new ReceiptsReportQuery(vista.Key, filtros), ct);
                    if (EntregaDeInformes.EsExportacion(filtros.Format) && EntregaDeInformes.EsFormatoValido(filtros.Format) && resultado.IsSuccess)
                        await auditoria.InformeExportadoAsync(vista.Key, EntregaDeInformes.Normalizar(filtros.Format), resultado.Value.Filas.Count, filtros.ParaAuditoria(), ct);
                    return await EntregaDeInformes.EntregarAsync(resultado, filtros.Format, vista.FileName, http);
                })
                .WithName($"Reportes_Recaudo_{vista.Key}")
                .WithMetadata(vista)
                .RequirePermission(PermisoDeVer)
                .RequirePermissionWhenExporting(PermisoDeExportar);
        }
    }
}
