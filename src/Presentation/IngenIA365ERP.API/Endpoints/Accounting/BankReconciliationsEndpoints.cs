using Carter;
using IngenIA365ERP.Application.Accounting.BankReconciliations.Commands.CreateBankReconciliation;
using IngenIA365ERP.Application.Accounting.BankReconciliations.Commands.ReconcileItem;
using IngenIA365ERP.Application.Accounting.BankReconciliations.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class BankReconciliationsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/bank-reconciliations")
            .WithTags("BankReconciliations")
            .RequireAuthorization();

        group.MapGet("/{accountId:guid}/{year:int}/{month:int}",
            async (Guid accountId, int year, int month, ISender sender) =>
        {
            var result = await sender.Send(new GetBankReconciliationQuery(accountId, year, month));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetBankReconciliation");

        group.MapPost("/", async (CreateBankReconciliationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/bank-reconciliations/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateBankReconciliation");

        group.MapPut("/items/{id:guid}/reconcile",
            async (Guid id, ReconcileItemRequest request, ISender sender) =>
        {
            var command = new ReconcileItemCommand(id, request.IsReconciled);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Error);
        }).WithName("ReconcileItem");
    }
}

public record ReconcileItemRequest(bool IsReconciled);
