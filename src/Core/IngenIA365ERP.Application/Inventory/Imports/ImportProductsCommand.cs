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
/// (<c>Import.Cell.PermissionRequired</c>) y el motivo (<c>requiresReason</c>), y lo hace <c>ChangeProductAccountingGroupCommand</c>
/// de US3: hasta entonces la fila responde <c>Import.Cell.NotYetAvailable</c>. Sin movimientos, es una edición más. Un código
/// numérico de 8, 12, 13 o 14 dígitos con el dígito de control EAN/UPC errado deja el aviso <c>Import.Barcode.CheckDigit</c>. (nuevo)
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

public sealed class ImportProductsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IDateTimeService reloj)
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
        public List<(FilaDeImportacion Fila, ImpuestoResuelto Impuesto)> Adicionales { get; } = [];
    }

    private sealed record Catalogos(
        CatalogoCitado<ProductCategory> Categorias,
        CatalogoCitado<Brand> Marcas,
        CatalogoCitado<UnitOfMeasure> Unidades,
        CatalogoCitado<AccountingGroup> Grupos,
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

        // Todo en bloque (§0.3): los catálogos citados y sólo los productos y códigos que el archivo nombra.
        var catalogos = await CatalogosAsync(ct);
        var codigosDelArchivo = hProductos.Filas.Select(f => f.Crudo(P.Codigo))
            .Concat(new[] { hUnidades, hCodigos, hImpuestos }.SelectMany(h => h.Filas.Select(f => f.Crudo(P.Producto))))
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

        var barrasDelArchivo = hCodigos.Filas.Select(f => ProductBarcode.Normalizar(f.Crudo(P.CodigoDeBarras))).Where(b => b.Length > 0).Distinct().ToList();
        var duenos = (await db.ProductBarcodes.AsNoTracking()
                .Where(b => barrasDelArchivo.Contains(b.Barcode))
                .Join(db.Products, b => b.ProductId, p => p.Id, (b, p) => new { b.Barcode, p.Id, p.PublicId, p.Code, p.Name })
                .ToListAsync(ct))
            .GroupBy(d => d.Barcode).ToDictionary(g => g.Key, g => g.First());

        var tocados = new Dictionary<string, Tocado>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existentes) tocados[p.Code] = new Tocado(p, esNuevo: false);

        Productos(ctx, hProductos, catalogos, tocados, conMovimientos);
        Unidades(hUnidades, catalogos, tocados, movimientos);
        Codigos(hCodigos, tocados, duenos.ToDictionary(d => d.Key, d => (d.Value.Id, d.Value.PublicId, d.Value.Code, d.Value.Name)));
        Impuestos(hImpuestos, catalogos, tocados);
        ImpuestosFinales(hProductos, catalogos, tocados);

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
        var tarifas = await db.TaxRates.ToListAsync(ct);
        // Una tarifa por código y definición: la vigencia más reciente (el producto guarda el código, T22).
        var vigentes = tarifas.GroupBy(r => (r.TaxDefinitionId, r.Code)).Select(g => g.OrderByDescending(r => r.ValidFrom).First()).ToList();

        return new Catalogos(
            CatalogoCitado<ProductCategory>.Desde(await db.ProductCategories.ToListAsync(ct), c => c, c => c.Code),
            CatalogoCitado<Brand>.Desde(await db.Brands.ToListAsync(ct), b => b, b => b.Code),
            CatalogoCitado<UnitOfMeasure>.Desde(await db.UnitsOfMeasure.ToListAsync(ct), u => u, u => u.Code),
            CatalogoCitado<AccountingGroup>.Desde(await db.AccountingGroups.ToListAsync(ct), g => g, g => g.Code),
            CatalogoCitado<WithholdingConcept>.Desde(await db.WithholdingConcepts.ToListAsync(ct), c => c, c => c.Code),
            CatalogoCitado<TaxRate>.Desde(vigentes.Where(r => definiciones.GetValueOrDefault(r.TaxDefinitionId)?.Kind == TaxKind.Iva), r => r, r => r.Code),
            CatalogoCitado<TaxRate>.Desde(vigentes, r => r, r => r.Code),
            definiciones,
            vigentes.ToDictionary(r => (r.TaxDefinitionId, r.Code)));
    }

    // ---------------------------------------------------------------------------------------------------- Productos --

    private void Productos(ContextoDeImportacion ctx, HojaDeImportacion hoja, Catalogos c, Dictionary<string, Tocado> tocados, HashSet<int> conMovimientos)
    {
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
            fila.Texto(P.MotivoCambioDeGrupo);

            // Lo que llega con I6 (§6): clases de producto y seguimiento por lote, serie o vencimiento.
            if (tipo is { } k && !ReglasDeProducto.ClasesDisponibles.Contains(k)) fila.TodaviaNoDisponible(P.Tipo, "I6");
            if (lote) fila.TodaviaNoDisponible(P.ControlaLote, "I6");
            if (serie) fila.TodaviaNoDisponible(P.ControlaSerie, "I6");
            if (vencimiento) fila.TodaviaNoDisponible(P.ControlaVencimiento, "I6");
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

            // Grupo de un producto con movimientos: la reclasificación de US3 (con permiso y motivo), nunca una edición.
            var tieneMovimientos = !tocado.EsNuevo && conMovimientos.Contains(producto.Id);
            if (tieneMovimientos && producto.AccountingGroupId != grupo?.Id && producto.BaseUnitId == unidadBase.Id)
            {
                tocado.ConError = true;
                if (!ctx.TienePermiso(P.PermisoDeReclasificar))
                {
                    fila.Error(P.GrupoContable, ImportErrors.CellPermissionRequired,
                        $"El producto {codigo} tiene movimientos: cambiar su grupo contable exige el permiso {P.PermisoDeReclasificar}.");
                    continue;
                }
                ctx.PedirMotivo();
                fila.Error(P.GrupoContable, ImportErrors.CellNotYetAvailable,
                    $"El producto {codigo} tiene movimientos: su grupo contable cambia por la reclasificación de grupo (Inventario → Productos → Grupo contable), que se habilita con la entrega de costos (US3).");
                continue;
            }

            var datos = new DatosDeProducto(nombre, producto.ShortName, producto.Description, tipo.Value, categoria.PublicId, marca?.PublicId,
                unidadBase.PublicId, grupo?.PublicId, tratamiento.Value, concepto?.PublicId, referencia, peso, volumen, lote, serie, vencimiento,
                producto.IsPurchasable, producto.IsSellable);
            var reglas = ReglasDeProducto.Aplicar(producto, datos, new ReferenciasDeProducto(categoria, marca, unidadBase, grupo, concepto), tieneMovimientos);
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

            var despues = Foto(producto, c) with { TarifaIva = tarifaIva?.Code };
            var campos = new List<CampoCambiadoDto>();
            foreach (var (columna, a, d) in antes!.Diferencias(despues)) campos.Add(new(columna, a, d));
            ctx.Registrar(fila, codigo, FilasDeCatalogo.Accion(campos), campos);
        }
    }

    /// <summary>El producto como lo muestra la plantilla, para los cambios campo a campo.</summary>
    private sealed record FotoDeProducto(
        string Nombre, string Tipo, string? Categoria, string? Marca, string? UnidadBase, string? Grupo, string Estado, string Iva, string? TarifaIva,
        string? Concepto, string? Referencia, string Peso, string Volumen)
    {
        public IEnumerable<(string, string?, string?)> Diferencias(FotoDeProducto d)
        {
            var pares = new (string, string?, string?)[]
            {
                (P.Nombre, Nombre, d.Nombre), (P.Tipo, Tipo, d.Tipo), (P.Categoria, Categoria, d.Categoria), (P.Marca, Marca, d.Marca),
                (P.UnidadBase, UnidadBase, d.UnidadBase), (P.GrupoContable, Grupo, d.Grupo), (P.Estado, Estado, d.Estado),
                (P.TratamientoIva, Iva, d.Iva), (P.TarifaIva, TarifaIva, d.TarifaIva), (P.ConceptoRetencion, Concepto, d.Concepto),
                (P.Referencia, Referencia, d.Referencia), (P.Peso, Peso, d.Peso), (P.Volumen, Volumen, d.Volumen),
            };
            return pares.Where(p => !string.Equals(p.Item2 ?? string.Empty, p.Item3 ?? string.Empty, StringComparison.Ordinal));
        }
    }

    private static FotoDeProducto Foto(Product p, Catalogos c) => new(
        p.Name, p.Kind.ToString(), p.Category?.Code, p.Brand?.Code, p.BaseUnit?.Code, p.AccountingGroup?.Code, p.Status.ToString(),
        p.VatSaleTreatment.ToString(), TarifaIvaGuardada(p, c), p.WithholdingConcept?.Code, p.Reference,
        FilasDeCatalogo.Numero(p.Weight), FilasDeCatalogo.Numero(p.Volume));

    private static string? TarifaIvaGuardada(Product p, Catalogos c) =>
        p.Taxes.Where(t => !t.IsDeleted && c.Definiciones.GetValueOrDefault(t.TaxDefinitionId)?.Kind == TaxKind.Iva)
            .Select(t => t.TaxRateCode).FirstOrDefault();

    /// <summary>La columna donde va el error de una regla del producto.</summary>
    private static string ColumnaDe(Error error) => error.Code switch
    {
        "Inventory.Product.AccountingGroupRequired" or "Inventory.Product.UseReclassifyAccountingGroup" or "Inventory.AccountingGroup.Inactive" => P.GrupoContable,
        "Inventory.Product.WithholdingConceptRequired" => P.ConceptoRetencion,
        "Inventory.Product.BaseUnitLocked" => P.UnidadBase,
        "Inventory.Product.KindNotAvailable" => P.Tipo,
        "Inventory.Product.TrackingNotAvailable" => P.ControlaLote,
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

        return Result.Success(new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [P.HojaProductos] = filasProductos,
            [P.HojaCodigos] = filasCodigos,
            [P.HojaUnidades] = filasUnidades,
            [P.HojaImpuestos] = filasImpuestos,
        }));
    }
}
