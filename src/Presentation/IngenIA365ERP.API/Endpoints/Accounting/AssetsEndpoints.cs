using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Accounting.Assets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints.Accounting;

/// <summary>
/// Activos fijos y diferidos (feature 009 E4, US12): el inventario, la ficha con cuotas y proyección, alta, corrección, baja, y la
/// corrida mensual de depreciación y amortización con su vista previa y su reversión. <c>Accounting.Assets.View</c> para leer,
/// <c>.Manage</c> para registrar, corregir y dar de baja, <c>.Run</c> para correr y reversar. El informe del inventario es la vista
/// <c>assets</c> de <c>/api/reports/accounting</c>.
/// </summary>
public class AssetsEndpoints : ICarterModule
{
    /// <summary>Cuerpo de <c>PUT /{id}</c>: el activo y, si cambia vida útil o residual, el motivo.</summary>
    public sealed record CorregirActivoRequest(FixedAssetInput Asset, string? Reason);

    /// <summary>Cuerpo de <c>POST /{id}/retire</c>.</summary>
    public sealed record DarDeBajaRequest(DateOnly Date, Guid CounterAccountPublicId, string Reason);

    /// <summary>Cuerpo de <c>POST /runs</c>.</summary>
    public sealed record CorrerMesRequest(int Year, int Month);

    /// <summary>Cuerpo de <c>POST /runs/{id}/reverse</c>.</summary>
    public sealed record ReversarCorridaRequest(string Reason);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting/assets")
            .WithTags("AccountingAssets")
            .RequireAuthorization();

        group.MapGet("/", async ([FromQuery] string? kind, [FromQuery] string? status, [FromQuery] string? search, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListFixedAssetsQuery(kind, status, search), ct))
            .WithName("Accounting_Assets_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.View");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetFixedAssetQuery(id), ct))
            .WithName("Accounting_Assets_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.View");

        group.MapPost("/", async (FixedAssetInput body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RegisterFixedAssetCommand(body), ct);
                return result.IsSuccess ? (object)Results.Created($"/api/accounting/assets/{result.Value}", new { publicId = result.Value }) : result;
            })
            .WithName("Accounting_Assets_Register")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.Manage");

        group.MapPut("/{id:guid}", async (Guid id, CorregirActivoRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateFixedAssetCommand(id, body.Asset, body.Reason), ct))
            .WithName("Accounting_Assets_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.Manage");

        group.MapPost("/{id:guid}/retire", async (Guid id, DarDeBajaRequest body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new RetireFixedAssetCommand(id, body.Date, body.CounterAccountPublicId, body.Reason ?? string.Empty), ct);
                return result.IsSuccess ? (object)Results.Ok(new { documentPublicId = result.Value == Guid.Empty ? (Guid?)null : result.Value }) : result;
            })
            .WithName("Accounting_Assets_Retire")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.Manage");

        group.MapGet("/runs", async ([FromQuery] int? year, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListAssetRunsQuery(year), ct))
            .WithName("Accounting_Assets_Runs")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.View");

        group.MapGet("/runs/preview", async ([FromQuery] int year, [FromQuery] int month, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewAssetRunQuery(year, month), ct))
            .WithName("Accounting_Assets_RunPreview")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.View");

        group.MapPost("/runs", async (CorrerMesRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new RunAssetDepreciationCommand(body.Year, body.Month), ct))
            .WithName("Accounting_Assets_Run")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.Run");

        group.MapPost("/runs/{id:guid}/reverse", async (Guid id, ReversarCorridaRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseAssetRunCommand(id, body.Reason ?? string.Empty), ct))
            .WithName("Accounting_Assets_RunReverse")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Accounting.Assets.Run");
    }
}
