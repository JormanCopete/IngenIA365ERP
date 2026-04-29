using Carter;
using IngenIA365ERP.Application.Inventory.Warehouses.Commands.CreateWarehouse;
using IngenIA365ERP.Application.Inventory.Warehouses.Commands.UpdateWarehouse;
using IngenIA365ERP.Application.Inventory.Warehouses.Commands.DeleteWarehouse;
using IngenIA365ERP.Application.Inventory.Warehouses.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class WarehousesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/warehouses")
            .WithTags("Warehouses")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWarehousesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWarehouses");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWarehouseByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWarehouseById");

        group.MapPost("/", async (CreateWarehouseCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/warehouses/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWarehouse");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWarehouseCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateWarehouse");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWarehouseCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWarehouse");
    }
}
