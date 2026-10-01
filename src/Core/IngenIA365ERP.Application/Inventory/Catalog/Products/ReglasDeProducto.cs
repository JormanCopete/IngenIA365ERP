using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Domain.Common.Text;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>Los datos del producto que comparten el alta, la edición y la plantilla (sin código, unidades, códigos ni impuestos). (nuevo)</summary>
public sealed record DatosDeProducto(
    string Name,
    string? ShortName,
    string? Description,
    ProductKind Kind,
    Guid CategoryPublicId,
    Guid? BrandPublicId,
    Guid BaseUnitPublicId,
    Guid? AccountingGroupPublicId,
    VatSaleTreatment VatSaleTreatment,
    Guid? WithholdingConceptPublicId,
    string? Reference,
    decimal? Weight,
    decimal? Volume,
    bool TracksLot,
    bool TracksSerial,
    bool TracksExpiry,
    bool IsPurchasable = true,
    bool IsSellable = true);

/// <summary>Las referencias del producto ya resueltas contra el catálogo (la plantilla las carga en bloque). (nuevo)</summary>
public sealed record ReferenciasDeProducto(
    ProductCategory Categoria, Brand? Marca, UnitOfMeasure UnidadBase, AccountingGroup? Grupo, WithholdingConcept? Concepto);

/// <summary>
/// Lo que se sabe de la historia de un producto guardado para las reglas que dependen de ella (I6, T918; data-model §1.6):
/// movimientos (bloquean unidad base, grupo y clase), existencia distinta de cero y documentos en borrador o en aprobación que lo
/// citan (bloquean el seguimiento), y dependientes —variantes, componentes o ser componente— (bloquean la clase). (nuevo)
/// </summary>
public sealed record HistoriaDeProducto(bool TieneMovimientos, bool TieneExistencia = false, int Borradores = 0, bool TieneDependientes = false)
{
    public static HistoriaDeProducto Nueva { get; } = new(false);

    public bool SeguimientoBloqueado => TieneExistencia || Borradores > 0;
}

/// <summary>Un impuesto pedido para el producto: la definición, la tarifa (cualquier vigencia de su código) y las unidades gravables. (nuevo)</summary>
public sealed record ImpuestoPedido(Guid TaxDefinitionPublicId, Guid? TaxRatePublicId, decimal? TaxableUnitsPerBaseUnit);

/// <summary>Un impuesto ya resuelto contra el catálogo tributario. (nuevo)</summary>
public sealed record ImpuestoResuelto(TaxDefinition Definicion, TaxRate? Tarifa, decimal? UnidadesGravables);

/// <summary>
/// El <b>único</b> método de reglas del producto (feature 012, T217; FR-023 a FR-028; data-model §1.6, §1.9) que comparten el
/// alta unitaria, la edición y <c>ImportProductsCommand</c> —como <c>ImportAccountsCommand</c> reusa
/// <c>CreateAccountCommandHandler.AplicarReglasAsync</c>—, así la plantilla responde los mismos códigos que la pantalla:
/// <list type="bullet">
/// <item>clase y seguimiento (I6, T918): las seis clases; <c>TracksExpiry</c> exige <c>TracksLot</c>
/// (<c>.ExpiryRequiresLot</c>); ni servicios ni combos se controlan por lote o serie (<c>.TrackingNotApplicable</c>); una
/// variante nace de su plantilla (<c>Inventory.Variant.ParentRequired</c>); en un producto guardado la clase no cambia con
/// movimientos, variantes o componentes (<c>.KindLocked</c>) y las marcas de seguimiento no cambian con existencia distinta de
/// cero ni con borradores que lo citen (<c>.TrackingLocked</c>);</item>
/// <item>grupo contable obligatorio salvo en plantillas (la matriz asigna la cuenta por él) y concepto de retención
/// obligatorio salvo en plantillas y combos;</item>
/// <item>con movimientos, la unidad base no cambia (<c>.BaseUnitLocked</c>) y el grupo contable cambia sólo por
/// <c>ChangeProductAccountingGroupCommand</c> (<c>.UseReclassifyAccountingGroup</c>);</item>
/// <item>tratamiento de IVA ↔ impuestos: gravado exige exactamente un IVA con tarifa; exento o excluido, ninguno con tarifa
/// distinta de cero (<see cref="ValidarIva"/>);</item>
/// <item><c>SearchText</c> por <c>NormalizadorDeBusqueda</c>: código, nombre, nombre corto, referencia, marca y códigos de
/// barras vivos (<see cref="TextoDeBusqueda"/>).</item>
/// </list>
/// «Tiene movimientos» = existe una línea de documento del producto (en I1 el kardex sólo nace de líneas confirmadas).
/// </summary>
public static class ReglasDeProducto
{
    /// <summary>Clases que pueden llevar seguimiento por lote, serie o vencimiento: las que tienen existencia propia y la plantilla, que lo hereda a sus variantes.</summary>
    public static readonly IReadOnlySet<ProductKind> ClasesConSeguimiento =
        new HashSet<ProductKind> { ProductKind.Inventoriable, ProductKind.Variant, ProductKind.Kit, ProductKind.Template };

    /// <summary>Las reglas de clase y seguimiento que no dependen de la historia del producto (I6, T918; data-model §1.6).</summary>
    public static Result ClaseYSeguimiento(ProductKind kind, bool lote, bool serie, bool vencimiento)
    {
        if (vencimiento && !lote) return Result.Failure(CatalogErrors.ProductExpiryRequiresLot());
        if ((lote || serie) && !ClasesConSeguimiento.Contains(kind)) return Result.Failure(CatalogErrors.ProductTrackingNotApplicable(kind));
        return Result.Success();
    }

    /// <summary>
    /// Resuelve las referencias de <paramref name="datos"/>, aplica las reglas y las fija en <paramref name="producto"/> (nuevo
    /// o existente). No guarda, no toca unidades, códigos ni impuestos y no valida el IVA contra los impuestos: eso lo hace
    /// quien llama con <see cref="ValidarIva"/>, porque en el alta los impuestos vienen en el mismo pedido.
    /// </summary>
    public static async Task<Result> AplicarAsync(IApplicationDbContext db, Product producto, DatosDeProducto datos, CancellationToken ct)
    {
        var clase = ClaseYSeguimiento(datos.Kind, datos.TracksLot, datos.TracksSerial, datos.TracksExpiry);
        if (clase.IsFailure) return clase;

        var categoria = await db.ProductCategories.FirstOrDefaultAsync(c => c.PublicId == datos.CategoryPublicId, ct);
        if (categoria is null) return Result.Failure(CatalogErrors.CategoryNotFound());

        Brand? marca = null;
        if (datos.BrandPublicId is { } mp)
        {
            marca = await db.Brands.FirstOrDefaultAsync(b => b.PublicId == mp, ct);
            if (marca is null) return Result.Failure(CatalogErrors.BrandNotFound());
        }

        var unidadBase = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.PublicId == datos.BaseUnitPublicId, ct);
        if (unidadBase is null) return Result.Failure(CatalogErrors.UnitNotFound());

        AccountingGroup? grupo = null;
        if (datos.AccountingGroupPublicId is { } gp)
        {
            grupo = await db.AccountingGroups.FirstOrDefaultAsync(g => g.PublicId == gp, ct);
            if (grupo is null) return Result.Failure(CatalogErrors.AccountingGroupNotFound());
        }

        WithholdingConcept? concepto = null;
        if (datos.WithholdingConceptPublicId is { } cp)
        {
            concepto = await db.WithholdingConcepts.FirstOrDefaultAsync(c => c.PublicId == cp, ct);
            if (concepto is null) return Result.Failure(TaxErrors.ConceptNotFound());
        }

        var historia = await HistoriaAsync(db, producto, datos, unidadBase.Id, grupo?.Id, ct);
        return Aplicar(producto, datos, new ReferenciasDeProducto(categoria, marca, unidadBase, grupo, concepto), historia);
    }

    /// <summary>
    /// Las mismas reglas de <see cref="AplicarAsync"/> con las referencias ya resueltas (la plantilla de productos las carga en
    /// bloque, T228: nunca una consulta por fila). <paramref name="tieneMovimientos"/> es si el producto existente estuvo
    /// alguna vez en un documento. No guarda.
    /// </summary>
    public static Result Aplicar(Product producto, DatosDeProducto datos, ReferenciasDeProducto referencias, bool tieneMovimientos) =>
        Aplicar(producto, datos, referencias, new HistoriaDeProducto(tieneMovimientos));

    /// <summary>
    /// Las reglas con lo que se sabe de la historia del producto guardado (<see cref="HistoriaDeProducto"/>): movimientos, existencia,
    /// borradores y dependientes. La plantilla la arma en bloque; el alta y la edición, con <see cref="HistoriaAsync"/>. No guarda.
    /// </summary>
    public static Result Aplicar(Product producto, DatosDeProducto datos, ReferenciasDeProducto referencias, HistoriaDeProducto historia)
    {
        var clase = ClaseYSeguimiento(datos.Kind, datos.TracksLot, datos.TracksSerial, datos.TracksExpiry);
        if (clase.IsFailure) return clase;
        var tieneMovimientos = historia.TieneMovimientos;

        var (categoria, marca, unidadBase, grupo, concepto) = referencias;
        if (grupo is null && datos.Kind != ProductKind.Template) return Result.Failure(CatalogErrors.ProductAccountingGroupRequired());
        if (concepto is null && datos.Kind is not (ProductKind.Template or ProductKind.Combo))
            return Result.Failure(CatalogErrors.ProductWithholdingConceptRequired());

        var esNuevo = producto.Id == 0;
        if (!esNuevo && producto.Kind != datos.Kind && (tieneMovimientos || historia.TieneDependientes || producto.ParentProductId is not null))
            return Result.Failure(CatalogErrors.ProductKindLocked(producto.Kind));
        if (datos.Kind == ProductKind.Variant && producto.ParentProductId is null && producto.ParentProduct is null)
            return Result.Failure(CatalogErrors.VariantParentRequired());
        if (!esNuevo && historia.SeguimientoBloqueado && CambiaSeguimiento(producto, datos))
            return Result.Failure(CatalogErrors.ProductTrackingLocked(historia.TieneExistencia, historia.Borradores));
        if (!esNuevo && tieneMovimientos)
        {
            if (producto.BaseUnitId != unidadBase.Id) return Result.Failure(CatalogErrors.ProductBaseUnitLocked());
            if (producto.AccountingGroupId != grupo?.Id) return Result.Failure(CatalogErrors.ProductUseReclassifyAccountingGroup());
        }
        if (grupo is not null && !grupo.IsActive && producto.AccountingGroupId != grupo.Id)
            return Result.Failure(CatalogErrors.AccountingGroupInactive(grupo.Code));

        producto.Name = datos.Name.Trim();
        producto.ShortName = Limpio(datos.ShortName);
        producto.Description = Limpio(datos.Description);
        producto.Kind = datos.Kind;
        producto.CategoryId = categoria.Id;
        producto.Category = categoria;
        producto.BrandId = marca?.Id;
        producto.Brand = marca;
        producto.BaseUnitId = unidadBase.Id;
        producto.BaseUnit = unidadBase;
        producto.AccountingGroupId = grupo?.Id;
        producto.AccountingGroup = grupo;
        producto.VatSaleTreatment = datos.VatSaleTreatment;
        producto.WithholdingConceptId = concepto?.Id;
        producto.WithholdingConcept = concepto;
        producto.Reference = Limpio(datos.Reference);
        producto.Weight = datos.Weight;
        producto.Volume = datos.Volume;
        producto.TracksLot = datos.TracksLot;
        producto.TracksSerial = datos.TracksSerial;
        producto.TracksExpiry = datos.TracksExpiry;
        producto.IsPurchasable = datos.IsPurchasable;
        producto.IsSellable = datos.IsSellable;
        return Result.Success();
    }

    /// <summary>¿El producto estuvo alguna vez en un documento (cualquier estado, incluido el borrador)?</summary>
    public static Task<bool> TieneMovimientosAsync(IApplicationDbContext db, int productoId, CancellationToken ct) =>
        db.InventoryDocumentLines.IgnoreQueryFilters().AnyAsync(l => l.ProductId == productoId, ct);

    /// <summary>¿Cambia alguna marca de seguimiento (lote, serie, vencimiento)?</summary>
    public static bool CambiaSeguimiento(Product producto, DatosDeProducto datos) =>
        producto.TracksLot != datos.TracksLot || producto.TracksSerial != datos.TracksSerial || producto.TracksExpiry != datos.TracksExpiry;

    /// <summary>
    /// La historia de un producto guardado, consultando sólo lo que el cambio pedido necesita: movimientos si cambia la unidad
    /// base, el grupo o la clase; dependientes (variantes o componentes) si cambia la clase; existencia y borradores si cambia el
    /// seguimiento. Un producto nuevo no tiene historia.
    /// </summary>
    public static async Task<HistoriaDeProducto> HistoriaAsync(
        IApplicationDbContext db, Product producto, DatosDeProducto datos, int unidadBaseId, int? grupoId, CancellationToken ct)
    {
        if (producto.Id == 0) return HistoriaDeProducto.Nueva;
        var cambiaClase = producto.Kind != datos.Kind;
        var cambiaBloqueado = cambiaClase || producto.BaseUnitId != unidadBaseId || producto.AccountingGroupId != grupoId;
        var movimientos = cambiaBloqueado && await TieneMovimientosAsync(db, producto.Id, ct);
        var dependientes = cambiaClase && await TieneDependientesAsync(db, producto.Id, ct);
        var (existencia, borradores) = CambiaSeguimiento(producto, datos) ? await SeguimientoAsync(db, producto.Id, ct) : (false, 0);
        return new HistoriaDeProducto(movimientos, existencia, borradores, dependientes);
    }

    /// <summary>¿Una variante cuelga de él, o es combo o kit con componentes, o es componente de alguno?</summary>
    public static async Task<bool> TieneDependientesAsync(IApplicationDbContext db, int productoId, CancellationToken ct) =>
        await db.Products.AnyAsync(p => p.ParentProductId == productoId, ct)
        || await db.ProductComponents.AnyAsync(c => c.ProductId == productoId || c.ComponentProductId == productoId, ct);

    /// <summary>La existencia distinta de cero en alguna bodega y los documentos en borrador o en aprobación que citan el producto.</summary>
    public static async Task<(bool Existencia, int Borradores)> SeguimientoAsync(IApplicationDbContext db, int productoId, CancellationToken ct)
    {
        var existencia = await db.StockBalances.AnyAsync(b => b.ProductId == productoId && b.Physical != 0m, ct);
        var borradores = await db.InventoryDocumentLines
            .Where(l => l.ProductId == productoId
                && (l.Document!.Status == DocumentStatus.Draft || l.Document.Status == DocumentStatus.PendingApproval))
            .Select(l => l.DocumentId).Distinct().CountAsync(ct);
        return (existencia, borradores);
    }

    /// <summary>
    /// Resuelve los impuestos pedidos contra el catálogo tributario: la definición existe (<c>Core.Tax.NotFound</c>), no es
    /// una retención (<c>.WithholdingNotAllowed</c>), no se repite (<c>.Duplicate</c>), la tarifa existe
    /// (<c>Core.TaxRate.NotFound</c>) y es de esa definición (<c>.RateNotOfDefinition</c>), y un impuesto por unidad trae
    /// sus unidades gravables (<c>.UnitsRequired</c>).
    /// </summary>
    public static async Task<Result<IReadOnlyList<ImpuestoResuelto>>> ResolverImpuestosAsync(
        IApplicationDbContext db, IReadOnlyList<ImpuestoPedido> pedidos, CancellationToken ct)
    {
        var resueltos = new List<ImpuestoResuelto>(pedidos.Count);
        foreach (var pedido in pedidos)
        {
            var definicion = await db.TaxDefinitions.FirstOrDefaultAsync(d => d.PublicId == pedido.TaxDefinitionPublicId, ct);
            if (definicion is null) return Falla(TaxErrors.TaxNotFound());
            if (EsRetencion(definicion)) return Falla(CatalogErrors.ProductTaxWithholdingNotAllowed(definicion.Code));
            if (resueltos.Any(r => r.Definicion.Id == definicion.Id)) return Falla(CatalogErrors.ProductTaxDuplicate(definicion.Code));

            TaxRate? tarifa = null;
            if (pedido.TaxRatePublicId is { } tp)
            {
                tarifa = await db.TaxRates.FirstOrDefaultAsync(r => r.PublicId == tp, ct);
                if (tarifa is null) return Falla(TaxErrors.RateNotFound());
                if (tarifa.TaxDefinitionId != definicion.Id) return Falla(CatalogErrors.ProductTaxRateNotOfDefinition(tarifa.Code, definicion.Code));
            }
            if (definicion.CalculationForm == TaxCalculationForm.AmountPerUnit && pedido.TaxableUnitsPerBaseUnit is not > 0)
                return Falla(CatalogErrors.ProductTaxUnitsRequired(definicion.Code));

            resueltos.Add(new ImpuestoResuelto(definicion, tarifa,
                definicion.CalculationForm == TaxCalculationForm.AmountPerUnit ? pedido.TaxableUnitsPerBaseUnit : null));
        }
        return Result.Success<IReadOnlyList<ImpuestoResuelto>>(resueltos);

        static Result<IReadOnlyList<ImpuestoResuelto>> Falla(Error e) => Result.Failure<IReadOnlyList<ImpuestoResuelto>>(e);
    }

    /// <summary>
    /// Tratamiento de IVA ↔ impuestos (data-model §1.9): gravado exige exactamente un IVA con tarifa; exento o excluido no
    /// llevan IVA con tarifa distinta de cero (el canónico informa el exento como IVA 0 por el tratamiento).
    /// </summary>
    public static Result ValidarIva(VatSaleTreatment tratamiento, IEnumerable<ImpuestoResuelto> impuestos)
    {
        var ivas = impuestos.Where(i => i.Definicion.Kind == TaxKind.Iva).ToList();
        if (tratamiento == VatSaleTreatment.Taxed)
            return ivas.Count == 1 && ivas[0].Tarifa is not null ? Result.Success() : Result.Failure(CatalogErrors.ProductTaxVatRateRequired());
        return ivas.Any(i => i.Tarifa?.Rate is not 0m)
            ? Result.Failure(CatalogErrors.ProductTaxVatRateNotAllowed(tratamiento))
            : Result.Success();
    }

    /// <summary>Deja en el producto exactamente estos impuestos: los que sobran quedan de baja lógica; los demás se actualizan o se agregan.</summary>
    public static void FijarImpuestos(Product producto, IReadOnlyList<ImpuestoResuelto> impuestos, DateTime ahora)
    {
        var vivos = producto.Taxes.Where(t => !t.IsDeleted).ToList();
        foreach (var viejo in vivos.Where(v => impuestos.All(i => i.Definicion.Id != v.TaxDefinitionId)))
        {
            viejo.IsDeleted = true;
            viejo.DeletedAt = ahora;
        }
        foreach (var i in impuestos)
        {
            var fila = vivos.FirstOrDefault(v => v.TaxDefinitionId == i.Definicion.Id);
            if (fila is null)
            {
                fila = new ProductTax { Product = producto, TaxDefinitionId = i.Definicion.Id, TaxDefinition = i.Definicion };
                producto.Taxes.Add(fila);
            }
            fila.TaxRateCode = i.Tarifa?.Code;
            fila.AppliesTo = i.Tarifa?.AppliesTo ?? TaxAppliesTo.Both;
            fila.TaxableUnitsPerBaseUnit = i.UnidadesGravables;
        }
    }

    /// <summary>Los impuestos vivos de un producto guardado, resueltos (la tarifa, la vigencia más reciente de su código).</summary>
    public static async Task<IReadOnlyList<ImpuestoResuelto>> ImpuestosVivosAsync(IApplicationDbContext db, int productoId, CancellationToken ct)
    {
        var filas = await db.ProductTaxes.AsNoTracking().Where(t => t.ProductId == productoId).ToListAsync(ct);
        var definiciones = filas.Select(f => f.TaxDefinitionId).ToList();
        var porId = await db.TaxDefinitions.AsNoTracking().Where(d => definiciones.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var codigos = filas.Select(f => f.TaxRateCode).OfType<string>().ToList();
        var tarifas = (await db.TaxRates.AsNoTracking().Where(r => codigos.Contains(r.Code)).ToListAsync(ct))
            .GroupBy(r => (r.TaxDefinitionId, r.Code)).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ValidFrom).First());
        return filas.Where(f => porId.ContainsKey(f.TaxDefinitionId))
            .Select(f => new ImpuestoResuelto(porId[f.TaxDefinitionId],
                f.TaxRateCode is { } c && tarifas.TryGetValue((f.TaxDefinitionId, c), out var t) ? t : null, f.TaxableUnitsPerBaseUnit))
            .ToList();
    }

    /// <summary>Retenciones: no son impuestos del producto (salen de su concepto de retención).</summary>
    public static bool EsRetencion(TaxDefinition definicion) =>
        definicion.IsWithholding || definicion.Kind is TaxKind.ReteFuente or TaxKind.ReteIva or TaxKind.ReteIca;

    /// <summary>
    /// El texto de búsqueda normalizado: código, nombre, nombre corto, referencia, marca y códigos de barras vivos, sin
    /// repetir, hasta <see cref="Product.LargoDeTextoDeBusqueda"/> caracteres.
    /// </summary>
    public static string TextoDeBusqueda(Product producto, string? marca, IEnumerable<string> codigosDeBarras)
    {
        var partes = new[] { producto.Code, producto.Name, producto.ShortName, producto.Reference, marca }
            .Concat(codigosDeBarras)
            .Select(NormalizadorDeBusqueda.Normalizar)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.Ordinal);
        var texto = string.Join(' ', partes);
        return texto.Length <= Product.LargoDeTextoDeBusqueda ? texto : texto[..Product.LargoDeTextoDeBusqueda];
    }

    /// <summary>Recalcula el texto de búsqueda de un producto guardado con su marca y sus códigos vivos más <paramref name="agregados"/> y menos <paramref name="retirados"/>.</summary>
    public static async Task RecalcularTextoDeBusquedaAsync(
        IApplicationDbContext db, Product producto, CancellationToken ct, IEnumerable<string>? agregados = null, IEnumerable<string>? retirados = null)
    {
        var marca = producto.BrandId is int b ? await db.Brands.Where(x => x.Id == b).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        List<string> codigos = producto.Id == 0 ? [] : await db.ProductBarcodes.Where(x => x.ProductId == producto.Id).Select(x => x.Barcode).ToListAsync(ct);
        var fuera = (retirados ?? []).ToHashSet(StringComparer.Ordinal);
        producto.SearchText = TextoDeBusqueda(producto, marca, codigos.Where(c => !fuera.Contains(c)).Concat(agregados ?? []));
    }

    public static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
