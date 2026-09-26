using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Periods;

/// <summary>Un producto en una bodega a una fecha: cantidad, ámbito de costo, promedio del ámbito y valor. (nuevo)</summary>
public sealed record FilaDeValorizado(int ProductId, int WarehouseId, int ScopeWarehouseId, decimal Quantity, decimal AverageCost, decimal Value);

/// <summary>
/// El valorizado a una fecha (feature 012, T289, T291; FR-047, SC-017; data-model §6.3), en un solo sitio para que el cierre y
/// la vista <c>valuation</c> digan lo mismo. Parte del último cierre vigente con fin en o antes de la fecha
/// (<c>INV_PeriodClosingBalances</c> de su <c>CloseVersion</c>, sin <c>Superseded</c>) y le suma el kardex posterior hasta la
/// fecha —cantidades por bodega, valores por ámbito de costo (<c>CostScopeWarehouseId</c>)—; sin cierre, todo el kardex. El valor
/// de cada bodega es cantidad × promedio del ámbito con el residuo por <c>Redondeo.Residuo</c>
/// (<see cref="Redondeo.ValorPorBodega"/>), así Σ por ámbito = el valor del ámbito a esa fecha (= <c>CostState.Value</c> si la
/// fecha es hoy). Dos consultas agregadas en la base, nada fila por fila (SC-017: &lt; 30 s con 50.000 productos en 50 bodegas).
/// No devuelve filas en cero. (nuevo)
/// </summary>
public sealed class ValorizadoALaFecha(IApplicationDbContext db, ILectorDeParametros parametros)
{
    /// <summary>
    /// El valorizado de <paramref name="fecha"/>, opcionalmente sólo de <paramref name="productos"/> (el ámbito entero de cada
    /// uno se calcula igual, para que el residuo caiga donde cae en el total).
    /// </summary>
    public async Task<IReadOnlyList<FilaDeValorizado>> CalcularAsync(DateOnly fecha, IReadOnlyCollection<int>? productos, CancellationToken ct)
    {
        var porBodega = await TextoAsync(ParametrosDeInventario.CosteoAmbito, "Cooperativa", fecha, ct) == "Bodega";
        var montos = Redondeo.MontosDesde(await TextoAsync(ParametrosDeInventario.RedondeoMontos, "Centavo", fecha, ct));
        var residuo = Redondeo.ResiduoDesde(await TextoAsync(ParametrosDeInventario.RedondeoResiduo, "MayorValor", fecha, ct));

        // 1. La base: el último cierre vigente con fin ≤ fecha.
        var cerrados = await db.InventoryPeriods.AsNoTracking().Where(p => p.Status == InventoryPeriodStatus.Closed)
            .Select(p => new { p.Id, p.Year, p.Month, p.CloseVersion }).ToListAsync(ct);
        var baseDeCierre = cerrados
            .Select(p => new { p.Id, p.CloseVersion, Fin = new DateOnly(p.Year, p.Month, 1).AddMonths(1).AddDays(-1) })
            .Where(p => p.Fin <= fecha)
            .OrderByDescending(p => p.Fin)
            .FirstOrDefault();

        var cantidades = new Dictionary<(int Producto, int Bodega), decimal>();
        var valores = new Dictionary<(int Producto, int Ambito), decimal>();
        DateOnly? desde = null;
        if (baseDeCierre is not null)
        {
            desde = baseDeCierre.Fin;
            var saldos = db.PeriodClosingBalances.AsNoTracking()
                .Where(b => b.PeriodId == baseDeCierre.Id && b.Version == baseDeCierre.CloseVersion && !b.Superseded);
            if (productos is not null) saldos = saldos.Where(b => productos.Contains(b.ProductId));
            foreach (var s in await saldos.Select(b => new { b.ProductId, b.WarehouseId, b.Quantity, b.Value }).ToListAsync(ct))
            {
                Sumar(cantidades, (s.ProductId, s.WarehouseId), s.Quantity);
                Sumar(valores, (s.ProductId, porBodega ? s.WarehouseId : 0), s.Value);
            }
        }

        // 2. El kardex posterior hasta la fecha, agregado en la base.
        var kardex = db.KardexEntries.AsNoTracking().Where(k => k.OperationDate <= fecha);
        if (desde is { } d) kardex = kardex.Where(k => k.OperationDate > d);
        if (productos is not null) kardex = kardex.Where(k => productos.Contains(k.ProductId));
        foreach (var c in await kardex.GroupBy(k => new { k.ProductId, k.WarehouseId })
                     .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Cantidad = g.Sum(k => k.QuantityBase) }).ToListAsync(ct))
            Sumar(cantidades, (c.ProductId, c.WarehouseId), c.Cantidad);
        foreach (var v in await kardex.GroupBy(k => new { k.ProductId, k.CostScopeWarehouseId })
                     .Select(g => new { g.Key.ProductId, g.Key.CostScopeWarehouseId, Valor = g.Sum(k => k.TotalCost) }).ToListAsync(ct))
            Sumar(valores, (v.ProductId, v.CostScopeWarehouseId), v.Valor);

        // 3. El valor de cada bodega: cantidad × promedio del ámbito, residuo por regla.
        var filas = new List<FilaDeValorizado>();
        var porAmbito = cantidades.ToLookup(c => (c.Key.Producto, Ambito: porBodega ? c.Key.Bodega : 0));
        var ambitos = porAmbito.Select(g => g.Key).Concat(valores.Keys.Select(k => (k.Producto, k.Ambito))).Distinct();
        foreach (var (producto, ambito) in ambitos.OrderBy(a => a.Producto).ThenBy(a => a.Ambito))
        {
            var bodegas = porAmbito[(producto, ambito)].OrderBy(c => c.Key.Bodega).ToList();
            var valor = valores.GetValueOrDefault((producto, ambito));
            var cantidad = bodegas.Sum(b => b.Value);
            var promedio = cantidad > 0m ? Math.Max(0m, Redondeo.CostoUnitario(valor / cantidad)) : 0m;
            // Todo valor del kardex nace en una bodega, así que un ámbito con valor tiene al menos una bodega (con cantidad cero,
            // el residuo que todavía no salió cae en ella y el total cuadra).
            if (bodegas.Count == 0) continue;
            var repartido = Redondeo.ValorPorBodega(valor, promedio, bodegas.Select(b => b.Value).ToList(), montos, residuo);
            for (var i = 0; i < bodegas.Count; i++)
            {
                if (bodegas[i].Value == 0m && repartido[i] == 0m) continue;
                filas.Add(new FilaDeValorizado(producto, bodegas[i].Key.Bodega, ambito, bodegas[i].Value, promedio, repartido[i]));
            }
        }
        return filas;
    }

    private static void Sumar<TClave>(Dictionary<TClave, decimal> mapa, TClave clave, decimal valor) where TClave : notnull =>
        mapa[clave] = mapa.GetValueOrDefault(clave) + valor;

    private async Task<string> TextoAsync(string clave, string defecto, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : defecto;
    }
}
