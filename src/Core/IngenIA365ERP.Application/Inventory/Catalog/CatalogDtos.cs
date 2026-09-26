using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Catalog;

// Las formas del catálogo tal como las ve la API (feature 012, T212–T220; contracts/api.md §3). Sólo PublicId hacia
// afuera (Principio VI); los enums salen como número.

/// <summary>Una referencia de catálogo: <c>{ publicId, code, name }</c>.</summary>
public sealed record CatalogRefDto(Guid PublicId, string Code, string Name);

/// <summary><c>UnitOfMeasureDto</c> (§3.1). <see cref="ProductsUsing"/> cuenta los productos vivos que la usan como base o alterna.</summary>
public sealed record UnitOfMeasureDto(
    Guid PublicId, string Code, string Name, string? Symbol, int AllowedDecimals, string? DianUnitCode, bool IsSeeded, bool IsActive, int ProductsUsing);

/// <summary><c>CategoryDto</c> (§3.2). <see cref="Path"/> es legible («ABARROTES › GRANOS»); <see cref="Products"/>, los productos vivos.</summary>
public sealed record CategoryDto(
    Guid PublicId, string Code, string Name, Guid? ParentPublicId, int Level, string Path, bool IsActive, int Products);

/// <summary>Marca (§3.3).</summary>
public sealed record BrandDto(Guid PublicId, string Code, string Name, bool IsActive, int Products);

/// <summary>Grupo contable (§3.4).</summary>
public sealed record AccountingGroupDto(Guid PublicId, string Code, string Name, string? Description, bool IsActive, int Products);

/// <summary>Canal de venta (§3.8).</summary>
public sealed record SalesChannelDto(Guid PublicId, string Code, string Name, bool IsActive);

/// <summary>Causa de ajuste (§3.7).</summary>
public sealed record AdjustmentCauseDto(
    Guid PublicId, string Code, string Name, bool AllowsPositive, bool AllowsNegative, bool AllowsTransitWriteOff,
    bool RequiresAttachment, bool IsSeeded, bool IsRequiredBySystem, bool IsActive);

/// <summary>La categoría del producto con su ruta legible.</summary>
public sealed record ProductCategoryRefDto(Guid PublicId, string Code, string Name, string Path);

/// <summary>Una unidad de medida dentro del producto.</summary>
public sealed record UnitRefDto(Guid PublicId, string Code, string Name, int AllowedDecimals);

/// <summary><c>ProductListItemDto</c> (§3.5), por código.</summary>
public sealed record ProductListItemDto(
    Guid PublicId, string Code, string Name, ProductKind Kind, ProductStatus Status, CatalogRefDto Category, CatalogRefDto? Brand,
    string BaseUnitCode, string? AccountingGroupCode, string? PrimaryBarcode, bool HasMovements);

/// <summary>Una unidad alterna (§3.6.1): el factor convierte a la base (caja × 12 = 12).</summary>
public sealed record ProductUnitDto(
    Guid ProductUnitPublicId, UnitRefDto Unit, decimal Factor, ProductUnitUsage Usage, bool IsDefaultPurchase, bool IsDefaultSale);

/// <summary>Un código de barras (§3.6.2): el empaque que identifica (nulo = la base) y su unidad.</summary>
public sealed record ProductBarcodeDto(Guid BarcodePublicId, string Barcode, Guid? ProductUnitPublicId, string UnitCode, bool IsPrimary);

/// <summary>La definición de un impuesto del producto.</summary>
public sealed record TaxDefinitionRefDto(Guid PublicId, string Code, string Name, TaxKind Kind);

/// <summary>La tarifa vigente hoy del código guardado en el producto.</summary>
public sealed record TaxRateRefDto(Guid PublicId, string Code, decimal? Rate, decimal? AmountPerUnit, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Un impuesto del producto (§3.6.3).</summary>
public sealed record ProductTaxDto(TaxDefinitionRefDto TaxDefinition, TaxRateRefDto? TaxRate, decimal? TaxableUnitsPerBaseUnit);

/// <summary><c>ProductTaxesDto</c> (§3.6.3): tratamiento de IVA, concepto de retención y los impuestos.</summary>
public sealed record ProductTaxesDto(VatSaleTreatment VatSaleTreatment, CatalogRefDto? WithholdingConcept, IReadOnlyList<ProductTaxDto> Taxes);

/// <summary>Una imagen del producto: un adjunto del dueño <c>InventoryProduct</c> (§3.6.5).</summary>
public sealed record ProductImageDto(Guid AttachmentPublicId, string FileName);

/// <summary><c>ProductDto</c> (§3.5).</summary>
public sealed record ProductDto(
    Guid PublicId,
    string Code,
    string Name,
    string? ShortName,
    string? Description,
    ProductKind Kind,
    ProductStatus Status,
    ProductCategoryRefDto Category,
    CatalogRefDto? Brand,
    UnitRefDto BaseUnit,
    CatalogRefDto? AccountingGroup,
    VatSaleTreatment VatSaleTreatment,
    CatalogRefDto? WithholdingConcept,
    string? Reference,
    decimal? Weight,
    decimal? Volume,
    bool TracksLot,
    bool TracksSerial,
    bool TracksExpiry,
    bool IsPurchasable,
    bool IsSellable,
    IReadOnlyList<ProductUnitDto> Units,
    IReadOnlyList<ProductBarcodeDto> Barcodes,
    IReadOnlyList<ProductTaxDto> Taxes,
    IReadOnlyList<ProductImageDto> Images,
    bool HasMovements,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt);

/// <summary>El empaque que identifica el código leído: su unidad alterna y el factor (US1-5).</summary>
public sealed record PackUnitDto(Guid ProductUnitPublicId, string UnitCode, decimal Factor);

/// <summary><c>ProductSearchItemDto</c> (§3.5, búsqueda).</summary>
public sealed record ProductSearchItemDto(
    Guid PublicId, string Code, string Name, string? Reference, string? BrandName, string BaseUnitCode, ProductStatus Status,
    string? MatchedBarcode, PackUnitDto? PackUnit, decimal? Available);

/// <summary>La respuesta de la búsqueda: la lectura exacta (código de barras o código) y los resultados mientras se escribe.</summary>
public sealed record ProductSearchResultDto(ProductSearchItemDto? Exact, IReadOnlyList<ProductSearchItemDto> Items);
