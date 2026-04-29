using Carter;
using IngenIA365ERP.Application.Inventory.ProductAccounts.Commands.CreateProductAccount;
using IngenIA365ERP.Application.Inventory.ProductAccounts.Commands.UpdateProductAccount;
using IngenIA365ERP.Application.Inventory.ProductAccounts.Commands.DeleteProductAccount;
using IngenIA365ERP.Application.Inventory.ProductAccounts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class ProductAccountsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/product-accounts")
            .WithTags("ProductAccounts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListProductAccountsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListProductAccounts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetProductAccountByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetProductAccountById");

        group.MapPost("/", async (CreateProductAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/product-accounts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateProductAccount");

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductAccountCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateProductAccount");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteProductAccountCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteProductAccount");
    }
}
