using Carter;
using IngenIA365ERP.Application.Lending.SavingsAccounts.Commands.CloseSavingsAccount;
using IngenIA365ERP.Application.Lending.SavingsAccounts.Commands.OpenSavingsAccount;
using IngenIA365ERP.Application.Lending.SavingsAccounts.Commands.ProcessDeposit;
using IngenIA365ERP.Application.Lending.SavingsAccounts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class SavingsAccountsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/savings-accounts")
            .WithTags("SavingsAccounts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListSavingsAccountsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListSavingsAccounts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetSavingsAccountByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSavingsAccountById");

        group.MapGet("/{id:guid}/statement", async (Guid id, DateOnly? dateFrom, DateOnly? dateTo, ISender sender) =>
        {
            var query = new GetSavingsStatementQuery
            {
                AccountPublicId = id,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetSavingsStatement");

        group.MapPost("/", async (OpenSavingsAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/savings-accounts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("OpenSavingsAccount");

        group.MapPost("/{id:guid}/deposit", async (Guid id, DepositRequest request, ISender sender) =>
        {
            var command = new ProcessDepositCommand
            {
                SavingsAccountPublicId = id,
                Amount = request.Amount,
                TransactionType = "D",
                Reference = request.Reference
            };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ProcessSavingsDeposit");

        group.MapPost("/{id:guid}/withdraw", async (Guid id, WithdrawRequest request, ISender sender) =>
        {
            var command = new ProcessDepositCommand
            {
                SavingsAccountPublicId = id,
                Amount = request.Amount,
                TransactionType = "R",
                Reference = request.Reference
            };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ProcessSavingsWithdrawal");

        group.MapPost("/{id:guid}/close", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new CloseSavingsAccountCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("CloseSavingsAccount");
    }
}

// Request DTOs
public record DepositRequest(decimal Amount, string? Reference);
public record WithdrawRequest(decimal Amount, string? Reference);
