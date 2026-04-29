using Carter;
using IngenIA365ERP.Application.Inventory.SalesPoints.Commands.CreateSalesPoint;
using IngenIA365ERP.Application.Inventory.SalesPoints.Commands.UpdateSalesPoint;
using IngenIA365ERP.Application.Inventory.SalesPoints.Commands.DeleteSalesPoint;
using IngenIA365ERP.Application.Inventory.SalesPoints.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class SalesPointsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/sales-points")
            .WithTags("SalesPoints")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSalesPointsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSalesPoints");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSalesPointByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSalesPointById");

        group.MapPost("/", async (CreateSalesPointCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/sales-points/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateSalesPoint");

        group.MapPut("/{id:guid}", async (Guid id, UpdateSalesPointCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateSalesPoint");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteSalesPointCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteSalesPoint");
    }
}
