using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pricing;

/// <summary>
/// Una promoción con vigencia (<c>INV_Promotions</c>; feature 012, I6, T856; FR-054, US14; data-model §14 «Promociones»). Sólo la
/// columna de su <see cref="Kind"/> va llena: <see cref="Rate"/> (fracción), <see cref="Amount"/> (por unidad),
/// <see cref="BuyQuantity"/>/<see cref="PayQuantity"/> (3×2), <see cref="BundlePrice"/> (precio del paquete); el precio por
/// cantidad va en <see cref="Tiers"/>. A qué alcanza lo dicen <see cref="Scopes"/>. <c>MotorDePromociones</c> produce descuentos no
/// condicionados por línea, nunca líneas a precio cero. Alta y cambio con motivo (sus comandos son <c>IConMotivo</c>, como los del
/// tope de descuento). Código único entre vivos (<c>CodigoDeCatalogo</c>).
/// </summary>
public class Promotion : AuditableEntity
{
    public const int LargoDelCodigo = 10;

    public const int LargoDelNombre = 80;

    public const int LargoDeLasNotas = 300;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public PromotionKind Kind { get; set; }

    /// <summary><see cref="PromotionKind.Percent"/>: fracción (0,10 = 10 %).</summary>
    public decimal? Rate { get; set; }

    /// <summary><see cref="PromotionKind.Amount"/>: descuento por unidad vendida.</summary>
    public decimal? Amount { get; set; }

    /// <summary><see cref="PromotionKind.BuyNPayM"/>: cuántas lleva (3 en 3×2).</summary>
    public decimal? BuyQuantity { get; set; }

    /// <summary><see cref="PromotionKind.BuyNPayM"/>: cuántas paga (2 en 3×2).</summary>
    public decimal? PayQuantity { get; set; }

    /// <summary><see cref="PromotionKind.BundlePrice"/>: precio del paquete completo.</summary>
    public decimal? BundlePrice { get; set; }

    public bool IsCumulative { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public ICollection<PromotionScope> Scopes { get; set; } = new List<PromotionScope>();

    public ICollection<PromotionTier> Tiers { get; set; } = new List<PromotionTier>();
}
