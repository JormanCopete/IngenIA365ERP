using Carter;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Lending.Reports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

public class LendingReportsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/lending")
            .WithTags("Lending Reports")
            .RequireAuthorization();

        // Portfolio Aging
        group.MapGet("/aging", async (DateTime? asOfDate, Guid? branchId, int? creditLineId, ISender sender) =>
        {
            var date = asOfDate ?? DateTime.UtcNow.Date;
            var result = await sender.Send(new GetPortfolioAgingQuery(date, branchId, creditLineId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetPortfolioAging");

        group.MapGet("/aging/pdf", async (DateTime? asOfDate, Guid? branchId, int? creditLineId, ISender sender) =>
        {
            var date = asOfDate ?? DateTime.UtcNow.Date;
            var result = await sender.Send(new GetPortfolioAgingQuery(date, branchId, creditLineId));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            var pdf = PortfolioAgingReport.Generate(result.Value);
            return Results.File(pdf, "application/pdf", $"CarteraEdades_{date:yyyyMMdd}.pdf");
        }).WithName("GetPortfolioAgingPdf");

        // Person Portfolio
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

        // Loan Statement
        group.MapGet("/loan-statement/{loanId:guid}/pdf", async (Guid loanId, ISender sender) =>
        {
            // TODO: Implement loan statement query by PublicId, build LoanStatementDto, generate PDF
            return Results.NotFound(new { message = "Loan statement query not yet implemented" });
        }).WithName("GetLoanStatementPdf");

        // Savings Statement
        group.MapGet("/savings-statement/{accountId:guid}/pdf", async (Guid accountId, DateTime? dateFrom, DateTime? dateTo, ISender sender) =>
        {
            // TODO: Implement savings statement query, build SavingsStatementDto, generate PDF
            return Results.NotFound(new { message = "Savings statement query not yet implemented" });
        }).WithName("GetSavingsStatementPdf");
    }
}
