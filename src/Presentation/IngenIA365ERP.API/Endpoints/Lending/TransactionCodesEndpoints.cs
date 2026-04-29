using Carter;
using IngenIA365ERP.Application.Lending.TransactionCodes.Commands.CreateTransactionCode;
using IngenIA365ERP.Application.Lending.TransactionCodes.Commands.UpdateTransactionCode;
using IngenIA365ERP.Application.Lending.TransactionCodes.Commands.DeleteTransactionCode;
using IngenIA365ERP.Application.Lending.TransactionCodes.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class TransactionCodesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/transaction-codes")
            .WithTags("TransactionCodes")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListTransactionCodesQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListTransactionCodes");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetTransactionCodeByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetTransactionCodeById");

        group.MapPost("/", async (CreateTransactionCodeCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/transaction-codes/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateTransactionCode");

        group.MapPut("/{id:guid}", async (Guid id, UpdateTransactionCodeCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateTransactionCode");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteTransactionCodeCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteTransactionCode");
    }
}
