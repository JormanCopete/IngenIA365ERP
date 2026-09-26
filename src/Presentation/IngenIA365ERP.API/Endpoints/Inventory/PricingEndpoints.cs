using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.Application.Inventory.Imports;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// Listas de precios y topes de descuento (feature 012; contracts/api.md §20). En I1 sólo publica la descarga vacía de las
/// plantillas 12 y 13 (T238, FR-095, US1-6; contracts/plantillas.md §12, §13): <c>GET /api/inventory/price-lists/template.xlsx</c>
/// y <c>GET /api/inventory/discount-caps/template.xlsx</c> con <c>Inventory.Prices.View</c>, sin importación ni datos (404)
/// hasta I3 (T628, T629). (nuevo)
/// </summary>
public class PricingEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/inventory/price-lists")
            .WithTags("Inventory Price Lists")
            .RequireAuthorization()
            .MapPlantilla("Inventory.Prices.View", null, CatalogoDePlantillas.ListasDePreciosClave, "Inventory_PriceLists");

        app.MapGroup("/api/inventory/discount-caps")
            .WithTags("Inventory Discount Caps")
            .RequireAuthorization()
            .MapPlantilla("Inventory.Prices.View", null, CatalogoDePlantillas.TopesDeDescuentoClave, "Inventory_DiscountCaps");
    }
}
