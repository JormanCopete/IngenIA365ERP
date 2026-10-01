using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeProductos;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// La plantilla 6 (feature 012, T228; contracts/plantillas.md §6; <c>POST /api/inventory/products/import?mode=review|apply</c>;
/// FR-023 a FR-030, US1-1, US1-4, US1-6): productos con sus unidades alternas, códigos de barras e impuestos adicionales, las
/// cuatro hojas revisadas y aplicadas juntas sobre <see cref="EjecutorDeImportacion"/>. Cada fila de <c>Productos</c> pasa por
/// <see cref="ReglasDeProducto"/> —las mismas del alta unitaria y con los mismos códigos— con los catálogos citados cargados en
/// bloque; las hojas hijas, por las reglas de sus subrecursos (unidad base, factor bloqueado con movimientos, código de barras
/// único entre los vivos nombrando al dueño, impuestos por unidad con sus unidades gravables) y el tratamiento de IVA se revisa
/// contra el conjunto final de impuestos. La plantilla nunca borra: lo que no viene queda como está.
/// <para>
/// Cambiar el grupo contable de un producto <b>con movimientos</b> exige <c>Inventory.Catalog.ReclassifyAccountingGroup</c>
/// (<c>Import.Cell.PermissionRequired</c>) y un motivo —el de la fila (<c>motivoCambioDeGrupo</c>) o, sin él, el de la importación
/// (<c>requiresReason</c>)—, y lo hace la misma regla que <c>ChangeProductAccountingGroupCommand</c> (<see cref="ReclasificacionDeGrupo"/>,
/// US3 T288), con fecha efectiva hoy: la revisión sólo la valida; la aplicación bloquea, reclasifica y emite
/// <c>GrupoContableReclasificado</c> si hay existencia, en la misma transacción. Sin movimientos, es una edición más. Un código
/// numérico de 8, 12, 13 o 14 dígitos con el dígito de control EAN/UPC errado deja el aviso <c>Import.Barcode.CheckDigit</c>. (nuevo)
/// </para>
/// <para>
/// I6 (T921, T922): las seis clases y las marcas de seguimiento con las reglas del alta unitaria (<see cref="HistoriaDeProducto"/>
/// armada en bloque: movimientos, existencia, borradores y dependientes), y dos hojas opcionales —decisión por defecto de T922,
/// revisar con el dueño—: <c>Variantes</c> (una fila por variante y atributo: la plantilla, el atributo y el valor; arma el
/// <c>VariantKey</c> con <see cref="GeneradorDeVariantes.ClaveDe"/> y rechaza la combinación repetida y los atributos distintos
/// de los de las demás variantes de la plantilla) y <c>Componentes</c> (agrega o cambia componentes de combos y kits —lo que no
/// viene queda como está— y revisa el conjunto final con <see cref="ValidadorDeComponentes"/>, como <c>SetProductComponentsCommand</c>).
/// </para>
/// </summary>
public sealed record ImportProductsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportProductsCommand>.LargoMaximo);
    }
}

public sealed class ImportProductsCommandHandler(
    IApplicationDbContext db, EjecutorDeImportacion ejecutor, IDateTimeService reloj, ReclasificacionDeGrupo? reclasificacion = null)
    : IRequestHandler<ImportProductsCommand, Result<ImportResultDto>>
{
    /// <summary>El aviso de un código de barras con el dígito de control EAN/UPC errado (no bloquea: hay códigos internos).</summary>
    public const string AvisoDeDigitoDeControl = "Import.Barcode.CheckDigit";

    public Task<Result<ImportResultDto>> Handle(ImportProductsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    /// <summary>Un producto que el archivo toca: la entidad, si vino en la hoja Productos (y su fila) y lo que pidió de IVA.</summary>
    private sealed class Tocado(Product producto, bool esNuevo)
    {
        public Product Producto { get; } = producto;
        public bool EsNuevo { get; } = esNuevo;
        public FilaDeImportacion? FilaDeProducto { get; set; }
        public bool ConError { get; set; }
        public TaxRate? TarifaIva { get; set; }
        /// <summary>El grupo al que se reclasifica un producto con movimientos (US3): lo aplica <see cref="ReclasificacionDeGrupo"/>.</summary>
        public AccountingGroup? Reclasificar { get; set; }
        public string? MotivoDeReclasificacion { get; set; }
        public List<(FilaDeImportacion Fila, ImpuestoResuelto Impuesto)> Adicionales { get; } = [];
    }

    /// <summary>La historia en bloque de los productos existentes que el archivo toca (I6, T918): lo que bloquea clase y seguimiento.</summary>
    private sealed record HistoriaEnBloque(HashSet<int> ConExistencia, Dictionary<int, int> Borradores, HashSet<int> ConDependientes);

    private sealed record Catalogos(
        CatalogoCitado<ProductCategory> Categorias,
        CatalogoCitado<Brand> Marcas,
        CatalogoCitado<UnitOfMeasure> Unidades,
        CatalogoCitado<AccountingGroup> Grupos,
        IReadOnlyDictionary<int, AccountingGroup> GruposPorId,
        CatalogoCitado<WithholdingConcept> Conceptos,
        CatalogoCitado<TaxRate> TarifasIva,
        CatalogoCitado<TaxRate> Tarifas,
        IReadOnlyDictionary<int, TaxDefinition> Definiciones,
        IReadOnlyDictionary<(int, string), TaxRate> TarifaPorCodigo);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hProductos = ctx.Hoja(P.HojaProductos);
        var hUnidades = ctx.Hoja(P.HojaUnidades);
        var hCodigos = ctx.Hoja(P.HojaCodigos);
        var hImpuestos = ctx.Hoja(P.HojaImpuestos);
        var hVariantes = ctx.Hoja(P.HojaVariantes);
        var hComponentes = ctx.Hoja(P.HojaComponentes);

        // Todo en bloque (§0.3): los catálogos citados y sólo los productos y códigos que el archivo nombra.
        var catalogos = await CatalogosAsync(ct);
        var codigosDelArchivo = hProductos.Filas.Select(f => f.Crudo(P.Codigo))
            .Concat(new[] { hUnidades, hCodigos, hImpuestos, hVariantes, hComponentes }.SelectMany(h => h.Filas.Select(f => f.Crudo(P.Producto))))
            .Concat(hVariantes.Filas.Select(f => f.Crudo(P.Plantilla)))
            .Concat(hComponentes.Filas.Select(f => f.Crudo(P.Componente)))
            .Select(CodigoDeCatalogo.Normalizar).OfType<string>().Distinct().ToList();
        var existentes = await db.Products
            .Include(p => p.Units).ThenInclude(u => u.Unit)
            .Include(p => p.Barcodes)
            .Include(p => p.Taxes)
            .Include(p => p.Brand)
            .Where(p => codigosDelArchivo.Contains(p.Code))
            .ToListAsync(ct);
        var idsExistentes = existentes.Select(p => p.Id).ToList();
        var movimientos = (await db.InventoryDocumentLines.IgnoreQueryFilters().AsNoTracking()
                .Where(l => idsExistentes.Contains(l.ProductId))
                .Select(l => new { l.ProductId, l.UnitId }).Distinct().ToListAsync(ct))
            .Select(m => (m.ProductId, m.UnitId)).ToHashSet();
        var conMovimientos = movimientos.Select(m => m.ProductId).ToHashSet();
        var historia = await HistoriaAsync(idsExistentes, ct);

        var barrasDelArchivo = hCodigos.Filas.Select(f => ProductBarcode.Normalizar(f.Crudo(P.CodigoDeBarras))).Where(b => b.Length > 0).Distinct().ToList();
        var duenos = (await db.ProductBarcodes.AsNoTracking()
                .Where(b => barrasDelArchivo.Contains(b.Barcode))
                .Join(db.Products, b => b.ProductId, p => p.Id, (b, p) => new { b.Barcode, p.Id, p.PublicId, p.Code, p.Name })
                .ToListAsync(ct))
            .GroupBy(d => d.Barcode).ToDictionary(g => g.Key, g => g.First());

        var tocados = new Dictionary<string, Tocado>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existentes) tocados[p.Code] = new Tocado(p, esNuevo: false);

        Productos(ctx, hProductos, hVariantes, catalogos, tocados, conMovimientos, historia);
        await VariantesAsync(hVariantes, tocados, ct);
        await ComponentesAsync(hComponentes, tocados, ct);
        Unidades(hUnidades, catalogos, tocados, movimientos);
        Codigos(hCodigos, tocados, duenos.ToDictionary(d => d.Key, d => (d.Value.Id, d.Value.PublicId, d.Value.Code, d.Value.Name)));
        Impuestos(hImpuestos, catalogos, tocados);
        ImpuestosFinales(hProductos, catalogos, tocados);
        await ReclasificacionesAsync(ctx, tocados, ct);

        // El texto de búsqueda de todo lo tocado, con su marca y sus códigos vivos (incluidos los nuevos).
        foreach (var t in tocados.Values.Where(t => !t.ConError))
        {
            var p = t.Producto;
            p.SearchText = ReglasDeProducto.TextoDeBusqueda(p, p.Brand?.Name, p.Barcodes.Where(b => !b.IsDeleted).Select(b => b.Barcode));
        }
    }

    private async Task<Catalogos> CatalogosAsync(CancellationToken ct)
    {
        // Con seguimiento: el producto nuevo las cita por navegación y no deben entrar como altas.
        var definiciones = await db.TaxDefinitions.ToDictionaryAsync(d => d.Id, ct);
        var grupos = await db.AccountingGroups.ToListAsync(ct);
        var tarifas = await db.TaxRates.ToListAsync(ct);
        // Una tarifa por código y definición: la vigencia más reciente (el producto guarda el código, T22).
        var vigentes = tarifas.GroupBy(r => (r.TaxDefinitionId, r.Code)).Select(g => g.OrderByDescending(r => r.ValidFrom).First()).ToList();

        return new Catalogos(
            CatalogoCitado<ProductCategory>.Desde(await db.ProductCategories.ToListAsync(ct), c => c, c => c.Code),
            CatalogoCitado<Brand>.Desde(await db.Brands.ToListAsync(ct), b => b, b => b.Code),
            CatalogoCitado<UnitOfMeasure>.Desde(await db.UnitsOfMeasure.ToListAsync(ct), u => u, u => u.Code),
            CatalogoCitado<AccountingGroup>.Desde(grupos, g => g, g => g.Code),
            grupos.ToDictionary(g => g.Id),
            CatalogoCitado<WithholdingConcept>.Desde(await db.WithholdingConcepts.ToListAsync(ct), c => c, c => c.Code),
            CatalogoCitado<TaxRate>.Desde(vigentes.Where(r => definiciones.GetValueOrDefault(r.TaxDefinitionId)?.Kind == TaxKind.Iva), r => r, r => r.Code),
            CatalogoCitado<TaxRate>.Desde(vigentes, r => r, r => r.Code),
            definiciones,
            vigentes.ToDictionary(r => (r.TaxDefinitionId, r.Code)));
    }

    // ---------------------------------------------------------------------------------------------------- Productos --

    /// <summary>Existencia distinta de cero, borradores que citan y dependientes (variantes o componentes) de los existentes, en bloque.</summary>
    private async Task<HistoriaEnBloque> HistoriaAsync(List<int> ids, CancellationToken ct)
    {
        var conExistencia = (await db.StockBalances.AsNoTracking().Where(b => ids.Contains(b.ProductId) && b.Physical != 0m)
            .Select(b => b.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        var borradores = (await db.InventoryDocumentLines.AsNoTracking()
                .Where(l => ids.Contains(l.ProductId)
                    && (l.Document!.Status == DocumentStatus.Draft || l.Document.Status == DocumentStatus.PendingApproval))
                .Select(l => new { l.ProductId, l.DocumentId }).Distinct().ToListAsync(ct))
            .GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Count());
        var padres = await db.Products.AsNoTracking().Where(p => p.ParentProductId != null && ids.Contains(p.ParentProductId.Value))
            .Select(p => p.ParentProductId!.Value).Distinct().ToListAsync(ct);
        var enComponentes = await db.ProductComponents.AsNoTracking().Where(x => ids.Contains(x.ProductId) || ids.Contains(x.ComponentProductId))
            .Select(x => new { x.ProductId, x.ComponentProductId }).ToListAsync(ct);
        var dependientes = padres.Concat(enComponentes.Select(x => x.ProductId)).Concat(enComponentes.Select(x => x.ComponentProductId))
            .Where(ids.Contains).ToHashSet();
        return new HistoriaEnBloque(conExistencia, borradores, dependientes);
    }

    private void Productos(ContextoDeImportacion ctx, HojaDeImportacion hoja, HojaDeImportacion hVariantes, Catalogos c, Dictionary<string, Tocado> tocados,
        HashSet<int> conMovimientos, HistoriaEnBloque historia)
    {
        // La plantilla de cada variante nueva (hoja Variantes), para que la variante nazca de ella (Inventory.Variant.ParentRequired).
        var codigosDeLaHoja = hoja.Filas.Select(f => CodigoDeCatalogo.Normalizar(f.Crudo(P.Codigo))).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var plantillaDe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in hVariantes.Filas)
            if (CodigoDeCatalogo.Normalizar(f.Crudo(P.Producto)) is { } v && CodigoDeCatalogo.Normalizar(f.Crudo(P.Plantilla)) is { } t)
                plantillaDe.TryAdd(v, t);

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var nombre = fila.Texto(P.Nombre);
            var tipo = fila.Enumeracion(P.Tipo, P.EtiquetasDeTipo);
            var categoria = fila.Referencia(P.Categoria, c.Categorias, "una categoría", "en la plantilla de categorías o en Inventario → Categorías");
            var marca = fila.Referencia(P.Marca, c.Marcas, "una marca", "en la plantilla de marcas o en Inventario → Marcas");
            var unidadBase = fila.Referencia(P.UnidadBase, c.Unidades, "una unidad de medida", "en la plantilla de unidades o en Inventario → Unidades");
            var grupo = fila.Referencia(P.GrupoContable, c.Grupos, "un grupo contable", "en la plantilla de grupos o en Inventario → Grupos contables");
            var estado = fila.Enumeracion(P.Estado, P.EtiquetasDeEstado);
            var lote = fila.SiNo(P.ControlaLote);
            var serie = fila.SiNo(P.ControlaSerie);
            var vencimiento = fila.SiNo(P.ControlaVencimiento);
            var tratamiento = fila.Enumeracion(P.TratamientoIva, P.EtiquetasDeIva);
            var tarifaIva = fila.Referencia(P.TarifaIva, c.TarifasIva, "una tarifa de IVA", "en la plantilla de impuestos o en Maestros → Impuestos");
            var concepto = fila.Referencia(P.ConceptoRetencion, c.Conceptos, "un concepto de retención", "en la plantilla de impuestos o en Maestros → Impuestos");
            var referencia = fila.Texto(P.Referencia);
            var peso = fila.Cantidad(P.Peso);
            var volumen = fila.Cantidad(P.Volumen);
            var motivoDeGrupo = fila.Texto(P.MotivoCambioDeGrupo);

            if (peso is < 0) fila.Error(P.Peso, ImportErrors.CellFormat, "El peso no puede ser negativo.");
            if (volumen is < 0) fila.Error(P.Volumen, ImportErrors.CellFormat, "El volumen no puede ser negativo.");

            var llaveUnica = hoja.LlaveUnica(fila, codigo, P.Codigo);
            if (codigo is null || !llaveUnica)
                continue;
            if (!tocados.TryGetValue(codigo, out var tocado))
                tocados[codigo] = tocado = new Tocado(new Product { Code = codigo, Status = ProductStatus.Active }, esNuevo: true);
            tocado.FilaDeProducto = fila;
            if (fila.TieneErrores || nombre is null || tipo is null || categoria is null || unidadBase is null || tratamiento is null)
            {
                tocado.ConError = true;
                continue;
            }

            var producto = tocado.Producto;
            var antes = tocado.EsNuevo ? null : Foto(producto, c);

            // Grupo de un producto con movimientos: la reclasificación de US3 (con permiso y motivo), nunca una edición. Las demás
            // reglas del producto corren con el grupo que tiene; la reclasificación va después (ReclasificacionesAsync).
            var tieneMovimientos = !tocado.EsNuevo && conMovimientos.Contains(producto.Id);
            var grupoDeLasReglas = grupo;
            if (tieneMovimientos && grupo is not null && producto.AccountingGroupId is int actual && actual != grupo.Id
                && producto.BaseUnitId == unidadBase.Id)
            {
                if (!ctx.TienePermiso(P.PermisoDeReclasificar))
                {
                    tocado.ConError = true;
                    fila.Error(P.GrupoContable, ImportErrors.CellPermissionRequired,
                        $"El producto {codigo} tiene movimientos: cambiar su grupo contable exige el permiso {P.PermisoDeReclasificar}.");
                    continue;
                }
                if (motivoDeGrupo is null) ctx.PedirMotivo();
                tocado.Reclasificar = grupo;
                tocado.MotivoDeReclasificacion = motivoDeGrupo;
                grupoDeLasReglas = c.GruposPorId.GetValueOrDefault(actual);
            }

            // Una variante nueva nace de la plantilla que le da la hoja Variantes (de este archivo o existente).
            if (tipo == ProductKind.Variant && producto.ParentProductId is null && producto.ParentProduct is null
                && plantillaDe.TryGetValue(codigo, out var codigoDePlantilla))
            {
                if (!tocados.TryGetValue(codigoDePlantilla, out var padre) && codigosDeLaHoja.Contains(codigoDePlantilla))
                    tocados[codigoDePlantilla] = padre = new Tocado(new Product { Code = codigoDePlantilla, Status = ProductStatus.Active }, esNuevo: true);
                if (padre is not null)
                {
                    producto.ParentProduct = padre.Producto;
                    if (padre.Producto.Id > 0) producto.ParentProductId = padre.Producto.Id;
                }
            }

            var datos = new DatosDeProducto(nombre, producto.ShortName, producto.Description, tipo.Value, categoria.PublicId, marca?.PublicId,
                unidadBase.PublicId, grupoDeLasReglas?.PublicId, tratamiento.Value, concepto?.PublicId, referencia, peso, volumen, lote, serie, vencimiento,
                producto.IsPurchasable, producto.IsSellable);
            var historiaDelProducto = tocado.EsNuevo
                ? HistoriaDeProducto.Nueva
                : new HistoriaDeProducto(tieneMovimientos || conMovimientos.Contains(producto.Id), historia.ConExistencia.Contains(producto.Id),
                    historia.Borradores.GetValueOrDefault(producto.Id), historia.ConDependientes.Contains(producto.Id));
            var reglas = ReglasDeProducto.Aplicar(producto, datos, new ReferenciasDeProducto(categoria, marca, unidadBase, grupoDeLasReglas, concepto), historiaDelProducto);
            if (reglas.IsFailure)
            {
                FilasDeCatalogo.Error(fila, ColumnaDe(reglas.Error), reglas.Error);
                tocado.ConError = true;
                continue;
            }

            var estadoFinal = estado ?? (tocado.EsNuevo ? ProductStatus.Active : producto.Status);
            if (!tocado.EsNuevo && estadoFinal != producto.Status) ctx.PedirMotivo();
            producto.Status = estadoFinal;
            tocado.TarifaIva = tarifaIva;

            if (tocado.EsNuevo)
            {
                db.Products.Add(producto);
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create,
                [
                    new(P.Nombre, null, nombre), new(P.Tipo, null, tipo.Value.ToString()), new(P.Categoria, null, categoria.Code),
                    new(P.UnidadBase, null, unidadBase.Code), new(P.GrupoContable, null, grupo?.Code), new(P.TratamientoIva, null, tratamiento.Value.ToString()),
                ]);
                continue;
            }

            var despues = Foto(producto, c) with { TarifaIva = tarifaIva?.Code, Grupo = tocado.Reclasificar?.Code ?? producto.AccountingGroup?.Code };
            var campos = new List<CampoCambiadoDto>();
            foreach (var (columna, a, d) in antes!.Diferencias(despues)) campos.Add(new(columna, a, d));
            ctx.Registrar(fila, codigo, FilasDeCatalogo.Accion(campos), campos);
        }
    }

    /// <summary>
    /// Las reclasificaciones de grupo de los productos con movimientos (US3, T288): en la revisión, las reglas de la fecha y del
    /// grupo; en la aplicación, la reclasificación completa (cerrojo, fila del historial, grupo del producto y mensaje), con fecha
    /// efectiva hoy y el motivo de la fila o, sin él, el de la importación.
    /// </summary>
    private async Task ReclasificacionesAsync(ContextoDeImportacion ctx, Dictionary<string, Tocado> tocados, CancellationToken ct)
    {
        foreach (var t in tocados.Values.Where(t => t.Reclasificar is not null && !t.ConError && t.FilaDeProducto is not null))
        {
            var fila = t.FilaDeProducto!;
            if (reclasificacion is null)
            {
                fila.Error(P.GrupoContable, ImportErrors.CellNotYetAvailable,
                    $"El producto {t.Producto.Code} tiene movimientos: su grupo contable cambia por la reclasificación de grupo (Inventario → Productos → Grupo contable).");
                continue;
            }

            var resultado = ctx.Modo == ModoDeImportacion.Review
                ? await reclasificacion.ValidarAsync(t.Producto, t.Reclasificar!, null, ct)
                : (await reclasificacion.AplicarAsync(t.Producto, t.Reclasificar!, null, t.MotivoDeReclasificacion ?? ctx.Motivo ?? string.Empty, ct)) is { IsFailure: true } fallida
                    ? Result.Failure<DateOnly>(fallida.Error)
                    : Result.Success(reloj.HoyLocal);
            if (resultado.IsFailure)
            {
                t.ConError = true;
                FilasDeCatalogo.Error(fila, resultado.Error.Code == "Inventory.Product.MovementsAfterEffectiveDate" ? P.GrupoContable : ColumnaDe(resultado.Error), resultado.Error);
            }
        }
    }

    /// <summary>El producto como lo muestra la plantilla, para los cambios campo a campo.</summary>
    private sealed record FotoDeProducto(
        string Nombre, string Tipo, string? Categoria, string? Marca, string? UnidadBase, string? Grupo, string Estado, string Iva, string? TarifaIva,
        string? Concepto, string? Referencia, string Peso, string Volumen, string Lote, string Serie, string Vencimiento)
    {
        public IEnumerable<(string, string?, string?)> Diferencias(FotoDeProducto d)
        {
            var pares = new (string, string?, string?)[]
            {
                (P.Nombre, Nombre, d.Nombre), (P.Tipo, Tipo, d.Tipo), (P.Categoria, Categoria, d.Categoria), (P.Marca, Marca, d.Marca),
                (P.UnidadBase, UnidadBase, d.UnidadBase), (P.GrupoContable, Grupo, d.Grupo), (P.Estado, Estado, d.Estado),
                (P.TratamientoIva, Iva, d.Iva), (P.TarifaIva, TarifaIva, d.TarifaIva), (P.ConceptoRetencion, Concepto, d.Concepto),
                (P.Referencia, Referencia, d.Referencia), (P.Peso, Peso, d.Peso), (P.Volumen, Volumen, d.Volumen),
                (P.ControlaLote, Lote, d.Lote), (P.ControlaSerie, Serie, d.Serie), (P.ControlaVencimiento, Vencimiento, d.Vencimiento),
            };
            return pares.Where(p => !string.Equals(p.Item2 ?? string.Empty, p.Item3 ?? string.Empty, StringComparison.Ordinal));
        }
    }

    private static FotoDeProducto Foto(Product p, Catalogos c) => new(
        p.Name, p.Kind.ToString(), p.Category?.Code, p.Brand?.Code, p.BaseUnit?.Code, p.AccountingGroup?.Code, p.Status.ToString(),
        p.VatSaleTreatment.ToString(), TarifaIvaGuardada(p, c), p.WithholdingConcept?.Code, p.Reference,
        FilasDeCatalogo.Numero(p.Weight), FilasDeCatalogo.Numero(p.Volume),
        FilasDeCatalogo.SiNo(p.TracksLot), FilasDeCatalogo.SiNo(p.TracksSerial), FilasDeCatalogo.SiNo(p.TracksExpiry));

    private static string? TarifaIvaGuardada(Product p, Catalogos c) =>
        p.Taxes.Where(t => !t.IsDeleted && c.Definiciones.GetValueOrDefault(t.TaxDefinitionId)?.Kind == TaxKind.Iva)
            .Select(t => t.TaxRateCode).FirstOrDefault();

    /// <summary>La columna donde va el error de una regla del producto.</summary>
    private static string ColumnaDe(Error error) => error.Code switch
    {
        "Inventory.Product.AccountingGroupRequired" or "Inventory.Product.UseReclassifyAccountingGroup" or "Inventory.AccountingGroup.Inactive"
            or "Inventory.Product.AccountingGroupUnchanged" or "Inventory.Period.Closed" => P.GrupoContable,
        "Inventory.Product.WithholdingConceptRequired" => P.ConceptoRetencion,
        "Inventory.Product.BaseUnitLocked" => P.UnidadBase,
        "Inventory.Product.KindLocked" or "Inventory.Variant.ParentRequired" => P.Tipo,
        "Inventory.Product.ExpiryRequiresLot" => P.ControlaVencimiento,
        "Inventory.Product.TrackingNotApplicable" or "Inventory.Product.TrackingLocked" => P.ControlaLote,
        _ => P.Codigo,
    };

    /// <summary>El producto de una fila de una hoja hija: de la hoja Productos (sin errores) o existente; si no, el error.</summary>
    private static Tocado? ProductoDe(FilaDeImportacion fila, Dictionary<string, Tocado> tocados)
    {
        var codigo = fila.Codigo(P.Producto);
        if (codigo is null) return null;
        if (!tocados.TryGetValue(codigo, out var tocado))
        {
            fila.Error(P.Producto, ImportErrors.CellNotFound, $"No hay un producto «{codigo}». Créelo en la hoja Productos de este archivo o en Inventario → Productos.");
            return null;
        }
        // Un producto cuya fila tiene errores ya los muestra en su hoja: sus filas hijas no se procesan.
        return tocado.ConError ? null : tocado;
    }

    // ---------------------------------------------------------------------------------------------------- Variantes --

    /// <summary>
    /// La hoja <c>Variantes</c> (I6, T922, decisión por defecto): cada variante del archivo con su plantilla y un valor por atributo.
    /// Arma el <c>VariantKey</c> como <c>GenerateProductVariantsCommand</c>; la combinación no se repite en la plantilla
    /// (<c>Inventory.Variant.CombinationExists</c>), todas las variantes de una plantilla llevan los mismos atributos
    /// (<c>Inventory.Variant.AttributesMismatch</c>) y la combinación de una variante existente no cambia por plantilla.
    /// </summary>
    private async Task VariantesAsync(HojaDeImportacion hoja, Dictionary<string, Tocado> tocados, CancellationToken ct)
    {
        if (hoja.Filas.Count == 0) return;
        var atributos = (await db.VariantAttributes.Include(a => a.Values).ToListAsync(ct))
            .ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);

        var porVariante = new Dictionary<Tocado, List<(FilaDeImportacion Fila, Tocado Plantilla, VariantAttribute Atributo, VariantAttributeValue Valor)>>();
        var conError = new HashSet<Tocado>();
        foreach (var fila in hoja.Filas)
        {
            var tocado = ProductoDe(fila, tocados);
            var codigoDePlantilla = fila.Codigo(P.Plantilla);
            var codigoDeAtributo = fila.Codigo(P.Atributo);
            var codigoDeValor = fila.Codigo(P.Valor);
            if (tocado is null || codigoDePlantilla is null || codigoDeAtributo is null || codigoDeValor is null || fila.TieneErrores)
            {
                if (tocado is not null) conError.Add(tocado);
                continue;
            }
            if (!hoja.LlaveUnica(fila, $"{tocado.Producto.Code}|{codigoDeAtributo}", P.Atributo)) { conError.Add(tocado); continue; }

            if (tocado.Producto.Kind != ProductKind.Variant)
                fila.Error(P.Producto, ImportErrors.CellFormat, $"El producto {tocado.Producto.Code} no es una variante: en la hoja Productos su tipo debe ser Variant (variante).");
            else if (!tocados.TryGetValue(codigoDePlantilla, out var plantilla))
                fila.Error(P.Plantilla, ImportErrors.CellNotFound, $"No hay un producto «{codigoDePlantilla}». Créelo como plantilla en la hoja Productos o en Inventario → Productos.");
            else if (plantilla.Producto.Kind != ProductKind.Template)
                FilasDeCatalogo.Error(fila, P.Plantilla, CatalogErrors.VariantNotATemplate(plantilla.Producto.Code));
            else if (!ReferenceEquals(tocado.Producto.ParentProduct ?? plantilla.Producto, plantilla.Producto)
                && tocado.Producto.ParentProductId != plantilla.Producto.Id)
                fila.Error(P.Plantilla, ImportErrors.CellFormat, $"La variante {tocado.Producto.Code} es de otra plantilla: una variante no cambia de plantilla.");
            else if (!atributos.TryGetValue(codigoDeAtributo, out var atributo))
                FilasDeCatalogo.Error(fila, P.Atributo, CatalogErrors.VariantAttributeNotFound(codigoDeAtributo));
            else if (atributo.Values.FirstOrDefault(v => !v.IsDeleted && string.Equals(v.Code, codigoDeValor, StringComparison.OrdinalIgnoreCase)) is not { } valor)
                FilasDeCatalogo.Error(fila, P.Valor, CatalogErrors.VariantAttributeValueNotFound(atributo.Code, codigoDeValor));
            else
            {
                if (!porVariante.TryGetValue(tocado, out var filas)) porVariante[tocado] = filas = [];
                if (filas.Count > 0 && !ReferenceEquals(filas[0].Plantilla, plantilla))
                {
                    fila.Error(P.Plantilla, ImportErrors.CellFormat, $"La variante {tocado.Producto.Code} trae otra plantilla en otra fila: una variante tiene una sola.");
                    conError.Add(tocado);
                    continue;
                }
                filas.Add((fila, plantilla, atributo, valor));
                continue;
            }
            conError.Add(tocado);
        }

        // Las claves que ya tienen las plantillas existentes (sin las variantes que el archivo toca).
        var idsDePlantilla = porVariante.Values.Select(f => f[0].Plantilla.Producto.Id).Where(id => id > 0).Distinct().ToList();
        var existentes = await db.Products.AsNoTracking()
            .Where(p => p.ParentProductId != null && idsDePlantilla.Contains(p.ParentProductId.Value) && p.VariantKey != null)
            .Select(p => new { p.Id, Plantilla = p.ParentProductId!.Value, p.Code, Clave = p.VariantKey! }).ToListAsync(ct);

        var claves = new List<(Tocado Variante, Tocado Plantilla, string Clave, List<(FilaDeImportacion Fila, Tocado Plantilla, VariantAttribute Atributo, VariantAttributeValue Valor)> Filas)>();
        foreach (var (variante, filas) in porVariante.Where(x => !conError.Contains(x.Key) && !x.Key.ConError))
        {
            var clave = GeneradorDeVariantes.ClaveDe(filas.Select(f => (f.Atributo.Code, f.Valor.Code)));
            if (variante.Producto.VariantKey is { } actual && !string.Equals(actual, clave, StringComparison.Ordinal))
            {
                filas[0].Fila.Error(P.Valor, ImportErrors.CellFormat,
                    $"La variante {variante.Producto.Code} ya es {actual}: la combinación de una variante existente no cambia por plantilla.");
                continue;
            }
            claves.Add((variante, filas[0].Plantilla, clave, filas));
        }

        foreach (var grupo in claves.GroupBy(c => c.Plantilla))
        {
            var plantilla = grupo.Key.Producto;
            var deLaBase = existentes.Where(e => e.Plantilla == plantilla.Id && grupo.All(g => g.Variante.Producto.Id != e.Id)).ToList();
            var atributosEsperados = deLaBase.Select(e => AtributosDe(e.Clave)).Concat(grupo.Select(g => AtributosDe(g.Clave))).FirstOrDefault();
            var vistas = deLaBase.ToDictionary(e => e.Clave, e => e.Code, StringComparer.Ordinal);
            foreach (var (variante, _, clave, filas) in grupo)
            {
                if (AtributosDe(clave) != atributosEsperados)
                {
                    filas[0].Fila.Error(P.Atributo, GeneradorDeVariantes.CodigoAtributosDistintos,
                        $"Las variantes de la plantilla {plantilla.Code} llevan los atributos {atributosEsperados}; {variante.Producto.Code} lleva {AtributosDe(clave)}.");
                    continue;
                }
                if (!vistas.TryAdd(clave, variante.Producto.Code))
                {
                    FilasDeCatalogo.Error(filas[0].Fila, P.Valor, CatalogErrors.VariantCombinationExists([clave]));
                    continue;
                }

                var producto = variante.Producto;
                var esNueva = producto.VariantKey is null;
                producto.ParentProduct = plantilla;
                if (plantilla.Id > 0) producto.ParentProductId = plantilla.Id;
                producto.VariantKey = clave;
                foreach (var f in filas)
                {
                    if (producto.VariantValues.Any(v => !v.IsDeleted && (ReferenceEquals(v.VariantAttributeValue, f.Valor) || v.VariantAttributeValueId == f.Valor.Id)))
                        continue;
                    producto.VariantValues.Add(new ProductVariantValue
                    {
                        Product = producto, VariantAttributeId = f.Atributo.Id, VariantAttribute = f.Atributo,
                        VariantAttributeValueId = f.Valor.Id, VariantAttributeValue = f.Valor,
                    });
                }
                foreach (var f in filas)
                    hoja.Contexto.Registrar(f.Fila, $"{producto.Code} {f.Atributo.Code}", esNueva ? AccionDeImportacion.Create : AccionDeImportacion.Unchanged,
                        esNueva ? [new(P.Plantilla, null, plantilla.Code), new(P.Valor, null, f.Valor.Code)] : null);
            }
        }
    }

    private static string AtributosDe(string clave) =>
        string.Join(',', clave.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=')[0]));

    // -------------------------------------------------------------------------------------------------- Componentes --

    /// <summary>
    /// La hoja <c>Componentes</c> (I6, T922, decisión por defecto): agrega o cambia la cantidad de componentes de combos y kits
    /// (lo que no viene queda como está) y revisa el conjunto final de cada producto con <see cref="ValidadorDeComponentes"/> sobre
    /// el grafo de la base más el del archivo, igual que <c>SetProductComponentsCommand</c>.
    /// </summary>
    private async Task ComponentesAsync(HojaDeImportacion hoja, Dictionary<string, Tocado> tocados, CancellationToken ct)
    {
        if (hoja.Filas.Count == 0) return;
        var porProducto = new Dictionary<Tocado, List<(FilaDeImportacion Fila, Tocado Componente, decimal Cantidad)>>();
        foreach (var fila in hoja.Filas)
        {
            var tocado = ProductoDe(fila, tocados);
            var codigoDeComponente = fila.Codigo(P.Componente);
            var cantidad = fila.Cantidad(P.Cantidad);
            Tocado? componente = null;
            if (codigoDeComponente is not null && !tocados.TryGetValue(codigoDeComponente, out componente))
                fila.Error(P.Componente, ImportErrors.CellNotFound, $"No hay un producto «{codigoDeComponente}». Créelo en la hoja Productos de este archivo o en Inventario → Productos.");
            if (tocado is null || componente is null || cantidad is null || fila.TieneErrores) continue;
            if (!hoja.LlaveUnica(fila, $"{tocado.Producto.Code}|{componente.Producto.Code}", P.Componente)) continue;
            if (componente.ConError) continue;
            if (!porProducto.TryGetValue(tocado, out var filas)) porProducto[tocado] = filas = [];
            filas.Add((fila, componente, cantidad.Value));
        }
        if (porProducto.Count == 0) return;

        // Ids para el validador: los de la base, y negativos para lo nuevo del archivo.
        var temporales = new Dictionary<Product, int>(ReferenceEqualityComparer.Instance);
        int IdDe(Product p)
        {
            if (p.Id > 0) return p.Id;
            if (!temporales.TryGetValue(p, out var id)) temporales[p] = id = -(temporales.Count + 1);
            return id;
        }

        var existentesIds = porProducto.Keys.Select(t => t.Producto.Id).Where(id => id > 0).ToList();
        var vigentes = await db.ProductComponents.Include(c => c.ComponentProduct).ThenInclude(p => p!.BaseUnit)
            .Where(c => existentesIds.Contains(c.ProductId)).ToListAsync(ct);
        var aristas = await db.ProductComponents.AsNoTracking().Where(c => !existentesIds.Contains(c.ProductId))
            .Select(c => new { c.ProductId, c.ComponentProductId }).ToListAsync(ct);

        // El conjunto final de cada producto: lo vigente más lo del archivo (que agrega o cambia la cantidad).
        var finales = porProducto.ToDictionary(x => x.Key, x =>
        {
            var lista = vigentes.Where(v => v.ProductId == x.Key.Producto.Id && x.Key.Producto.Id > 0)
                .Where(v => x.Value.All(f => f.Componente.Producto.Id != v.ComponentProductId))
                .Select(v => (Producto: v.ComponentProduct!, Cantidad: v.Quantity, Fila: (FilaDeImportacion?)null)).ToList();
            lista.AddRange(x.Value.Select(f => (f.Componente.Producto, f.Cantidad, (FilaDeImportacion?)f.Fila)));
            return lista;
        });

        var grafo = aristas.GroupBy(a => a.ProductId).ToDictionary(g => g.Key, g => g.Select(a => a.ComponentProductId).ToList());
        foreach (var (tocado, lista) in finales) grafo[IdDe(tocado.Producto)] = lista.Select(l => IdDe(l.Producto)).ToList();
        var grafoLeido = grafo.ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Value);

        foreach (var (tocado, lista) in finales)
        {
            var producto = tocado.Producto;
            var nodos = new Dictionary<int, ProductoDelGrafo> { [IdDe(producto)] = Nodo(producto, IdDe(producto)) };
            foreach (var (componente, _, _) in lista) nodos[IdDe(componente)] = Nodo(componente, IdDe(componente));
            var resultado = ValidadorDeComponentes.Validar(new PedidoDeComponentes(IdDe(producto),
                lista.Select(l => new ComponentePropuesto(IdDe(l.Producto), l.Cantidad)).ToList(), nodos,
                grafoLeido.Where(g => g.Key != IdDe(producto)).ToDictionary(g => g.Key, g => g.Value)));
            if (!resultado.Valido)
            {
                var primeraFila = porProducto[tocado][0].Fila;
                foreach (var e in resultado.Errores)
                {
                    var fila = lista.FirstOrDefault(l => e.ComponentProductId is int id && IdDe(l.Producto) == id).Fila ?? primeraFila;
                    var columna = e.Codigo == ValidadorDeComponentes.CodigoCantidadInvalida ? P.Cantidad
                        : e.ComponentProductId is null ? P.Producto : P.Componente;
                    fila.Error(columna, e.Codigo, e.Mensaje);
                }
                continue;
            }

            foreach (var (fila, componente, cantidad) in porProducto[tocado])
            {
                var actual = vigentes.FirstOrDefault(v => v.ProductId == producto.Id && producto.Id > 0 && v.ComponentProductId == componente.Producto.Id && componente.Producto.Id > 0);
                var llave = $"{producto.Code} {componente.Producto.Code}";
                if (actual is null)
                {
                    producto.Components.Add(new ProductComponent { Product = producto, ComponentProduct = componente.Producto, ComponentProductId = componente.Producto.Id, Quantity = cantidad });
                    hoja.Contexto.Registrar(fila, llave, AccionDeImportacion.Create, [new(P.Cantidad, null, FilasDeCatalogo.Numero(cantidad))]);
                    continue;
                }
                var campos = new List<CampoCambiadoDto>();
                FilasDeCatalogo.Diferencia(campos, P.Cantidad, FilasDeCatalogo.Numero(actual.Quantity), FilasDeCatalogo.Numero(cantidad));
                if (actual.Quantity != cantidad) actual.Quantity = cantidad;
                hoja.Contexto.Registrar(fila, llave, FilasDeCatalogo.Accion(campos), campos);
            }
        }
    }

    private static ProductoDelGrafo Nodo(Product p, int id) => new(id, p.Code, p.Kind, p.BaseUnit?.AllowedDecimals ?? 0);

    // ----------------------------------------------------------------------------------------------------- Unidades --

    private static void Unidades(HojaDeImportacion hoja, Catalogos c, Dictionary<string, Tocado> tocados, HashSet<(int, int)> movimientos)
    {
        foreach (var fila in hoja.Filas)
        {
            var tocado = ProductoDe(fila, tocados);
            var unidad = fila.Referencia(P.Unidad, c.Unidades, "una unidad de medida", "en la plantilla de unidades o en Inventario → Unidades");
            var factor = fila.Costo(P.Factor);
            var uso = fila.Enumeracion(P.Uso, P.EtiquetasDeUso);
            if (factor is { } f && !ValidacionDeProducto.FactorValido(f))
                fila.Error(P.Factor, ImportErrors.CellFormat, "El factor es mayor que cero, con hasta 6 decimales.");
            if (tocado is null || unidad is null || factor is null || uso is null || fila.TieneErrores) continue;
            if (!hoja.LlaveUnica(fila, $"{tocado.Producto.Code}|{unidad.Code}", P.Unidad)) continue;

            var producto = tocado.Producto;
            if (unidad.Id == producto.BaseUnitId)
            {
                FilasDeCatalogo.Error(fila, P.Unidad, CatalogErrors.ProductUnitIsBaseUnit(unidad.Code));
                continue;
            }

            var vivas = producto.Units.Where(u => !u.IsDeleted).ToList();
            var alterna = vivas.FirstOrDefault(u => ReferenceEquals(u.Unit, unidad) || (u.UnitId != 0 && u.UnitId == unidad.Id));
            var llave = $"{producto.Code} {unidad.Code}";
            if (alterna is null)
            {
                alterna = new ProductUnit { Product = producto, UnitId = unidad.Id, Unit = unidad, Factor = factor.Value };
                alterna.FijarUso(uso.Value);
                alterna.IsDefaultPurchase = alterna.UsedForPurchase && vivas.All(v => !v.IsDefaultPurchase);
                alterna.IsDefaultSale = alterna.UsedForSale && vivas.All(v => !v.IsDefaultSale);
                producto.Units.Add(alterna);
                hoja.Contexto.Registrar(fila, llave, AccionDeImportacion.Create,
                    [new(P.Factor, null, FilasDeCatalogo.Numero(factor)), new(P.Uso, null, uso.Value.ToString())]);
                continue;
            }

            if (alterna.Factor != factor.Value && movimientos.Contains((producto.Id, unidad.Id)))
            {
                FilasDeCatalogo.Error(fila, P.Factor, CatalogErrors.ProductUnitFactorLocked(unidad.Code));
                continue;
            }
            var campos = new List<CampoCambiadoDto>();
            FilasDeCatalogo.Diferencia(campos, P.Factor, FilasDeCatalogo.Numero(alterna.Factor), FilasDeCatalogo.Numero(factor));
            FilasDeCatalogo.Diferencia(campos, P.Uso, alterna.Usage.ToString(), uso.Value.ToString());
            if (alterna.Factor != factor.Value) alterna.Factor = factor.Value;
            if (alterna.Usage != uso.Value) alterna.FijarUso(uso.Value);
            hoja.Contexto.Registrar(fila, llave, FilasDeCatalogo.Accion(campos), campos);
        }
    }

    // ----------------------------------------------------------------------------------------------- CodigosDeBarras --

    private static void Codigos(HojaDeImportacion hoja, Dictionary<string, Tocado> tocados,
        IReadOnlyDictionary<string, (int Id, Guid PublicId, string Code, string Name)> duenos)
    {
        foreach (var fila in hoja.Filas)
        {
            var tocado = ProductoDe(fila, tocados);
            var crudo = fila.Texto(P.CodigoDeBarras);
            var unidad = fila.Codigo(P.Unidad);
            string? barras = null;
            if (crudo is not null)
            {
                if (!ValidacionDeProducto.CodigoDeBarrasValido(crudo))
                    fila.Error(P.CodigoDeBarras, ImportErrors.CellFormat,
                        $"«{crudo}» no es válido en «{P.CodigoDeBarras}». Hasta {ProductBarcode.MaxLength} letras, dígitos o guiones, sin espacios.");
                else barras = ProductBarcode.Normalizar(crudo);
            }
            if (barras is null || !hoja.LlaveUnica(fila, barras, P.CodigoDeBarras) || tocado is null || fila.TieneErrores) continue;

            var producto = tocado.Producto;
            if (duenos.TryGetValue(barras, out var dueno) && dueno.Id != producto.Id)
            {
                FilasDeCatalogo.Error(fila, P.CodigoDeBarras, CatalogErrors.BarcodeDuplicate(barras, dueno.PublicId, dueno.Code, dueno.Name));
                continue;
            }

            // El empaque: vacío o la base = la unidad base; si no, una unidad alterna viva del producto (también de este archivo).
            ProductUnit? empaque = null;
            if (unidad is not null && !string.Equals(unidad, producto.BaseUnit?.Code, StringComparison.OrdinalIgnoreCase))
            {
                empaque = producto.Units.FirstOrDefault(u => !u.IsDeleted && string.Equals(u.Unit?.Code, unidad, StringComparison.OrdinalIgnoreCase));
                if (empaque is null)
                {
                    fila.Error(P.Unidad, ImportErrors.CellNotFound,
                        $"«{unidad}» no es la unidad base ni una unidad alterna del producto {producto.Code}. Agréguela en la hoja Unidades o en el producto.");
                    continue;
                }
            }

            if (DigitoDeControlErrado(barras))
                fila.Aviso(P.CodigoDeBarras, AvisoDeDigitoDeControl,
                    $"El código {barras} no cumple el dígito de control EAN/UPC. Si es un código interno, puede dejarlo así.");

            var existente = producto.Barcodes.FirstOrDefault(b => !b.IsDeleted && b.Barcode == barras);
            var llave = barras;
            if (existente is null)
            {
                producto.Barcodes.Add(new ProductBarcode
                {
                    Product = producto, Barcode = barras, ProductUnit = empaque, IsPrimary = producto.Barcodes.All(b => b.IsDeleted || !b.IsPrimary),
                });
                hoja.Contexto.Registrar(fila, llave, AccionDeImportacion.Create, [new(P.Producto, null, producto.Code), new(P.Unidad, null, empaque?.Unit?.Code)]);
                continue;
            }

            var campos = new List<CampoCambiadoDto>();
            FilasDeCatalogo.Diferencia(campos, P.Unidad, existente.ProductUnit?.Unit?.Code, empaque?.Unit?.Code);
            if (!ReferenceEquals(existente.ProductUnit, empaque) && existente.ProductUnitId != empaque?.Id)
            {
                existente.ProductUnit = empaque;
                existente.ProductUnitId = empaque?.Id is > 0 ? empaque.Id : null;
            }
            hoja.Contexto.Registrar(fila, llave, FilasDeCatalogo.Accion(campos), campos);
        }
    }

    /// <summary>Un código numérico de 8, 12, 13 o 14 dígitos (EAN-8, UPC-A, EAN-13, GTIN-14) con el dígito de control errado.</summary>
    public static bool DigitoDeControlErrado(string codigo)
    {
        if (codigo.Length is not (8 or 12 or 13 or 14) || !codigo.All(char.IsAsciiDigit)) return false;
        var suma = 0;
        for (var i = codigo.Length - 2; i >= 0; i--)
        {
            var posicionDesdeLaDerecha = codigo.Length - 1 - i;
            suma += (codigo[i] - '0') * (posicionDesdeLaDerecha % 2 == 1 ? 3 : 1);
        }
        var esperado = (10 - suma % 10) % 10;
        return esperado != codigo[^1] - '0';
    }

    // ------------------------------------------------------------------------------------------ ImpuestosAdicionales --

    private static void Impuestos(HojaDeImportacion hoja, Catalogos c, Dictionary<string, Tocado> tocados)
    {
        foreach (var fila in hoja.Filas)
        {
            var tocado = ProductoDe(fila, tocados);
            var tarifa = fila.Referencia(P.Tarifa, c.Tarifas, "una tarifa", "en la plantilla de impuestos o en Maestros → Impuestos");
            var unidades = fila.Costo(P.UnidadesGravables);
            if (tocado is null || tarifa is null || fila.TieneErrores) continue;
            if (!hoja.LlaveUnica(fila, $"{tocado.Producto.Code}|{tarifa.Code}", P.Tarifa)) continue;

            var definicion = c.Definiciones.GetValueOrDefault(tarifa.TaxDefinitionId);
            if (definicion is null) continue;
            if (ReglasDeProducto.EsRetencion(definicion))
            {
                FilasDeCatalogo.Error(fila, P.Tarifa, CatalogErrors.ProductTaxWithholdingNotAllowed(definicion.Code));
                continue;
            }
            if (definicion.Kind == TaxKind.Iva)
            {
                fila.Error(P.Tarifa, ImportErrors.CellFormat, $"«{tarifa.Code}» es una tarifa de IVA: va en la columna {P.TarifaIva} de la hoja Productos.");
                continue;
            }
            if (tocado.Adicionales.Any(a => a.Impuesto.Definicion.Id == definicion.Id))
            {
                FilasDeCatalogo.Error(fila, P.Tarifa, CatalogErrors.ProductTaxDuplicate(definicion.Code));
                continue;
            }
            if (definicion.CalculationForm == TaxCalculationForm.AmountPerUnit && unidades is not > 0)
            {
                FilasDeCatalogo.Error(fila, P.UnidadesGravables, CatalogErrors.ProductTaxUnitsRequired(definicion.Code));
                continue;
            }
            var gravables = definicion.CalculationForm == TaxCalculationForm.AmountPerUnit ? unidades : null;
            tocado.Adicionales.Add((fila, new ImpuestoResuelto(definicion, tarifa, gravables)));

            var actual = tocado.Producto.Taxes.FirstOrDefault(t => !t.IsDeleted && t.TaxDefinitionId == definicion.Id);
            var llave = $"{tocado.Producto.Code} {tarifa.Code}";
            if (actual is null)
            {
                hoja.Contexto.Registrar(fila, llave, AccionDeImportacion.Create, [new(P.Tarifa, null, tarifa.Code), new(P.UnidadesGravables, null, FilasDeCatalogo.Numero(gravables))]);
                continue;
            }
            var campos = new List<CampoCambiadoDto>();
            FilasDeCatalogo.Diferencia(campos, P.Tarifa, actual.TaxRateCode, tarifa.Code);
            FilasDeCatalogo.Diferencia(campos, P.UnidadesGravables, FilasDeCatalogo.Numero(actual.TaxableUnitsPerBaseUnit), FilasDeCatalogo.Numero(gravables));
            hoja.Contexto.Registrar(fila, llave, FilasDeCatalogo.Accion(campos), campos);
        }
    }

    /// <summary>
    /// El conjunto final de impuestos de cada producto tocado —el IVA de <c>tarifaIva</c> si vino su fila, los adicionales del
    /// archivo y los demás que ya tenía— contra su tratamiento de IVA (<see cref="ReglasDeProducto.ValidarIva"/>), y se fija.
    /// </summary>
    private void ImpuestosFinales(HojaDeImportacion hProductos, Catalogos c, Dictionary<string, Tocado> tocados)
    {
        var ahora = reloj.UtcNow;
        foreach (var t in tocados.Values.Where(t => !t.ConError && (t.FilaDeProducto is not null || t.Adicionales.Count > 0)))
        {
            var p = t.Producto;
            var finales = new List<ImpuestoResuelto>();
            foreach (var guardado in p.Taxes.Where(x => !x.IsDeleted))
            {
                if (c.Definiciones.GetValueOrDefault(guardado.TaxDefinitionId) is not { } def) continue;
                if (def.Kind == TaxKind.Iva && t.FilaDeProducto is not null) continue;
                if (t.Adicionales.Any(a => a.Impuesto.Definicion.Id == def.Id)) continue;
                var tarifa = guardado.TaxRateCode is { } codigo ? c.TarifaPorCodigo.GetValueOrDefault((def.Id, codigo)) : null;
                finales.Add(new ImpuestoResuelto(def, tarifa, guardado.TaxableUnitsPerBaseUnit));
            }
            if (t.FilaDeProducto is not null && t.TarifaIva is { } iva && c.Definiciones.GetValueOrDefault(iva.TaxDefinitionId) is { } defIva)
                finales.Add(new ImpuestoResuelto(defIva, iva, null));
            finales.AddRange(t.Adicionales.Select(a => a.Impuesto));

            var validacion = ReglasDeProducto.ValidarIva(p.VatSaleTreatment, finales);
            if (validacion.IsFailure)
            {
                var fila = t.FilaDeProducto ?? t.Adicionales[0].Fila;
                FilasDeCatalogo.Error(fila, t.FilaDeProducto is null ? P.Tarifa : P.TarifaIva, validacion.Error);
                t.ConError = true;
                continue;
            }
            ReglasDeProducto.FijarImpuestos(p, finales, ahora);
        }
        _ = hProductos;
    }
}

/// <summary>La plantilla 6 llena (§0.6): las cuatro hojas con lo que hoy tiene la cooperativa. (nuevo)</summary>
public sealed record GetProductsTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetProductsTemplateDataQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductsTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetProductsTemplateDataQuery request, CancellationToken ct)
    {
        var productos = await db.Products.AsNoTracking()
            .Include(p => p.Category).Include(p => p.Brand).Include(p => p.BaseUnit).Include(p => p.AccountingGroup).Include(p => p.WithholdingConcept)
            .Include(p => p.Units).ThenInclude(u => u.Unit)
            .Include(p => p.Barcodes).ThenInclude(b => b.ProductUnit).ThenInclude(u => u!.Unit)
            .Include(p => p.Taxes)
            .Include(p => p.ParentProduct)
            .Include(p => p.VariantValues).ThenInclude(v => v.VariantAttribute)
            .Include(p => p.VariantValues).ThenInclude(v => v.VariantAttributeValue)
            .Include(p => p.Components).ThenInclude(c => c.ComponentProduct)
            .OrderBy(p => p.Code)

            .ToListAsync(ct);
        var definiciones = await db.TaxDefinitions.AsNoTracking().ToDictionaryAsync(d => d.Id, ct);
        bool EsIva(ProductTax t) => definiciones.GetValueOrDefault(t.TaxDefinitionId)?.Kind == TaxKind.Iva;

        var filasProductos = productos.Select(p => (IReadOnlyList<object?>)
        [
            p.Code, p.Name, p.Kind.ToString(), p.Category?.Code, p.Brand?.Code, p.BaseUnit?.Code, p.AccountingGroup?.Code, p.Status.ToString(),
            p.TracksLot, p.TracksSerial, p.TracksExpiry, p.VatSaleTreatment.ToString(),
            p.Taxes.Where(EsIva).Select(t => t.TaxRateCode).FirstOrDefault(), p.WithholdingConcept?.Code, p.Reference, p.Weight, p.Volume, null,
        ]).ToList();
        var filasCodigos = productos.SelectMany(p => p.Barcodes.OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Barcode)
            .Select(b => (IReadOnlyList<object?>)[p.Code, b.Barcode, b.ProductUnit?.Unit?.Code])).ToList();
        var filasUnidades = productos.SelectMany(p => p.Units.OrderBy(u => u.Unit?.Code)
            .Select(u => (IReadOnlyList<object?>)[p.Code, u.Unit?.Code, u.Factor, u.Usage.ToString()])).ToList();
        var filasImpuestos = productos.SelectMany(p => p.Taxes.Where(t => !EsIva(t))
            .Select(t => (IReadOnlyList<object?>)[p.Code, t.TaxRateCode, t.TaxableUnitsPerBaseUnit])).ToList();
        var filasVariantes = productos.Where(p => p.ParentProduct is not null).SelectMany(p => p.VariantValues
            .OrderBy(v => v.VariantAttribute?.Code, StringComparer.Ordinal)
            .Select(v => (IReadOnlyList<object?>)[p.Code, p.ParentProduct!.Code, v.VariantAttribute?.Code, v.VariantAttributeValue?.Code])).ToList();
        var filasComponentes = productos.SelectMany(p => p.Components.OrderBy(c => c.ComponentProduct?.Code, StringComparer.Ordinal)
            .Select(c => (IReadOnlyList<object?>)[p.Code, c.ComponentProduct?.Code, c.Quantity])).ToList();

        return Result.Success(new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [P.HojaProductos] = filasProductos,
            [P.HojaCodigos] = filasCodigos,
            [P.HojaUnidades] = filasUnidades,
            [P.HojaImpuestos] = filasImpuestos,
            [P.HojaVariantes] = filasVariantes,
            [P.HojaComponentes] = filasComponentes,
        }));
    }
}
