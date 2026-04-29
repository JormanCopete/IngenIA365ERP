using Carter;
using IngenIA365ERP.Application.Lending.Defaults.Commands.CalculateDefaults;
using IngenIA365ERP.Application.Lending.Defaults.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class DefaultsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/defaults")
            .WithTags("LendingDefaults")
            .RequireAuthorization();

        group.MapPost("/calculate", async (CalculateDefaultsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("CalculateDefaults");

        group.MapGet("/", async ([AsParameters] ListDefaultPortfoliosQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDefaultPortfolios");

        group.MapGet("/summary", async (ISender sender) =>
        {
            var result = await sender.Send(new GetDefaultSummaryQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetDefaultSummary");
    }
}
