using Carter;
using IngenIA365ERP.Application.Core.WithdrawalReasons.Commands.CreateWithdrawalReason;
using IngenIA365ERP.Application.Core.WithdrawalReasons.Commands.UpdateWithdrawalReason;
using IngenIA365ERP.Application.Core.WithdrawalReasons.Commands.DeleteWithdrawalReason;
using IngenIA365ERP.Application.Core.WithdrawalReasons.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Core;

public class WithdrawalReasonsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/withdrawal-reasons")
            .WithTags("WithdrawalReasons")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWithdrawalReasonsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWithdrawalReasons");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWithdrawalReasonByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWithdrawalReasonById");

        group.MapPost("/", async (CreateWithdrawalReasonCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/core/withdrawal-reasons/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWithdrawalReason");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWithdrawalReasonCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateWithdrawalReason");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWithdrawalReasonCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWithdrawalReason");
    }
}
