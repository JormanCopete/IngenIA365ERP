using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Transfers;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Traslados en dos pasos (feature 012, US10, T378; contracts/api.md §11): la lista con su estado derivado y el detalle con lo
/// despachado, recibido, faltante, sobrante y pendiente (<c>Transfers.View</c>); el borrador del despacho por el ciclo común
/// —crear, reemplazar, descartar— (<c>Transfers.Create</c>); despachar (<c>Transfers.Dispatch</c>); recibir y resolver una diferencia
/// (<c>Transfers.Receive</c>); las diferencias (<c>Transfers.View</c>); anular un despacho no recibido (<c>Transfers.Void</c>); y los
/// destinos permitidos (<c>Transfers.Create</c>, también por <c>GET /warehouses?purpose=TransferDestination</c>). Toda escritura
/// exige <c>Idempotency-Key</c> (<c>LosComandosDeInventarioLlevanClave</c>); sin permiso o fuera del alcance, el mismo 404. (nuevo)
/// </summary>
public class TransfersEndpoints : ICarterModule
{
    public const string Ruta = "/api/inventory/transfers";
    private const string Ver = "Inventory.Transfers.View";
    private const string Crear = "Inventory.Transfers.Create";
    private const string Despachar = "Inventory.Transfers.Dispatch";
    private const string Recibir = "Inventory.Transfers.Receive";
    private const string Anular = "Inventory.Transfers.Void";
    private const DocumentClassGroup Grupo = DocumentClassGroup.Transfers;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup(Ruta).WithTags("Inventory Transfers").RequireAuthorization();

        g.MapGet("/", async (string? state, Guid? originWarehousePublicId, Guid? destinationWarehousePublicId, DateOnly? from, DateOnly? to,
                    int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTransfersQuery(state, originWarehousePublicId, destinationWarehousePublicId, from, to,
                    new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_Transfers_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/discrepancies", async (string? status, Guid? warehousePublicId, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTransferDiscrepanciesQuery(status, warehousePublicId, new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_Transfers_Discrepancies_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/destinations", async (ISender sender, CancellationToken ct) => await sender.Send(new ListTransferDestinationsQuery(), ct))
            .WithName("Inventory_Transfers_Destinations").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Crear);

        g.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetTransferQuery(id), ct))
            .WithName("Inventory_Transfers_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (SaveInventoryDraftRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SaveInventoryDraftCommand(null, Grupo, body) { OperationKey = http.ClaveDeOperacion() }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{result.Value.PublicId}", result.Value) : result;
            })
            .WithName("Inventory_Transfers_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Crear);

        g.MapPut("/{id:guid}", async (Guid id, SaveInventoryDraftRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SaveInventoryDraftCommand(id, Grupo, body) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Transfers_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Crear);

        g.MapPost("/{id:guid}/discard", async (Guid id, CicloDeDocumentoRutas.MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DiscardInventoryDraftCommand(id, Grupo, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Transfers_Discard").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Crear);

        g.MapPost("/{id:guid}/dispatch", async (Guid id, CicloDeDocumentoRutas.ConfirmarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DispatchTransferCommand(id) { RowVersion = body?.RowVersion, OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Transfers_Dispatch").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Despachar);

        g.MapPost("/{id:guid}/receive", async (Guid id, RecibirTrasladoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ReceiveTransferCommand(id, body.Lines ?? [], body.OperationDate, body.DocumentTypePublicId, body.Notes)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{id}", result.Value) : result;
            })
            .WithName("Inventory_Transfers_Receive").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Recibir);

        g.MapPost("/discrepancies/{id:guid}/resolve", async (Guid id, ResolverDiferenciaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new ResolveTransferDiscrepancyCommand(id, body.Resolution, body.Reason ?? string.Empty, body.Quantity,
                    body.AdjustmentCausePublicId, body.OperationDate, body.ToLocationPublicId)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/discrepancies/{id}", result.Value) : result;
            })
            .WithName("Inventory_Transfers_Discrepancies_Resolve").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Recibir);

        g.MapPost("/{id:guid}/void", async (Guid id, CicloDeDocumentoRutas.AnularRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new VoidInventoryDocumentCommand(id, Grupo, body.Reason ?? string.Empty, body.OperationDate)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"{Ruta}/{id}", result.Value) : result;
            })
            .WithName("Inventory_Transfers_Void").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Anular);
    }

    /// <summary>El cuerpo de <c>POST /{id}/receive</c> (§11).</summary>
    public sealed record RecibirTrasladoRequest(
        DateOnly? OperationDate,
        Guid? DocumentTypePublicId,
        IReadOnlyList<LineaRecibidaRequest>? Lines,
        string? Notes);

    /// <summary>El cuerpo de <c>POST /discrepancies/{id}/resolve</c> (§11).</summary>
    public sealed record ResolverDiferenciaRequest(
        TransferDiscrepancyResolution Resolution,
        decimal? Quantity,
        Guid? AdjustmentCausePublicId,
        DateOnly? OperationDate,
        Guid? ToLocationPublicId,
        string? Reason);
}
