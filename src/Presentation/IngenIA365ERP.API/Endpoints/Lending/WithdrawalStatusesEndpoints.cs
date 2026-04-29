using Carter;
using IngenIA365ERP.Application.Lending.WithdrawalStatuses.Commands.CreateWithdrawalStatus;
using IngenIA365ERP.Application.Lending.WithdrawalStatuses.Commands.UpdateWithdrawalStatus;
using IngenIA365ERP.Application.Lending.WithdrawalStatuses.Commands.DeleteWithdrawalStatus;
using IngenIA365ERP.Application.Lending.WithdrawalStatuses.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class WithdrawalStatusesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/withdrawal-statuses")
            .WithTags("WithdrawalStatuses")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListWithdrawalStatusesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListWithdrawalStatuses");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetWithdrawalStatusByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetWithdrawalStatusById");

        group.MapPost("/", async (CreateWithdrawalStatusCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/withdrawal-statuses/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateWithdrawalStatus");

        group.MapPut("/{id:guid}", async (Guid id, UpdateWithdrawalStatusCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateWithdrawalStatus");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteWithdrawalStatusCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteWithdrawalStatus");
    }
}
