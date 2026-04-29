using Carter;
using IngenIA365ERP.Application.Accounting.AccountGroups.Commands.CreateAccountGroup;
using IngenIA365ERP.Application.Accounting.AccountGroups.Commands.UpdateAccountGroup;
using IngenIA365ERP.Application.Accounting.AccountGroups.Commands.DeleteAccountGroup;
using IngenIA365ERP.Application.Accounting.AccountGroups.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class AccountGroupsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/account-groups")
            .WithTags("AccountGroups")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListAccountGroupsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListAccountGroups");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountGroupByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetAccountGroupById");

        group.MapPost("/", async (CreateAccountGroupCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/account-groups/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateAccountGroup");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAccountGroupCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateAccountGroup");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteAccountGroupCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteAccountGroup");
    }
}
