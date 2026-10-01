namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>Cuántos productos tienen existencia distinta de cero y cuánta suman, en unidades base. (nuevo)</summary>
public sealed record ExistenciaAgregada(int Products, decimal Quantity)
{
    public static ExistenciaAgregada Ninguna { get; } = new(0, 0m);

    public bool HayExistencia => Products > 0 || Quantity != 0m;
}

/// <summary>
/// Lo que el catálogo y las bodegas (US1) preguntan de la existencia antes de que exista el kardex (feature 012, T222,
/// T223, T220): si una bodega o una ubicación tiene existencia para inactivarla (<c>Inventory.Warehouse.HasStock</c>,
/// <c>.TransitHasStock</c>, <c>Inventory.Location.HasStock</c>) y el disponible de unos productos en una bodega para la
/// búsqueda (<c>available</c>). Las proyecciones <c>INV_StockBalances</c>/<c>INV_StockDetails</c> son de US2, que registra
/// la implementación real sobre ellas en lugar de <see cref="ExistenciasSinKardex"/> (TryAdd). (nuevo)
/// </summary>
public interface IExistenciasParaElCatalogo
{
    Task<ExistenciaAgregada> DeBodegaAsync(int warehouseId, CancellationToken ct);

    Task<ExistenciaAgregada> DeUbicacionAsync(int locationId, CancellationToken ct);

    /// <summary>El disponible por producto en la bodega; un producto sin dato no viene.</summary>
    Task<IReadOnlyDictionary<int, decimal>> DisponibleAsync(IReadOnlyCollection<int> productIds, int warehouseId, CancellationToken ct);
}

/// <summary>
/// Sin kardex (antes de US2) no hay existencia en ninguna parte: toda bodega y ubicación se puede inactivar y el
/// disponible no se informa. (nuevo)
/// </summary>
public sealed class ExistenciasSinKardex : IExistenciasParaElCatalogo
{
    public Task<ExistenciaAgregada> DeBodegaAsync(int warehouseId, CancellationToken ct) => Task.FromResult(ExistenciaAgregada.Ninguna);

    public Task<ExistenciaAgregada> DeUbicacionAsync(int locationId, CancellationToken ct) => Task.FromResult(ExistenciaAgregada.Ninguna);

    public Task<IReadOnlyDictionary<int, decimal>> DisponibleAsync(IReadOnlyCollection<int> productIds, int warehouseId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, decimal>>(new Dictionary<int, decimal>());
}
