using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Inventory.Pos;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Puntos de venta y cajas (feature 012; contracts/api.md §20.1, §22.3; T628): <c>/api/inventory/points-of-sale</c> (lista, detalle,
/// alta y edición), sus cajas (<c>{id}/cash-registers</c>), la plantilla 10 —descarga vacía desde I1 (T238), importación desde I3 con
/// <c>ImportPointsOfSaleCommand</c>— y dónde se ofrece cada medio de pago (<c>/api/inventory/payment-means/{id}/availability</c>).
/// Consulta con <c>Inventory.PointsOfSale.View</c>; escritura con <c>Inventory.PointsOfSale.Manage</c> e <c>Idempotency-Key</c>. Un
/// punto fuera del alcance es el mismo 404 que uno inexistente (lo decide la consulta). Cada ruta sólo reenvía al
/// <see cref="ISender"/>. (nuevo)
/// </summary>
public class PointsOfSaleEndpoints : ICarterModule
{
    public const string PermisoDeConsulta = "Inventory.PointsOfSale.View";
    public const string PermisoDeEscritura = "Inventory.PointsOfSale.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/points-of-sale")
            .WithTags("Inventory Points of Sale")
            .RequireAuthorization();

        group.MapGet("/", async (Guid? branch, bool? active, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPointsOfSaleQuery(branch, active), ct))
            .WithName("Inventory_PointsOfSale_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPointOfSaleQuery(id), ct))
            .WithName("Inventory_PointsOfSale_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/", async (PuntoDeVentaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreatePointOfSaleCommand(
                    body.Code ?? string.Empty, body.Name ?? string.Empty, body.BranchPublicId ?? Guid.Empty, body.SalesChannelPublicId ?? Guid.Empty,
                    body.PosEnabled ?? true, body.DefaultWarehousePublicId ?? Guid.Empty, body.Address)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess ? (object)Results.Created($"/api/inventory/points-of-sale/{result.Value.PointOfSalePublicId}", result.Value) : result;
            })
            .WithName("Inventory_PointsOfSale_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}", async (Guid id, PuntoDeVentaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdatePointOfSaleCommand(
                    id, body.Name ?? string.Empty, body.SalesChannelPublicId ?? Guid.Empty, body.PosEnabled ?? true,
                    body.DefaultWarehousePublicId ?? Guid.Empty, body.IsActive ?? true, body.Address)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_PointsOfSale_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapGet("/{id:guid}/cash-registers", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListCashRegistersQuery(id), ct))
            .WithName("Inventory_CashRegisters_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        group.MapPost("/{id:guid}/cash-registers", async (Guid id, CajaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCashRegisterCommand(
                    id, body.Code ?? string.Empty, body.Name ?? string.Empty, body.WarehousePublicId ?? Guid.Empty, body.DefaultCardTerminalPublicId,
                    body.PrintFormat ?? CashRegisterPrintFormat.Ticket80, body.DocumentTypes ?? [], body.DianCashRegisterPlate, body.PrintCopies,
                    body.DianCashRegisterTypeCode)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct);
                return result.IsSuccess
                    ? (object)Results.Created($"/api/inventory/points-of-sale/{id}/cash-registers/{result.Value.CashRegisterPublicId}", result.Value)
                    : result;
            })
            .WithName("Inventory_CashRegisters_Create")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        group.MapPut("/{id:guid}/cash-registers/{registerId:guid}", async (Guid id, Guid registerId, CajaRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new UpdateCashRegisterCommand(
                    id, registerId, body.Name ?? string.Empty, body.WarehousePublicId ?? Guid.Empty, body.DefaultCardTerminalPublicId,
                    body.PrintFormat ?? CashRegisterPrintFormat.Ticket80, body.DocumentTypes ?? [], body.DianCashRegisterPlate, body.PrintCopies,
                    body.IsActive ?? true, body.DianCashRegisterTypeCode)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_CashRegisters_Update")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);

        // Plantilla 10 (contracts/plantillas.md §10): puntos y cajas con sus tipos por rol.
        group.MapPlantilla(PermisoDeConsulta, PermisoDeEscritura, CatalogoDePlantillas.PuntosDeVentaClave, "Inventory_PointsOfSale",
            importar: (modo, archivo, motivo, clave) => new ImportPointsOfSaleCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });

        // §22.3: los conjuntos explícitos de un medio (las marcas «todos» viven en el medio de Core, §22.1).
        var disponibilidad = app.MapGroup("/api/inventory/payment-means")
            .WithTags("Inventory Payment Means Availability")
            .RequireAuthorization();

        disponibilidad.MapGet("/{id:guid}/availability", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPaymentMeansAvailabilityQuery(id), ct))
            .WithName("Inventory_PaymentMeansAvailability_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission(PermisoDeConsulta);

        disponibilidad.MapPut("/{id:guid}/availability", async (Guid id, DisponibilidadRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetPaymentMeansAvailabilityCommand(
                    id, body.PointOfSalePublicIds ?? [], body.SalesChannelPublicIds ?? [], body.DocumentTypePublicIds ?? [])
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_PaymentMeansAvailability_Set")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .ConClaveDeOperacion()
            .RequirePermission(PermisoDeEscritura);
    }

    /// <summary>
    /// El cuerpo de un punto (§20.1). El alta usa código y sucursal; la edición los ignora (no cambian) y usa <c>isActive</c>.
    /// <c>reason</c> se admite y no se guarda: el punto no es un parámetro con historial propio.
    /// </summary>
    public sealed record PuntoDeVentaRequest(
        string? Code, string? Name, Guid? BranchPublicId, Guid? SalesChannelPublicId, bool? PosEnabled, Guid? DefaultWarehousePublicId,
        bool? IsActive, string? Address, string? Reason);

    /// <summary>El cuerpo de una caja (§20.1). La edición ignora el código.</summary>
    public sealed record CajaRequest(
        string? Code, string? Name, Guid? WarehousePublicId, Guid? DefaultCardTerminalPublicId, CashRegisterPrintFormat? PrintFormat,
        IReadOnlyList<CashRegisterDocumentTypeInput>? DocumentTypes, string? DianCashRegisterPlate, byte? PrintCopies, bool? IsActive,
        string? DianCashRegisterTypeCode = null);

    /// <summary>Los tres conjuntos de la disponibilidad de un medio (§22.3).</summary>
    public sealed record DisponibilidadRequest(
        IReadOnlyList<Guid>? PointOfSalePublicIds, IReadOnlyList<Guid>? SalesChannelPublicIds, IReadOnlyList<Guid>? DocumentTypePublicIds);
}
