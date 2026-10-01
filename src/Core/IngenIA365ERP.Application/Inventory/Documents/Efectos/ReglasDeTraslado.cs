using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// Lo que comparten el despacho, la recepción y el movimiento entre ubicaciones (feature 012, US10, T367, T368; contracts/api.md
/// §10, §11): las reglas de producto, la bodega de tránsito de una sucursal, las ubicaciones por defecto y la anulación
/// <b>neutra</b> de un cambio de lugar —cada salida vuelve a entrar y cada entrada vuelve a salir al mismo costo, sin la diferencia
/// contra el promedio que deja anular una compra—. (nuevo)
/// </summary>
public static class ReglasDeTraslado
{
    /// <summary>Producto inventariable, activo y (salvo <paramref name="admiteBloqueado"/>) no bloqueado, por línea viva.</summary>
    public static async Task<IReadOnlyList<Error>> ProductosAsync(InventoryDocument documento, IMaestrosDelDocumento maestros, bool admiteBloqueado, CancellationToken ct)
    {
        var errores = new List<Error>();
        var vivas = documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productos = (await maestros.ProductosPorIdAsync(vivas.Select(l => l.ProductId).Distinct().ToList(), ct)).ToDictionary(p => p.Id);
        foreach (var linea in vivas)
        {
            if (!productos.TryGetValue(linea.ProductId, out var producto)) continue;
            if (!producto.Inventariable) errores.Add(InventoryErrors.ProductNotInventoriable(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Blocked && !admiteBloqueado) errores.Add(InventoryErrors.ProductBlocked(linea.LineNumber, producto.Code));
            else if (producto.Status == ProductStatus.Inactive) errores.Add(InventoryErrors.ProductInactive(linea.LineNumber, producto.Code));
        }
        return errores;
    }

    /// <summary>La bodega de tránsito activa de la sucursal (una por sucursal, la crea el alta de la primera bodega), o nula.</summary>
    public static Task<int?> TransitoDeLaSucursalAsync(IApplicationDbContext db, int sucursalId, CancellationToken ct) =>
        db.Warehouses.AsNoTracking()
            .Where(w => w.BranchId == sucursalId && w.Behavior == WarehouseBehavior.Transit && w.IsActive)
            .OrderBy(w => w.Id).Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);

    /// <summary>La ubicación por defecto (activa) de cada bodega.</summary>
    public static async Task<IReadOnlyDictionary<int, int>> UbicacionesPorDefectoAsync(IApplicationDbContext db, IReadOnlyCollection<int> bodegas, CancellationToken ct)
    {
        var filas = await db.WarehouseLocations.AsNoTracking()
            .Where(l => bodegas.Contains(l.WarehouseId) && l.IsDefault && l.IsActive)
            .Select(l => new { l.WarehouseId, l.Id })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.WarehouseId).ToDictionary(g => g.Key, g => g.Min(f => f.Id));
    }

    /// <summary>
    /// La anulación neutra (despacho no recibido, movimiento entre ubicaciones): los movimientos que revierten el kardex del original
    /// al <b>mismo</b> costo —la salida de lo que entró va al costo de origen, no como devolución de una entrada— con las salidas
    /// primero y cada entrada siguiendo a la salida de su línea (<see cref="MovimientoDeKardex.AlCostoDe"/>): el valor total no cambia.
    /// </summary>
    public static async Task<IReadOnlyList<MovimientoDeKardex>> ReversionNeutraAsync(
        ReversionDeKardex reversion, InventoryDocument anulacion, InventoryDocument original, CancellationToken ct)
    {
        var inversos = (await reversion.MovimientosAsync(anulacion, original, ct))
            .Select(m => m.Valoracion == ValoracionDelMovimiento.DevolucionDeEntrada ? m with { Valoracion = ValoracionDelMovimiento.AlCostoDeOrigen } : m)
            .ToList();
        var salidas = inversos.Where(m => m.QuantityBase < 0m).ToList();
        var resultado = new List<MovimientoDeKardex>(inversos.Count);
        resultado.AddRange(salidas);
        foreach (var entrada in inversos.Where(m => m.QuantityBase > 0m))
        {
            var suSalida = salidas.FirstOrDefault(s => ReferenceEquals(s.Linea, entrada.Linea) && s.QuantityBase == -entrada.QuantityBase);
            resultado.Add(suSalida is null ? entrada : entrada with { AlCostoDe = suSalida });
        }
        return resultado;
    }

    /// <summary>El valor al costo estimado de las salidas (lo que se aprueba; §1.3).</summary>
    public static async Task<Result<decimal>> ValorDeLasSalidasAsync(RegistroDeKardex registro, InventoryDocument documento, IReadOnlyList<MovimientoDeKardex> movimientos, CancellationToken ct)
    {
        var salidas = movimientos.Where(m => m.QuantityBase < 0m).ToList();
        if (salidas.Count == 0) return Result.Success(0m);
        var preparado = await registro.PrepararAsync(documento, salidas, ct);
        return preparado.IsFailure ? Result.Failure<decimal>(preparado.Error) : Result.Success(preparado.Value.ValorEstimado);
    }
}
