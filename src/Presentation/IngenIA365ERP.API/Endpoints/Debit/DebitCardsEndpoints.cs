using Carter;
using IngenIA365ERP.Application.Debit.Cards.Commands.BlockCard;
using IngenIA365ERP.Application.Debit.Cards.Commands.IssueDebitCard;
using IngenIA365ERP.Application.Debit.Cards.Commands.ProcessDebitTransaction;
using IngenIA365ERP.Application.Debit.Cards.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Debit;

public class DebitCardsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/debit/cards")
            .WithTags("DebitCards")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListDebitCardsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListDebitCards");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetDebitCardByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDebitCardById");

        group.MapGet("/{id:guid}/transactions", async (Guid id, int? pageNumber, int? pageSize,
            DateOnly? dateFrom, DateOnly? dateTo, ISender sender) =>
        {
            var query = new GetDebitCardTransactionsQuery
            {
                CardPublicId = id,
                PageNumber = pageNumber ?? 1,
                PageSize = pageSize ?? 20,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetDebitCardTransactions");

        group.MapPost("/", async (IssueDebitCardCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/debit/cards/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("IssueDebitCard");

        group.MapPost("/{id:guid}/transaction", async (Guid id, TransactionRequest request, ISender sender) =>
        {
            var command = new ProcessDebitTransactionCommand
            {
                CardPublicId = id,
                Amount = request.Amount,
                MerchantName = request.MerchantName,
                TransactionType = request.TransactionType,
                PosTerminalPublicId = request.PosTerminalPublicId
            };
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/debit/cards/{id}/transactions", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("ProcessDebitTransaction");

        group.MapPost("/{id:guid}/block", async (Guid id, BlockRequest? request, ISender sender) =>
        {
            var command = new BlockCardCommand
            {
                CardPublicId = id,
                BlockType = request?.BlockType ?? "T",
                Reason = request?.Reason
            };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("BlockDebitCard");
    }
}

// Request DTOs
public record TransactionRequest(decimal Amount, string? MerchantName, string TransactionType, Guid? PosTerminalPublicId);
public record BlockRequest(string BlockType, string? Reason);
