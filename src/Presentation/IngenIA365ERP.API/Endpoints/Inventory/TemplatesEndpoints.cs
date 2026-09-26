using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Imports;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La lista de las dieciséis plantillas de la parametrización (feature 012, T160; contracts/api.md §3.9,
/// contracts/plantillas.md §0.1): <c>GET /api/inventory/templates</c> con <c>Inventory.Catalog.View</c>. Es la <b>única</b>
/// ruta de la lista (dos <c>MapGet</c> iguales rompen al arrancar): <c>CatalogEndpoints</c> no la publica. La descarga y
/// la importación de cada plantilla van junto a su catálogo (<c>RutasDePlantilla.MapPlantilla</c>). (nuevo)
/// </summary>
public class TemplatesEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/templates")
            .WithTags("Inventory Templates")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) => await sender.Send(new ListImportTemplatesQuery(), ct))
            .WithName("Inventory_Templates_List")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("Inventory.Catalog.View");
    }
}
