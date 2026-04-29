using Carter;
using IngenIA365ERP.Application.Inventory.PriceListTypes.Commands.CreatePriceListType;
using IngenIA365ERP.Application.Inventory.PriceListTypes.Commands.UpdatePriceListType;
using IngenIA365ERP.Application.Inventory.PriceListTypes.Commands.DeletePriceListType;
using IngenIA365ERP.Application.Inventory.PriceListTypes.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class PriceListTypesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/price-list-types")
            .WithTags("PriceListTypes")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPriceListTypesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPriceListTypes");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPriceListTypeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPriceListTypeById");

        group.MapPost("/", async (CreatePriceListTypeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/price-list-types/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePriceListType");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePriceListTypeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePriceListType");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePriceListTypeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePriceListType");
    }
}
