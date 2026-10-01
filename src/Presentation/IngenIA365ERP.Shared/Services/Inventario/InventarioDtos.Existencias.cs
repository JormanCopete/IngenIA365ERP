namespace IngenIA365ERP.Shared.Services.Inventario;

// DTO espejo de existencias e integridad (feature 012, T264; contracts/api.md §5, §6.2). Los enums llegan como número; los
// valores (costo promedio, valor, estado de costo) llegan nulos sin Inventory.Costs.Read.

/// <summary>El producto de una fila de existencias. <see cref="Status"/>: 1 activo, 2 inactivo, 3 bloqueado.</summary>
public sealed record ProductoDeExistenciaDto(Guid PublicId, string Code, string Name, string BaseUnitCode, int Status);

/// <summary>La bodega de una fila de existencias.</summary>
public sealed record BodegaDeExistenciaDto(Guid PublicId, string Code, string Name, bool IsTransit);

/// <summary>Una ubicación.</summary>
public sealed record UbicacionDeExistenciaDto(Guid PublicId, string Code);

/// <summary><c>StockRowDto</c> (§5).</summary>
public sealed record FilaDeExistenciaDto(
    ProductoDeExistenciaDto Product,
    BodegaDeExistenciaDto Warehouse,
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

/// <summary>Los filtros de <c>GET /api/inventory/stock</c>.</summary>
public sealed record FiltroDeExistencias
{
    public Guid? WarehousePublicId { get; init; }
    public Guid? ProductPublicId { get; init; }
    public Guid? CategoryPublicId { get; init; }
    public Guid? LocationPublicId { get; init; }
    public string? Search { get; init; }
    public bool OnlyWithStock { get; init; }
    public bool BelowReorderPoint { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>Los totales del producto en el alcance.</summary>
public sealed record TotalesDeExistenciaDto(decimal Physical, decimal Reserved, decimal Available, decimal InTransit, decimal? Value);

/// <summary>El producto en una bodega.</summary>
public sealed record ExistenciaPorBodegaDto(BodegaDeExistenciaDto Warehouse, decimal Physical, decimal Reserved, decimal Available, decimal InTransitTo, decimal? Value);

/// <summary>El producto por ubicación y lote.</summary>
public sealed record ExistenciaPorUbicacionDto(BodegaDeExistenciaDto Warehouse, UbicacionDeExistenciaDto Location, string? Lot, decimal Quantity);

/// <summary>Un despacho con algo del producto en tránsito.</summary>
public sealed record ExistenciaEnTransitoDto(Guid TransferPublicId, string? DispatchNumber, BodegaDeExistenciaDto? From, BodegaDeExistenciaDto To, decimal Quantity, DateOnly DispatchedOn);

/// <summary>El estado de costo. <see cref="Scope"/>: 1 cooperativa, 2 bodega; <see cref="Method"/>: 1 promedio ponderado, 2 PEPS.</summary>
public sealed record EstadoDeCostoDto(int Scope, int Method, decimal Quantity, decimal AverageCost, decimal LastUnitCost, decimal Value);

/// <summary><c>ProductStockDto</c> (§5, <c>GET /stock/{productId}</c>).</summary>
public sealed record ExistenciaDelProductoDto(
    ProductoDeExistenciaDto Product,
    TotalesDeExistenciaDto Totals,
    IReadOnlyList<ExistenciaPorBodegaDto> ByWarehouse,
    IReadOnlyList<ExistenciaPorUbicacionDto> ByLocation,
    IReadOnlyList<ExistenciaEnTransitoDto> InTransit,
    EstadoDeCostoDto? CostState);

// ------------------------------------------------------------------------------------------- integridad --

/// <summary>Una referencia corta (producto, bodega, ubicación).</summary>
public sealed record ReferenciaDeIntegridadDto(Guid PublicId, string Code);

/// <summary>Una diferencia entre el kardex y una proyección. <see cref="Kind"/>: StockBalance, StockDetail, CostState o CostLayer.</summary>
public sealed record IncidenteDeIntegridadDto(
    string Kind,
    ReferenciaDeIntegridadDto Product,
    ReferenciaDeIntegridadDto? Warehouse,
    ReferenciaDeIntegridadDto? Location,
    string? Lot,
    string Field,
    decimal Expected,
    decimal Actual,
    decimal Difference);

/// <summary>Cuántas filas se revisaron.</summary>
public sealed record RevisadasDeIntegridadDto(int StockBalances, int StockDetails, int CostStates, int CostLayers);

/// <summary><c>IntegrityReportDto</c> (§6.2).</summary>
public sealed record InformeDeIntegridadDto(DateTime VerifiedAt, RevisadasDeIntegridadDto Checked, IReadOnlyList<IncidenteDeIntegridadDto> Incidents, Guid? AlertPublicId);

/// <summary>Una corrección de la reconstrucción.</summary>
public sealed record CorreccionDeIntegridadDto(string Kind, ReferenciaDeIntegridadDto Product, ReferenciaDeIntegridadDto? Warehouse, string Field, decimal Before, decimal After);

/// <summary>Las filas que quedaron por proyección.</summary>
public sealed record FilasReconstruidasDto(int StockBalances, int StockDetails, int CostStates, int CostLayers);

/// <summary>El resultado de reconstruir (§6.2).</summary>
public sealed record ResultadoDeReconstruccionDto(DateTime RebuiltAt, FilasReconstruidasDto Rows, IReadOnlyList<CorreccionDeIntegridadDto> Corrected);

/// <summary>El cuerpo de verificar: vacío = todo lo del alcance.</summary>
public sealed record VerificarIntegridadRequest(IReadOnlyList<Guid>? ProductPublicIds, IReadOnlyList<Guid>? WarehousePublicIds);

/// <summary>El cuerpo de reconstruir, con motivo.</summary>
public sealed record ReconstruirIntegridadRequest(IReadOnlyList<Guid>? ProductPublicIds, IReadOnlyList<Guid>? WarehousePublicIds, string Reason);

// ---------------------------------------------------------------------------------------------- ajustes --

/// <summary>Una diferencia de costo que dejó la anulación (sólo con <c>Inventory.Costs.Read</c>).</summary>
public sealed record AjusteDeCostoDeAnulacionDto(ReferenciaDeInventarioDto Product, decimal Difference);
