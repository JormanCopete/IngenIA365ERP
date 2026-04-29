using Carter;
using IngenIA365ERP.Application.Accounting.AccountSubgroups.Commands.CreateAccountSubgroup;
using IngenIA365ERP.Application.Accounting.AccountSubgroups.Commands.UpdateAccountSubgroup;
using IngenIA365ERP.Application.Accounting.AccountSubgroups.Commands.DeleteAccountSubgroup;
using IngenIA365ERP.Application.Accounting.AccountSubgroups.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Accounting;

public class AccountSubgroupsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/account-subgroups")
            .WithTags("AccountSubgroups")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListAccountSubgroupsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListAccountSubgroups");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountSubgroupByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetAccountSubgroupById");

        group.MapPost("/", async (CreateAccountSubgroupCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/accounting/account-subgroups/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateAccountSubgroup");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAccountSubgroupCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateAccountSubgroup");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteAccountSubgroupCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteAccountSubgroup");
    }
}
