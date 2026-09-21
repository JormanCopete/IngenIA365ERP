using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// Feature 010 US2 (contracts/api.md §11): la vista <c>consignacion-cesantias</c> del centro de
/// reportes de nómina, por el mismo camino que las de la 006 (<see cref="EntregaDeInformes"/>): JSON
/// para la pantalla y <c>xlsx</c> (una hoja por fondo), <c>pdf</c> o <c>docx</c> para entregar al fondo.
/// Vive en su propio módulo para que la 010 no toque <c>PayrollReportsEndpoints</c>.
/// </summary>
public sealed class CesantiasReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        group.MapGet("/consignacion-cesantias", async (Guid runId, Guid? fundId, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(
                    await sender.Send(new ConsignacionCesantiasReportQuery(runId, fundId, EntregaDeInformes.Normalizar(format)), ct),
                    format, "consignacion-cesantias", http))
            .WithName("Reportes_Nomina_ConsignacionCesantias")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Severance.View");
    }
}
