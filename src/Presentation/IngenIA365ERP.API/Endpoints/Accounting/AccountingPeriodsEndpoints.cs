using Carter;
using IngenIA365ERP.Application.Accounting.AccountingPeriods.Commands.CreateAccountingPeriod;
using IngenIA365ERP.Application.Accounting.AccountingPeriods.Commands.UpdateAccountingPeriod;
using IngenIA365ERP.Application.Accounting.AccountingPeriods.Commands.DeleteAccountingPeriod;
using IngenIA365ERP.Application.Accounting.AccountingPeriods.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class AccountingPeriodsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/accounting-periods")
            .WithTags("AccountingPeriods")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListAccountingPeriodsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListAccountingPeriods");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountingPeriodByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetAccountingPeriodById");

        group.MapPost("/", async (CreateAccountingPeriodCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/accounting-periods/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateAccountingPeriod");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAccountingPeriodCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateAccountingPeriod");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteAccountingPeriodCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteAccountingPeriod");
    }
}
