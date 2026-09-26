using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.Application.Inventory.Imports;

namespace IngenIA365ERP.API.Endpoints.Core;

/// <summary>
/// Medios de pago de Core (feature 012; contracts/api.md §30). En I1 sólo publica la descarga vacía de la plantilla 11
/// (T238, FR-095, US1-6; contracts/plantillas.md §11): <c>GET /api/core/payment-means/template.xlsx</c> con
/// <c>Core.PaymentMeans.View</c>, sin importación ni datos (404) hasta I3 (T25), que suma aquí las rutas del catálogo. (nuevo)
/// </summary>
public class PaymentMeansEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/core/payment-means")
            .WithTags("Core Payment Means")
            .RequireAuthorization();

        group.MapPlantilla("Core.PaymentMeans.View", null, CatalogoDePlantillas.MediosDePagoClave, "Core_PaymentMeans");
    }
}
