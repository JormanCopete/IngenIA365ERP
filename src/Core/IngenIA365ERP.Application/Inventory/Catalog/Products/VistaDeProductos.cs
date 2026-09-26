using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Catalog.Categories;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Cómo se arma el <c>ProductDto</c> (feature 012, T218–T220; contracts/api.md §3.5, §3.6): categoría con su ruta, marca,
/// unidad base, grupo, concepto de retención, unidades alternas, códigos de barras vivos, impuestos con la tarifa vigente
/// hoy de su código, imágenes (adjuntos disponibles del dueño <see cref="DuenoDeImagenes"/>) y si tiene movimientos. Lo
/// usan los comandos y la consulta del producto, así todos devuelven lo mismo. (nuevo)
/// </summary>
public static class VistaDeProductos
{
    /// <summary>El tipo de dueño de las imágenes del producto en <c>COR_Attachments</c> (T41; data-model §1.6).</summary>
    public const string DuenoDeImagenes = Attachments.Common.AdjuntosDeModulo.ProductoDeInventario;

    public static async Task<ProductDto> ArmarAsync(IApplicationDbContext db, int productoId, DateOnly hoy, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Brand).Include(x => x.BaseUnit).Include(x => x.AccountingGroup).Include(x => x.WithholdingConcept)
            .FirstAsync(x => x.Id == productoId, ct);

        var categorias = await db.ProductCategories.AsNoTracking().ToListAsync(ct);
        var porId = categorias.ToDictionary(c => c.Id);
        var categoria = p.Category!;

        var unidades = await UnidadesAsync(db, p.Id, ct);
        var codigos = await CodigosAsync(db, p, unidades, ct);
        var impuestos = await ImpuestosAsync(db, p.Id, hoy, ct);
        var imagenes = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityType == DuenoDeImagenes && a.OwnerEntityPublicId == p.PublicId && a.Status == EstadoDeAdjunto.Available)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ProductImageDto(a.PublicId, a.FileName))
            .ToListAsync(ct);

        return new ProductDto(
            p.PublicId, p.Code, p.Name, p.ShortName, p.Description, p.Kind, p.Status,
            new ProductCategoryRefDto(categoria.PublicId, categoria.Code, categoria.Name, VistaDeCategorias.RutaLegible(categoria, porId)),
            p.Brand is { } b ? new CatalogRefDto(b.PublicId, b.Code, b.Name) : null,
            Unidad(p.BaseUnit!),
            p.AccountingGroup is { } g ? new CatalogRefDto(g.PublicId, g.Code, g.Name) : null,
            p.VatSaleTreatment,
            p.WithholdingConcept is { } c ? new CatalogRefDto(c.PublicId, c.Code, c.Name) : null,
            p.Reference, p.Weight, p.Volume, p.TracksLot, p.TracksSerial, p.TracksExpiry, p.IsPurchasable, p.IsSellable,
            unidades, codigos, impuestos, imagenes,
            await ReglasDeProducto.TieneMovimientosAsync(db, p.Id, ct),
            p.CreatedAt, p.CreatedBy, p.UpdatedAt);
    }

    public static async Task<IReadOnlyList<ProductUnitDto>> UnidadesAsync(IApplicationDbContext db, int productoId, CancellationToken ct) =>
        (await db.ProductUnits.AsNoTracking().Include(u => u.Unit).Where(u => u.ProductId == productoId).ToListAsync(ct))
        .OrderBy(u => u.Factor).ThenBy(u => u.Unit!.Code)
        .Select(Unidad)
        .ToList();

    public static ProductUnitDto Unidad(ProductUnit u) =>
        new(u.PublicId, Unidad(u.Unit!), u.Factor, u.Usage, u.IsDefaultPurchase, u.IsDefaultSale);

    public static UnitRefDto Unidad(UnitOfMeasure u) => new(u.PublicId, u.Code, u.Name, u.AllowedDecimals);

    public static async Task<IReadOnlyList<ProductBarcodeDto>> CodigosAsync(
        IApplicationDbContext db, Product producto, IReadOnlyList<ProductUnitDto> unidades, CancellationToken ct)
    {
        var filas = await db.ProductBarcodes.AsNoTracking().Include(b => b.ProductUnit).ThenInclude(u => u!.Unit)
            .Where(b => b.ProductId == producto.Id).OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Barcode).ToListAsync(ct);
        var baseCodigo = producto.BaseUnit?.Code
            ?? await db.UnitsOfMeasure.Where(u => u.Id == producto.BaseUnitId).Select(u => u.Code).FirstAsync(ct);
        return filas.Select(b => new ProductBarcodeDto(b.PublicId, b.Barcode, b.ProductUnit?.PublicId, b.ProductUnit?.Unit?.Code ?? baseCodigo, b.IsPrimary)).ToList();
    }

    /// <summary>Los impuestos vivos con la tarifa de su código vigente a <paramref name="hoy"/> (o la última, si ninguna rige hoy).</summary>
    public static async Task<IReadOnlyList<ProductTaxDto>> ImpuestosAsync(IApplicationDbContext db, int productoId, DateOnly hoy, CancellationToken ct)
    {
        var filas = await db.ProductTaxes.AsNoTracking().Include(t => t.TaxDefinition).Where(t => t.ProductId == productoId).ToListAsync(ct);
        var codigos = filas.Select(f => f.TaxRateCode).OfType<string>().ToList();
        var tarifas = await db.TaxRates.AsNoTracking().Where(r => codigos.Contains(r.Code)).ToListAsync(ct);
        return filas.OrderBy(f => f.TaxDefinition!.Kind).ThenBy(f => f.TaxDefinition!.Code).Select(f =>
        {
            var d = f.TaxDefinition!;
            var candidatas = tarifas.Where(r => r.TaxDefinitionId == d.Id && r.Code == f.TaxRateCode).ToList();
            var t = candidatas.FirstOrDefault(r => r.ValidFrom <= hoy && (r.ValidTo is null || r.ValidTo >= hoy))
                ?? candidatas.OrderByDescending(r => r.ValidFrom).FirstOrDefault();
            return new ProductTaxDto(
                new TaxDefinitionRefDto(d.PublicId, d.Code, d.Name, d.Kind),
                t is null ? null : new TaxRateRefDto(t.PublicId, t.Code, t.Rate, t.AmountPerUnit, t.ValidFrom, t.ValidTo),
                f.TaxableUnitsPerBaseUnit);
        }).ToList();
    }
}
