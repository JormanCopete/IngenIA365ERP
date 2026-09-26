using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// La vista <c>kardex</c> de <c>/api/reports/inventory</c> (feature 012, T260; contracts/api.md §6.1, §27): los movimientos de
/// <b>un</b> producto en orden <c>(OperationDate, Id)</c>, con la primera fila en el saldo al día anterior a <c>from</c>, saldo
/// acumulado en cantidad y valor y el costo promedio del ámbito tras cada movimiento. La pantalla <c>/inventario/kardex</c> la
/// lee en JSON y la misma vista exporta. (nuevo)
/// <list type="bullet">
/// <item>filtros: <c>product</c> (obligatorio), <c>warehouse</c>, <c>from</c>/<c>to</c> (hasta 5 años, lo valida la ruta) y los
/// propios <c>location</c> e <c>includeCostAdjustments</c> (por defecto sí);</item>
/// <item>el saldo en valor de una bodega en ámbito cooperativa es su cantidad × el promedio del ámbito (T18), nunca Σ
/// <c>TotalCost</c> de la bodega; sin filtros y con alcance total es el valor del ámbito;</item>
/// <item>sin <c>Inventory.Costs.Read</c> las columnas de costo y valor van vacías y la nota lo dice;</item>
/// <item>el alcance se aplica en la consulta (<see cref="IAlcanceDeInventario"/>): una bodega fuera es 404.</item>
/// </list>
/// </summary>
public sealed record KardexReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Location = null, bool IncludeCostAdjustments = true)
    : IRequest<Result<TablaExportable>>;

public sealed class KardexReportQueryHandler(
    IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IPermissionChecker permisos, IDateTimeService reloj)
    : IRequestHandler<KardexReportQuery, Result<TablaExportable>>
{
    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Fecha de operación", TipoDeColumna.Fecha),
        new("Registrado", TipoDeColumna.Texto),
        new("Documento", TipoDeColumna.Texto),
        new("Motivo", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Ubicación", TipoDeColumna.Texto),
        new("Lote/serie", TipoDeColumna.Texto),
        new("Entrada", TipoDeColumna.Cantidad),
        new("Salida", TipoDeColumna.Cantidad),
        new("Saldo (cantidad)", TipoDeColumna.Cantidad),
        new("Costo unitario", TipoDeColumna.Costo),
        new("Valor entrada", TipoDeColumna.Moneda),
        new("Valor salida", TipoDeColumna.Moneda),
        new("Saldo (valor)", TipoDeColumna.Moneda),
        new("Costo promedio", TipoDeColumna.Costo),
        new("Documento", TipoDeColumna.Texto, "_documento"),
        new("Producto", TipoDeColumna.Texto, "_producto"),
        new("Bodega", TipoDeColumna.Texto, "_bodega"),
    ];

    public async Task<Result<TablaExportable>> Handle(KardexReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        if (f.Product is not { } productoPublico)
            return Result.Failure<TablaExportable>(new ErrorConDatos("Validation.Invalid", "Indicá el producto del kardex.", new { field = "product" }));
        var producto = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == productoPublico, ct);
        if (producto is null) return Result.Failure<TablaExportable>(ErroresDelDocumento.ProductoInexistente());

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        int? bodegaFiltro = null;
        if (f.Warehouse is { } wp)
        {
            var id = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == wp).Select(w => (int?)w.Id).FirstOrDefaultAsync(ct);
            if (id is not int b || !alcance.IncluyeBodega(b)) return Result.Failure<TablaExportable>(ErroresDeAlcance.BodegaInexistente());
            bodegaFiltro = b;
        }
        int? ubicacionFiltro = null;
        if (request.Location is { } lp)
        {
            var ubicacion = await db.WarehouseLocations.AsNoTracking().Where(l => l.PublicId == lp).Select(l => new { l.Id, l.WarehouseId }).FirstOrDefaultAsync(ct);
            if (ubicacion is null || !alcance.IncluyeBodega(ubicacion.WarehouseId) || (bodegaFiltro is int bf && bf != ubicacion.WarehouseId))
                return Result.Failure<TablaExportable>(ErroresDelDocumento.UbicacionInexistente());
            ubicacionFiltro = ubicacion.Id;
            bodegaFiltro ??= ubicacion.WarehouseId;
        }

        var hoy = reloj.HoyLocal;
        var desde = f.Desde(hoy);
        var hasta = f.Hasta(hoy);
        var conCostos = await permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, ct);

        // Todos los hechos del producto hasta la fecha final: el promedio de un ámbito cooperativa se forma en todas las bodegas.
        var hechos = await db.KardexEntries.AsNoTracking()
            .Where(k => k.ProductId == producto.Id && k.OperationDate <= hasta)
            .OrderBy(k => k.OperationDate).ThenBy(k => k.Id)
            .ToListAsync(ct);

        bool Visible(int bodega) => bodegaFiltro is int b ? bodega == b : alcance.IncluyeBodega(bodega);
        var todoElAmbito = bodegaFiltro is null && ubicacionFiltro is null && alcance.TodasLasBodegas;

        var ambitos = new Dictionary<int, (decimal Cantidad, decimal Valor, decimal Promedio)>();
        var porBodega = new Dictionary<int, decimal>();
        var ambitoDeBodega = new Dictionary<int, int>();
        var enUbicacion = 0m;

        decimal SaldoCantidad() => ubicacionFiltro is not null ? enUbicacion : porBodega.Where(b => Visible(b.Key)).Sum(b => b.Value);
        decimal SaldoValor()
        {
            if (todoElAmbito) return ambitos.Values.Sum(a => a.Valor);
            if (ubicacionFiltro is not null && bodegaFiltro is int ub)
                return Math.Round(enUbicacion * ambitos.GetValueOrDefault(ambitoDeBodega.GetValueOrDefault(ub)).Promedio, 2, MidpointRounding.AwayFromZero);
            return porBodega.Where(b => Visible(b.Key)).Sum(b =>
            {
                var ambito = ambitoDeBodega.GetValueOrDefault(b.Key);
                return ambito == b.Key && ambito != 0
                    ? ambitos.GetValueOrDefault(ambito).Valor
                    : Math.Round(b.Value * ambitos.GetValueOrDefault(ambito).Promedio, 2, MidpointRounding.AwayFromZero);
            });
        }
        decimal PromedioVisible()
        {
            var visibles = ambitoDeBodega.Where(a => Visible(a.Key)).Select(a => a.Value).Distinct().ToList();
            return visibles.Count == 1 ? ambitos.GetValueOrDefault(visibles[0]).Promedio : 0m;
        }

        void Aplicar(Domain.Entities.Inventory.Transactions.KardexEntry k)
        {
            var anterior = ambitos.GetValueOrDefault(k.CostScopeWarehouseId);
            var cantidad = anterior.Cantidad + k.QuantityBase;
            var valor = anterior.Valor + k.TotalCost;
            var promedio = cantidad > 0m ? Math.Max(0m, Redondeo.CostoUnitario(valor / cantidad)) : (k.Kind == KardexEntryKind.Entry ? k.UnitCost : anterior.Promedio);
            ambitos[k.CostScopeWarehouseId] = (cantidad, valor, promedio);
            porBodega[k.WarehouseId] = porBodega.GetValueOrDefault(k.WarehouseId) + k.QuantityBase;
            ambitoDeBodega[k.WarehouseId] = k.CostScopeWarehouseId;
            if (ubicacionFiltro == k.LocationId) enUbicacion += k.QuantityBase;
        }

        var antes = hechos.Where(k => k.OperationDate < desde).ToList();
        foreach (var k in antes) Aplicar(k);

        var enRango = hechos.Where(k => k.OperationDate >= desde).ToList();
        var mostrados = enRango.Where(k => Visible(k.WarehouseId) && (ubicacionFiltro is null || k.LocationId == ubicacionFiltro)
            && (request.IncludeCostAdjustments || k.Kind != KardexEntryKind.CostAdjustment)).Select(k => k.Id).ToHashSet();

        var documentoIds = enRango.Where(k => mostrados.Contains(k.Id)).Select(k => k.DocumentId).Distinct().ToList();
        var documentos = await db.InventoryDocuments.AsNoTracking().Where(d => documentoIds.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Class, d.Prefix, d.Number, Tipo = d.DocumentType!.Code })
            .ToDictionaryAsync(d => d.Id, ct);
        var bodegaIds = hechos.Select(k => k.WarehouseId).Distinct().ToList();
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => bodegaIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => new { w.Code, w.PublicId }, ct);
        var ubicacionIds = hechos.Select(k => k.LocationId).Distinct().ToList();
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().Where(l => ubicacionIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, ct);

        var filas = new List<FilaExportable>
        {
            new([desde.AddDays(-1), null, "Saldo inicial", null, null, null, null, null, null, SaldoCantidad(), null, null, null,
                conCostos ? SaldoValor() : null, conCostos ? PromedioVisible() : null, null, producto.PublicId.ToString(), null], Resaltada: true),
        };

        foreach (var k in enRango)
        {
            Aplicar(k);
            if (!mostrados.Contains(k.Id)) continue;
            var d = documentos.GetValueOrDefault(k.DocumentId);
            var bodega = bodegas.GetValueOrDefault(k.WarehouseId);
            filas.Add(new FilaExportable(
            [
                k.OperationDate,
                k.RegisteredAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                d is null ? null : $"{d.Class} · {d.Tipo} {VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number)}".Trim(),
                Motivo(k.Kind, k.Reason) + (k.ReversesEntryId is null ? string.Empty : " (anulación)"),
                bodega?.Code,
                ubicaciones.GetValueOrDefault(k.LocationId),
                null,
                k.QuantityBase > 0m ? k.QuantityBase : null,
                k.QuantityBase < 0m ? -k.QuantityBase : null,
                SaldoCantidad(),
                conCostos ? k.UnitCost : null,
                conCostos && k.TotalCost > 0m ? k.TotalCost : null,
                conCostos && k.TotalCost < 0m ? -k.TotalCost : null,
                conCostos ? SaldoValor() : null,
                conCostos ? ambitos.GetValueOrDefault(k.CostScopeWarehouseId).Promedio : null,
                d?.PublicId.ToString(),
                producto.PublicId.ToString(),
                bodega?.PublicId.ToString(),
            ]));
        }

        var notas = new List<string> { "Orden: fecha de operación y registro. Cantidades en la unidad base del producto." };
        if (!conCostos) notas.Add("Sin el permiso de ver costos (Inventory.Costs.Read) las columnas de costo y valor van vacías.");
        if (!todoElAmbito) notas.Add("El saldo en valor de una bodega en ámbito cooperativa es su cantidad por el promedio del ámbito.");
        var subtitulo = $"{producto.Code} · {producto.Name} · del {desde:yyyy-MM-dd} al {hasta:yyyy-MM-dd}"
            + (bodegaFiltro is int bfi && bodegas.TryGetValue(bfi, out var bb) ? $" · bodega {bb.Code}" : string.Empty);
        return Result.Success(new TablaExportable("Kardex", subtitulo, Columnas, filas, null, notas));
    }

    /// <summary>El tipo y el motivo de una línea del kardex, en palabras.</summary>
    public static string Motivo(KardexEntryKind kind, KardexReason reason) => (kind, reason) switch
    {
        (KardexEntryKind.Entry, KardexReason.Normal) => "Entrada",
        (KardexEntryKind.Exit, KardexReason.Normal) => "Salida",
        (_, KardexReason.VoidDifference) => "Ajuste de costo: diferencia de anulación",
        (_, KardexReason.RoundingResidue) => "Ajuste de costo: residuo de redondeo",
        (_, KardexReason.NegativeRegularization) => "Ajuste de costo: regularización de negativo",
        (_, KardexReason.Retroactive) => "Ajuste de costo: retroactivo",
        (_, KardexReason.PriceDifference) => "Ajuste de costo: diferencia de precio",
        (_, KardexReason.LandedCost) => "Ajuste de costo: costos adicionales",
        (_, KardexReason.MethodChange) => "Ajuste de costo: cambio de método",
        _ => kind.ToString(),
    };
}
