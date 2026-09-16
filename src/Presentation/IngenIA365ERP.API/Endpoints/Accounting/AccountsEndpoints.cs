using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.Accounts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>Plan de cuentas (feature 009, contracts/api.md §3): árbol, buscador, ficha, historial, auxiliares y parametrizaciones inválidas.</summary>
public class AccountsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/accounts")
            .WithTags("AccountingAccounts")
            .RequireAuthorization();

        group.MapGet("/tree", async ([FromQuery] Guid? parent, [FromQuery] bool onlyActive, [FromQuery] bool all, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetAccountTreeQuery(parent, onlyActive, all), ct))
            .WithName("Accounting_Accounts_Tree")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.View");

        group.MapGet("/search", async ([FromQuery] string q, [FromQuery] string? module, [FromQuery] bool? onlyMovement, ISender sender, CancellationToken ct) =>
                await sender.Send(new SearchAccountsQuery(q ?? string.Empty, module, onlyMovement ?? true), ct))
            .WithName("Accounting_Accounts_Search")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.View");

        group.MapGet("/invalid-parameterizations", async (ISender sender, CancellationToken ct) => await sender.Send(new ListInvalidParameterizationsQuery(), ct))
            .WithName("Accounting_Accounts_InvalidParameterizations")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetAccountByPublicIdQuery(id), ct))
            .WithName("Accounting_Accounts_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.View");

        group.MapGet("/{id:guid}/history", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetAccountHistoryQuery(id), ct))
            .WithName("Accounting_Accounts_History")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.View");

        group.MapPost("/", async (CreateAccountCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/accounting/accounts/{result.Value}", new { PublicId = result.Value }) : result;
            })
            .WithName("Accounting_Accounts_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.Manage");

        group.MapPut("/{id:guid}", async (Guid id, UpdateAccountCommand command, ISender sender, CancellationToken ct) =>
                await sender.Send(command with { PublicId = id }, ct))
            .WithName("Accounting_Accounts_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.Manage");

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new SetAccountActiveCommand(id, false), ct))
            .WithName("Accounting_Accounts_Deactivate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.Manage");

        group.MapPost("/{id:guid}/activate", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new SetAccountActiveCommand(id, true), ct))
            .WithName("Accounting_Accounts_Activate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.Manage");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new DeleteAccountCommand(id), ct))
            .WithName("Accounting_Accounts_Delete")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Accounts.Manage");
    }
}
