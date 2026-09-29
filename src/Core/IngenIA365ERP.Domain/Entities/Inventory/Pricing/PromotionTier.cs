using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pricing;

/// <summary>
/// Un escalón del precio por cantidad (<c>INV_PromotionTiers</c>; feature 012, I6, T856; data-model §14 «Promociones»): desde
/// <see cref="MinQuantity"/>, <see cref="UnitPrice"/>. Sólo en <c>QuantityPrice</c>. Único <c>(PromotionId, MinQuantity)</c> entre vivos.
/// </summary>
public class PromotionTier : AuditableEntity
{
    public int PromotionId { get; set; }

    public Promotion? Promotion { get; set; }

    public decimal MinQuantity { get; set; }

    public decimal UnitPrice { get; set; }
}
