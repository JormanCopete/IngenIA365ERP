using Carter;
using IngenIA365ERP.Application.Core.Positions.Commands.CreatePosition;
using IngenIA365ERP.Application.Core.Positions.Commands.UpdatePosition;
using IngenIA365ERP.Application.Core.Positions.Commands.DeletePosition;
using IngenIA365ERP.Application.Core.Positions.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class PositionsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/positions")
            .WithTags("Positions")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPositionsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPositions");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPositionByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPositionById");

        group.MapPost("/", async (CreatePositionCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/positions/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePosition");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePositionCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePosition");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePositionCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePosition");
    }
}
