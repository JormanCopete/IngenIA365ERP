using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Producto (<c>INV_Products</c>; feature 012, T200; FR-020, FR-023 a FR-030; data-model §1.6). El kardex va siempre en
/// <see cref="BaseUnitId"/>, que no cambia con movimientos; el grupo contable cambia sólo por
/// <c>ChangeProductAccountingGroupCommand</c> cuando hay movimientos. <see cref="SearchText"/> es el texto normalizado
/// (<c>NormalizadorDeBusqueda</c>) de código, nombre, nombre corto, referencia, marca y códigos de barras vivos que
/// recorre la búsqueda mientras se escribe (T43). Las imágenes son adjuntos del dueño <c>InventoryProduct</c> (sin
/// columna). <c>ParentProductId</c> y <c>VariantKey</c> llegan con I6.
/// </summary>
public class Product : AuditableEntity
{
    public const int LargoDeTextoDeBusqueda = 400;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary><b>(nuevo)</b> Nombre para la tirilla de 80 mm; vacío = <see cref="Name"/> recortado.</summary>
    public string? ShortName { get; set; }

    public string? Description { get; set; }

    /// <summary>En I1 sólo <see cref="ProductKind.Inventoriable"/> y <see cref="ProductKind.Service"/>.</summary>
    public ProductKind Kind { get; set; } = ProductKind.Inventoriable;

    public ProductStatus Status { get; set; } = ProductStatus.Active;

    public int CategoryId { get; set; }

    public ProductCategory? Category { get; set; }

    public int? BrandId { get; set; }

    public Brand? Brand { get; set; }

    public int BaseUnitId { get; set; }

    public UnitOfMeasure? BaseUnit { get; set; }

    public int? AccountingGroupId { get; set; }

    public AccountingGroup? AccountingGroup { get; set; }

    public VatSaleTreatment VatSaleTreatment { get; set; } = VatSaleTreatment.Taxed;

    /// <summary>Concepto de retención en compras (FR-027) → <c>COR_WithholdingConcepts</c>.</summary>
    public int? WithholdingConceptId { get; set; }

    public WithholdingConcept? WithholdingConcept { get; set; }

    public string? Reference { get; set; }

    /// <summary>Kg por unidad base (prorrateo por peso, I5).</summary>
    public decimal? Weight { get; set; }

    /// <summary>Litros por unidad base (prorrateo por volumen, I5).</summary>
    public decimal? Volume { get; set; }

    public bool TracksLot { get; set; }

    public bool TracksSerial { get; set; }

    public bool TracksExpiry { get; set; }

    /// <summary><b>(nuevo)</b> Un flete es sólo de compra; el POS no ofrece lo que no se vende.</summary>
    public bool IsPurchasable { get; set; } = true;

    /// <summary><b>(nuevo)</b></summary>
    public bool IsSellable { get; set; } = true;

    public string SearchText { get; set; } = string.Empty;

    public ICollection<ProductUnit> Units { get; set; } = new List<ProductUnit>();

    public ICollection<ProductBarcode> Barcodes { get; set; } = new List<ProductBarcode>();

    public ICollection<ProductTax> Taxes { get; set; } = new List<ProductTax>();

    /// <summary>Maneja existencias: inventariable o variante (un servicio nunca produce kardex; un combo no tiene existencia propia).</summary>
    public bool EsInventariable => Kind is ProductKind.Inventoriable or ProductKind.Variant;
}
