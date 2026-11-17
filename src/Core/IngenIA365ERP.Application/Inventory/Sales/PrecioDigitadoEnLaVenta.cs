using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Los productos de precio digitado en la venta (2026-11-16, pedido del dueño: un servicio de valor variable, <c>Product.PriceEnteredAtSale</c>).
/// El precio lo pone <see cref="PrecificacionDeVenta"/> con lo que se digitó; aquí, la regla de la confirmación: en una venta o una
/// cotización, la línea de un producto así no se confirma en cero (<c>Inventory.Price.EnteredAtSaleRequired</c>). (nuevo)
/// </summary>
public static class PrecioDigitadoEnLaVenta
{
    private static readonly HashSet<DocumentClass> ConPrecio =
    [
        DocumentClass.SalesQuote, DocumentClass.SalesOrder, DocumentClass.SalesInvoice, DocumentClass.SalesInvoiceFromShipments,
        DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt,
    ];

    /// <summary>La primera línea de un producto de precio digitado que va en cero, o nada.</summary>
    public static async Task<Error?> FaltanteAsync(IApplicationDbContext db, InventoryDocument documento, CancellationToken ct)
    {
        if (!ConPrecio.Contains(documento.Class)) return null;
        var enCero = documento.Lines.Where(l => !l.IsDeleted && l.UnitPrice <= 0m).ToList();
        if (enCero.Count == 0) return null;
        var ids = enCero.Select(l => l.ProductId).Distinct().ToList();
        var libres = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.PriceEnteredAtSale)
            .ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var primera = enCero.OrderBy(l => l.LineNumber).FirstOrDefault(l => libres.ContainsKey(l.ProductId));
        return primera is null ? null : ErroresDePrecios.PriceEnteredAtSaleRequired(primera.LineNumber, libres[primera.ProductId]);
    }
}
