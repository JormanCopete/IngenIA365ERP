using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.Pricing;

/// <summary>
/// Los errores de listas de precios, topes de descuento y precificación (feature 012, I3, T597–T602; contracts/api.md §19.5).
/// <c>Inventory.PriceList.NotFound</c>, <c>.ProductNotPriceable</c>, <c>.UnitNotForSale</c>, <c>Inventory.DiscountCap.RoleNotFound</c>
/// e <c>Inventory.Discount.CapInsufficient</c> son (nuevo). (nuevo)
/// </summary>
public static class ErroresDePrecios
{
    public const string PriceListNotFoundCode = "Inventory.PriceList.NotFound";
    public const string OverlapsCode = "Inventory.PriceList.Overlaps";
    public const string SegmentUnknownCode = "Inventory.PriceList.SegmentUnknown";
    public const string ScopeLockedCode = "Inventory.PriceList.ScopeLocked";
    public const string ProductNotPriceableCode = "Inventory.PriceList.ProductNotPriceable";
    public const string UnitNotForSaleCode = "Inventory.PriceList.UnitNotForSale";
    public const string PriceNotFoundCode = "Inventory.Price.NotFound";
    public const string DiscountCapOverlapsCode = "Inventory.DiscountCap.Overlaps";
    public const string DiscountCapRoleNotFoundCode = "Inventory.DiscountCap.RoleNotFound";
    public const string ApprovalPendingCode = "Inventory.Discount.ApprovalPending";
    public const string CapInsufficientCode = "Inventory.Discount.CapInsufficient";
    public const string BelowCostCode = "Inventory.Sales.BelowCost";
    public const string PromotionNotFoundCode = "Inventory.Promotion.NotFound";
    public const string PromotionInUseCode = "Inventory.Promotion.InUse";
    public const string PromotionScopeTargetNotFoundCode = "Inventory.Promotion.ScopeTargetNotFound";
    public const string PromotionAppliedCode = "Inventory.Discount.PromotionApplied";

    /// <summary>La lista no existe (404).</summary>
    public static Error PriceListNotFound() => new(PriceListNotFoundCode, "La lista de precios no existe.");

    /// <summary>Otra lista del mismo ámbito se cruza en el tiempo (422, §19.1), nombrándola.</summary>
    public static Error Overlaps(Guid priceListPublicId, string code, DateOnly validFrom, DateOnly? validTo) => new ErrorConDatos(OverlapsCode,
        $"La lista {code} tiene el mismo ámbito y está vigente desde {validFrom:yyyy-MM-dd}"
        + (validTo is { } hasta ? $" hasta {hasta:yyyy-MM-dd}" : " sin fecha de cierre")
        + ": dos listas del mismo ámbito no se cruzan en el tiempo. Cierre la vigencia de esa lista o cambie las fechas.",
        new { priceListPublicId, code, validFrom, validTo });

    /// <summary>El segmento no es ninguna clase de asociado existente (422, T51).</summary>
    public static Error SegmentUnknown(string segment, IReadOnlyList<string> allowed) => new ErrorConDatos(SegmentUnknownCode,
        allowed.Count == 0
            ? $"No hay asociados con clase «{segment}»: todavía no existe ninguna clase de asociado para usar como segmento."
            : $"No hay asociados con clase «{segment}». Admite: {string.Join(", ", allowed)}.",
        new { segment, allowed });

    /// <summary>El ámbito, el código o <c>includesTaxes</c> no cambian (422, §19.1).</summary>
    /// <param name="field"><c>scope</c>, <c>code</c> o <c>includesTaxes</c>.</param>
    public static Error ScopeLocked(string field) => new ErrorConDatos(ScopeLockedCode,
        "El ámbito, el código y si el precio incluye impuestos no cambian en una lista: cree otra lista con otra vigencia.",
        new { field });

    /// <summary>Una plantilla no tiene precio: se vende por sus variantes (data-model §14).</summary>
    public static Error ProductNotPriceable(string productCode) => new ErrorConDatos(ProductNotPriceableCode,
        $"El producto {productCode} es una plantilla: el precio va en cada variante.", new { productCode });

    /// <summary>La unidad no es la base ni una alterna de venta del producto.</summary>
    public static Error UnitNotForSale(string productCode, string unitCode) => new ErrorConDatos(UnitNotForSaleCode,
        $"La unidad {unitCode} no es la base ni una unidad de venta del producto {productCode}.", new { productCode, unitCode });

    /// <summary>Ninguna lista aplicable trae el producto en esa unidad (422, §19.2).</summary>
    public static Error PriceNotFound(string product, string unit) => new ErrorConDatos(PriceNotFoundCode,
        $"No hay precio para {product} en {unit} en ninguna lista vigente que aplique. Agréguelo en Ventas → Listas de precios.",
        new { product, unit });

    /// <summary>El rol ya tiene un tope que empieza ese día o después (422, §19.3).</summary>
    public static Error DiscountCapOverlaps(Guid discountCapPublicId, DateOnly validFrom, DateOnly? validTo) => new ErrorConDatos(DiscountCapOverlapsCode,
        $"El rol ya tiene un tope vigente desde {validFrom:yyyy-MM-dd}: una vigencia nueva empieza después de la última.",
        new { discountCapPublicId, validFrom, validTo });

    public static Error DiscountCapRoleNotFound(string? codigo = null) => new(DiscountCapRoleNotFoundCode,
        codigo is null ? "El rol no existe." : $"No hay un rol «{codigo}» en la cooperativa. Créelo en Seguridad → Roles.");

    /// <summary>Confirmar o cobrar con un descuento pendiente o rechazado (422, §19.3).</summary>
    public static Error ApprovalPending(IReadOnlyList<int> lines) => new ErrorConDatos(ApprovalPendingCode,
        $"Hay descuentos sobre el tope sin aprobar en las líneas {string.Join(", ", lines)}: espere la aprobación o quítelos.",
        new { lines });

    /// <summary>Quien decide no tiene un tope suficiente para aprobar ese descuento (§19.3). (nuevo)</summary>
    public static Error CapInsufficient(decimal requestedRate, decimal approverRate) => new ErrorConDatos(CapInsufficientCode,
        $"El descuento pedido es de {requestedRate:P2} y su tope es de {approverRate:P2}: lo aprueba alguien con un tope mayor.",
        new { requestedRate, approverRate });

    /// <summary>Venta por debajo del costo con <c>Ventas.BajoCosto = Bloquear</c> (422).</summary>
    public static Error BelowCost(int lineNumber, string productCode, decimal unitPrice, decimal averageCost) => new ErrorConDatos(BelowCostCode,
        $"La línea {lineNumber} ({productCode}) se vende a {unitPrice:N2} y su costo promedio es {averageCost:N2}: la cooperativa no permite vender por debajo del costo.",
        new { lineNumber, productCode, unitPrice, averageCost });

    // ------------------------------------------------------------------------------------ promociones (I6) --

    /// <summary>La promoción no existe (404). (nuevo, I6 T873)</summary>
    public static Error PromotionNotFound() => new(PromotionNotFoundCode, "La promoción no existe.");

    /// <summary>
    /// La promoción ya se aplicó en un documento confirmado: sólo cambian nombre, fin de vigencia y activo (422, §19.4).
    /// <paramref name="fields"/> son los campos pedidos que no pueden cambiar.
    /// </summary>
    public static Error PromotionInUse(IReadOnlyList<string> fields) => new ErrorConDatos(PromotionInUseCode,
        $"La promoción ya se aplicó en documentos confirmados: sólo cambian el nombre, el fin de la vigencia y si está activa (pidió cambiar {string.Join(", ", fields)}). "
        + "Para otra mecánica, cierre ésta y cree otra.",
        new { fields });

    /// <summary>Un ámbito nombra un producto, categoría o canal que no existe. (nuevo, I6 T873)</summary>
    public static Error PromotionScopeTargetNotFound(string kind, Guid publicId) => new ErrorConDatos(PromotionScopeTargetNotFoundCode,
        $"El ámbito de la promoción nombra un {kind} que no existe o fue eliminado.", new { kind, publicId });

    /// <summary>
    /// Un descuento manual en una línea que ya tiene promoción (422, F9: no son acumulables; «el manual no se suma»). Incluye el
    /// descuento por total cuando todas las líneas tienen promoción. (nuevo, I6 T875)
    /// </summary>
    public static Error PromotionApplied(IReadOnlyList<int> lines, IReadOnlyList<string> promotions) => new ErrorConDatos(PromotionAppliedCode,
        $"Las líneas {string.Join(", ", lines)} ya tienen la promoción {string.Join(", ", promotions)}: un descuento manual no se suma a una promoción. "
        + "Quite el descuento o el precio digitado de esas líneas.",
        new { lines, promotions });
}
