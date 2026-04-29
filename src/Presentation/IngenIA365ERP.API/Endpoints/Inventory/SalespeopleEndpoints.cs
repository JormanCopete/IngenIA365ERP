using Carter;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.CreateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.UpdateSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Commands.DeleteSalesperson;
using IngenIA365ERP.Application.Inventory.Salespeople.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class SalespeopleEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/salespeople")
            .WithTags("Salespeople")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSalespeopleQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSalespeople");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSalespersonByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSalespersonById");

        group.MapPost("/", async (CreateSalespersonCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/salespeople/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSalesperson");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSalespersonCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSalesperson");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSalespersonCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSalesperson");
    }
}
