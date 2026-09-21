using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>Feature 010 (US8, contracts/api.md §11): la vista <c>dispersion</c> del centro de reportes (líneas y pendientes de un archivo).</summary>
public sealed class DispersionReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        group.MapGet("/dispersion", async (Guid fileId, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new DispersionReportQuery(fileId, EntregaDeInformes.EsExportacion(format)), ct), format, "dispersion", http))
            .WithName("Reportes_Nomina_Dispersion").RequirePermission("Payroll.Disbursement.View");
    }
}
