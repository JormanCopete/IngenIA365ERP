using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>Cómo se encontró el producto de una línea del XML (<see cref="SugerenciaDeLinea.MatchedBy"/>). (nuevo)</summary>
public static class FormasDeEmparejar
{
    /// <summary>La equivalencia recordada para ese proveedor y ese código (<c>INV_SupplierItemCodes</c>).</summary>
    public const string Equivalencia = "Equivalencia";

    /// <summary>El código de barras del XML (o el código del proveedor, que muchas veces es el código de barras): también da el empaque.</summary>
    public const string CodigoDeBarras = "CodigoDeBarras";

    /// <summary>La referencia del producto igual al código del proveedor: la pantalla la marca para confirmarla.</summary>
    public const string Referencia = "Referencia";

    /// <summary>El código interno del producto igual al código del proveedor: la pantalla la marca para confirmarla.</summary>
    public const string CodigoInterno = "CodigoInterno";

    /// <summary>¿La pantalla debe pedir que la confirmen? Las coincidencias de texto pueden ser casuales.</summary>
    public static bool PorConfirmar(string? forma) => forma is Referencia or CodigoInterno;
}

/// <summary>
/// El producto sugerido para una línea del XML: el producto, su clase, la unidad en que viene la línea, cómo se encontró y si la unidad
/// del XML coincide con una del producto (<see cref="UnitMatched"/> falso: el producto no tiene la unidad en que factura el proveedor).
/// (nuevo)
/// </summary>
public sealed record SugerenciaDeLinea(Guid ProductPublicId, ProductKind Kind, Guid UnitPublicId, string MatchedBy, bool UnitMatched);

/// <summary>Una equivalencia que la compra directa recuerda: el código del proveedor, el producto y el empaque (nulo = la unidad base). (nuevo)</summary>
public sealed record EquivalenciaARecordar(string Codigo, int ProductId, int? ProductUnitId);

/// <summary>
/// El emparejamiento de las líneas del XML del proveedor con los productos de la cooperativa, en un solo sitio (compra directa desde el
/// XML, decisión del dueño del 2026-11-14). Lo usan el prellenado de la factura y la compra directa. En este orden:
/// <list type="number">
/// <item>la equivalencia recordada para ese proveedor y ese código;</item>
/// <item>el código de barras del XML y, si no, el código del proveedor tomado como código de barras (los dos dan el empaque);</item>
/// <item>la referencia del producto igual al código del proveedor, sólo si es de <b>un único</b> producto;</item>
/// <item>el código interno del producto igual al código del proveedor.</item>
/// </list>
/// La unidad: la del empaque (código de barras o equivalencia); si no, la del producto cuyo código DIAN es el del XML (la base primero, luego
/// las de compra); si ninguna coincide, la de compra por defecto o la base, con <see cref="SugerenciaDeLinea.UnitMatched"/> falso. (nuevo)
/// </summary>
public static class EquivalenciasDelProveedor
{
    private sealed record UnidadDelProducto(int ProductUnitId, Guid UnitPublicId, string? DianCode, bool ParaCompra, bool PorDefecto);

    private sealed record ProductoConUnidades(int Id, Guid PublicId, ProductKind Kind, Guid BaseUnitPublicId, string? BaseDianCode, List<UnidadDelProducto> Unidades);

    /// <summary>Una sugerencia por línea, en el mismo orden; nula si ninguna regla encuentra un único producto.</summary>
    public static async Task<IReadOnlyList<SugerenciaDeLinea?>> SugerirAsync(IApplicationDbContext db, int? proveedorId, IReadOnlyList<LineaLeida> lineas,
        CancellationToken ct)
    {
        var codigos = lineas.Select(l => SupplierItemCode.Normalizar(l.SupplierItemCode)).Where(c => c.Length > 0).Distinct().ToList();
        var crudos = lineas.Select(l => l.SupplierItemCode?.Trim()).OfType<string>().Where(c => c.Length > 0).Distinct().ToList();
        var barras = lineas.Select(l => ProductBarcode.Normalizar(l.Barcode))
            .Concat(lineas.Select(l => ProductBarcode.Normalizar(l.SupplierItemCode)))
            .Where(b => b.Length > 0).Distinct().ToList();
        var internos = crudos.Select(CodigoDeCatalogo.Normalizar).OfType<string>().Distinct().ToList();

        var equivalencias = proveedorId is int proveedor && codigos.Count > 0
            ? await db.SupplierItemCodes.AsNoTracking().Where(e => e.SupplierPersonId == proveedor && codigos.Contains(e.ItemCode))
                .Select(e => new { e.ItemCode, e.ProductId, e.ProductUnitId }).ToListAsync(ct)
            : [];
        var porBarras = barras.Count == 0 ? []
            : await db.ProductBarcodes.AsNoTracking().Where(b => barras.Contains(b.Barcode))
                .Select(b => new { b.Barcode, b.ProductId, b.ProductUnitId }).ToListAsync(ct);
        var porReferencia = crudos.Count == 0 ? []
            : await db.Products.AsNoTracking().Where(p => p.Reference != null && crudos.Contains(p.Reference))
                .Select(p => new { p.Id, Reference = p.Reference! }).ToListAsync(ct);
        var porCodigo = internos.Count == 0 ? []
            : await db.Products.AsNoTracking().Where(p => internos.Contains(p.Code)).Select(p => new { p.Id, p.Code }).ToListAsync(ct);

        var ids = equivalencias.Select(e => e.ProductId).Concat(porBarras.Select(b => b.ProductId))
            .Concat(porReferencia.Select(p => p.Id)).Concat(porCodigo.Select(p => p.Id)).Distinct().ToList();
        var productos = ids.Count == 0 ? new Dictionary<int, ProductoConUnidades>()
            : (await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
                .Select(p => new ProductoConUnidades(p.Id, p.PublicId, p.Kind, p.BaseUnit!.PublicId, p.BaseUnit.DianUnitCode,
                    p.Units.Where(u => !u.IsDeleted)
                        .Select(u => new UnidadDelProducto(u.Id, u.Unit!.PublicId, u.Unit.DianUnitCode, u.UsedForPurchase, u.IsDefaultPurchase)).ToList()))
                .ToListAsync(ct)).ToDictionary(p => p.Id);

        return lineas.Select(l =>
        {
            var codigo = SupplierItemCode.Normalizar(l.SupplierItemCode);
            if (codigo.Length > 0 && equivalencias.FirstOrDefault(e => e.ItemCode == codigo) is { } e
                && productos.TryGetValue(e.ProductId, out var porEquivalencia))
                return ConEmpaque(porEquivalencia, e.ProductUnitId, FormasDeEmparejar.Equivalencia);

            foreach (var barra in new[] { ProductBarcode.Normalizar(l.Barcode), ProductBarcode.Normalizar(l.SupplierItemCode) }.Where(b => b.Length > 0))
                if (porBarras.FirstOrDefault(b => b.Barcode == barra) is { } b && productos.TryGetValue(b.ProductId, out var porBarra))
                    return ConEmpaque(porBarra, b.ProductUnitId, FormasDeEmparejar.CodigoDeBarras);

            var crudo = l.SupplierItemCode?.Trim();
            if (!string.IsNullOrEmpty(crudo))
            {
                var deLaReferencia = porReferencia.Where(p => p.Reference == crudo).Select(p => p.Id).Distinct().ToList();
                if (deLaReferencia.Count == 1 && productos.TryGetValue(deLaReferencia[0], out var porRef))
                    return PorUnidadDelXml(porRef, l.UnitCode, FormasDeEmparejar.Referencia);
                if (deLaReferencia.Count == 0 && CodigoDeCatalogo.Normalizar(crudo) is { } interno
                    && porCodigo.FirstOrDefault(p => p.Code == interno) is { } c && productos.TryGetValue(c.Id, out var porInterno))
                    return PorUnidadDelXml(porInterno, l.UnitCode, FormasDeEmparejar.CodigoInterno);
            }
            return (SugerenciaDeLinea?)null;
        }).ToList();
    }

    private static SugerenciaDeLinea ConEmpaque(ProductoConUnidades p, int? productUnitId, string forma) =>
        new(p.PublicId, p.Kind,
            productUnitId is int id && p.Unidades.FirstOrDefault(u => u.ProductUnitId == id) is { } empaque ? empaque.UnitPublicId : p.BaseUnitPublicId,
            forma, UnitMatched: true);

    private static SugerenciaDeLinea PorUnidadDelXml(ProductoConUnidades p, string? unidadDelXml, string forma)
    {
        var porDefecto = p.Unidades.FirstOrDefault(u => u.ParaCompra && u.PorDefecto)?.UnitPublicId ?? p.BaseUnitPublicId;
        var codigo = unidadDelXml?.Trim();
        if (string.IsNullOrEmpty(codigo)) return new(p.PublicId, p.Kind, porDefecto, forma, UnitMatched: true);
        if (string.Equals(p.BaseDianCode, codigo, StringComparison.OrdinalIgnoreCase)) return new(p.PublicId, p.Kind, p.BaseUnitPublicId, forma, UnitMatched: true);
        var deCompra = p.Unidades.OrderByDescending(u => u.ParaCompra).ThenByDescending(u => u.PorDefecto)
            .FirstOrDefault(u => string.Equals(u.DianCode, codigo, StringComparison.OrdinalIgnoreCase));
        return deCompra is not null
            ? new(p.PublicId, p.Kind, deCompra.UnitPublicId, forma, UnitMatched: true)
            : new(p.PublicId, p.Kind, porDefecto, forma, UnitMatched: false);
    }

    /// <summary>
    /// Guarda las equivalencias del proveedor (una viva por código): una que ya existe con otro producto o empaque se actualiza; quien llama
    /// guarda. Lo llama la compra directa dentro de su transacción.
    /// </summary>
    public static async Task RecordarAsync(IApplicationDbContext db, int proveedorId, IReadOnlyList<EquivalenciaARecordar> equivalencias, CancellationToken ct)
    {
        var porCodigo = equivalencias
            .Select(e => e with { Codigo = SupplierItemCode.Normalizar(e.Codigo) })
            .Where(e => e.Codigo.Length is > 0 and <= SupplierItemCode.LargoDelCodigo)
            .GroupBy(e => e.Codigo, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        if (porCodigo.Count == 0) return;

        var codigos = porCodigo.Keys.ToList();
        var existentes = await db.SupplierItemCodes.Where(e => e.SupplierPersonId == proveedorId && codigos.Contains(e.ItemCode)).ToListAsync(ct);
        foreach (var (codigo, e) in porCodigo)
        {
            if (existentes.FirstOrDefault(x => x.ItemCode == codigo) is { } existente)
            {
                if (existente.ProductId != e.ProductId) existente.ProductId = e.ProductId;
                if (existente.ProductUnitId != e.ProductUnitId) existente.ProductUnitId = e.ProductUnitId;
                continue;
            }
            db.SupplierItemCodes.Add(new SupplierItemCode { SupplierPersonId = proveedorId, ItemCode = codigo, ProductId = e.ProductId, ProductUnitId = e.ProductUnitId });
        }
    }
}
