using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.Application.Inventory.Imports;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Puntos de venta y cajas (feature 012; contracts/api.md §19). En I1 sólo publica la descarga vacía de la plantilla 10
/// (T238, FR-095, US1-6; contracts/plantillas.md §10): <c>GET /api/inventory/points-of-sale/template.xlsx</c> con
/// <c>Inventory.PointsOfSale.View</c>, sin <c>POST …/import</c> ni <c>?withData</c> (404) hasta I3 (T627), que suma aquí las
/// rutas de los puntos, las cajas y la importación. (nuevo)
/// </summary>
public class PointsOfSaleEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/points-of-sale")
            .WithTags("Inventory Points of Sale")
            .RequireAuthorization();

        group.MapPlantilla("Inventory.PointsOfSale.View", null, CatalogoDePlantillas.PuntosDeVentaClave, "Inventory_PointsOfSale");
    }
}
