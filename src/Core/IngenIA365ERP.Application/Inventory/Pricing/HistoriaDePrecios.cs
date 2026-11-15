using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;

namespace IngenIA365ERP.Application.Inventory.Pricing;

/// <summary>
/// El único escritor de <c>INV_PriceListItemHistory</c> (2026-11-15; <c>LaHistoriaDePreciosSeEscribeEnUnSoloSitio</c>): cada alta, cambio,
/// retiro o paso de unidad de un precio deja su fila en el mismo guardado que el cambio. No guarda. (nuevo)
/// </summary>
public static class HistoriaDePrecios
{
    /// <summary>El largo máximo del motivo en la historia.</summary>
    public const int LargoDelMotivo = 500;

    public static void Registrar(IApplicationDbContext db, PriceListItem item, PriceChangeKind cambio, decimal? anterior, decimal? nuevo, string? motivo,
        int? unidadAnterior = null)
    {
        var texto = string.IsNullOrWhiteSpace(motivo) ? "Sin motivo" : motivo.Trim();
        db.PriceListItemHistory.Add(new PriceListItemHistory
        {
            PriceListId = item.PriceListId,
            PriceList = item.PriceListId == 0 ? item.PriceList : null,
            ProductId = item.ProductId,
            UnitId = item.UnitId,
            Kind = cambio,
            PreviousPrice = anterior,
            NewPrice = nuevo,
            PreviousUnitId = unidadAnterior,
            Reason = texto.Length > LargoDelMotivo ? texto[..LargoDelMotivo] : texto,
        });
    }
}
