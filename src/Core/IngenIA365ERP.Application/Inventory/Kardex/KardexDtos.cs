using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Kardex;

// Los DTO de existencias, kardex e integridad (feature 012, T256, T258; contracts/api.md §5, §6.2). Sólo PublicId hacia
// afuera (Principio VI). Los valores (costo promedio, valor, estado de costo) salen nulos sin Inventory.Costs.Read.

/// <summary>El producto de una fila de existencias. (nuevo)</summary>
public sealed record StockProductRefDto(Guid PublicId, string Code, string Name, string BaseUnitCode, ProductStatus Status);

/// <summary>La bodega de una fila de existencias. (nuevo)</summary>
public sealed record StockWarehouseRefDto(Guid PublicId, string Code, string Name, bool IsTransit);

/// <summary>Una ubicación de bodega. (nuevo)</summary>
public sealed record StockLocationRefDto(Guid PublicId, string Code);

/// <summary><c>StockRowDto</c> (§5): existencia de un producto en una bodega, en unidad base. (nuevo)</summary>
public sealed record StockRowDto(
    StockProductRefDto Product,
    StockWarehouseRefDto Warehouse,
    decimal Physical,
    decimal Reserved,
    decimal Available,
    decimal InTransitTo,
    decimal? AverageCost,
    decimal? Value,
    decimal? Minimum,
    decimal? ReorderPoint,
    decimal? Maximum,
    decimal? Position);

/// <summary>Los totales del producto en el alcance (§5). (nuevo)</summary>
public sealed record ProductStockTotalsDto(decimal Physical, decimal Reserved, decimal Available, decimal InTransit, decimal? Value);

/// <summary>La existencia del producto en una bodega. (nuevo)</summary>
public sealed record ProductStockByWarehouseDto(StockWarehouseRefDto Warehouse, decimal Physical, decimal Reserved, decimal Available, decimal InTransitTo, decimal? Value);

/// <summary>La existencia del producto por ubicación y lote. (nuevo)</summary>
public sealed record ProductStockByLocationDto(StockWarehouseRefDto Warehouse, StockLocationRefDto Location, string? Lot, decimal Quantity);

/// <summary>Un despacho con algo del producto todavía en tránsito. (nuevo)</summary>
public sealed record ProductStockInTransitDto(Guid TransferPublicId, string? DispatchNumber, StockWarehouseRefDto? From, StockWarehouseRefDto To, decimal Quantity, DateOnly DispatchedOn);

/// <summary>El estado de costo del producto: el del ámbito cooperativa, o la suma de las bodegas visibles en ámbito bodega. (nuevo)</summary>
public sealed record ProductCostStateDto(CostScope Scope, CostMethod Method, decimal Quantity, decimal AverageCost, decimal LastUnitCost, decimal Value);

/// <summary><c>ProductStockDto</c> (§5, <c>GET /stock/{productId}</c>). (nuevo)</summary>
public sealed record ProductStockDto(
    StockProductRefDto Product,
    ProductStockTotalsDto Totals,
    IReadOnlyList<ProductStockByWarehouseDto> ByWarehouse,
    IReadOnlyList<ProductStockByLocationDto> ByLocation,
    IReadOnlyList<ProductStockInTransitDto> InTransit,
    ProductCostStateDto? CostState);

// ------------------------------------------------------------------------------------------- integridad --

/// <summary>Qué proyección tiene la diferencia (§6.2; viaja por nombre). (nuevo)</summary>
public static class TiposDeIncidente
{
    public const string StockBalance = nameof(StockBalance);
    public const string StockDetail = nameof(StockDetail);
    public const string CostState = nameof(CostState);
    public const string CostLayer = nameof(CostLayer);
}

/// <summary>Una referencia corta de un producto o una bodega. (nuevo)</summary>
public sealed record IntegrityRefDto(Guid PublicId, string Code);

/// <summary>Una diferencia entre el kardex y una proyección (§6.2). (nuevo)</summary>
public sealed record IntegrityIncidentDto(
    string Kind,
    IntegrityRefDto Product,
    IntegrityRefDto? Warehouse,
    IntegrityRefDto? Location,
    string? Lot,
    string Field,
    decimal Expected,
    decimal Actual,
    decimal Difference);

/// <summary>Cuántas filas se revisaron de cada proyección. (nuevo)</summary>
public sealed record IntegrityCheckedDto(int StockBalances, int StockDetails, int CostStates, int CostLayers);

/// <summary><c>IntegrityReportDto</c> (§6.2). (nuevo)</summary>
public sealed record IntegrityReportDto(DateTime VerifiedAt, IntegrityCheckedDto Checked, IReadOnlyList<IntegrityIncidentDto> Incidents, Guid? AlertPublicId);

/// <summary>Una corrección de la reconstrucción: el valor antes y después. (nuevo)</summary>
public sealed record RebuildCorrectionDto(string Kind, IntegrityRefDto Product, IntegrityRefDto? Warehouse, string Field, decimal Before, decimal After);

/// <summary>Cuántas filas quedaron en cada proyección tras reconstruir. (nuevo)</summary>
public sealed record RebuildRowsDto(int StockBalances, int StockDetails, int CostStates, int CostLayers);

/// <summary>El resultado de <c>POST /integrity/rebuild</c> (§6.2). (nuevo)</summary>
public sealed record RebuildResultDto(DateTime RebuiltAt, RebuildRowsDto Rows, IReadOnlyList<RebuildCorrectionDto> Corrected);
