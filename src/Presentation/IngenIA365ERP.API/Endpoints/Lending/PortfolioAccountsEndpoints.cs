using Carter;
using IngenIA365ERP.Application.Lending.PortfolioAccounts.Commands.CreatePortfolioAccount;
using IngenIA365ERP.Application.Lending.PortfolioAccounts.Commands.UpdatePortfolioAccount;
using IngenIA365ERP.Application.Lending.PortfolioAccounts.Commands.DeletePortfolioAccount;
using IngenIA365ERP.Application.Lending.PortfolioAccounts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

public class PortfolioAccountsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lending/portfolio-accounts")
            .WithTags("PortfolioAccounts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListPortfolioAccountsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListPortfolioAccounts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetPortfolioAccountByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetPortfolioAccountById");

        group.MapPost("/", async (CreatePortfolioAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/lending/portfolio-accounts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreatePortfolioAccount");

        group.MapPut("/{id:guid}", async (Guid id, UpdatePortfolioAccountCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdatePortfolioAccount");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeletePortfolioAccountCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeletePortfolioAccount");
    }
}
