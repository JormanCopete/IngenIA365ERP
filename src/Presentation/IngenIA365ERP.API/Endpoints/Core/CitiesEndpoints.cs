using Carter;
using IngenIA365ERP.Application.Core.Cities.Commands.CreateCity;
using IngenIA365ERP.Application.Core.Cities.Commands.UpdateCity;
using IngenIA365ERP.Application.Core.Cities.Commands.DeleteCity;
using IngenIA365ERP.Application.Core.Cities.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class CitiesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/cities")
            .WithTags("Cities")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListCitiesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListCities");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCityByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetCityById");

        group.MapPost("/", async (CreateCityCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/cities/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateCity");

        group.MapPut("/{id:guid}", async (Guid id, UpdateCityCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateCity");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCityCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteCity");
    }
}
