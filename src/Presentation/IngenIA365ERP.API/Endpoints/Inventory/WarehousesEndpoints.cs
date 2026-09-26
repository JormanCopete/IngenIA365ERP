using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Inventory.Warehouses;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Bodegas, ubicaciones, tipos de bodega y mínimos y máximos (feature 012, T231; contracts/api.md §4.1–§4.4, §2.2). Es el
/// archivo nuevo: el heredado lo retiró la fase 2 (<c>RetiroDelInventarioHeredado</c>). Leer con
/// <c>Inventory.Warehouses.View</c>; escribir con <c>Inventory.Warehouses.Manage</c> e <c>Idempotency-Key</c>. Toda lectura
/// pasa por el alcance del usuario: una bodega fuera de él es el mismo 404 <c>Inventory.Warehouse.NotFound</c> que una
/// inexistente. La plantilla 7 (<c>template.xlsx</c>, <c>import</c>) va por <see cref="RutasDePlantilla.MapPlantilla"/>. Cada
/// ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class WarehousesEndpoints : ICarterModule
{
    public const string Ver = "Inventory.Warehouses.View";
    public const string Administrar = "Inventory.Warehouses.Manage";
    public const string Activar = "Inventory.Warehouses.Activate";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        TiposDeBodega(app);
        Bodegas(app);
        Reorden(app);
    }

    private static void TiposDeBodega(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory/warehouse-types").WithTags("Inventory Warehouse Types").RequireAuthorization();

        g.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListWarehouseTypesQuery(includeInactive ?? false), ct))
            .WithName("Inventory_WarehouseTypes_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (TipoDeBodegaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateWarehouseTypeCommand(body.Code ?? string.Empty, body.Name ?? string.Empty,
                    body.Behavior ?? WarehouseBehavior.Operational) { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/warehouse-types/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_WarehouseTypes_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, TipoDeBodegaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateWarehouseTypeCommand(id, body.Name ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_WarehouseTypes_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/deactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetWarehouseTypeActiveCommand(id, false, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_WarehouseTypes_Deactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/reactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetWarehouseTypeActiveCommand(id, true, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_WarehouseTypes_Reactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);
    }

    private static void Bodegas(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory/warehouses").WithTags("Inventory Warehouses").RequireAuthorization();

        // US10 (T375): ?purpose=TransferDestination devuelve los destinos de un traslado (todas las operativas activas, fuera del
        // alcance, sólo con Transfers.Create). Quien no ve bodegas los pide por GET /api/inventory/transfers/destinations.
        g.MapGet("/", async (Guid? branchPublicId, Guid? typePublicId, WarehouseActivationStatus? activationStatus, bool? includeTransit,
                    bool? includeInactive, string? purpose, ISender sender, CancellationToken ct) =>
                string.Equals(purpose, ListTransferDestinationsQuery.Proposito, StringComparison.Ordinal)
                    ? (object)await sender.Send(new ListTransferDestinationsQuery(), ct)
                    : await sender.Send(new ListWarehousesQuery(branchPublicId, typePublicId, activationStatus, includeTransit ?? false, includeInactive ?? false), ct))
            .WithName("Inventory_Warehouses_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => await sender.Send(new GetWarehouseQuery(id), ct))
            .WithName("Inventory_Warehouses_Get").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/", async (CrearBodegaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateWarehouseCommand(body.Code ?? string.Empty, body.Name ?? string.Empty, body.BranchPublicId,
                    body.WarehouseTypePublicId, body.Notes,
                    body.TransitWarehouse is { } t ? new BodegaDeTransitoPedida(t.Code ?? string.Empty, t.Name) : null)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/warehouses/{r.Value.Warehouse.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_Warehouses_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}", async (Guid id, EditarBodegaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateWarehouseCommand(id, body.Name ?? string.Empty, body.WarehouseTypePublicId, body.Notes)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Warehouses_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/deactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetWarehouseActiveCommand(id, false, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Warehouses_Deactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/reactivate", async (Guid id, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetWarehouseActiveCommand(id, true, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Warehouses_Reactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        // §4.3 Ubicaciones.
        g.MapGet("/{id:guid}/locations", async (Guid id, bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLocationsQuery(id, includeInactive ?? false), ct))
            .WithName("Inventory_Warehouses_Locations_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{id:guid}/locations", async (Guid id, UbicacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var r = await sender.Send(new CreateWarehouseLocationCommand(id, body.Code ?? string.Empty, body.Name ?? string.Empty, body.IsDefault ?? false)
                    { OperationKey = http.ClaveDeOperacion() }, ct);
                return r.IsSuccess ? (object)Results.Created($"/api/inventory/warehouses/{id}/locations/{r.Value.PublicId}", r.Value) : r;
            })
            .WithName("Inventory_Warehouses_Locations_Create").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPut("/{id:guid}/locations/{locationId:guid}", async (Guid id, Guid locationId, UbicacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateWarehouseLocationCommand(id, locationId, body.Name ?? string.Empty, body.IsDefault ?? false)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Warehouses_Locations_Update").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/locations/{locationId:guid}/deactivate", async (Guid id, Guid locationId, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetWarehouseLocationActiveCommand(id, locationId, false, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Warehouses_Locations_Deactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapPost("/{id:guid}/locations/{locationId:guid}/reactivate", async (Guid id, Guid locationId, MotivoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetWarehouseLocationActiveCommand(id, locationId, true, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Warehouses_Locations_Reactivate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        // US4 (T317, api.md §13.3): la vista previa y la activación de una bodega, con Inventory.Warehouses.Activate (404 sin él).
        // Antes de I2 no hay comparación contable: fuera de producción sólo aceptando la diferencia (PuestaEnMarchaOptions).
        g.MapGet("/{id:guid}/activation", async (Guid id, DateOnly? cutoffDate, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetWarehouseActivationPreviewQuery(id, cutoffDate), ct))
            .WithName("Inventory_Warehouses_Activation_Preview").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Activar);

        g.MapPost("/{id:guid}/activation", async (Guid id, ActivacionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ActivateWarehouseCommand(id, body.CutoffDate, body.AcceptDifference ?? false, body.Reason ?? string.Empty)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Warehouses_Activate").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Activar);

        // Plantilla 7 (contracts/plantillas.md §7, §0.7): descarga con Warehouses.View, con datos además Catalog.Export;
        // importar con Warehouses.Manage; la columna stockNegativo exige además Inventory.Parameters.Manage (el ejecutor).
        g.MapPlantilla(Ver, Administrar, PlantillaDeBodegas.Clave, "Inventory_Warehouses",
            importar: (modo, archivo, motivo, clave) => new ImportWarehousesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave },
            datos: () => new GetWarehousesTemplateDataQuery(),
            permisoDeExportacion: CatalogEndpoints.Exportar);
    }

    private static void Reorden(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory/reorder-policies").WithTags("Inventory Reorder Policies").RequireAuthorization();

        g.MapGet("/", async (Guid? warehousePublicId, Guid? productPublicId, bool? belowReorderPoint, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListReorderPoliciesQuery(warehousePublicId, productPublicId, belowReorderPoint, new PageRequest(page ?? 1, pageSize ?? 20)), ct))
            .WithName("Inventory_ReorderPolicies_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPut("/", async (ReordenRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetReorderPolicyCommand(body.ProductPublicId, body.WarehousePublicId, body.Minimum, body.Maximum, body.ReorderPoint)
                    { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_ReorderPolicies_Set").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new DeleteReorderPolicyCommand(id) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_ReorderPolicies_Delete").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Administrar);
    }

    public sealed record TipoDeBodegaRequest(string? Code, string? Name, WarehouseBehavior? Behavior);

    public sealed record TransitoRequest(string? Code, string? Name);

    /// <summary>El alta (§4.2): <c>transitWarehouse</c> sólo con la primera bodega operativa de la sucursal.</summary>
    public sealed record CrearBodegaRequest(string? Code, string? Name, Guid BranchPublicId, Guid WarehouseTypePublicId, string? Notes, TransitoRequest? TransitWarehouse);

    public sealed record EditarBodegaRequest(string? Name, Guid WarehouseTypePublicId, string? Notes);

    public sealed record UbicacionRequest(string? Code, string? Name, bool? IsDefault);

    public sealed record ReordenRequest(Guid ProductPublicId, Guid WarehousePublicId, decimal Minimum, decimal Maximum, decimal ReorderPoint);

    public sealed record MotivoRequest(string? Reason);

    /// <summary>El cuerpo de la activación (§13.3).</summary>
    public sealed record ActivacionRequest(DateOnly CutoffDate, bool? AcceptDifference, string? Reason);
}
