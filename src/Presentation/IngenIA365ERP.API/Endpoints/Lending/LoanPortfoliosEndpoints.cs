using Carter;
using IngenIA365ERP.Application.Lending.LoanPortfolios.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class LoanPortfoliosEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/portfolios")
            .WithTags("LoanPortfolios")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListLoanPortfoliosQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListLoanPortfolios");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetLoanPortfolioByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetLoanPortfolioById");

        group.MapGet("/{id:guid}/statement", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetLoanStatementQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetLoanStatement");

        group.MapGet("/{id:guid}/installments", async (Guid id, ISender sender) =>
        {
            // Returns detail with installments
            var result = await sender.Send(new GetLoanPortfolioByIdQuery(id));
            return result.IsSuccess
                ? Results.Ok(result.Value.Installments)
                : Results.NotFound(result.Error);
        }).WithName("GetLoanInstallments");
    }
}
