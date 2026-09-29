using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Catalog;

/// <summary>
/// Los códigos de error del catálogo (feature 012, T212; contracts/api.md §3.10; decisiones-transversales §2.17): unidades,
/// categorías, marcas, grupos contables, productos con sus unidades, códigos de barras e impuestos, causas de ajuste y
/// canales. Los <c>*.NotFound</c> responden 404 (no existe, está de baja o no hay permiso: el mismo mensaje); lo demás,
/// 422, con el <c>data</c> que la persona necesita para corregir. El duplicado de código es <c>Catalogo.CodigoDuplicado</c>
/// (<c>CodigoDeCatalogo.Duplicado</c> con <c>existingPublicId</c>/<c>existingName</c>).
/// </summary>
public static class CatalogErrors
{
    // ------------------------------------------------------------------------------ no existe --

    public static Error UnitNotFound(string? codigo = null) => NoExiste("Inventory.Unit.NotFound", "La unidad de medida", codigo);
    public static Error CategoryNotFound(string? codigo = null) => NoExiste("Inventory.Category.NotFound", "La categoría", codigo);
    public static Error BrandNotFound(string? codigo = null) => NoExiste("Inventory.Brand.NotFound", "La marca", codigo);
    public static Error AccountingGroupNotFound(string? codigo = null) => NoExiste("Inventory.AccountingGroup.NotFound", "El grupo contable", codigo);
    public static Error ProductNotFound(string? codigo = null) => NoExiste("Inventory.Product.NotFound", "El producto", codigo);
    public static Error ProductUnitNotFound() => new("Inventory.ProductUnit.NotFound", "La unidad alterna no existe en este producto.");
    public static Error BarcodeNotFound() => new("Inventory.Barcode.NotFound", "El código de barras no existe en este producto.");
    public static Error AdjustmentCauseNotFound(string? codigo = null) => NoExiste("Inventory.AdjustmentCause.NotFound", "La causa de ajuste", codigo);
    public static Error SalesChannelNotFound(string? codigo = null) => NoExiste("Inventory.SalesChannel.NotFound", "El canal de venta", codigo);

    // -------------------------------------------------------------------------------- unidades --

    public static Error UnitDianCodeUnknown(string dianUnitCode) => new ErrorConDatos("Inventory.Unit.DianCodeUnknown",
        $"El código {dianUnitCode} no está en la tabla UN/ECE Rec. 20 vigente de la DIAN. Revíselo (por ejemplo 94 para unidad, KGM para kilogramo).",
        new { dianUnitCode });

    public static Error UnitDecimalsInUse(int maxDecimalsUsed) => new ErrorConDatos("Inventory.Unit.DecimalsInUse",
        $"Ya hay cantidades registradas con {maxDecimalsUsed} decimal(es) en esta unidad: los decimales no pueden bajar de ahí.",
        new { maxDecimalsUsed });

    public static Error UnitInUse(int products, IReadOnlyList<string> examples) => new ErrorConDatos("Inventory.Unit.InUse",
        $"La unidad es base o alterna de {products} producto(s) activo(s) ({string.Join(", ", examples)}): cámbielos o inactívelos antes.",
        new { products, examples });

    // ------------------------------------------------------------------------------ categorías --

    public static Error CategoryTooDeep(int maxLevel = Domain.Entities.Inventory.Catalog.ProductCategory.MaxLevel) => new ErrorConDatos(
        "Inventory.Category.TooDeep",
        $"Las categorías tienen hasta {maxLevel} niveles, contando la subcategoría más profunda de la rama.", new { maxLevel });

    public static Error CategoryCycle() => new("Inventory.Category.Cycle",
        "La categoría no puede quedar debajo de sí misma ni de una de sus subcategorías.");

    public static Error CategoryInUse(int children, int products) => new ErrorConDatos("Inventory.Category.InUse",
        $"La categoría tiene {children} subcategoría(s) y {products} producto(s) activo(s): muévalos o inactívelos antes.",
        new { children, products });

    // ------------------------------------------------------------------------ grupos contables --

    public static Error AccountingGroupInUse(int products) => new ErrorConDatos("Inventory.AccountingGroup.InUse",
        $"El grupo lo usan {products} producto(s) inventariable(s) activo(s): cámbieles el grupo antes de inactivarlo.", new { products });

    public static Error AccountingGroupInactive(string codigo) => new ErrorConDatos("Inventory.AccountingGroup.Inactive",
        $"El grupo contable {codigo} está inactivo.", new { accountingGroupCode = codigo });

    // -------------------------------------------------------------------------------- productos --

    /// <summary>I6 (T918): el vencimiento se controla por lote (data-model §1.6). (nuevo)</summary>
    public static Error ProductExpiryRequiresLot() => new("Inventory.Product.ExpiryRequiresLot",
        "El control por vencimiento va por lote: encienda también el control por lote.");

    /// <summary>I6 (T918): el seguimiento sólo tiene sentido en lo que tiene existencia propia (y en la plantilla, que lo hereda). (nuevo)</summary>
    public static Error ProductTrackingNotApplicable(ProductKind kind) => new ErrorConDatos("Inventory.Product.TrackingNotApplicable",
        $"Un producto de clase {kind} no maneja existencia propia: no se controla por lote, serie ni vencimiento.",
        new { kind = kind.ToString() });

    /// <summary>I6 (T918; data-model §1.6): las marcas de seguimiento no cambian con existencia ni con borradores que citen el producto. (nuevo)</summary>
    public static Error ProductTrackingLocked(bool hasStock, int drafts) => new ErrorConDatos("Inventory.Product.TrackingLocked",
        hasStock
            ? "El producto tiene existencia: el control por lote, serie o vencimiento no cambia hasta que quede en cero."
            : $"El producto está en {drafts} documento(s) en borrador o en aprobación: confírmelos o descártelos antes de cambiar el control por lote, serie o vencimiento.",
        new { hasStock, drafts });

    /// <summary>I6 (T918; data-model §1.6): la clase no cambia después del primer movimiento ni con variantes o componentes que dependan de ella. (nuevo)</summary>
    public static Error ProductKindLocked(ProductKind actual) => new ErrorConDatos("Inventory.Product.KindLocked",
        $"El producto ya tiene movimientos, variantes o componentes: su clase ({actual}) no cambia. Cree otro producto.",
        new { kind = actual.ToString() });

    public static Error ProductAccountingGroupRequired() => new("Inventory.Product.AccountingGroupRequired",
        "El producto necesita su grupo contable: con él la matriz de Contabilidad sabe qué cuentas usar.");

    /// <summary>Concepto de retención obligatorio salvo en plantillas y combos (data-model §1.6, FR-027). (nuevo)</summary>
    public static Error ProductWithholdingConceptRequired() => new("Inventory.Product.WithholdingConceptRequired",
        "El producto necesita su concepto de retención en compras (compras, servicios, honorarios…).");

    public static Error ProductBaseUnitLocked() => new("Inventory.Product.BaseUnitLocked",
        "El producto ya tiene movimientos: su unidad base no cambia (el kardex va en ella). Agregue otra unidad alterna.");

    public static Error ProductUseReclassifyAccountingGroup() => new("Inventory.Product.UseReclassifyAccountingGroup",
        "El producto ya tiene movimientos: el grupo contable se cambia con «Cambiar grupo contable», con fecha efectiva y motivo.");

    /// <summary>US1-3: con historia no se borra; se ofrece inactivar o bloquear.</summary>
    public static Error ProductHasHistory() => new ErrorConDatos("Inventory.Product.HasHistory",
        "El producto ya estuvo en documentos: no se borra. Inactívelo (no se ofrece en documentos nuevos) o bloquéelo (no admite movimientos).",
        new { alternatives = new[] { nameof(ProductStatus.Inactive), nameof(ProductStatus.Blocked) } });

    public static Error ProductStatusUnchanged(ProductStatus status) => new ErrorConDatos("Inventory.Product.StatusUnchanged",
        $"El producto ya está en estado {status}.", new { status = status.ToString() });

    /// <summary>§3.6.4 (US3, T288): el grupo pedido es el mismo que tiene.</summary>
    public static Error ProductAccountingGroupUnchanged(string accountingGroupCode) => new ErrorConDatos("Inventory.Product.AccountingGroupUnchanged",
        $"El producto ya está en el grupo contable {accountingGroupCode}.", new { accountingGroupCode });

    /// <summary>§3.6.4 (US3, T288): la fecha efectiva no puede dejar movimientos ya registrados en el grupo equivocado.</summary>
    public static Error ProductMovementsAfterEffectiveDate(DateOnly lastMovementDate) => new ErrorConDatos("Inventory.Product.MovementsAfterEffectiveDate",
        $"El producto tiene movimientos hasta el {lastMovementDate:yyyy-MM-dd}: la fecha efectiva del cambio de grupo debe ser ese día o después.",
        new { lastMovementDate });

    public static Error ProductNotInventoriable(string productCode) => new ErrorConDatos("Inventory.Product.NotInventoriable",
        $"El producto {productCode} no maneja existencias.", new { productCode });

    // ------------------------------------------------------------------------- unidades alternas --

    public static Error ProductUnitIsBaseUnit(string unitCode) => new ErrorConDatos("Inventory.ProductUnit.IsBaseUnit",
        $"La unidad {unitCode} es la base del producto (factor 1): no se agrega como alterna.", new { unitCode });

    public static Error ProductUnitDuplicate(string unitCode) => new ErrorConDatos("Inventory.ProductUnit.Duplicate",
        $"El producto ya tiene la unidad {unitCode}.", new { unitCode });

    public static Error ProductUnitFactorLocked(string unitCode) => new ErrorConDatos("Inventory.ProductUnit.FactorLocked",
        $"La unidad {unitCode} ya tiene movimientos: su factor no cambia (el kardex conserva el de cada línea). Cree otra unidad.",
        new { unitCode });

    public static Error ProductUnitInUse(string unitCode, string motivo) => new ErrorConDatos("Inventory.ProductUnit.InUse",
        $"La unidad {unitCode} no se puede retirar: {motivo}.", new { unitCode, reason = motivo });

    // -------------------------------------------------------------------------- códigos de barras --

    /// <summary>FR-024, US1-4: el código ya lo tiene otro producto vivo, y se nombra.</summary>
    public static Error BarcodeDuplicate(string barcode, Guid productPublicId, string productCode, string productName) => new ErrorConDatos(
        "Inventory.Barcode.Duplicate",
        $"El código de barras {barcode} ya lo tiene el producto {productCode} «{productName}».",
        new { productPublicId, productCode, productName });

    // -------------------------------------------------------------------------------- impuestos --

    public static Error ProductTaxVatRateRequired() => new("Inventory.ProductTax.VatRateRequired",
        "Un producto gravado lleva exactamente un IVA con su tarifa (19 %, 5 %…).");

    public static Error ProductTaxVatRateNotAllowed(VatSaleTreatment tratamiento) => new ErrorConDatos("Inventory.ProductTax.VatRateNotAllowed",
        $"Un producto {(tratamiento == VatSaleTreatment.Exempt ? "exento" : "excluido")} no lleva tarifa de IVA distinta de cero: el documento electrónico lo informa por su tratamiento.",
        new { vatSaleTreatment = tratamiento.ToString() });

    public static Error ProductTaxRateNotOfDefinition(string taxRateCode, string taxCode) => new ErrorConDatos("Inventory.ProductTax.RateNotOfDefinition",
        $"La tarifa {taxRateCode} no es del impuesto {taxCode}.", new { taxRateCode, taxCode });

    public static Error ProductTaxWithholdingNotAllowed(string taxCode) => new ErrorConDatos("Inventory.ProductTax.WithholdingNotAllowed",
        $"{taxCode} es una retención: no es impuesto del producto, sale de su concepto de retención.", new { taxCode });

    public static Error ProductTaxUnitsRequired(string taxCode) => new ErrorConDatos("Inventory.ProductTax.UnitsRequired",
        $"{taxCode} se cobra por unidad: indique cuántas unidades gravables tiene una unidad base (bolsa = 1).", new { taxCode });

    public static Error ProductTaxDuplicate(string taxCode) => new ErrorConDatos("Inventory.ProductTax.Duplicate",
        $"El impuesto {taxCode} está dos veces.", new { taxCode });

    // --------------------------------------------------------------------- causas y canales --

    public static Error AdjustmentCauseRequiredBySystem(string codigo) => new ErrorConDatos("Inventory.AdjustmentCause.RequiredBySystem",
        $"La causa {codigo} la usa el sistema (ajustes de conteo o diferencias de traslado): no se inactiva.", new { code = codigo });

    public static Error SalesChannelInUse(IReadOnlyList<string> usedBy) => new ErrorConDatos("Inventory.SalesChannel.InUse",
        $"El canal lo usan {string.Join(", ", usedBy)}: cámbielos antes de inactivarlo.", new { usedBy });

    // --------------------------------------------------------------------------- variantes (I6) --

    /// <summary>I6 (T918, T919): una variante nace de su plantilla. (nuevo)</summary>
    public static Error VariantParentRequired() => new("Inventory.Variant.ParentRequired",
        "Una variante nace de su plantilla: genérela desde la plantilla (Generar variantes) o, en la plantilla de productos, con sus filas en la hoja Variantes.");

    /// <summary>I6 (T919): se generan variantes sólo sobre una plantilla. (nuevo)</summary>
    public static Error VariantNotATemplate(string productCode) => new ErrorConDatos("Inventory.Variant.NotATemplate",
        $"El producto {productCode} no es una plantilla: sólo una plantilla tiene variantes.", new { productCode });

    /// <summary>I6 (T919, US15-1): la combinación ya existe en la plantilla. (nuevo)</summary>
    public static Error VariantCombinationExists(IReadOnlyList<string> variantKeys) => new ErrorConDatos("Inventory.Variant.CombinationExists",
        $"La plantilla ya tiene {(variantKeys.Count == 1 ? "la variante" : "las variantes")} {string.Join(", ", variantKeys)}: no se repiten.",
        new { variantKeys });

    public static Error VariantAttributeNotFound(string? codigo = null) => NoExiste("Inventory.VariantAttribute.NotFound", "El atributo de variante", codigo);

    /// <summary>I6 (T919): el valor no es de ese atributo. (nuevo)</summary>
    public static Error VariantAttributeValueNotFound(string attributeCode, string? valueCode = null) => new ErrorConDatos(
        "Inventory.VariantAttribute.ValueNotFound",
        valueCode is null ? $"Un valor elegido no es del atributo {attributeCode}." : $"El atributo {attributeCode} no tiene el valor «{valueCode}».",
        new { attributeCode, valueCode });

    /// <summary>I6 (T919): con un atributo inactivo no se generan variantes nuevas. (nuevo)</summary>
    public static Error VariantAttributeInactive(string attributeCode) => new ErrorConDatos("Inventory.VariantAttribute.Inactive",
        $"El atributo {attributeCode} está inactivo: actívelo para generar variantes con él.", new { attributeCode });

    /// <summary>I6 (T919): el código de un atributo o un valor que usan variantes no cambia ni se retira (está en su <c>VariantKey</c>). (nuevo)</summary>
    public static Error VariantAttributeInUse(string code, int variants) => new ErrorConDatos("Inventory.VariantAttribute.InUse",
        $"{code} lo usan {variants} variante(s): su código no cambia ni se retira. Puede cambiarle el nombre o inactivar el atributo.",
        new { code, variants });

    /// <summary>I6 (T919): el mismo valor dos veces en un atributo. (nuevo)</summary>
    public static Error VariantAttributeValueDuplicate(string valueCode) => new ErrorConDatos("Inventory.VariantAttribute.ValueDuplicate",
        $"El valor {valueCode} está dos veces en el atributo.", new { valueCode });

    // ------------------------------------------------------------------------ lotes y series (I6) --

    /// <summary>I6 (T921, T908): el producto controla lote y la entrada no lo trae. (nuevo)</summary>
    public static Error LotRequired(string productCode) => new ErrorConDatos("Inventory.Lot.Required",
        $"El producto {productCode} se controla por lote: indique el lote.", new { productCode });

    /// <summary>I6 (T921): el producto controla vencimiento y el lote no trae la fecha. (nuevo)</summary>
    public static Error LotExpiryRequired(string productCode) => new ErrorConDatos("Inventory.Lot.ExpiryRequired",
        $"El producto {productCode} controla vencimiento: indique la fecha de vencimiento del lote.", new { productCode });

    /// <summary>I6 (T921, T908): un mismo lote con otra fecha de vencimiento. (nuevo)</summary>
    public static Error LotExpiryMismatch(string productCode, string lotCode, DateOnly? expiryDate) => new ErrorConDatos("Inventory.Lot.ExpiryMismatch",
        expiryDate is { } f
            ? $"El lote {lotCode} del producto {productCode} ya existe y vence el {f:yyyy-MM-dd}: un lote tiene una sola fecha de vencimiento."
            : $"El lote {lotCode} del producto {productCode} ya existe sin fecha de vencimiento: un lote tiene una sola fecha.",
        new { productCode, lotCode, expiryDate });

    /// <summary>I6 (T921): el producto no se controla por lote. (nuevo)</summary>
    public static Error LotNotTracked(string productCode) => new ErrorConDatos("Inventory.Lot.NotTracked",
        $"El producto {productCode} no se controla por lote: deje vacíos el lote y el vencimiento.", new { productCode });

    /// <summary>I6 (T921): el producto controla serie y la entrada no la trae. (nuevo)</summary>
    public static Error SerialRequired(string productCode) => new ErrorConDatos("Inventory.Serial.Required",
        $"El producto {productCode} se controla por serie: indique la serie de la unidad.", new { productCode });

    /// <summary>I6 (T921): el producto no se controla por serie. (nuevo)</summary>
    public static Error SerialNotTracked(string productCode) => new ErrorConDatos("Inventory.Serial.NotTracked",
        $"El producto {productCode} no se controla por serie: deje vacía la serie.", new { productCode });

    /// <summary>I6 (T921, T908): una serie es una unidad: una línea por serie. (nuevo)</summary>
    public static Error SerialQuantityNotOne(string productCode, string serialNumber) => new ErrorConDatos("Inventory.Serial.QuantityNotOne",
        $"La serie {serialNumber} del producto {productCode} es una unidad: la cantidad es 1 (una línea por serie).",
        new { productCode, serialNumber });

    /// <summary>US15-5 (data-model §13): una entrada con una serie que ya está en existencia. (nuevo)</summary>
    public static Error SerialAlreadyInStock(string productCode, string serialNumber) => new ErrorConDatos("Inventory.Serial.AlreadyInStock",
        $"La serie {serialNumber} del producto {productCode} ya está en existencia: no entra dos veces.", new { productCode, serialNumber });

    private static Error NoExiste(string codigo, string que, string? cual) =>
        new(codigo, cual is null ? $"{que} no existe." : $"{que} «{cual}» no existe.");
}
