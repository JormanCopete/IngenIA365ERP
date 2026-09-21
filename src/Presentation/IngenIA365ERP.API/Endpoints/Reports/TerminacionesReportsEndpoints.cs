using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// Feature 010, US3 (contracts/api.md §11): las vistas <c>terminaciones</c> y
/// <c>saldos-iniciales-prestaciones</c> del centro de reportes de nómina, por el mismo camino que las
/// demás (<c>EntregaDeInformes</c>: JSON para la pantalla, xlsx/pdf/docx para descargar). Módulo
/// propio para no tocar <c>PayrollReportsEndpoints</c>, que comparten otras historias.
/// </summary>
public sealed class TerminacionesReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/payroll")
            .WithTags("Payroll Reports")
            .RequireAuthorization();

        group.MapGet("/terminaciones", async (DateOnly desde, DateOnly hasta, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new TerminacionesReportQuery(desde, hasta), ct), format, "terminaciones", http))
            .WithName("Reportes_Nomina_Terminaciones")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.Settlements.View");

        group.MapGet("/saldos-iniciales-prestaciones", async (DateOnly? asOf, string? format, ISender sender, HttpContext http, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new SaldosInicialesPrestacionesReportQuery(asOf), ct), format, "saldos-iniciales-prestaciones", http))
            .WithName("Reportes_Nomina_SaldosInicialesPrestaciones")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Payroll.BenefitBalances.View");
    }
}
