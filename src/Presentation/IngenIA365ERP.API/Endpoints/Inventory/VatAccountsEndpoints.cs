using Carter;
using IngenIA365ERP.Application.Inventory.VatAccounts.Commands.CreateVatAccount;
using IngenIA365ERP.Application.Inventory.VatAccounts.Commands.UpdateVatAccount;
using IngenIA365ERP.Application.Inventory.VatAccounts.Commands.DeleteVatAccount;
using IngenIA365ERP.Application.Inventory.VatAccounts.Queries;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

public class VatAccountsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/vat-accounts")
            .WithTags("VatAccounts")
            .RequireAuthorization();

        group.MapGet("/", async ([AsParameters] ListVatAccountsQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        }).WithName("ListVatAccounts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetVatAccountByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        }).WithName("GetVatAccountById");

        group.MapPost("/", async (CreateVatAccountCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/inventory/vat-accounts/{result.Value}", result.Value)
                : Results.BadRequest(result.Error);
        }).WithName("CreateVatAccount");

        group.MapPut("/{id:guid}", async (Guid id, UpdateVatAccountCommand command, ISender sender) =>
        {
            if (command.PublicId != id) command = command with { PublicId = id };
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("UpdateVatAccount");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteVatAccountCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        }).WithName("DeleteVatAccount");
    }
}
