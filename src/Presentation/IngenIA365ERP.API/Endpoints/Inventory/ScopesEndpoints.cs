using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Security.Scopes;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// El alcance comercial de un usuario (feature 012, T35, T090; contracts/api.md §16.3): bodegas y puntos de venta
/// asignados. Las dos rutas exigen <c>Inventory.Scopes.Manage</c>; el <c>PUT</c> lleva <c>Idempotency-Key</c>. Quien
/// administra sólo asigna lo de su propio alcance (lo demás, 404). Es la pestaña «Alcance comercial» de
/// <c>/admin/usuarios</c> (fase 11). Cada ruta sólo reenvía al <see cref="ISender"/>. Los permisos los siembra el catálogo
/// de Inventario (fase 3, T48); hasta entonces sólo entra el administrador maestro.
/// </summary>
public class ScopesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/scopes")
            .WithTags("Inventory Scopes")
            .RequireAuthorization();

        group.MapGet("/users/{userPublicId:guid}", async (Guid userPublicId, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetUserCommercialScopeQuery(userPublicId), ct))
            .WithName("Inventory_Scopes_GetUser")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Scopes.Manage");

        group.MapPut("/users/{userPublicId:guid}", async (Guid userPublicId, FijarAlcanceRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetUserCommercialScopeCommand(userPublicId, body.Warehouses ?? [], body.PointsOfSale)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Scopes_SetUser")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission("Inventory.Scopes.Manage");
    }

    /// <summary>El cuerpo del reemplazo (§16.3); <c>pointsOfSale</c> ausente no toca los puntos.</summary>
    public sealed record FijarAlcanceRequest(
        IReadOnlyList<WarehouseScopeInput>? Warehouses,
        IReadOnlyList<PointOfSaleScopeInput>? PointsOfSale);
}
