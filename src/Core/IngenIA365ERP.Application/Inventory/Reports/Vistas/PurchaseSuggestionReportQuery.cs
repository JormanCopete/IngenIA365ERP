using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>purchase-suggestion</c> de <c>/api/reports/inventory</c> (feature 012, I6, US17, T964; FR-035, FR-086; contracts/api.md §27): por
/// producto y bodega con política de reorden, lo que <b>hay que pedir</b> —las filas cuya posición es igual o menor que el punto de reorden—,
/// con la posición (disponible + en tránsito + por recibir de las órdenes aprobadas sin recibir, I5), mínimo, punto, máximo y el sugerido
/// (máximo − posición), todo por <see cref="EvaluacionDeReposicion"/> y <c>CalculoDeReposicion</c> —lo mismo que dicen la vista
/// <c>reorder-alerts</c> y la revisión nocturna—, más el <b>último costo</b> y el <b>proveedor habitual</b>: los de la última recepción de compra
/// confirmada del producto (en cualquier bodega). El último costo sólo con <c>Inventory.Costs.Read</c>. Filtros <c>warehouse</c>,
/// <c>category</c>, <c>product</c> y el propio <c>supplier</c> (sólo lo que habitualmente se le compra a ese proveedor). Alcance por bodega
/// (<see cref="IAlcanceDeInventario"/>). (nuevo)
/// </summary>
public sealed record PurchaseSuggestionReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? Supplier = null) : IRequest<Result<TablaExportable>>;

public sealed class PurchaseSuggestionReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IPermissionChecker permisos,
    EvaluacionDeReposicion evaluacion,
    AnaliticaDeInventario analitica)
    : IRequestHandler<PurchaseSuggestionReportQuery, Result<TablaExportable>>
{
    /// <summary>Lo que la vista declara al publicarse (T966).</summary>
    public static readonly VistaDeInformeDeInventario Vista = new(
        "purchase-suggestion", "Sugerido de compras",
        "Lo que está en o bajo su punto de reorden, con el sugerido para llegar al máximo, el último costo y el proveedor habitual.",
        "sugerido-de-compras", ["warehouse", "category", "product"], ["supplier"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Producto", TipoDeColumna.Texto),
        new("Bodega", TipoDeColumna.Texto),
        new("Posición", TipoDeColumna.Cantidad),
        new("Mínimo", TipoDeColumna.Cantidad),
        new("Punto de reorden", TipoDeColumna.Cantidad),
        new("Máximo", TipoDeColumna.Cantidad),
        new("Sugerido", TipoDeColumna.Cantidad),
        new("Último costo", TipoDeColumna.Costo),
        new("Proveedor habitual", TipoDeColumna.Texto),
        new("Producto", TipoDeColumna.Texto, "_producto"),
    ];

    public async Task<Result<TablaExportable>> Handle(PurchaseSuggestionReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var ambito = await analitica.AmbitoAsync(f, alcance, ct);
        if (ambito.IsFailure) return Result.Failure<TablaExportable>(ambito.Error);
        var a = ambito.Value;

        var politicas = db.ReorderPolicies.AsNoTracking().PorBodega(alcance, r => r.WarehouseId);
        if (a.Bodegas is not null)
        {
            var bodegas = a.Bodegas.ToArray();
            politicas = politicas.Where(r => bodegas.Contains(r.WarehouseId));
        }
        if (a.ProductoId is int producto) politicas = politicas.Where(r => r.ProductId == producto);
        if (a.CategoriaId is int categoria) politicas = politicas.Where(r => db.Products.Any(p => p.Id == r.ProductId && p.CategoryId == categoria));

        var pedir = (await evaluacion.EvaluarAsync(politicas, ct)).Where(x => x.Resultado.RequiereReorden).ToList();
        var ultimas = await UltimasRecepcionesAsync(pedir.Select(x => x.ProductId).Distinct().ToList(), ct);

        int? proveedor = null;
        if (request.Supplier is { } sp)
            proveedor = await db.People.AsNoTracking().Where(p => p.PublicId == sp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1;
        if (proveedor is int soloDe) pedir = pedir.Where(x => ultimas.TryGetValue(x.ProductId, out var u) && u.ProveedorId == soloDe).ToList();

        var conCostos = await permisos.HasPermissionAsync(AnaliticaDeInventario.PermisoDeCostos, ct);
        var nombres = await NombresAsync(ultimas.Values.Select(u => u.ProveedorId).OfType<int>().Distinct().ToList(), ct);

        var filas = pedir.Select(x =>
        {
            (int? ProveedorId, decimal CostoUnitario)? u = ultimas.TryGetValue(x.ProductId, out var ultima) ? ultima : null;
            return new FilaExportable(
            [
                $"{x.ProductCode} · {x.ProductName}", x.WarehouseCode, x.Resultado.Posicion, x.Minimo, x.PuntoDeReorden, x.Maximo, x.Resultado.Sugerido,
                conCostos ? u?.CostoUnitario : null, u?.ProveedorId is int pid ? nombres.GetValueOrDefault(pid) : null, x.ProductPublicId.ToString(),
            ], Resaltada: x.Resultado.Quiebre);
        }).ToList();

        var notas = new List<string>
        {
            "Posición = disponible + en tránsito hacia la bodega + por recibir (órdenes de compra aprobadas con saldo abierto). Sugerido = máximo − posición.",
            "Último costo y proveedor habitual: los de la última recepción de compra confirmada del producto.",
        };
        if (!conCostos) notas.Add(AnaliticaDeInventario.NotaSinCostos);
        return Result.Success(new TablaExportable("Sugerido de compras", a.Descripcion, Columnas, filas, null, notas));
    }

    /// <summary>La última recepción de compra confirmada de cada producto: su proveedor y el costo unitario con que entró (unidad base).</summary>
    private async Task<Dictionary<int, (int? ProveedorId, decimal CostoUnitario)>> UltimasRecepcionesAsync(IReadOnlyCollection<int> productos, CancellationToken ct)
    {
        if (productos.Count == 0) return [];
        var entradas = await (
                from k in db.KardexEntries.AsNoTracking()
                where productos.Contains(k.ProductId) && k.Kind == KardexEntryKind.Entry
                join d in db.InventoryDocuments.AsNoTracking() on k.DocumentId equals d.Id
                where d.Class == DocumentClass.PurchaseReceipt && d.Status == DocumentStatus.Confirmed
                select new { k.ProductId, k.Id, k.UnitCost, d.OperationDate, d.CounterpartyPersonId })
            .ToListAsync(ct);
        return entradas.GroupBy(e => e.ProductId)
            .ToDictionary(g => g.Key, g =>
            {
                var ultima = g.OrderByDescending(e => e.OperationDate).ThenByDescending(e => e.Id).First();
                return (ultima.CounterpartyPersonId, ultima.UnitCost);
            });
    }

    private async Task<Dictionary<int, string>> NombresAsync(IReadOnlyCollection<int> personas, CancellationToken ct)
    {
        if (personas.Count == 0) return [];
        return (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id))
                .Select(p => new { p.Id, p.TaxId, p.BusinessName, p.FirstName, p.LastName }).ToListAsync(ct))
            .ToDictionary(p => p.Id, p => $"{p.TaxId} {(string.IsNullOrWhiteSpace(p.BusinessName) ? $"{p.FirstName} {p.LastName}".Trim() : p.BusinessName)}".Trim());
    }
}
