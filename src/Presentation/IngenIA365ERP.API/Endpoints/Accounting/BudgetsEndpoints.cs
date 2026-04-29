using Carter;
using IngenIA365ERP.Application.Accounting.Budgets.Commands.CreateBudget;
using IngenIA365ERP.Application.Accounting.Budgets.Commands.DeleteBudget;
using IngenIA365ERP.Application.Accounting.Budgets.Commands.UpdateBudget;
using IngenIA365ERP.Application.Accounting.Budgets.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class BudgetsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/budgets")
            .WithTags("Budgets")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListBudgetsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListBudgets");

        group.MapGet("/execution", async ([AsParameters] GetBudgetExecutionQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("GetBudgetExecution");

        group.MapPost("/", async (CreateBudgetCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/budgets/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateBudget");

        group.MapPut("/{id:guid}", async (Guid id, UpdateBudgetCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateBudget");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteBudgetCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteBudget");
    }
}
