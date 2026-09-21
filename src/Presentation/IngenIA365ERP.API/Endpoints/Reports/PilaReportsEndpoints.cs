using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>Feature 010 (N2, contracts/api.md §11): las vistas <c>pila-lineas</c>, <c>pila-cuadre</c>, <c>pila-inconsistencias</c> y <c>retencion-p2</c> del centro de reportes.</summary>
public sealed class PilaReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        group.MapGet("/pila-lineas", async (Guid generationId, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new PilaLineasReportQuery(generationId, EntregaDeInformes.EsExportacion(format)), ct), format, "pila-lineas", http))
            .WithName("Reportes_Nomina_PilaLineas").RequirePermission("Payroll.Pila.View");

        group.MapGet("/pila-cuadre", async (Guid generationId, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new PilaCuadreReportQuery(generationId, EntregaDeInformes.EsExportacion(format)), ct), format, "pila-cuadre", http))
            .WithName("Reportes_Nomina_PilaCuadre").RequirePermission("Payroll.Pila.View");

        group.MapGet("/pila-inconsistencias", async (Guid? generationId, int? year, int? month, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new PilaInconsistenciasReportQuery(generationId, year is { } y ? (short)y : null, month is { } m ? (byte)m : null, EntregaDeInformes.EsExportacion(format)), ct), format, "pila-inconsistencias", http))
            .WithName("Reportes_Nomina_PilaInconsistencias").RequirePermission("Payroll.Pila.View");

        group.MapGet("/retencion-p2", async (Guid? calculationId, int? year, int? semester, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new RetencionP2ReportQuery(calculationId, year is { } y ? (short)y : null, semester is { } s ? (byte)s : null, EntregaDeInformes.EsExportacion(format)), ct), format, "retencion-p2", http))
            .WithName("Reportes_Nomina_RetencionP2").RequirePermission("Payroll.WithholdingRate.View");
    }
}
