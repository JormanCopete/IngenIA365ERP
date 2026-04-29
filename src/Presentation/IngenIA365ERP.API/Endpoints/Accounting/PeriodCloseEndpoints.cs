using Carter;
using IngenIA365ERP.Application.Accounting.PeriodClose.Commands.CloseAccountingPeriod;
using IngenIA365ERP.Application.Accounting.PeriodClose.Commands.ReopenAccountingPeriod;
using IngenIA365ERP.Application.Accounting.PeriodClose.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class PeriodCloseEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/periods")
            .WithTags("AccountingPeriodClose")
            .RequireAuthorization();

        group.MapPost("/{year:int}/{month:int}/close", async (int year, int month, ISender sender) =>
        {
            var result = await sender.Send(new CloseAccountingPeriodCommand(year, month));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("CloseAccountingPeriod");

        group.MapPost("/{year:int}/{month:int}/reopen", async (int year, int month, ReopenRequest? request, ISender sender) =>
        {
            var command = new ReopenAccountingPeriodCommand(year, month, request?.Reason ?? "");
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("ReopenAccountingPeriod");

        group.MapGet("/{year:int}/{month:int}/close-preview", async (int year, int month, ISender sender) =>
        {
            var result = await sender.Send(new GetPeriodClosePreviewQuery(year, month));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetPeriodClosePreview");

        var trialBalance = app.MapGroup("/api/accounting/trial-balance")
            .WithTags("AccountingPeriodClose")
            .RequireAuthorization();

        trialBalance.MapGet("/{year:int}/{month:int}", async (int year, int month, ISender sender) =>
        {
            var result = await sender.Send(new GetTrialBalanceQuery(year, month));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetTrialBalance");
    }
}

public record ReopenRequest(string Reason);
