using Carter;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// Feature 010 (contracts/api.md §11): las vistas del centro de reportes de las liquidaciones
/// especiales, por <c>runId</c> y para cualquier tipo (prima, cesantías, vacaciones, definitiva).
/// Mismo mecanismo de la 006: <c>TablaExportable</c> entregada por <c>EntregaDeInformes</c> como
/// JSON o archivo (<c>format=json|xlsx|pdf|docx</c>). La ruta exige <c>Payroll.Runs.View</c> —el
/// mínimo común de quien ve corridas— y el handler exige además el <c>.View</c> del recurso del
/// <c>Kind</c> de la corrida; sin él responde 404 indistinguible, como el filtro de permisos (el
/// estado lo pone <see cref="EntregaDeInformes"/> según el código, igual que en las demás vistas).
/// </summary>
public sealed class LiquidacionesReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        group.MapGet("/liquidacion-especial-resumen", async (Guid runId, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new LiquidacionEspecialResumenReportQuery(runId, EntregaDeInformes.EsExportacion(format)), ct), format, "liquidacion-especial-resumen", http))
            .WithName("Reportes_Nomina_LiquidacionEspecialResumen").RequirePermission("Payroll.Runs.View");

        group.MapGet("/liquidacion-especial-detalle", async (Guid runId, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new LiquidacionEspecialDetalleReportQuery(runId, EntregaDeInformes.EsExportacion(format)), ct), format, "liquidacion-especial-detalle", http))
            .WithName("Reportes_Nomina_LiquidacionEspecialDetalle").RequirePermission("Payroll.Runs.View");
    }
}
