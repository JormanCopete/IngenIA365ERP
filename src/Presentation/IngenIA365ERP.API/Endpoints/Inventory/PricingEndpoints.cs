using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Inventory.Pricing;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Listas de precios, resolución del precio y topes de descuento (feature 012; contracts/api.md §19.1–§19.3; T629):
/// <c>/api/inventory/price-lists</c> (lista, detalle con sus precios paginados, alta, edición, <c>{id}/items</c> y la plantilla 12),
/// <c>GET /api/inventory/prices/resolve</c> y <c>/api/inventory/discount-caps</c> (lista, <c>mine</c>, alta y la plantilla 13). Las
/// dos plantillas se descargan vacías desde I1 (T238) e importan desde I3. Consulta con <c>Inventory.Prices.View</c>; listas con
/// <c>Inventory.Prices.Manage</c> y topes con <c>Inventory.DiscountCaps.Manage</c>, siempre con <c>Idempotency-Key</c>. Los precios y
/// los topes son catálogos de la cooperativa: no filtran por bodega ni punto. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PricingEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Inventory.Prices.View";
    public const string PermisoDeListas = "Inventory.Prices.Manage";
    public const string PermisoDeTopes = "Inventory.DiscountCaps.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        Listas(app);
        Resolucion(app);
        Topes(app);
    }

    private static void Listas(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/price-lists")
            .WithTags("Inventory Price Lists")
            .RequireAuthorization();

        group.MapGet("/", async (DateOnly? asOf, string? scope, bool? active, string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPriceListsQuery(asOf, scope, active, search, new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_PriceLists_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPriceListQuery(id, search, new PageRequest(page ?? 1, pageSize ?? 50)), ct))
            .WithName("Inventory_PriceLists_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (ListaDePreciosRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreatePriceListCommand(
                    body.Code ?? string.Empty, body.Name ?? string.Empty, body.IncludesTaxes ?? true, body.Scope,
                    body.ValidFrom ?? DateOnly.MinValue, body.ValidTo, body.Reason ?? string.Empty, body.Notes)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/price-lists/{result.Value}", new { priceListPublicId = result.Value })
                    : result;
            })
            .WithName("Inventory_PriceLists_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeListas);

        // El ámbito, el código e includesTaxes no cambian: si vienen distintos, el handler responde Inventory.PriceList.ScopeLocked.
        group.MapPut("/{id:guid}", async (Guid id, ListaDePreciosRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdatePriceListCommand(
                    id, body.Name ?? string.Empty, body.ValidTo, body.IsActive ?? true, body.Reason ?? string.Empty, body.Notes,
                    body.Code, body.IncludesTaxes, body.Scope)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_PriceLists_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeListas);

        group.MapPut("/{id:guid}/items", async (Guid id, PreciosDeListaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetPriceListItemsCommand(id, body.Items ?? [], body.Reason ?? string.Empty, body.RemoveProductPublicIds)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_PriceLists_SetItems")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeListas);

        // Plantilla 12 (contracts/plantillas.md §12).
        group.MapPlantilla(PermisoDeConsulta, PermisoDeListas, CatalogoDePlantillas.ListasDePreciosClave, "Inventory_PriceLists",
            importar: (modo, archivo, motivo, clave) => new ImportPriceListsCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
    }

    private static void Resolucion(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/prices")
            .WithTags("Inventory Prices")
            .RequireAuthorization();

        group.MapGet("/resolve", async (Guid product, Guid? unit, Guid? person, Guid? salesChannel, Guid? branch, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ResolvePriceQuery(product, unit, person, salesChannel, branch, date), ct))
            .WithName("Inventory_Prices_Resolve")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);
    }

    private static void Topes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/discount-caps")
            .WithTags("Inventory Discount Caps")
            .RequireAuthorization();

        group.MapGet("/", async (Guid? role, DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListDiscountCapsQuery(role, asOf), ct))
            .WithName("Inventory_DiscountCaps_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/mine", async (DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetMyDiscountCapQuery(date), ct))
            .WithName("Inventory_DiscountCaps_Mine")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (TopeRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateDiscountCapCommand(
                    body.RolePublicId ?? Guid.Empty, body.MaxLinePercent, body.MaxDocumentPercent, body.ValidFrom ?? DateOnly.MinValue, body.ValidTo,
                    body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/discount-caps/{result.Value}", new { discountCapPublicId = result.Value })
                    : result;
            })
            .WithName("Inventory_DiscountCaps_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeTopes);

        // Plantilla 13 (contracts/plantillas.md §13): se descarga con Prices.View y se importa con DiscountCaps.Manage.
        group.MapPlantilla(PermisoDeConsulta, PermisoDeTopes, CatalogoDePlantillas.TopesDeDescuentoClave, "Inventory_DiscountCaps",
            importar: (modo, archivo, motivo, clave) => new ImportDiscountCapsCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
    }

    /// <summary>El cuerpo de una lista (§19.1). La edición usa nombre, vigencia hasta, activa, notas y motivo.</summary>
    public sealed record ListaDePreciosRequest(
        string? Code, string? Name, bool? IncludesTaxes, PriceListScopeInput? Scope, DateOnly? ValidFrom, DateOnly? ValidTo, bool? IsActive,
        string? Reason, string? Notes);

    /// <summary>Los precios de una lista por (producto, unidad) y los productos que salen (§19.1).</summary>
    public sealed record PreciosDeListaRequest(IReadOnlyList<PriceListItemInput>? Items, IReadOnlyList<Guid>? RemoveProductPublicIds, string? Reason);

    /// <summary>El tope de un rol (§19.3); los porcentajes son fracción.</summary>
    public sealed record TopeRequest(Guid? RolePublicId, decimal MaxLinePercent, decimal MaxDocumentPercent, DateOnly? ValidFrom, DateOnly? ValidTo, string? Reason);
}
