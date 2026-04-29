using Carter;
using IngenIA365ERP.Application.Inventory.ProductGroups.Commands.CreateProductGroup;
using IngenIA365ERP.Application.Inventory.ProductGroups.Commands.UpdateProductGroup;
using IngenIA365ERP.Application.Inventory.ProductGroups.Commands.DeleteProductGroup;
using IngenIA365ERP.Application.Inventory.ProductGroups.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class ProductGroupsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/product-groups")
            .WithTags("ProductGroups")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListProductGroupsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListProductGroups");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProductGroupByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetProductGroupById");

        group.MapPost("/", async (CreateProductGroupCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/product-groups/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateProductGroup");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductGroupCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateProductGroup");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteProductGroupCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteProductGroup");
    }
}
