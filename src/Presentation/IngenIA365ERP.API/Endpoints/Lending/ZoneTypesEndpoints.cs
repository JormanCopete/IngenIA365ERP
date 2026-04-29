using Carter;
using IngenIA365ERP.Application.Lending.ZoneTypes.Commands.CreateZoneType;
using IngenIA365ERP.Application.Lending.ZoneTypes.Commands.UpdateZoneType;
using IngenIA365ERP.Application.Lending.ZoneTypes.Commands.DeleteZoneType;
using IngenIA365ERP.Application.Lending.ZoneTypes.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class ZoneTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/zone-types")
            .WithTags("ZoneTypes")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListZoneTypesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListZoneTypes");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetZoneTypeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetZoneTypeById");

        group.MapPost("/", async (CreateZoneTypeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/zone-types/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateZoneType");

        group.MapPut("/{id:guid}", async (Guid id, UpdateZoneTypeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateZoneType");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteZoneTypeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteZoneType");
    }
}
