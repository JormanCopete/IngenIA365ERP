using Carter;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Savings.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// Informes de ahorros y aportes (feature 013, T0186; contracts/api.md §25, enmienda A10). Salieron de
/// <c>/api/reports/lending</c> al retirar la cartera heredada: el portafolio de la persona ya no trae créditos
/// (la Cartera nueva los informa por su centro de informes). Conservan el permiso de hoy —sólo sesión— y la
/// exención por nombre de <c>LosEndpointsProtegidosExigenPermiso</c> hasta la feature de Ahorros.
/// </summary>
public class SavingsReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/savings")
            .WithTags("Savings Reports")
            .RequireAuthorization();

        // Portafolio de la persona (ahorros, aportes y CDT; sin créditos)
        group.MapGet("/person-portfolio/{personId:guid}", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonPortfolioReportQuery(personId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetPersonPortfolio");

        group.MapGet("/person-portfolio/{personId:guid}/pdf", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonPortfolioReportQuery(personId));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            var pdf = PersonPortfolioReport.Generate(result.Value);
            return Results.File(pdf, "application/pdf", $"Portafolio_{result.Value.TaxId}.pdf");
        }).WithName("GetPersonPortfolioPdf");

        // Extracto de ahorro
        group.MapGet("/savings-statement/{accountId:guid}/pdf", (Guid accountId, DateTime? dateFrom, DateTime? dateTo) =>
        {
            // Pendiente desde antes del retiro: la consulta del extracto de ahorro la construye la feature de Ahorros.
            return Results.NotFound(new { message = "Savings statement query not yet implemented" });
        }).WithName("GetSavingsStatementPdf");
    }
}
