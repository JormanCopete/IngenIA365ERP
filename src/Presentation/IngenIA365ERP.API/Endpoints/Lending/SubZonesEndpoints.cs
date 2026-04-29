using Carter;
using IngenIA365ERP.Application.Lending.SubZones.Commands.CreateSubZone;
using IngenIA365ERP.Application.Lending.SubZones.Commands.UpdateSubZone;
using IngenIA365ERP.Application.Lending.SubZones.Commands.DeleteSubZone;
using IngenIA365ERP.Application.Lending.SubZones.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class SubZonesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/sub-zones")
            .WithTags("SubZones")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSubZonesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSubZones");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSubZoneByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSubZoneById");

        group.MapPost("/", async (CreateSubZoneCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/sub-zones/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSubZone");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSubZoneCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSubZone");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSubZoneCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSubZone");
    }
}
