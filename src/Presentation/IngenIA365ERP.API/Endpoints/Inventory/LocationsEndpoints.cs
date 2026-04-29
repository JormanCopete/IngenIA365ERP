using Carter;
using IngenIA365ERP.Application.Inventory.Locations.Commands.CreateLocation;
using IngenIA365ERP.Application.Inventory.Locations.Commands.UpdateLocation;
using IngenIA365ERP.Application.Inventory.Locations.Commands.DeleteLocation;
using IngenIA365ERP.Application.Inventory.Locations.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class LocationsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/locations")
            .WithTags("Locations")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListLocationsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListLocations");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetLocationByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetLocationById");

        group.MapPost("/", async (CreateLocationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/locations/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateLocation");

        group.MapPut("/{id:guid}", async (Guid id, UpdateLocationCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateLocation");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteLocationCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteLocation");
    }
}
