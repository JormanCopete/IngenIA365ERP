using Carter;
using IngenIA365ERP.Application.Lending.Classifications.Commands.ClassifyPortfolio;
using IngenIA365ERP.Application.Lending.Classifications.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class ClassificationsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/classifications")
            .WithTags("LendingClassifications")
            .RequireAuthorization();

        group.MapPost("/", async (ClassifyPortfolioCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ClassifyPortfolio");

        group.MapGet("/current", async (ISender sender) =>
        {
            var result = await sender.Send(new GetPortfolioClassificationQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetCurrentClassification");

        group.MapGet("/history", async (ISender sender) =>
        {
            var result = await sender.Send(new GetClassificationHistoryQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetClassificationHistory");
    }
}
