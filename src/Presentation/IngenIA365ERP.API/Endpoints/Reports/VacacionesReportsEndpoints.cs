using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// Feature 010 US4 (contracts/api.md §11): las vistas de vacaciones del centro de reportes,
/// <c>saldos-vacaciones</c> y <c>movimientos-vacaciones</c>, con el mismo mecanismo de la 006
/// (<c>TablaExportable</c> + <see cref="EntregaDeInformes"/>): <c>format=json</c> para la pantalla,
/// <c>xlsx|pdf|docx</c> para el archivo. Exigen <c>Payroll.Vacations.View</c>.
/// </summary>
public sealed class VacacionesReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        group.MapGet("/saldos-vacaciones", async (DateOnly? asOf, string? search, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new SaldosVacacionesReportQuery(asOf, search), ct), format, "saldos-vacaciones", http))
            .WithName("Reportes_Nomina_SaldosVacaciones").RequirePermission("Payroll.Vacations.View");

        group.MapGet("/movimientos-vacaciones", async (DateOnly desde, DateOnly hasta, Guid? employeeId, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new MovimientosVacacionesReportQuery(desde, hasta, employeeId), ct), format, "movimientos-vacaciones", http))
            .WithName("Reportes_Nomina_MovimientosVacaciones").RequirePermission("Payroll.Vacations.View");
    }
}
