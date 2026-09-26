using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Accounting;

/// <summary>
/// Lo único de Inventario que Contabilidad conoce, además de la bandeja (feature 012, T27, T31; contracts/contabilidad.md
/// §1, §2.5 y §7.1; plantillas.md §16). Contabilidad no lee tablas <c>INV_</c>: los códigos que la matriz usa como
/// dimensión (grupos contables, bodegas con su sucursal y comportamiento, puntos de venta, causas de ajuste, tipos de
/// documento y, desde I3, medios de pago) y las combinaciones en uso los pide aquí. Lo implementa Inventario
/// (<c>Application/Inventory/Integration/DimensionesDeInventario</c>, T519; los medios y los puntos, T622).
///
/// <para>
/// La interfaz nace con la sección de la matriz de I2 (T505–T510) porque la validación de una regla la usa; ninguna tarea la
/// creaba. (nuevo)
/// </para>
/// </summary>
public interface IDimensionesDeInventario
{
    /// <summary>Los códigos vivos de cada dimensión de Inventario (y los medios de pago, cuando existan).</summary>
    Task<CatalogoDeDimensionesDto> CatalogoAsync(CancellationToken ct);

    /// <summary>
    /// Las combinaciones operación × grupo × bodega en uso de los tipos de documento cuyo modo de paso vigente a
    /// <paramref name="fecha"/> no es «no pasa» (§7.1): lo que la completitud compara con la matriz.
    /// </summary>
    Task<IReadOnlyList<CombinacionEnUsoDto>> CombinacionesEnUsoAsync(DateOnly fecha, CancellationToken ct);
}

/// <summary>
/// Los códigos vivos de cada dimensión (nuevo). <see cref="PaymentMeans"/> es nulo mientras el catálogo de medios de pago
/// no exista (I3): entonces el código de un medio no se puede comprobar y la regla se guarda sin esa comprobación.
/// </summary>
public sealed record CatalogoDeDimensionesDto(
    IReadOnlyList<CodigoDeDimensionDto> AccountingGroups,
    IReadOnlyList<BodegaDeDimensionDto> Warehouses,
    IReadOnlyList<CodigoDeDimensionDto> PointsOfSale,
    IReadOnlyList<CodigoDeDimensionDto> AdjustmentCauses,
    IReadOnlyList<CodigoDeDimensionDto> DocumentTypes,
    IReadOnlyList<MedioDePagoDeDimensionDto>? PaymentMeans = null)
{
    public static CatalogoDeDimensionesDto Vacio { get; } = new([], [], [], [], []);
}

/// <summary>Un código de dimensión con su nombre (nuevo).</summary>
public sealed record CodigoDeDimensionDto(string Code, string Name);

/// <summary>Una bodega: su sucursal (la de Core) y su comportamiento (<c>Transit</c> resuelve el rol <c>Transito</c>). (nuevo)</summary>
public sealed record BodegaDeDimensionDto(string Code, string Name, Guid? BranchPublicId, string? BranchCode, WarehouseBehavior Behavior);

/// <summary>Un medio de pago activo con su clase (nuevo; lo llena T622 en I3).</summary>
public sealed record MedioDePagoDeDimensionDto(string Code, string Name, PaymentMeansClass Class);

/// <summary>Una combinación en uso (nuevo): la operación, su grupo y su bodega, y qué tipos la usan.</summary>
public sealed record CombinacionEnUsoDto(
    string Operation,
    string? AccountingGroupCode,
    string? WarehouseCode,
    IReadOnlyList<string> DocumentTypeCodes,
    DateOnly? LastUsedAt);
