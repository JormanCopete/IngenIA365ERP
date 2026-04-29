using Carter;
using IngenIA365ERP.Application.Lending.Contributions.Commands.ProcessContribution;
using IngenIA365ERP.Application.Lending.Contributions.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class ContributionsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/contributions")
            .WithTags("Contributions")
            .RequireAuthorization();

        group.MapGet("/{personId:guid}", async (Guid personId, ISender sender) =>
        {
            var result = await sender.Send(new GetContributionBalanceQuery(personId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetContributionBalance");

        group.MapGet("/{personId:guid}/history", async (Guid personId, DateOnly? dateFrom, DateOnly? dateTo, ISender sender) =>
        {
            var query = new ListContributionsQuery
            {
                PersonPublicId = personId,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("ListContributions");

        group.MapPost("/", async (ProcessContributionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/contributions/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("ProcessContribution");
    }
}
