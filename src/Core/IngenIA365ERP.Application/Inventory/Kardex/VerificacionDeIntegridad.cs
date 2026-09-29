using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Kardex;

/// <summary>Qué verificar o reconstruir: productos y bodegas (Ids internos); nulo = sin ese filtro. (nuevo)</summary>
public sealed record AlcanceDeVerificacion(IReadOnlyCollection<int>? Productos, IReadOnlyCollection<int>? Bodegas)
{
    public static AlcanceDeVerificacion Todo { get; } = new(null, null);
}

/// <summary>Una diferencia entre la suma del kardex y una proyección, con Ids internos. (nuevo)</summary>
public sealed record IncidenteDeKardex(string Kind, int ProductId, int? WarehouseId, int? LocationId, int? LotId, string Field, decimal Expected, decimal Actual)
{
    public decimal Difference => Actual - Expected;
}

/// <summary>Lo que revisó la verificación y lo que encontró. (nuevo)</summary>
public sealed record ResultadoDeVerificacion(int StockBalances, int StockDetails, int CostStates, IReadOnlyList<IncidenteDeKardex> Incidentes)
{
    /// <summary>Las capas PEPS revisadas (I5, T837).</summary>
    public int CostLayers { get; init; }
}

/// <summary>
/// La verificación de integridad del kardex (feature 012, T258; FR-003; data-model §3.7; SC-006): compara por <c>GROUP BY</c> cada
/// proyección con la suma de sus hechos —<c>StockBalance.Physical</c> por (producto, bodega), <c>StockDetail.Quantity</c> por
/// (producto, bodega, ubicación, lote) y <c>CostState.Quantity/Value</c> por (producto, ámbito)— y devuelve una diferencia por
/// fila y campo, incluidas las filas que faltan de un lado; <c>StockBalance.Reserved</c> contra las reservas <c>Active</c> (I6, T877). Sólo lee; la alerta y la
/// auditoría las pone <see cref="VerifyInventoryIntegrityQuery"/>. (nuevo)
/// </summary>
public sealed class VerificacionDeIntegridad(IApplicationDbContext db, IngenIA365ERP.Application.Common.Parameters.ILectorDeParametros? parametros = null, IDateTimeService? reloj = null)
{
    public async Task<ResultadoDeVerificacion> VerificarAsync(AlcanceDeVerificacion alcance, CancellationToken ct)
    {
        var incidentes = new List<IncidenteDeKardex>();
        var productos = alcance.Productos;
        var bodegas = alcance.Bodegas;

        // ------------------------------------------------------------------------ existencias por bodega --
        var kardex = db.KardexEntries.AsNoTracking().AsQueryable();
        if (productos is not null) kardex = kardex.Where(k => productos.Contains(k.ProductId));
        var kardexDeBodegas = bodegas is null ? kardex : kardex.Where(k => bodegas.Contains(k.WarehouseId));

        var esperadas = await kardexDeBodegas
            .GroupBy(k => new { k.ProductId, k.WarehouseId })
            .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Cantidad = g.Sum(k => k.QuantityBase) })
            .ToListAsync(ct);
        var existencias = db.StockBalances.AsNoTracking().AsQueryable();
        if (productos is not null) existencias = existencias.Where(s => productos.Contains(s.ProductId));
        if (bodegas is not null) existencias = existencias.Where(s => bodegas.Contains(s.WarehouseId));
        var reales = await existencias.Select(s => new { s.ProductId, s.WarehouseId, s.Physical }).ToListAsync(ct);

        var porExistencia = reales.ToDictionary(r => (r.ProductId, r.WarehouseId), r => r.Physical);
        foreach (var e in esperadas)
        {
            var real = porExistencia.GetValueOrDefault((e.ProductId, e.WarehouseId));
            if (real != e.Cantidad)
                incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.StockBalance, e.ProductId, e.WarehouseId, null, null, "Physical", e.Cantidad, real));
        }
        var conKardex = esperadas.Select(e => (e.ProductId, e.WarehouseId)).ToHashSet();
        foreach (var r in reales.Where(r => r.Physical != 0m && !conKardex.Contains((r.ProductId, r.WarehouseId))))
            incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.StockBalance, r.ProductId, r.WarehouseId, null, null, "Physical", 0m, r.Physical));

        // ---------------------------------------------------------------- reservado (I6, T877; SC-006) --
        // Reserved = Σ (QuantityBase − ConsumedQuantityBase) de las reservas Active de (producto, bodega); sin reservas, cero.
        var reservasVivas = db.Reservations.AsNoTracking().Where(r => r.Status == ReservationStatus.Active && !r.IsDeleted);
        if (productos is not null) reservasVivas = reservasVivas.Where(r => productos.Contains(r.ProductId));
        if (bodegas is not null) reservasVivas = reservasVivas.Where(r => bodegas.Contains(r.WarehouseId));
        var reservadas = (await reservasVivas.GroupBy(r => new { r.ProductId, r.WarehouseId })
                .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Cantidad = g.Sum(r => r.QuantityBase - r.ConsumedQuantityBase) })
                .ToListAsync(ct))
            .ToDictionary(r => (r.ProductId, r.WarehouseId), r => r.Cantidad);
        var realesReservado = await existencias.Select(s => new { s.ProductId, s.WarehouseId, s.Reserved }).ToListAsync(ct);
        foreach (var r in realesReservado)
        {
            var esperado = reservadas.GetValueOrDefault((r.ProductId, r.WarehouseId));
            if (r.Reserved != esperado)
                incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.StockBalance, r.ProductId, r.WarehouseId, null, null, "Reserved", esperado, r.Reserved));
        }
        var conFila = realesReservado.Select(r => (r.ProductId, r.WarehouseId)).ToHashSet();
        foreach (var ((producto, bodega), cantidad) in reservadas.Where(x => x.Value != 0m && !conFila.Contains(x.Key)))
            incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.StockBalance, producto, bodega, null, null, "Reserved", cantidad, 0m));

        // ------------------------------------------------------------------ por ubicación y lote --
        var esperadosDetalle = await kardexDeBodegas
            .GroupBy(k => new { k.ProductId, k.WarehouseId, k.LocationId, k.LotId })
            .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, g.Key.LocationId, g.Key.LotId, Cantidad = g.Sum(k => k.QuantityBase) })
            .ToListAsync(ct);
        var detalles = db.StockDetails.AsNoTracking().AsQueryable();
        if (productos is not null) detalles = detalles.Where(s => productos.Contains(s.ProductId));
        if (bodegas is not null) detalles = detalles.Where(s => bodegas.Contains(s.WarehouseId));
        var realesDetalle = await detalles.Select(s => new { s.ProductId, s.WarehouseId, s.LocationId, s.LotId, s.Quantity }).ToListAsync(ct);

        var porDetalle = realesDetalle.ToDictionary(r => (r.ProductId, r.WarehouseId, r.LocationId, r.LotId), r => r.Quantity);
        foreach (var e in esperadosDetalle)
        {
            var real = porDetalle.GetValueOrDefault((e.ProductId, e.WarehouseId, e.LocationId, e.LotId));
            if (real != e.Cantidad)
                incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.StockDetail, e.ProductId, e.WarehouseId, e.LocationId, e.LotId, "Quantity", e.Cantidad, real));
        }
        var detallesConKardex = esperadosDetalle.Select(e => (e.ProductId, e.WarehouseId, e.LocationId, e.LotId)).ToHashSet();
        foreach (var r in realesDetalle.Where(r => r.Quantity != 0m && !detallesConKardex.Contains((r.ProductId, r.WarehouseId, r.LocationId, r.LotId))))
            incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.StockDetail, r.ProductId, r.WarehouseId, r.LocationId, r.LotId, "Quantity", 0m, r.Quantity));

        // ------------------------------------------------------------------ estado de costo por ámbito --
        // Un ámbito se mide con todos sus hechos (en ámbito cooperativa, de todas las bodegas). Con filtro de bodegas entran los
        // ámbitos de esas bodegas y el de la cooperativa de los productos que se movieron en ellas.
        IReadOnlyCollection<int>? productosDelCosto = productos;
        if (bodegas is not null)
            productosDelCosto = esperadas.Select(e => e.ProductId).Concat(reales.Select(r => r.ProductId)).Distinct().ToList();

        var kardexDeCosto = db.KardexEntries.AsNoTracking().AsQueryable();
        var estados = db.CostStates.AsNoTracking().AsQueryable();
        if (productosDelCosto is not null)
        {
            kardexDeCosto = kardexDeCosto.Where(k => productosDelCosto.Contains(k.ProductId));
            estados = estados.Where(c => productosDelCosto.Contains(c.ProductId));
        }
        if (bodegas is not null)
        {
            kardexDeCosto = kardexDeCosto.Where(k => k.CostScopeWarehouseId == 0 || bodegas.Contains(k.CostScopeWarehouseId));
            estados = estados.Where(c => c.ScopeWarehouseId == 0 || bodegas.Contains(c.ScopeWarehouseId));
        }
        var esperadosCosto = await kardexDeCosto
            .GroupBy(k => new { k.ProductId, k.CostScopeWarehouseId })
            .Select(g => new { g.Key.ProductId, Ambito = g.Key.CostScopeWarehouseId, Cantidad = g.Sum(k => k.QuantityBase), Valor = g.Sum(k => k.TotalCost) })
            .ToListAsync(ct);
        var realesCosto = await estados.Select(c => new { c.ProductId, Ambito = c.ScopeWarehouseId, c.Quantity, c.Value, c.Method }).ToListAsync(ct);

        var porCosto = realesCosto.ToDictionary(r => (r.ProductId, r.Ambito));
        foreach (var e in esperadosCosto)
        {
            porCosto.TryGetValue((e.ProductId, e.Ambito), out var real);
            var cantidad = real?.Quantity ?? 0m;
            var valor = real?.Value ?? 0m;
            var bodega = e.Ambito == 0 ? (int?)null : e.Ambito;
            if (cantidad != e.Cantidad)
                incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.CostState, e.ProductId, bodega, null, null, "Quantity", e.Cantidad, cantidad));
            if (valor != e.Valor)
                incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.CostState, e.ProductId, bodega, null, null, "Value", e.Valor, valor));
        }
        var costosConKardex = esperadosCosto.Select(e => (e.ProductId, e.Ambito)).ToHashSet();
        foreach (var r in realesCosto.Where(r => (r.Quantity != 0m || r.Value != 0m) && !costosConKardex.Contains((r.ProductId, r.Ambito))))
        {
            var bodega = r.Ambito == 0 ? (int?)null : r.Ambito;
            if (r.Quantity != 0m) incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.CostState, r.ProductId, bodega, null, null, "Quantity", 0m, r.Quantity));
            if (r.Value != 0m) incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.CostState, r.ProductId, bodega, null, null, "Value", 0m, r.Value));
        }

        // ---------------------------------------------------------------------- capas PEPS (I5, T837) --
        // data-model §3.5, §3.7: de cada capa, restante = original − Σ de sus consumos (0 ≤ restante ≤ original); y de cada ámbito en
        // PEPS con existencia no negativa, Σ round(restante × costo) de sus capas vivas = CostState.Value.
        var capas = db.CostLayers.AsNoTracking().AsQueryable();
        if (productosDelCosto is not null) capas = capas.Where(c => productosDelCosto.Contains(c.ProductId));
        if (bodegas is not null) capas = capas.Where(c => c.ScopeWarehouseId == 0 || bodegas.Contains(c.ScopeWarehouseId));
        var realesCapas = await capas.Select(c => new { c.Id, c.ProductId, c.ScopeWarehouseId, c.OriginalQuantity, c.RemainingQuantity, c.UnitCost }).ToListAsync(ct);
        if (realesCapas.Count > 0)
        {
            var ids = realesCapas.Select(c => c.Id).ToList();
            var consumido = await db.LayerConsumptions.AsNoTracking().Where(c => ids.Contains(c.LayerId))
                .GroupBy(c => c.LayerId).Select(g => new { g.Key, Cantidad = g.Sum(c => c.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Cantidad, ct);
            foreach (var capa in realesCapas)
            {
                var esperado = capa.OriginalQuantity - consumido.GetValueOrDefault(capa.Id);
                // Una capa cerrada por un cambio de método queda en 0 sin consumos (T836): no es diferencia.
                if (capa.RemainingQuantity != esperado && capa.RemainingQuantity != 0m)
                    incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.CostLayer, capa.ProductId, capa.ScopeWarehouseId == 0 ? null : capa.ScopeWarehouseId,
                        null, null, "RemainingQuantity", esperado, capa.RemainingQuantity));
            }

            var montos = RedondeoDeMontos.Centavo;
            if (parametros is not null)
            {
                var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.RedondeoMontos, reloj?.HoyLocal ?? DateOnly.MaxValue, ct: ct);
                if (leido.IsSuccess) montos = Redondeo.MontosDesde(leido.Value.Texto);
            }
            foreach (var estado in realesCosto)
            {
                if (estado.Method != CostMethod.Fifo || estado.Quantity < 0m) continue;
                var enCapas = realesCapas.Where(c => c.ProductId == estado.ProductId && c.ScopeWarehouseId == estado.Ambito && c.RemainingQuantity > 0m)
                    .Sum(c => Redondeo.Monto(c.RemainingQuantity * c.UnitCost, montos));
                if (enCapas != estado.Value)
                    incidentes.Add(new IncidenteDeKardex(TiposDeIncidente.CostLayer, estado.ProductId, estado.Ambito == 0 ? null : estado.Ambito,
                        null, null, "Value", estado.Value, enCapas));
            }
        }

        return new ResultadoDeVerificacion(reales.Count, realesDetalle.Count, realesCosto.Count, incidentes) { CostLayers = realesCapas.Count };
    }
}
