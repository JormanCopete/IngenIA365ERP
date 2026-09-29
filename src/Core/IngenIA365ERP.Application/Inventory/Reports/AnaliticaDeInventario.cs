using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Analytics;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// Sobre qué mide la analítica de inventario (feature 012, I6, US17; T960–T967): el alcance de quien pregunta más los filtros ya
/// resueltos a Ids internos —las bodegas (por <c>warehouse</c> o por las de <c>branch</c>), el producto, la categoría y el grupo
/// contable—. Lo arma <see cref="AnaliticaDeInventario.AmbitoAsync"/>, que responde el 404 de una bodega fuera del alcance. (nuevo)
/// </summary>
public sealed record AmbitoDeAnalitica(
    AlcanceDeInventario Alcance,
    IReadOnlySet<int>? Bodegas,
    int? SucursalId,
    int? PuntoId,
    int? ProductoId,
    int? CategoriaId,
    int? GrupoContableId,
    string Descripcion)
{
    /// <summary>La bodega entra: está en el alcance y en el filtro, si lo hay.</summary>
    public bool IncluyeBodega(int bodegaId) => Alcance.IncluyeBodega(bodegaId) && (Bodegas is null || Bodegas.Contains(bodegaId));

    /// <summary>El producto entra por los filtros de producto, categoría y grupo contable.</summary>
    public bool IncluyeProducto(ProductoDeAnalitica p) =>
        (ProductoId is null || p.Id == ProductoId) && (CategoriaId is null || p.CategoryId == CategoriaId)
        && (GrupoContableId is null || p.AccountingGroupId == GrupoContableId);
}

/// <summary>Un producto tal como lo nombran las vistas de analítica. (nuevo)</summary>
public sealed record ProductoDeAnalitica(int Id, Guid PublicId, string Code, string Name, int CategoryId, int? AccountingGroupId)
{
    public string Texto => $"{Code} · {Name}";
}

/// <summary>
/// Una línea de venta o de devolución confirmada con su costo de venta (feature 012, I6, T960): la venta neta (sin impuestos, con los
/// descuentos de la línea; negativa en una devolución) y el costo que salió del kardex por ella (negativo en una devolución con
/// reingreso). Las dimensiones son las del documento. (nuevo)
/// </summary>
public sealed record LineaDeMargen(
    int DocumentId,
    DocumentClass Class,
    DateOnly OperationDate,
    int ProductId,
    int? SalespersonId,
    int? PersonId,
    int? PointOfSaleId,
    int? WarehouseId,
    decimal VentaNeta,
    decimal CostoDeVenta);

/// <summary>Un lote con existencia que vence en la ventana pedida (feature 012, I6, T963). (nuevo)</summary>
public sealed record LotePorVencer(int ProductId, int WarehouseId, int LotId, Guid LotPublicId, string LotCode, DateOnly Vence, decimal Cantidad);

/// <summary>Un producto con existencia en una bodega y su último movimiento a la fecha (feature 012, I6, T963). (nuevo)</summary>
public sealed record ExistenciaSinMovimiento(int ProductId, int WarehouseId, DateOnly? UltimoMovimiento, int Dias, decimal Cantidad, decimal Valor);

/// <summary>
/// La analítica de inventario (feature 012, I6, US17; T960–T967; FR-086, FR-088): lo que miden las vistas <c>margin</c>, <c>turnover</c>,
/// <c>abc</c>, <c>no-movement</c>, <c>expiring</c> e <c>impairment</c> (motivo «sin movimiento») y el tablero, en <b>un solo sitio</b> para
/// que la vista y la ficha digan lo mismo (T967: el tablero no duplica cálculos). No aplica el alcance por su cuenta: lo recibe en el
/// <see cref="AmbitoDeAnalitica"/> que arma quien llama con <c>IAlcanceDeInventario</c>. Lee el kardex y sus proyecciones; nunca los escribe. (nuevo)
/// <list type="bullet">
/// <item><b>Venta neta</b>: la de las líneas de los documentos confirmados de las clases de venta (facturas, POS, comprobantes no
/// electrónicos, notas débito) menos las de devolución (notas crédito, notas no electrónicas, notas de ajuste POS).</item>
/// <item><b>Costo de venta</b>: lo que salió del kardex por esas líneas (una devolución con reingreso lo resta). La factura desde remisiones
/// no mueve el kardex: su costo es el de las líneas de remisión que factura, en proporción a la cantidad.</item>
/// <item><b>Rotación</b>: el costo de venta del kardex del período (ventas, remisiones y devoluciones por su fecha de operación) sobre el
/// inventario promedio de <see cref="IndicadoresDeRotacion"/>, con los saldos del día anterior al período, de cada fin de mes dentro de él
/// y del último día —cada uno por <see cref="ValorizadoALaFecha"/>, que parte de los cierres del período—.</item>
/// <item><b>Consumo</b> (ABC por consumo): el costo de las salidas normales del kardex, sin traslados, movimientos entre ubicaciones ni
/// anulaciones (no son consumo).</item>
/// </list>
/// </summary>
public sealed class AnaliticaDeInventario(IApplicationDbContext db, ValorizadoALaFecha valorizado)
{
    /// <summary>Un filtro propio con un valor que la vista no admite (<c>by</c>, <c>basis</c>, <c>days</c>, <c>year</c>): 422. (nuevo)</summary>
    public const string FiltroInvalidoCodigo = "Inventory.Report.FilterInvalid";

    /// <summary>El error de un filtro propio no admitido, con los valores que sí admite.</summary>
    public static Error FiltroInvalido(string filtro, string? valor, IEnumerable<string> admitidos) =>
        new(FiltroInvalidoCodigo, $"«{valor}» no es un valor de {filtro}. Admite: {string.Join(", ", admitidos)}.");

    /// <summary>El permiso de costos: sin él, las vistas de valor responden el 404 y las demás dejan vacíos los valores.</summary>
    public const string PermisoDeCostos = "Inventory.Costs.Read";

    /// <summary>La nota de una vista que deja vacíos los valores sin <see cref="PermisoDeCostos"/>.</summary>
    public const string NotaSinCostos = "Los valores salen vacíos: ver costos exige Inventory.Costs.Read.";

    /// <summary>Las clases que suman como venta.</summary>
    public static IReadOnlyList<DocumentClass> ClasesDeVenta => DatosDeVentasYCaja.ClasesDeVenta;

    /// <summary>Las clases que restan como devolución.</summary>
    public static IReadOnlyList<DocumentClass> ClasesDeDevolucion => DatosDeVentasYCaja.ClasesDeDevolucion;

    /// <summary>Las clases cuyo kardex es costo de venta (las de venta y devolución, y la remisión, que es la que saca la mercancía).</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesDeCostoDeVenta =
        [.. DatosDeVentasYCaja.ClasesDeVenta, .. DatosDeVentasYCaja.ClasesDeDevolucion, DocumentClass.Shipment];

    /// <summary>Las clases cuyas salidas no son consumo: mover mercancía no la consume y anular deshace.</summary>
    public static readonly IReadOnlyList<DocumentClass> ClasesQueNoSonConsumo =
        [DocumentClass.TransferDispatch, DocumentClass.TransferReceipt, DocumentClass.LocationMove, DocumentClass.Voiding];

    // ------------------------------------------------------------------------------------------------ ámbito --

    /// <summary>
    /// Los filtros comunes resueltos contra el alcance: <c>warehouse</c> (fuera del alcance, el 404 de la bodega), <c>branch</c> (sus
    /// bodegas), <c>pointOfSale</c> (fuera, el 404 del punto), <c>product</c>, <c>category</c> y <c>accountingGroup</c>.
    /// </summary>
    public async Task<Result<AmbitoDeAnalitica>> AmbitoAsync(FiltrosDeInformeDeInventario f, AlcanceDeInventario alcance, CancellationToken ct)
    {
        HashSet<int>? bodegas = null;
        var partes = new List<string>();
        if (f.Warehouse is { } wp)
        {
            var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => new { w.Id, w.Code }).FirstOrDefaultAsync(ct);
            if (bodega is null || !alcance.IncluyeBodega(bodega.Id)) return Result.Failure<AmbitoDeAnalitica>(ErroresDeAlcance.BodegaInexistente());
            bodegas = [bodega.Id];
            partes.Add($"Bodega {bodega.Code}");
        }
        int? sucursal = null;
        if (f.Branch is { } bp)
        {
            var s = await db.Branches.AsNoTracking().Where(b => b.PublicId == bp).Select(b => new { b.Id, b.Name }).FirstOrDefaultAsync(ct);
            sucursal = s?.Id ?? -1;
            var deLaSucursal = await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => w.BranchId == sucursal).Select(w => w.Id).ToListAsync(ct);
            bodegas = bodegas is null ? [.. deLaSucursal] : [.. bodegas.Where(deLaSucursal.Contains)];
            partes.Add($"Sucursal {s?.Name ?? "inexistente"}");
        }
        int? punto = null;
        if (f.PointOfSale is { } pp)
        {
            punto = await db.PointsOfSale.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (punto is null || !alcance.IncluyePunto(punto.Value)) return Result.Failure<AmbitoDeAnalitica>(ErroresDeAlcance.PuntoInexistente());
        }
        int? producto = null, categoria = null, grupo = null;
        if (f.Product is { } prp)
            producto = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == prp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
        if (f.Category is { } cp)
            categoria = await db.ProductCategories.AsNoTracking().IgnoreQueryFilters().Where(c => c.PublicId == cp).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct) ?? -1;
        if (f.AccountingGroup is { } gp)
            grupo = await db.AccountingGroups.AsNoTracking().IgnoreQueryFilters().Where(g => g.PublicId == gp).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct) ?? -1;

        var descripcion = partes.Count == 0 ? "Todas las bodegas del alcance" : string.Join(" · ", partes);
        return Result.Success(new AmbitoDeAnalitica(alcance, bodegas, sucursal, punto, producto, categoria, grupo, descripcion));
    }

    // ------------------------------------------------------------------------------------------- productos --

    /// <summary>Los productos por Id (también los retirados: tuvieron movimientos).</summary>
    public async Task<Dictionary<int, ProductoDeAnalitica>> ProductosAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return [];
        return await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id))
            .Select(p => new ProductoDeAnalitica(p.Id, p.PublicId, p.Code, p.Name, p.CategoryId, p.AccountingGroupId))
            .ToDictionaryAsync(p => p.Id, ct);
    }

    /// <summary>Los códigos de las bodegas por Id.</summary>
    public async Task<Dictionary<int, (Guid PublicId, string Code, WarehouseBehavior Behavior)>> BodegasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return [];
        return (await db.Warehouses.AsNoTracking().IgnoreQueryFilters().Where(w => lista.Contains(w.Id))
                .Select(w => new { w.Id, w.PublicId, w.Code, w.Behavior }).ToListAsync(ct))
            .ToDictionary(w => w.Id, w => (w.PublicId, w.Code, w.Behavior));
    }

    // ------------------------------------------------------------------------------------------------ ventas --

    /// <summary>
    /// Las líneas de venta y devolución confirmadas entre <paramref name="desde"/> y <paramref name="hasta"/> (fecha de operación) que ve
    /// quien pregunta (<see cref="FiltroDeAlcance.DocumentosVisibles"/>), con su venta neta y su costo de venta.
    /// </summary>
    public async Task<IReadOnlyList<LineaDeMargen>> LineasDeVentaAsync(AmbitoDeAnalitica a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var clases = ClasesDeVenta.Concat(ClasesDeDevolucion).ToList();
        var documentos = db.InventoryDocuments.AsNoTracking()
            .DocumentosVisibles(a.Alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking())
            .Where(d => d.Status == DocumentStatus.Confirmed && clases.Contains(d.Class) && d.OperationDate >= desde && d.OperationDate <= hasta);
        if (a.Bodegas is not null)
        {
            var bodegas = a.Bodegas.ToArray();
            documentos = documentos.Where(d => d.WarehouseId != null && bodegas.Contains(d.WarehouseId.Value));
        }
        if (a.PuntoId is int punto) documentos = documentos.Where(d => d.PointOfSaleId == punto);

        var filas = await (
                from l in db.InventoryDocumentLines.AsNoTracking()
                where !l.IsDeleted
                join d in documentos on l.DocumentId equals d.Id
                select new
                {
                    LineaId = l.Id, d.Id, d.Class, d.OperationDate, l.ProductId, d.SalespersonId, d.CounterpartyPersonId, d.PointOfSaleId,
                    d.WarehouseId, l.NetAmount,
                })
            .ToListAsync(ct);
        if (filas.Count == 0) return [];

        var productos = await ProductosAsync(filas.Select(f => f.ProductId), ct);
        filas = filas.Where(f => productos.TryGetValue(f.ProductId, out var p) && a.IncluyeProducto(p)).ToList();
        var lineaIds = filas.Select(f => f.LineaId).ToList();

        var costoDirecto = (await db.KardexEntries.AsNoTracking().Where(k => lineaIds.Contains(k.DocumentLineId))
                .Select(k => new { k.DocumentLineId, k.TotalCost }).ToListAsync(ct))
            .GroupBy(k => k.DocumentLineId).ToDictionary(g => g.Key, g => -g.Sum(k => k.TotalCost));

        // La factura desde remisiones no mueve el kardex: su costo es el de las líneas de remisión que factura, en proporción.
        var desdeRemision = filas.Where(f => f.Class == DocumentClass.SalesInvoiceFromShipments).Select(f => f.LineaId).ToList();
        var costoPorRemision = new Dictionary<int, decimal>();
        if (desdeRemision.Count > 0)
        {
            var vinculos = await (
                    from ll in db.DocumentLineLinks.AsNoTracking()
                    where desdeRemision.Contains(ll.TargetLineId) && !ll.IsDeleted
                    join origen in db.InventoryDocumentLines.AsNoTracking() on ll.SourceLineId equals origen.Id
                    join od in db.InventoryDocuments.AsNoTracking() on origen.DocumentId equals od.Id
                    where od.Class == DocumentClass.Shipment
                    select new { ll.TargetLineId, ll.SourceLineId, ll.QuantityBase, OrigenCantidad = origen.QuantityBase })
                .ToListAsync(ct);
            var origenes = vinculos.Select(v => v.SourceLineId).Distinct().ToList();
            var costoDeOrigen = (await db.KardexEntries.AsNoTracking().Where(k => origenes.Contains(k.DocumentLineId))
                    .Select(k => new { k.DocumentLineId, k.TotalCost }).ToListAsync(ct))
                .GroupBy(k => k.DocumentLineId).ToDictionary(g => g.Key, g => -g.Sum(k => k.TotalCost));
            foreach (var v in vinculos)
            {
                if (v.OrigenCantidad == 0m || !costoDeOrigen.TryGetValue(v.SourceLineId, out var costo)) continue;
                costoPorRemision[v.TargetLineId] = costoPorRemision.GetValueOrDefault(v.TargetLineId)
                    + Math.Round(costo * v.QuantityBase / v.OrigenCantidad, 2, MidpointRounding.AwayFromZero);
            }
        }

        return filas.Select(f =>
        {
            var devolucion = ClasesDeDevolucion.Contains(f.Class);
            var costo = costoDirecto.GetValueOrDefault(f.LineaId) + costoPorRemision.GetValueOrDefault(f.LineaId);
            return new LineaDeMargen(f.Id, f.Class, f.OperationDate, f.ProductId, f.SalespersonId, f.CounterpartyPersonId, f.PointOfSaleId,
                f.WarehouseId, devolucion ? -f.NetAmount : f.NetAmount, costo);
        }).ToList();
    }

    // ------------------------------------------------------------------------------------------------ kardex --

    /// <summary>
    /// El costo de venta del kardex por producto entre <paramref name="desde"/> y <paramref name="hasta"/>: lo que salió por ventas y
    /// remisiones menos lo que reingresó por devoluciones, en las bodegas del ámbito.
    /// </summary>
    public async Task<Dictionary<int, decimal>> CostoDeVentaPorProductoAsync(AmbitoDeAnalitica a, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        await PorProductoAsync(a, desde, hasta, ClasesDeCostoDeVenta, soloSalidas: false, ct);

    /// <summary>El consumo al costo por producto (salidas normales sin traslados ni anulaciones) entre las fechas, en las bodegas del ámbito.</summary>
    public async Task<Dictionary<int, decimal>> ConsumoPorProductoAsync(AmbitoDeAnalitica a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var excluidas = ClasesQueNoSonConsumo.ToList();
        var todas = Enum.GetValues<DocumentClass>().Where(c => !excluidas.Contains(c)).ToList();
        return await PorProductoAsync(a, desde, hasta, todas, soloSalidas: true, ct);
    }

    private async Task<Dictionary<int, decimal>> PorProductoAsync(
        AmbitoDeAnalitica a, DateOnly desde, DateOnly hasta, IReadOnlyList<DocumentClass> clases, bool soloSalidas, CancellationToken ct)
    {
        var lista = clases.ToList();
        var kardex = db.KardexEntries.AsNoTracking().PorBodega(a.Alcance, k => k.WarehouseId)
            .Where(k => k.OperationDate >= desde && k.OperationDate <= hasta && k.Kind != KardexEntryKind.CostAdjustment);
        if (soloSalidas) kardex = kardex.Where(k => k.Kind == KardexEntryKind.Exit && k.Reason == KardexReason.Normal);
        if (a.Bodegas is not null)
        {
            var bodegas = a.Bodegas.ToArray();
            kardex = kardex.Where(k => bodegas.Contains(k.WarehouseId));
        }
        if (a.ProductoId is int producto) kardex = kardex.Where(k => k.ProductId == producto);
        var filas = await (
                from k in kardex
                join d in db.InventoryDocuments.AsNoTracking() on k.DocumentId equals d.Id
                where lista.Contains(d.Class)
                select new { k.ProductId, k.TotalCost })
            .ToListAsync(ct);
        var productos = await ProductosAsync(filas.Select(f => f.ProductId), ct);
        return filas.Where(f => a.IncluyeProducto(productos[f.ProductId]))
            .GroupBy(f => f.ProductId).ToDictionary(g => g.Key, g => -g.Sum(f => f.TotalCost));
    }

    /// <summary>
    /// El valorizado a <paramref name="fecha"/> en las bodegas del ámbito (con las de tránsito que el alcance deja ver, salvo
    /// <paramref name="sinTransito"/>), filtrado por producto, categoría y grupo contable.
    /// </summary>
    public async Task<IReadOnlyList<FilaDeValorizado>> ExistenciasAsync(AmbitoDeAnalitica a, DateOnly fecha, bool sinTransito, CancellationToken ct)
    {
        IReadOnlyCollection<int>? productos = a.ProductoId is int p ? [p] : null;
        var filas = (await valorizado.CalcularAsync(fecha, productos, ct)).Where(x => a.IncluyeBodega(x.WarehouseId)).ToList();
        var datos = await ProductosAsync(filas.Select(x => x.ProductId), ct);
        filas = filas.Where(x => a.IncluyeProducto(datos[x.ProductId])).ToList();
        if (!sinTransito) return filas;
        var bodegas = await BodegasAsync(filas.Select(x => x.WarehouseId), ct);
        return filas.Where(x => bodegas[x.WarehouseId].Behavior != WarehouseBehavior.Transit).ToList();
    }

    /// <summary>
    /// El inventario promedio al costo por producto en el período: el promedio de los saldos del día anterior a <paramref name="desde"/>,
    /// de cada fin de mes anterior a <paramref name="hasta"/> dentro del período y de <paramref name="hasta"/> (<see cref="IndicadoresDeRotacion.InventarioPromedio"/>).
    /// </summary>
    public async Task<Dictionary<int, decimal>> InventarioPromedioPorProductoAsync(AmbitoDeAnalitica a, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var fechas = FechasDeSaldo(desde, hasta);
        var saldosPorFecha = new List<Dictionary<int, decimal>>(fechas.Count);
        foreach (var fecha in fechas)
            saldosPorFecha.Add((await ExistenciasAsync(a, fecha, sinTransito: false, ct)).GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Value)));
        var productos = saldosPorFecha.SelectMany(s => s.Keys).Distinct();
        return productos.ToDictionary(p => p, p => IndicadoresDeRotacion.InventarioPromedio(saldosPorFecha.Select(s => s.GetValueOrDefault(p)).ToList()));
    }

    /// <summary>Las fechas de saldo del período: la víspera, cada fin de mes dentro y el último día.</summary>
    public static IReadOnlyList<DateOnly> FechasDeSaldo(DateOnly desde, DateOnly hasta)
    {
        var fechas = new List<DateOnly> { desde.AddDays(-1) };
        var finDeMes = new DateOnly(desde.Year, desde.Month, 1).AddMonths(1).AddDays(-1);
        while (finDeMes < hasta)
        {
            fechas.Add(finDeMes);
            finDeMes = finDeMes.AddDays(1).AddMonths(1).AddDays(-1);
        }
        fechas.Add(hasta);
        return fechas;
    }

    /// <summary>
    /// Lo que tiene existencia a <paramref name="fecha"/> (sin bodegas de tránsito) y no se movió en los últimos <paramref name="dias"/>
    /// días: su último movimiento (entrada o salida del kardex hasta la fecha) es de hace <paramref name="dias"/> o más.
    /// </summary>
    public async Task<IReadOnlyList<ExistenciaSinMovimiento>> SinMovimientoAsync(AmbitoDeAnalitica a, DateOnly fecha, int dias, CancellationToken ct)
    {
        var existencias = (await ExistenciasAsync(a, fecha, sinTransito: true, ct)).Where(x => x.Quantity > 0m).ToList();
        if (existencias.Count == 0) return [];
        var productos = existencias.Select(x => x.ProductId).Distinct().ToList();
        var bodegas = existencias.Select(x => x.WarehouseId).Distinct().ToList();
        var ultimos = (await db.KardexEntries.AsNoTracking()
                .Where(k => productos.Contains(k.ProductId) && bodegas.Contains(k.WarehouseId) && k.OperationDate <= fecha && k.Kind != KardexEntryKind.CostAdjustment)
                .GroupBy(k => new { k.ProductId, k.WarehouseId })
                .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Ultimo = g.Max(k => k.OperationDate) })
                .ToListAsync(ct))
            .ToDictionary(x => (x.ProductId, x.WarehouseId), x => x.Ultimo);
        return existencias
            .Select(x =>
            {
                DateOnly? ultimo = ultimos.TryGetValue((x.ProductId, x.WarehouseId), out var u) ? u : null;
                var transcurridos = ultimo is { } fechaUltima ? fecha.DayNumber - fechaUltima.DayNumber : int.MaxValue;
                return new ExistenciaSinMovimiento(x.ProductId, x.WarehouseId, ultimo, transcurridos, x.Quantity, x.Value);
            })
            .Where(x => x.Dias >= dias)
            .ToList();
    }

    /// <summary>
    /// Los lotes con existencia en las bodegas del ámbito (sin tránsito) que vencen desde <paramref name="fecha"/> hasta
    /// <paramref name="dias"/> días después. Los ya vencidos no son «por vencer»: son indicio de deterioro (vista <c>impairment</c>).
    /// La existencia es la vigente por lote (<c>INV_StockDetails</c>).
    /// </summary>
    public async Task<IReadOnlyList<LotePorVencer>> LotesPorVencerAsync(AmbitoDeAnalitica a, DateOnly fecha, int dias, CancellationToken ct)
    {
        var limite = fecha.AddDays(Math.Max(0, dias));
        var existencia = db.StockDetails.AsNoTracking().PorBodega(a.Alcance, s => s.WarehouseId).Where(s => s.LotId != null && s.Quantity > 0m);
        if (a.Bodegas is not null)
        {
            var bodegas = a.Bodegas.ToArray();
            existencia = existencia.Where(s => bodegas.Contains(s.WarehouseId));
        }
        if (a.ProductoId is int producto) existencia = existencia.Where(s => s.ProductId == producto);
        var lotes = await existencia
            .GroupBy(s => new { s.ProductId, s.WarehouseId, LotId = s.LotId!.Value })
            .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, g.Key.LotId, Cantidad = g.Sum(s => s.Quantity) })
            .Join(db.Lots.AsNoTracking().Where(l => l.ExpiryDate != null && l.ExpiryDate >= fecha && l.ExpiryDate <= limite), x => x.LotId, l => l.Id,
                (x, l) => new LotePorVencer(x.ProductId, x.WarehouseId, x.LotId, l.PublicId, l.Code, l.ExpiryDate!.Value, x.Cantidad))
            .ToListAsync(ct);
        if (lotes.Count == 0) return [];
        var productos = await ProductosAsync(lotes.Select(l => l.ProductId), ct);
        var datosBodegas = await BodegasAsync(lotes.Select(l => l.WarehouseId), ct);
        return lotes.Where(l => a.IncluyeProducto(productos[l.ProductId]) && datosBodegas[l.WarehouseId].Behavior != WarehouseBehavior.Transit)
            .OrderBy(l => l.Vence).ThenBy(l => l.LotCode, StringComparer.Ordinal).ToList();
    }

    /// <summary>El costo promedio a la fecha de cada (producto, bodega) del ámbito, para valorizar lotes y existencias por lote.</summary>
    public async Task<Dictionary<(int ProductId, int WarehouseId), decimal>> CostosPromedioAsync(AmbitoDeAnalitica a, DateOnly fecha, CancellationToken ct) =>
        (await ExistenciasAsync(a, fecha, sinTransito: false, ct)).ToDictionary(x => (x.ProductId, x.WarehouseId), x => x.AverageCost);

    /// <summary>El margen en puntos porcentuales (dos decimales); sin venta, nulo.</summary>
    public static decimal? PorcentajeDeMargen(decimal ventaNeta, decimal costo) =>
        ventaNeta == 0m ? null : Math.Round((ventaNeta - costo) * 100m / ventaNeta, 2, MidpointRounding.AwayFromZero);
}
