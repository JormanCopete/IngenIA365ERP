using Carter;
using IngenIA365ERP.Application.Accounting.ChartOfAccounts.Commands.CreateChartOfAccount;
using IngenIA365ERP.Application.Accounting.ChartOfAccounts.Commands.UpdateChartOfAccount;
using IngenIA365ERP.Application.Accounting.ChartOfAccounts.Commands.DeleteChartOfAccount;
using IngenIA365ERP.Application.Accounting.ChartOfAccounts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class ChartOfAccountsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/chart-of-accounts")
            .WithTags("ChartOfAccounts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListChartOfAccountsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListChartOfAccounts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetChartOfAccountByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetChartOfAccountById");

        group.MapPost("/", async (CreateChartOfAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/chart-of-accounts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateChartOfAccount");

        group.MapPut("/{id:guid}", async (Guid id, UpdateChartOfAccountCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateChartOfAccount");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteChartOfAccountCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteChartOfAccount");
    }
}
