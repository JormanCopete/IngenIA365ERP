namespace IngenIA365ERP.Shared.Services.Inventario;

// DTO espejo del catálogo y de las bodegas (feature 012, T232; contracts/api.md §3 y §4). Mismos nombres de propiedad que la
// API; los enums llegan como número (`ProductKind`, `ProductStatus`, `VatSaleTreatment`, `ProductUnitUsage`,
// `WarehouseBehavior`, `WarehouseActivationStatus`) y se traducen en `TextosDeInventario`. (nuevo)

// ------------------------------------------------------------------------------------------ catálogos simples --

/// <summary>Unidad de medida (§3.1).</summary>
public sealed record UnidadDeMedidaDto(Guid PublicId, string Code, string Name, string? Symbol, int AllowedDecimals, string? DianUnitCode,
    bool IsSeeded, bool IsActive, int ProductsUsing);

/// <summary>Categoría (§3.2): <c>Path</c> legible («ABARROTES › GRANOS»).</summary>
public sealed record CategoriaDto(Guid PublicId, string Code, string Name, Guid? ParentPublicId, int Level, string Path, bool IsActive, int Products);

/// <summary>Marca (§3.3).</summary>
public sealed record MarcaDto(Guid PublicId, string Code, string Name, bool IsActive, int Products);

/// <summary>Grupo contable (§3.4).</summary>
public sealed record GrupoContableDto(Guid PublicId, string Code, string Name, string? Description, bool IsActive, int Products);

/// <summary>Canal de venta (§3.8).</summary>
public sealed record CanalDeVentaDto(Guid PublicId, string Code, string Name, bool IsActive);

/// <summary>Causa de ajuste (§3.7).</summary>
public sealed record CausaDeAjusteDto(Guid PublicId, string Code, string Name, bool AllowsPositive, bool AllowsNegative, bool AllowsTransitWriteOff,
    bool RequiresAttachment, bool IsSeeded, bool IsRequiredBySystem, bool IsActive);

public sealed record UnidadRequest(string? Code, string Name, string? Symbol, int AllowedDecimals, string? DianUnitCode);

public sealed record CategoriaRequest(string? Code, string Name, Guid? ParentPublicId);

public sealed record CodigoYNombreRequest(string? Code, string Name);

public sealed record GrupoContableRequest(string? Code, string Name, string? Description);

public sealed record CausaDeAjusteRequest(string? Code, string Name, bool AllowsPositive, bool AllowsNegative, bool AllowsTransitWriteOff, bool RequiresAttachment);

// -------------------------------------------------------------------------------------------------- productos --

/// <summary>Una referencia de catálogo con su ruta (la categoría del producto).</summary>
public sealed record CategoriaDelProductoDto(Guid PublicId, string Code, string Name, string Path);

/// <summary>Una unidad dentro del producto.</summary>
public sealed record UnidadDelProductoDto(Guid PublicId, string Code, string Name, int AllowedDecimals);

/// <summary><c>ProductListItemDto</c> (§3.5).</summary>
public sealed record ProductoDeListaDto(Guid PublicId, string Code, string Name, int Kind, int Status, ReferenciaDeInventarioDto Category,
    ReferenciaDeInventarioDto? Brand, string BaseUnitCode, string? AccountingGroupCode, string? PrimaryBarcode, bool HasMovements);

/// <summary>Una unidad alterna (§3.6.1).</summary>
public sealed record UnidadAlternaDto(Guid ProductUnitPublicId, UnidadDelProductoDto Unit, decimal Factor, int Usage, bool IsDefaultPurchase, bool IsDefaultSale);

/// <summary>Un código de barras (§3.6.2).</summary>
public sealed record CodigoDeBarrasDto(Guid BarcodePublicId, string Barcode, Guid? ProductUnitPublicId, string UnitCode, bool IsPrimary);

public sealed record DefinicionDeImpuestoRefDto(Guid PublicId, string Code, string Name, int Kind);

public sealed record TarifaRefDto(Guid PublicId, string Code, decimal? Rate, decimal? AmountPerUnit, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Un impuesto del producto (§3.6.3).</summary>
public sealed record ImpuestoDelProductoDto(DefinicionDeImpuestoRefDto TaxDefinition, TarifaRefDto? TaxRate, decimal? TaxableUnitsPerBaseUnit);

/// <summary><c>ProductTaxesDto</c> (§3.6.3).</summary>
public sealed record ImpuestosDelProductoDto(int VatSaleTreatment, ReferenciaDeInventarioDto? WithholdingConcept, IReadOnlyList<ImpuestoDelProductoDto> Taxes);

public sealed record ImagenDelProductoDto(Guid AttachmentPublicId, string FileName);

/// <summary><c>ProductDto</c> (§3.5).</summary>
public sealed record ProductoDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string? ShortName { get; init; }
    public string? Description { get; init; }
    public int Kind { get; init; }
    public int Status { get; init; }
    public CategoriaDelProductoDto Category { get; init; } = new(Guid.Empty, "", "", "");
    public ReferenciaDeInventarioDto? Brand { get; init; }
    public UnidadDelProductoDto BaseUnit { get; init; } = new(Guid.Empty, "", "", 0);
    public ReferenciaDeInventarioDto? AccountingGroup { get; init; }
    public int VatSaleTreatment { get; init; }
    public ReferenciaDeInventarioDto? WithholdingConcept { get; init; }
    public string? Reference { get; init; }
    public decimal? Weight { get; init; }
    public decimal? Volume { get; init; }
    public bool TracksLot { get; init; }
    public bool TracksSerial { get; init; }
    public bool TracksExpiry { get; init; }
    public bool IsPurchasable { get; init; } = true;
    public bool IsSellable { get; init; } = true;
    public IReadOnlyList<UnidadAlternaDto> Units { get; init; } = [];
    public IReadOnlyList<CodigoDeBarrasDto> Barcodes { get; init; } = [];
    public IReadOnlyList<ImpuestoDelProductoDto> Taxes { get; init; } = [];
    public IReadOnlyList<ImagenDelProductoDto> Images { get; init; } = [];
    public bool HasMovements { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>El empaque que identifica el código leído (US1-5).</summary>
public sealed record EmpaqueDto(Guid ProductUnitPublicId, string UnitCode, decimal Factor);

/// <summary><c>ProductSearchItemDto</c> (§3.5, búsqueda).</summary>
public sealed record ProductoEncontradoDto(Guid PublicId, string Code, string Name, string? Reference, string? BrandName, string BaseUnitCode, int Status,
    string? MatchedBarcode, EmpaqueDto? PackUnit, decimal? Available);

/// <summary>La respuesta de la búsqueda: la lectura exacta y los resultados mientras se escribe.</summary>
public sealed record BusquedaDeProductosDto(ProductoEncontradoDto? Exact, IReadOnlyList<ProductoEncontradoDto> Items);

public sealed record UnidadDelAltaRequest(Guid UnitPublicId, decimal Factor, int Usage);

public sealed record CodigoDelAltaRequest(string Barcode, Guid? UnitPublicId);

public sealed record ImpuestoRequest(Guid TaxDefinitionPublicId, Guid? TaxRatePublicId, decimal? TaxableUnitsPerBaseUnit);

/// <summary><c>CreateProductRequest</c> (§3.5).</summary>
public sealed record CrearProductoRequest(
    string Code, string Name, string? ShortName, string? Description, int Kind, Guid CategoryPublicId, Guid? BrandPublicId, Guid BaseUnitPublicId,
    Guid? AccountingGroupPublicId, int VatSaleTreatment, Guid? WithholdingConceptPublicId, string? Reference, decimal? Weight, decimal? Volume,
    bool TracksLot, bool TracksSerial, bool TracksExpiry, IReadOnlyList<UnidadDelAltaRequest>? Units, IReadOnlyList<CodigoDelAltaRequest>? Barcodes,
    IReadOnlyList<ImpuestoRequest>? Taxes, bool IsPurchasable = true, bool IsSellable = true);

/// <summary><c>UpdateProductRequest</c> (§3.5).</summary>
public sealed record EditarProductoRequest(
    string Name, string? ShortName, string? Description, Guid CategoryPublicId, Guid? BrandPublicId, Guid BaseUnitPublicId,
    Guid? AccountingGroupPublicId, int VatSaleTreatment, Guid? WithholdingConceptPublicId, string? Reference, decimal? Weight, decimal? Volume,
    bool TracksLot, bool TracksSerial, bool TracksExpiry, bool IsPurchasable = true, bool IsSellable = true);

public sealed record EstadoDeProductoRequest(int Status, string Reason);

public sealed record UnidadAlternaRequest(Guid UnitPublicId, decimal Factor, int Usage, bool? IsDefaultPurchase = null, bool? IsDefaultSale = null);

public sealed record CodigoDeBarrasRequest(string Barcode, Guid? ProductUnitPublicId);

public sealed record ImpuestosDelProductoRequest(int VatSaleTreatment, Guid? WithholdingConceptPublicId, IReadOnlyList<ImpuestoRequest> Taxes);

// ---------------------------------------------------------------------------------------------------- bodegas --

/// <summary>Tipo de bodega (§4.1). <c>Behavior</c>: 1 operativa, 2 tránsito.</summary>
public sealed record TipoDeBodegaDto(Guid PublicId, string Code, string Name, int Behavior, bool IsSeeded, bool IsActive);

public sealed record SucursalDeBodegaDto(Guid PublicId, string? Code, string Name);

public sealed record TipoDeLaBodegaDto(Guid PublicId, string Code, string Name, int Behavior);

/// <summary>Una ubicación (§4.3).</summary>
public sealed record UbicacionDto(Guid PublicId, string Code, string Name, bool IsDefault, bool IsActive);

public sealed record SaldoInicialDeBodegaDto(Guid DocumentPublicId, string? Number, int Status, DateOnly? CutoffDate);

/// <summary><c>WarehouseDto</c> (§4.2). <c>ActivationStatus</c>: 0 no activa, 1 activa.</summary>
public sealed record BodegaDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public SucursalDeBodegaDto Branch { get; init; } = new(Guid.Empty, null, "");
    public TipoDeLaBodegaDto Type { get; init; } = new(Guid.Empty, "", "", 1);
    public bool IsTransit { get; init; }
    public ReferenciaDeInventarioDto? TransitWarehouse { get; init; }
    public int ActivationStatus { get; init; }
    public DateOnly? CutoffDate { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public string? ActivatedBy { get; init; }
    public string? Address { get; init; }
    public bool IsActive { get; init; }
    public bool IsDefault { get; init; }
    public bool NegativeStockAllowed { get; init; }
    public IReadOnlyList<UbicacionDto>? Locations { get; init; }
    public SaldoInicialDeBodegaDto? OpeningBalance { get; init; }
}

/// <summary>La respuesta del alta (§4.2).</summary>
public sealed record AltaDeBodegaDto(BodegaDto Warehouse, ReferenciaDeInventarioDto? TransitWarehouseCreated, IReadOnlyList<AvisoDeInventarioDto> Warnings);

public sealed record TransitoRequest(string Code, string? Name);

public sealed record CrearBodegaRequest(string Code, string Name, Guid BranchPublicId, Guid WarehouseTypePublicId, string? Notes, TransitoRequest? TransitWarehouse);

public sealed record EditarBodegaRequest(string Name, Guid WarehouseTypePublicId, string? Notes);

public sealed record TipoDeBodegaRequest(string? Code, string Name, int? Behavior);

public sealed record UbicacionRequest(string? Code, string Name, bool IsDefault);

/// <summary>Una política de reorden (§4.4): posición y disponible sólo con <c>Inventory.Stock.View</c>.</summary>
public sealed record PoliticaDeReordenDto(Guid PublicId, ReferenciaDeInventarioDto Product, ReferenciaDeInventarioDto Warehouse, decimal Minimum, decimal Maximum,
    decimal ReorderPoint, decimal? Position, decimal? Available);

public sealed record PoliticaDeReordenRequest(Guid ProductPublicId, Guid WarehousePublicId, decimal Minimum, decimal Maximum, decimal ReorderPoint);

/// <summary>Una sucursal contable para elegir la de una bodega (<c>GET /api/core/branches</c>).</summary>
public sealed record SucursalContableDto(Guid PublicId, string? Code, string Name);

/// <summary>Lo que devuelve <c>SelectorDeProducto</c>: el producto y, si se leyó el código de un empaque, su unidad y factor.</summary>
public sealed record ProductoElegido(ProductoEncontradoDto Producto, EmpaqueDto? Empaque);
