using Carter;
using IngenIA365ERP.Application.Lending.Zones.Commands.CreateZone;
using IngenIA365ERP.Application.Lending.Zones.Commands.UpdateZone;
using IngenIA365ERP.Application.Lending.Zones.Commands.DeleteZone;
using IngenIA365ERP.Application.Lending.Zones.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class ZonesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/zones")
            .WithTags("Zones")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListZonesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListZones");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetZoneByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetZoneById");

        group.MapPost("/", async (CreateZoneCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/zones/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateZone");

        group.MapPut("/{id:guid}", async (Guid id, UpdateZoneCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateZone");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteZoneCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteZone");
    }
}
