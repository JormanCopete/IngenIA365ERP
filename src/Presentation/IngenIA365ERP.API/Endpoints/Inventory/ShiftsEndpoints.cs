using Carter;
using IngenIA365ERP.Application.Inventory.Shifts.Commands.CreateShift;
using IngenIA365ERP.Application.Inventory.Shifts.Commands.UpdateShift;
using IngenIA365ERP.Application.Inventory.Shifts.Commands.DeleteShift;
using IngenIA365ERP.Application.Inventory.Shifts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class ShiftsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/shifts")
            .WithTags("Shifts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListShiftsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListShifts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetShiftByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetShiftById");

        group.MapPost("/", async (CreateShiftCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/shifts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateShift");

        group.MapPut("/{id:guid}", async (Guid id, UpdateShiftCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateShift");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteShiftCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteShift");
    }
}
