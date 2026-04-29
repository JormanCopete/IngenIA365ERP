using Carter;
using IngenIA365ERP.Application.Debit.PosTerminals.Commands.CreatePosTerminal;
using IngenIA365ERP.Application.Debit.PosTerminals.Commands.UpdatePosTerminal;
using IngenIA365ERP.Application.Debit.PosTerminals.Commands.DeletePosTerminal;
using IngenIA365ERP.Application.Debit.PosTerminals.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Debit;

public class PosTerminalsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/debit/pos-terminals")
            .WithTags("PosTerminals")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPosTerminalsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPosTerminals");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPosTerminalByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPosTerminalById");

        group.MapPost("/", async (CreatePosTerminalCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/debit/pos-terminals/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePosTerminal");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePosTerminalCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePosTerminal");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePosTerminalCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePosTerminal");
    }
}
