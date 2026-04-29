using Carter;
using IngenIA365ERP.Application.Inventory.InventoryTransactionTypes.Commands.CreateInventoryTransactionType;
using IngenIA365ERP.Application.Inventory.InventoryTransactionTypes.Commands.UpdateInventoryTransactionType;
using IngenIA365ERP.Application.Inventory.InventoryTransactionTypes.Commands.DeleteInventoryTransactionType;
using IngenIA365ERP.Application.Inventory.InventoryTransactionTypes.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class InventoryTransactionTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/transaction-types")
            .WithTags("InventoryTransactionTypes")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListInventoryTransactionTypesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListInventoryTransactionTypes");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetInventoryTransactionTypeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetInventoryTransactionTypeById");

        group.MapPost("/", async (CreateInventoryTransactionTypeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/transaction-types/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateInventoryTransactionType");

        group.MapPut("/{id:guid}", async (Guid id, UpdateInventoryTransactionTypeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateInventoryTransactionType");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteInventoryTransactionTypeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteInventoryTransactionType");
    }
}
