using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Reports.Dashboard;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// El tablero de inventario (feature 012, I6, US17, T968; FR-088; contracts/api.md §28): <c>GET /api/inventory/dashboard?branch=&amp;warehouse=&amp;asOf=</c>
/// con <c>Inventory.Dashboard.View</c>. Sólo lee: no lleva <c>Idempotency-Key</c>. La ruta reenvía a <see cref="GetInventoryDashboardQuery"/> con la
/// tolerancia técnica de los lotes programados (<c>Integration:Dispatcher:LateToleranceMinutes</c>), la misma de la vista
/// <c>accounting-batches</c>. Sin el permiso, el 404 del sobre. (nuevo)
/// </summary>
public class DashboardEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var tolerancia = app.ServiceProvider.GetService<Microsoft.Extensions.Options.IOptions<IngenIA365ERP.API.Integration.IntegrationOptions>>()
            ?.Value.Dispatcher.LateToleranceMinutes ?? AccountingBatchesReportQueryHandler.ToleranciaPorDefecto;

        var group = app.MapGroup("/api/inventory")
            .WithTags("Inventory Dashboard")
            .RequireAuthorization();

        group.MapGet("/dashboard", async (Guid? branch, Guid? warehouse, DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetInventoryDashboardQuery(branch, warehouse, asOf, tolerancia), ct))
            .WithName("Inventory_Dashboard_Get")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Dashboard.View");
    }
}
