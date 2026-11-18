using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Inventory.Sales;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Inventory;

/// <summary>
/// La referencia de la línea de venta (2026-11-18, feature 020): qué productos la piden y la resolución de lo digitado (la placa o el lateral
/// del vehículo de un servicio). Las usan el POS y la factura de oficina; cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class LineReferenceEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory/sales/line-references")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapGet("/products", async (ISender sender, CancellationToken ct) => await sender.Send(new ListLineReferenceProductsQuery(), ct))
            .WithName("Inventory_Sales_LineReferences_Products")
            .RequirePermission(new AlgunPermiso("Inventory.Sales.Create", "Inventory.Pos.Sell"));

        group.MapGet("/resolve", async (Guid product, string q, ISender sender, CancellationToken ct) =>
                await sender.Send(new ResolveLineReferenceQuery(product, q), ct))
            .WithName("Inventory_Sales_LineReferences_Resolve")
            .RequirePermission(new AlgunPermiso("Inventory.Sales.Create", "Inventory.Pos.Sell"));
    }
}
