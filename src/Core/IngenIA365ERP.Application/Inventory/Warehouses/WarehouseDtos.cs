using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

// Las formas de bodegas, ubicaciones y políticas de reorden (feature 012, T222–T225; contracts/api.md §4). Sólo PublicId.

/// <summary>Tipo de bodega (§4.1).</summary>
public sealed record WarehouseTypeDto(Guid PublicId, string Code, string Name, WarehouseBehavior Behavior, bool IsSeeded, bool IsActive);

/// <summary>La sucursal de una bodega; su código es el de la 009 (<c>LegacyCode</c>, opcional).</summary>
public sealed record BranchRefDto(Guid PublicId, string? Code, string Name);

/// <summary>El tipo de una bodega con su comportamiento.</summary>
public sealed record WarehouseTypeRefDto(Guid PublicId, string Code, string Name, WarehouseBehavior Behavior);

/// <summary>Una bodega por su código.</summary>
public sealed record WarehouseRefDto(Guid PublicId, string Code, string Name);

/// <summary>Una ubicación (§4.3).</summary>
public sealed record WarehouseLocationDto(Guid PublicId, string Code, string Name, bool IsDefault, bool IsActive);

/// <summary>El saldo inicial de la bodega, si ya tiene uno (US4).</summary>
public sealed record OpeningBalanceRefDto(Guid DocumentPublicId, string? Number, DocumentStatus Status, DateOnly? CutoffDate);

/// <summary>
/// <c>WarehouseDto</c> (§4.2). <see cref="IsDefault"/> es la bodega por defecto <b>de quien pregunta</b>;
/// <see cref="NegativeStockAllowed"/>, <c>Existencias.StockNegativoPermitido</c> vigente hoy para la bodega. El detalle
/// suma <see cref="Locations"/> y <see cref="OpeningBalance"/> (en la lista van nulos).
/// </summary>
public sealed record WarehouseDto(
    Guid PublicId,
    string Code,
    string Name,
    BranchRefDto Branch,
    WarehouseTypeRefDto Type,
    bool IsTransit,
    WarehouseRefDto? TransitWarehouse,
    WarehouseActivationStatus ActivationStatus,
    DateOnly? CutoffDate,
    DateTime? ActivatedAt,
    string? ActivatedBy,
    string? Address,
    bool IsActive,
    bool IsDefault,
    bool NegativeStockAllowed,
    IReadOnlyList<WarehouseLocationDto>? Locations = null,
    OpeningBalanceRefDto? OpeningBalance = null);

/// <summary>La respuesta del alta (§4.2): la bodega, la de tránsito si nació con ella, y los avisos.</summary>
public sealed record CreateWarehouseResultDto(WarehouseDto Warehouse, WarehouseRefDto? TransitWarehouseCreated, IReadOnlyList<AvisoDto> Warnings);

/// <summary>
/// Una política de reorden (§4.4). <see cref="Position"/> y <see cref="Available"/> sólo con <c>Inventory.Stock.View</c>;
/// la posición la calcula <c>PosicionDeReposicion</c> (US2, T257): hasta entonces sale nula.
/// </summary>
public sealed record ReorderPolicyDto(
    Guid PublicId, CatalogRefDto Product, WarehouseRefDto Warehouse, decimal Minimum, decimal Maximum, decimal ReorderPoint,
    decimal? Position, decimal? Available);
