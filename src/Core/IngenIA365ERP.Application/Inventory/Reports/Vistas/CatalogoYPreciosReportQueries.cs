using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports.Vistas;

/// <summary>
/// La vista <c>products</c> de <c>/api/reports/inventory</c> (2026-11-26, pedido del dueño: «exportar todo el cargado de productos
/// completo, con precios, costo, etc.»): una fila por producto con sus datos de catálogo (tipo, estado, categoría, marca, unidad base,
/// referencia, códigos de barras, grupo contable, concepto de retención, IVA en la venta, impuestos, si se compra y se vende), la
/// existencia física en las bodegas del alcance, el costo (promedio, último y valor, sólo con <c>Inventory.Costs.Read</c>; sin él las
/// columnas no salen) y una columna de precio por cada lista activa y vigente hoy, en la unidad base. Filtros <c>category</c> y
/// <c>product</c>. Se descarga desde <c>/inventario/productos</c> con «Exportar». (nuevo)
/// </summary>
public sealed record ProductsCatalogReportQuery(FiltrosDeInformeDeInventario Filtros) : IRequest<Result<TablaExportable>>;

/// <summary>
/// La vista <c>price-lists</c> de <c>/api/reports/inventory</c> (2026-11-26, pedido del dueño): una fila por precio de cada lista (lista,
/// si incluye impuestos, vigencia, estado, producto, unidad y precio) con el costo promedio en esa unidad y el margen sobre el precio
/// cuando quien consulta tiene <c>Inventory.Costs.Read</c>. Filtro propio <c>priceList</c> (una sola lista, desde su detalle) y los
/// comunes <c>category</c> y <c>product</c>. Se descarga desde <c>/ventas/listas-de-precios</c> y desde el detalle de una lista. (nuevo)
/// </summary>
public sealed record PriceListsReportQuery(FiltrosDeInformeDeInventario Filtros, Guid? PriceList = null) : IRequest<Result<TablaExportable>>;

/// <summary>El costo de un producto para las dos vistas: el del ámbito cooperativa o, si el costo va por bodega, la suma de sus ámbitos.</summary>
internal static class CostoParaInformes
{
    public sealed record Costo(decimal Cantidad, decimal Valor, decimal Promedio, decimal Ultimo);

    public static async Task<Dictionary<int, Costo>> LeerAsync(IApplicationDbContext db, IReadOnlyCollection<int>? productos, CancellationToken ct)
    {
        var consulta = db.CostStates.AsNoTracking();
        if (productos is not null) consulta = consulta.Where(c => productos.Contains(c.ProductId));
        var estados = await consulta.Select(c => new { c.ProductId, c.ScopeWarehouseId, c.Quantity, c.Value, c.AverageCost, c.LastUnitCost }).ToListAsync(ct);
        return estados.GroupBy(c => c.ProductId).ToDictionary(g => g.Key, g =>
        {
            var cooperativa = g.FirstOrDefault(c => c.ScopeWarehouseId == 0);
            if (cooperativa is not null) return new Costo(cooperativa.Quantity, cooperativa.Value, cooperativa.AverageCost, cooperativa.LastUnitCost);
            var cantidad = g.Sum(c => c.Quantity);
            var valor = g.Sum(c => c.Value);
            return new Costo(cantidad, valor, cantidad > 0m ? Math.Round(valor / cantidad, 6) : g.Max(c => c.AverageCost), g.Max(c => c.LastUnitCost));
        });
    }
}

public sealed class ProductsCatalogReportQueryHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IPermissionChecker permisos, IDateTimeService reloj)
    : IRequestHandler<ProductsCatalogReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "products", "Catálogo de productos",
        "Todos los productos con sus datos, existencia, costo (con permiso de costos) y el precio de cada lista vigente.",
        "catalogo-de-productos", ["category", "product"], []);

    public async Task<Result<TablaExportable>> Handle(ProductsCatalogReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var conCostos = await permisos.HasPermissionAsync(ValuationReportQueryHandler.PermisoDeCostos, ct);

        var consulta = db.Products.AsNoTracking();
        if (f.Category is { } cp) consulta = consulta.Where(p => p.Category != null && p.Category.PublicId == cp);
        if (f.Product is { } pp) consulta = consulta.Where(p => p.PublicId == pp);

        var productos = await consulta
            .OrderBy(p => p.Code)
            .Select(p => new
            {
                p.Id, p.PublicId, p.Code, p.Name, p.ShortName, p.Kind, p.Status, p.Reference, p.VatSaleTreatment,
                p.IsPurchasable, p.IsSellable, p.PriceEnteredAtSale, p.BaseUnitId,
                Categoria = p.Category != null ? p.Category.Code + " · " + p.Category.Name : null,
                Marca = p.Brand != null ? p.Brand.Name : null,
                Unidad = p.BaseUnit != null ? p.BaseUnit.Code : null,
                Grupo = p.AccountingGroup != null ? p.AccountingGroup.Code + " · " + p.AccountingGroup.Name : null,
                Retencion = p.WithholdingConcept != null ? p.WithholdingConcept.Name : null,
            })
            .ToListAsync(ct);
        var ids = productos.Select(p => p.Id).ToList();

        var barras = (await db.ProductBarcodes.AsNoTracking().Where(b => ids.Contains(b.ProductId))
                .Select(b => new { b.ProductId, b.Barcode, b.IsPrimary }).ToListAsync(ct))
            .GroupBy(b => b.ProductId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Barcode).Select(b => b.Barcode)));
        var impuestos = (await db.ProductTaxes.AsNoTracking().Where(t => ids.Contains(t.ProductId))
                .Select(t => new { t.ProductId, Codigo = t.TaxDefinition != null ? t.TaxDefinition.Code : null, t.TaxRateCode }).ToListAsync(ct))
            .GroupBy(t => t.ProductId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(t => t.TaxRateCode ?? t.Codigo).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct()));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var existencias = (await db.StockBalances.AsNoTracking().PorBodega(alcance, s => s.WarehouseId)
                .Where(s => ids.Contains(s.ProductId))
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, Fisico = g.Sum(s => s.Physical) })
                .ToListAsync(ct))
            .ToDictionary(x => x.ProductId, x => x.Fisico);
        var costos = conCostos ? await CostoParaInformes.LeerAsync(db, ids, ct) : [];

        var listas = await db.PriceLists.AsNoTracking()
            .Where(l => l.IsActive && l.ValidFrom <= hoy && (l.ValidTo == null || l.ValidTo >= hoy))
            .OrderBy(l => l.Code)
            .Select(l => new { l.Id, l.Code, l.IncludesTaxes })
            .ToListAsync(ct);
        var listaIds = listas.Select(l => l.Id).ToList();
        var precios = (await db.PriceListItems.AsNoTracking()
                .Where(i => listaIds.Contains(i.PriceListId) && ids.Contains(i.ProductId))
                .Select(i => new { i.PriceListId, i.ProductId, i.UnitId, i.Price })
                .ToListAsync(ct))
            .ToLookup(i => (i.PriceListId, i.ProductId));

        var columnas = new List<ColumnaExportable>
        {
            new("Código"), new("Nombre"), new("Nombre corto"), new("Tipo"), new("Estado"), new("Categoría"), new("Marca"),
            new("Unidad base"), new("Referencia"), new("Códigos de barras"), new("Grupo contable"), new("Concepto de retención"),
            new("IVA en la venta"), new("Impuestos"), new("Se compra"), new("Se vende"), new("Precio digitado en la venta"),
            new("Existencia", TipoDeColumna.Cantidad),
        };
        if (conCostos)
            columnas.AddRange([new("Costo promedio", TipoDeColumna.Costo), new("Último costo", TipoDeColumna.Costo), new("Valor del inventario", TipoDeColumna.Moneda)]);
        columnas.AddRange(listas.Select(l => new ColumnaExportable($"Precio {l.Code}{(l.IncludesTaxes ? " (con impuestos)" : "")}", TipoDeColumna.Moneda)));
        columnas.Add(new("Producto", TipoDeColumna.Texto, "_producto"));

        var filas = productos.Select(p =>
        {
            var valores = new List<object?>
            {
                p.Code, p.Name, p.ShortName, TextoDeTipo(p.Kind), TextoDeEstado(p.Status), p.Categoria, p.Marca, p.Unidad, p.Reference,
                barras.GetValueOrDefault(p.Id), p.Grupo, p.Retencion, TextoDeIva(p.VatSaleTreatment), impuestos.GetValueOrDefault(p.Id),
                p.IsPurchasable ? "Sí" : "No", p.IsSellable ? "Sí" : "No", p.PriceEnteredAtSale ? "Sí" : "No",
                existencias.TryGetValue(p.Id, out var fisico) ? fisico : (decimal?)null,
            };
            if (conCostos)
            {
                var costo = costos.GetValueOrDefault(p.Id);
                valores.AddRange([costo?.Promedio, costo?.Ultimo, costo?.Valor]);
            }
            foreach (var l in listas)
                valores.Add(precios[(l.Id, p.Id)].FirstOrDefault(i => i.UnitId == p.BaseUnitId)?.Price);
            valores.Add(p.PublicId.ToString());
            return new FilaExportable(valores, Resaltada: p.Status != ProductStatus.Active);
        }).ToList();

        var notas = new List<string>
        {
            "Existencia: la cantidad física en la unidad base, sumada en las bodegas a su alcance.",
            "Precios: los de cada lista activa y vigente hoy, en la unidad base del producto; vacío si la lista no lo tiene. Los precios en otras unidades están en el informe «Precios por lista».",
        };
        if (!conCostos) notas.Add("Las columnas de costo no se incluyen: exigen el permiso de ver costos de inventario.");
        return Result.Success(new TablaExportable("Catálogo de productos", $"{productos.Count} productos · precios vigentes al {hoy:dd/MM/yyyy}",
            columnas, filas, null, notas));
    }

    internal static string TextoDeTipo(ProductKind k) => k switch
    {
        ProductKind.Inventoriable => "Inventariable",
        ProductKind.Service => "Servicio",
        ProductKind.Combo => "Combo",
        ProductKind.Kit => "Kit",
        ProductKind.Template => "Plantilla",
        ProductKind.Variant => "Variante",
        _ => k.ToString(),
    };

    internal static string TextoDeEstado(ProductStatus s) => s switch
    {
        ProductStatus.Active => "Activo",
        ProductStatus.Inactive => "Inactivo",
        ProductStatus.Blocked => "Bloqueado",
        _ => s.ToString(),
    };

    private static string TextoDeIva(VatSaleTreatment t) => t switch
    {
        VatSaleTreatment.Taxed => "Gravado",
        VatSaleTreatment.Exempt => "Exento",
        VatSaleTreatment.Excluded => "Excluido",
        _ => t.ToString(),
    };
}

public sealed class PriceListsReportQueryHandler(IApplicationDbContext db, IPermissionChecker permisos, IDateTimeService reloj)
    : IRequestHandler<PriceListsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeInventario Vista = new(
        "price-lists", "Precios por lista",
        "Cada precio de cada lista con su unidad y vigencia; con permiso de costos, el costo promedio y el margen.",
        "precios-por-lista", ["category", "product"], ["priceList"]);

    public async Task<Result<TablaExportable>> Handle(PriceListsReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        var conCostos = await permisos.HasPermissionAsync(ValuationReportQueryHandler.PermisoDeCostos, ct);

        var listas = db.PriceLists.AsNoTracking();
        string subtitulo = "Todas las listas";
        if (request.PriceList is { } lp)
        {
            var lista = await listas.Where(l => l.PublicId == lp).Select(l => new { l.Id, l.Code, l.Name }).FirstOrDefaultAsync(ct);
            if (lista is null) return Result.Failure<TablaExportable>(Error.NotFound);
            listas = listas.Where(l => l.Id == lista.Id);
            subtitulo = $"Lista {lista.Code} · {lista.Name}";
        }
        var productos = db.Products.AsNoTracking();
        if (f.Category is { } cp) productos = productos.Where(p => p.Category != null && p.Category.PublicId == cp);
        if (f.Product is { } pp) productos = productos.Where(p => p.PublicId == pp);

        var filas = await (
                from i in db.PriceListItems.AsNoTracking()
                join l in listas on i.PriceListId equals l.Id
                join p in productos on i.ProductId equals p.Id
                join u in db.UnitsOfMeasure.AsNoTracking() on i.UnitId equals u.Id
                orderby l.Code, p.Code, u.Code
                select new
                {
                    ListaCodigo = l.Code, ListaNombre = l.Name, l.IncludesTaxes, l.ValidFrom, l.ValidTo, l.IsActive,
                    ProductoId = p.Id, ProductoPublico = p.PublicId, ProductoCodigo = p.Code, ProductoNombre = p.Name, p.Status, p.BaseUnitId,
                    i.UnitId, Unidad = u.Code, i.Price,
                })
            .ToListAsync(ct);

        Dictionary<int, CostoParaInformes.Costo> costos = [];
        Dictionary<(int, int), decimal> factores = [];
        if (conCostos)
        {
            var ids = filas.Select(x => x.ProductoId).Distinct().ToList();
            costos = await CostoParaInformes.LeerAsync(db, ids, ct);
            factores = (await db.ProductUnits.AsNoTracking().Where(u => ids.Contains(u.ProductId))
                    .Select(u => new { u.ProductId, u.UnitId, u.Factor }).ToListAsync(ct))
                .GroupBy(u => (u.ProductId, u.UnitId)).ToDictionary(g => g.Key, g => g.First().Factor);
        }

        var columnas = new List<ColumnaExportable>
        {
            new("Lista"), new("Nombre de la lista"), new("Incluye impuestos"), new("Vigente desde", TipoDeColumna.Fecha),
            new("Vigente hasta", TipoDeColumna.Fecha), new("Estado de la lista"), new("Código del producto"), new("Producto"),
            new("Estado del producto"), new("Unidad"), new("Precio", TipoDeColumna.Moneda),
        };
        if (conCostos) columnas.AddRange([new("Costo promedio en la unidad", TipoDeColumna.Costo), new("Margen sobre el precio", TipoDeColumna.Porcentaje)]);
        columnas.Add(new("Producto", TipoDeColumna.Texto, "_producto"));

        var tabla = filas.Select(x =>
        {
            var vigente = x.IsActive && x.ValidFrom <= hoy && (x.ValidTo is null || x.ValidTo >= hoy);
            var valores = new List<object?>
            {
                x.ListaCodigo, x.ListaNombre, x.IncludesTaxes ? "Sí" : "No", x.ValidFrom, x.ValidTo,
                !x.IsActive ? "Inactiva" : vigente ? "Vigente" : x.ValidFrom > hoy ? "Futura" : "Vencida",
                x.ProductoCodigo, x.ProductoNombre, ProductsCatalogReportQueryHandler.TextoDeEstado(x.Status), x.Unidad, x.Price,
            };
            if (conCostos)
            {
                decimal? costoUnidad = null;
                if (costos.TryGetValue(x.ProductoId, out var costo))
                {
                    var factor = x.UnitId == x.BaseUnitId ? 1m : factores.GetValueOrDefault((x.ProductoId, x.UnitId), 0m);
                    if (factor > 0m) costoUnidad = Math.Round(costo.Promedio * factor, 6);
                }
                decimal? margen = costoUnidad is { } c && x.Price > 0m && !x.IncludesTaxes ? Math.Round((x.Price - c) / x.Price * 100m, 2) : null;
                valores.AddRange([costoUnidad, margen]);
            }
            valores.Add(x.ProductoPublico.ToString());
            return new FilaExportable(valores, Resaltada: !vigente);
        }).ToList();

        var notas = new List<string> { "Las filas resaltadas son de listas inactivas, vencidas o futuras." };
        if (conCostos) notas.Add("Margen = (precio − costo promedio en la unidad) ÷ precio. No se calcula en las listas con impuestos incluidos.");
        else notas.Add("Las columnas de costo no se incluyen: exigen el permiso de ver costos de inventario.");
        return Result.Success(new TablaExportable("Precios por lista", subtitulo, columnas, tabla, null, notas));
    }
}
