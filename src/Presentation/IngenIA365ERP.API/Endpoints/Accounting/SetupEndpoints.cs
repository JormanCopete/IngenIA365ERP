using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.Setup;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// Configuración contable y catálogos (feature 009, contracts/api.md §2). Cada tramo lleva su
/// permiso (<c>Accounting.Setup.View</c> o <c>.Manage</c>) y la envolvente de errores; sin
/// permiso la puerta responde 404, como en todo el sistema.
/// </summary>
public class SetupEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/setup")
            .WithTags("AccountingSetup")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new GetAccountingSetupQuery(), ct))
            .WithName("Accounting_Setup_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.View");

        group.MapPost("/initialize", async (InitializeAccountingCommand command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                return result.IsSuccess ? (object)Results.Created("/api/accounting/setup", result.Value) : result;
            })
            .WithName("Accounting_Setup_Initialize")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.Manage");

        group.MapPut("/", async (UpdateAccountingSetupCommand command, ISender sender, CancellationToken ct) => await sender.Send(command, ct))
            .WithName("Accounting_Setup_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.Manage");

        group.MapGet("/catalogs", async (ISender sender, CancellationToken ct) => await sender.Send(new ListAccountCatalogsQuery(), ct))
            .WithName("Accounting_Catalogs_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.View");

        group.MapGet("/catalogs/updates", async (ISender sender, CancellationToken ct) => await sender.Send(new ListCatalogUpdatesQuery(), ct))
            .WithName("Accounting_Catalogs_Updates")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.View");

        group.MapPost("/catalogs/updates/adopt", async (AdoptCatalogUpdatesCommand command, ISender sender, CancellationToken ct) => await sender.Send(command, ct))
            .WithName("Accounting_Catalogs_AdoptUpdates")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.Manage");

        group.MapGet("/catalogs/{code}/entries", async (string code, [FromQuery] byte? level, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetCatalogEntriesQuery(code, level), ct))
            .WithName("Accounting_Catalogs_Entries")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.View");

        group.MapPost("/catalogs/import", ImportarAsync)
            .WithName("Accounting_Catalogs_Import")
            .DisableAntiforgery()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.Manage");

        group.MapPost("/catalogs/{code}/validate", async (string code, ISender sender, CancellationToken ct) => await sender.Send(new ValidateCatalogCommand(code), ct))
            .WithName("Accounting_Catalogs_Validate")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.Manage");

        group.MapDelete("/catalogs/{code}", async (string code, ISender sender, CancellationToken ct) => await sender.Send(new RemoveCatalogCommand(code), ct))
            .WithName("Accounting_Catalogs_Remove")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Setup.Manage");
    }

    private static async Task<object?> ImportarAsync(
        [FromForm] IFormFile archivo,
        [FromForm] string name,
        ISender sender,
        CancellationToken ct)
    {
        using var ms = new MemoryStream();
        if (archivo is not null) await archivo.CopyToAsync(ms, ct);
        var result = await sender.Send(new ImportAccountCatalogCommand(name ?? string.Empty, archivo?.FileName ?? string.Empty, ms.ToArray()), ct);
        return result.IsSuccess ? Results.Created($"/api/accounting/setup/catalogs/{result.Value.Code}/entries", result.Value) : result;
    }
}
