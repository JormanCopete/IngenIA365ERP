using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pricing;

/// <summary>
/// A qué alcanza una promoción (<c>INV_PromotionScopes</c>; feature 012, I6, T856; data-model §14 «Promociones»): una sola columna
/// destino llena por fila, la de su <see cref="ScopeKind"/> (lo fija un <c>CHECK</c> en los dos motores). Dentro de una misma clase,
/// cualquiera (O); entre clases distintas, todas (Y); sin filas de una clase, esa clase no restringe. La categoría incluye sus
/// descendientes. <see cref="RequiredQuantity"/> sólo en <see cref="PromotionKind.BundlePrice"/>: cuántas unidades del producto forman
/// el paquete.
/// </summary>
public class PromotionScope : AuditableEntity
{
    public const int LargoDelSegmento = 4;

    public int PromotionId { get; set; }

    public Promotion? Promotion { get; set; }

    public PromotionScopeKind ScopeKind { get; set; }

    public int? ProductId { get; set; }

    public int? ProductCategoryId { get; set; }

    public string? Segment { get; set; }

    public int? SalesChannelId { get; set; }

    public decimal? RequiredQuantity { get; set; }
}
