using Carter;
using IngenIA365ERP.Application.Inventory.Products.Commands.CreateProduct;
using IngenIA365ERP.Application.Inventory.Products.Commands.UpdateProduct;
using IngenIA365ERP.Application.Inventory.Products.Commands.DeleteProduct;
using IngenIA365ERP.Application.Inventory.Products.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class ProductsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/products")
            .WithTags("Products")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListProductsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListProducts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProductByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetProductById");

        group.MapPost("/", async (CreateProductCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/products/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateProduct");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateProduct");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteProductCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteProduct");
    }
}
