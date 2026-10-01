using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Periods;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Los períodos de inventario (feature 012, T292; contracts/api.md §13.4; FR-047): la lista de meses y la vista previa del cierre
/// con <c>Inventory.Periods.View</c>; cerrar con <c>Inventory.Periods.Close</c> y reabrir el último con el permiso especial
/// <c>Inventory.Periods.Reopen</c> y motivo, los dos con <c>Idempotency-Key</c>. Los valores del valorizado salen nulos sin
/// <c>Inventory.Costs.Read</c> (lo decide el comando). Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PeriodsEndpoints : ICarterModule
{
    public const string Ver = "Inventory.Periods.View";
    public const string Cerrar = "Inventory.Periods.Close";
    public const string Reabrir = "Inventory.Periods.Reopen";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/inventory/periods").WithTags("Inventory Periods").RequireAuthorization();

        g.MapGet("/", async (int? year, ISender sender, CancellationToken ct) => await sender.Send(new ListInventoryPeriodsQuery(year), ct))
            .WithName("Inventory_Periods_List").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapGet("/{year:int}/{month:int}/close", async (int year, int month, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPeriodCloseCheckQuery(year, month), ct))
            .WithName("Inventory_Periods_CloseCheck").AddEndpointFilter<ErrorEnvelopeFilter>().RequirePermission(Ver);

        g.MapPost("/{year:int}/{month:int}/close", async (int year, int month, CerrarRequest? body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new CloseInventoryPeriodCommand(year, month, body?.AcknowledgeWarnings ?? false, body?.AcceptUnbilledShipments ?? false, body?.Reason)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Inventory_Periods_Close").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Cerrar);

        g.MapPost("/{year:int}/{month:int}/reopen", async (int year, int month, ReabrirRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReopenInventoryPeriodCommand(year, month, body.Reason ?? string.Empty) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Inventory_Periods_Reopen").AddEndpointFilter<ErrorEnvelopeFilter>().ConClaveDeOperacion().RequirePermission(Reabrir);
    }

    /// <summary>El cuerpo de cerrar (§13.4): reconocer los avisos y, en I6, aceptar las remisiones sin facturar con motivo.</summary>
    public sealed record CerrarRequest(bool? AcknowledgeWarnings, bool? AcceptUnbilledShipments, string? Reason);

    /// <summary>El cuerpo de reabrir: el motivo es obligatorio.</summary>
    public sealed record ReabrirRequest(string? Reason);
}
