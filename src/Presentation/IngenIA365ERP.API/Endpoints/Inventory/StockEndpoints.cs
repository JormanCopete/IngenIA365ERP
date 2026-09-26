using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Kardex;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Existencias e integridad del kardex (feature 012, T261; contracts/api.md §5, §6.2). Leer existencias con
/// <c>Inventory.Stock.View</c> (los valores, además, con <c>Inventory.Costs.Read</c>: sin él salen nulos); verificar con
/// <c>Inventory.Integrity.Verify</c> —es consulta, sin <c>Idempotency-Key</c>— y reconstruir con
/// <c>Inventory.Integrity.Rebuild</c>, motivo e <c>Idempotency-Key</c>. Fuera del alcance, el mismo 404 que lo inexistente. El
/// kardex no tiene ruta aquí: es la vista <c>kardex</c> de <c>/api/reports/inventory</c> (§6.1). Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class StockEndpoints : ICarterModule
{
    public const string VerExistencias = "Inventory.Stock.View";
    public const string Verificar = "Inventory.Integrity.Verify";
    public const string Reconstruir = "Inventory.Integrity.Rebuild";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var stock = app.MapGroup("/api/inventory/stock").WithTags("Inventory Stock").RequireAuthorization();

        stock.MapGet("/", async (Guid? warehousePublicId, Guid? productPublicId, Guid? categoryPublicId, Guid? locationPublicId, string? search,
                    bool? onlyWithStock, bool? belowReorderPoint, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetStockQuery(warehousePublicId, productPublicId, categoryPublicId, locationPublicId, search,
                    onlyWithStock ?? false, belowReorderPoint ?? false, new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_Stock_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(VerExistencias);

        stock.MapGet("/{productId:guid}", async (Guid productId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetProductStockQuery(productId), ct))
            .WithName("Inventory_Stock_Product").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(VerExistencias);

        var integridad = app.MapGroup("/api/inventory/integrity").WithTags("Inventory Integrity").RequireAuthorization();

        integridad.MapPost("/verify", async (VerificarRequest? body, ISender sender, CancellationToken ct) =>
                await sender.Send(new VerifyInventoryIntegrityQuery(body?.ProductPublicIds, body?.WarehousePublicIds), ct))
            .WithName("Inventory_Integrity_Verify").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Verificar);

        integridad.MapPost("/rebuild", async (ReconstruirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new RebuildInventoryProjectionsCommand(body.ProductPublicIds, body.WarehousePublicIds, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Integrity_Rebuild").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Reconstruir);
    }

    /// <summary>El cuerpo de verificar (§6.2): vacío = todo lo del alcance.</summary>
    public sealed record VerificarRequest(IReadOnlyList<Guid>? ProductPublicIds, IReadOnlyList<Guid>? WarehousePublicIds);

    /// <summary>El cuerpo de reconstruir (§6.2), con motivo.</summary>
    public sealed record ReconstruirRequest(IReadOnlyList<Guid>? ProductPublicIds, IReadOnlyList<Guid>? WarehousePublicIds, string? Reason);
}
