using Carter;
using IngenIA365ERP.Application.Inventory.DiscountTypes.Commands.CreateDiscountType;
using IngenIA365ERP.Application.Inventory.DiscountTypes.Commands.UpdateDiscountType;
using IngenIA365ERP.Application.Inventory.DiscountTypes.Commands.DeleteDiscountType;
using IngenIA365ERP.Application.Inventory.DiscountTypes.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class DiscountTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/discount-types")
            .WithTags("DiscountTypes")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDiscountTypesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDiscountTypes");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDiscountTypeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDiscountTypeById");

        group.MapPost("/", async (CreateDiscountTypeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/discount-types/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateDiscountType");

        group.MapPut("/{id:guid}", async (Guid id, UpdateDiscountTypeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateDiscountType");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteDiscountTypeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteDiscountType");
    }
}
