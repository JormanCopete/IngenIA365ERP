using Carter;
using IngenIA365ERP.Application.Core.People.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class PeopleDetailEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/people")
            .WithTags("People")
            .RequireAuthorization();

        group.MapGet("/{id:guid}/detail", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPersonDetailQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPersonDetail");

        group.MapGet("/search", async (string? q, ISender sender) =>
        {
            var result = await sender.Send(new SearchPeopleQuery(q ?? ""));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("SearchPeople");
    }
}
