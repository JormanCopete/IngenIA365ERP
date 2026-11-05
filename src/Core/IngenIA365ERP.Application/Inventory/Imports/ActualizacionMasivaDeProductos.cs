using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeActualizacionDeProductos;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// La plantilla <c>inventory.products-update</c>: cambia en bloque lo que ya existe. Sólo <c>codigo</c> es obligatorio y una celda vacía
/// no cambia nada; nunca crea productos. Las cuentas no se editan aquí: las da el grupo contable por la matriz de Contabilidad. (nuevo)
/// </summary>
public static class PlantillaDeActualizacionDeProductos
{
    public const string Clave = "inventory.products-update";

    public const string PermisoDePrecios = "Inventory.Prices.Manage";

    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Categoria = "categoria";
    public const string Marca = "marca";
    public const string UnidadBase = "unidadBase";
    public const string GrupoContable = "grupoContable";
    public const string Estado = "estado";
    public const string TratamientoIva = "tratamientoIva";
    public const string TarifaIva = "tarifaIva";
    public const string ConceptoRetencion = "conceptoRetencion";
    public const string Referencia = "referencia";
    public const string ListaDePrecios = "listaDePrecios";
    public const string Precio = "precio";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Actualización masiva de productos", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoLargo, Reglas: "llave; el producto debe existir: esta plantilla no crea productos", Ejemplo: "ARZ-001"),
            new(Nombre, TipoDeValor.Texto, Largo: 200, Reglas: "vacío = no cambia", Ejemplo: "ARROZ DIANA 500 G"),
            new(Categoria, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de categoría; vacío = no cambia"),
            new(Marca, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de marca; vacío = no cambia"),
            new(UnidadBase, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de unidad; vacío = no cambia. No cambia si el producto tiene movimientos", Ejemplo: "GAL"),
            new(GrupoContable, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto,
                Reglas: "código de grupo: de él salen las cuentas; vacío = no cambia. Con movimientos exige " + PlantillaDeProductos.PermisoDeReclasificar + " y motivo"),
            new(Estado, TipoDeValor.Enumeracion, Reglas: "Active (activo), Inactive (inactivo), Blocked (bloqueado); vacío = no cambia; cambiarlo pide motivo"),
            new(TratamientoIva, TipoDeValor.Enumeracion, Reglas: "Taxed (gravado), Exempt (exento), Excluded (excluido); vacío = no cambia", Ejemplo: "Excluded"),
            new(TarifaIva, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de tarifa de IVA; vacío = no cambia. Un gravado sin tarifa es un error", Ejemplo: "IVA19"),
            new(ConceptoRetencion, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "concepto de retención en compras; vacío = no cambia"),
            new(Referencia, TipoDeValor.Texto, Largo: 60, Reglas: "vacío = no cambia"),
            new(ListaDePrecios, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de una lista que ya existe; va con precio", Ejemplo: "GENERAL", Permiso: PermisoDePrecios),
            new(Precio, TipoDeValor.Monto, Reglas: "≥ 0, en la unidad base del producto; va con listaDePrecios", Ejemplo: "2100,00", Permiso: PermisoDePrecios),
        ]),
    ]);
}

/// <summary>
/// El único camino de la actualización masiva: la plantilla y el formulario llegan aquí con las mismas filas. Cada fila pasa por
/// <see cref="ReglasDeProducto"/>, por <see cref="ReclasificacionDeGrupo"/> si cambia el grupo de un producto con movimientos y por
/// <see cref="ReglasDeListaDePrecios"/> si trae precio. No guarda: guarda el ejecutor, y sólo si no hubo errores. (nuevo)
/// </summary>
public sealed class ActualizadorDeProductos(IApplicationDbContext db, IDateTimeService reloj, ReclasificacionDeGrupo? reclasificacion = null)
{
    private sealed record Catalogos(
        CatalogoCitado<ProductCategory> Categorias, CatalogoCitado<Brand> Marcas, CatalogoCitado<UnitOfMeasure> Unidades, CatalogoCitado<AccountingGroup> Grupos,
        CatalogoCitado<WithholdingConcept> Conceptos, CatalogoCitado<TaxRate> TarifasIva, IReadOnlyDictionary<int, TaxDefinition> Definiciones,
        IReadOnlyDictionary<(int, string), TaxRate> TarifaPorCodigo, IReadOnlyDictionary<string, PriceList> Listas);

    private sealed record Reclasificar(FilaDeImportacion Fila, Product Producto, AccountingGroup Destino);

    public async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        var codigos = filas.Select(f => CodigoDeCatalogo.Normalizar(f.Crudo(P.Codigo))).OfType<string>().Distinct().ToList();
        var productos = (await db.Products
                .Include(p => p.Category).Include(p => p.Brand).Include(p => p.BaseUnit).Include(p => p.AccountingGroup).Include(p => p.WithholdingConcept)
                .Include(p => p.Units).Include(p => p.Barcodes).Include(p => p.Taxes)
                .Where(p => codigos.Contains(p.Code))
                .ToListAsync(ct))
            .ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var ids = productos.Values.Select(p => p.Id).ToList();
        var conMovimientos = (await db.InventoryDocumentLines.IgnoreQueryFilters().AsNoTracking()
            .Where(l => ids.Contains(l.ProductId)).Select(l => l.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        var conPrecios = (await db.PriceListItems.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId)).Select(i => new { i.ProductId, i.UnitId }).ToListAsync(ct))
            .Select(i => (i.ProductId, i.UnitId)).ToHashSet();

        var catalogos = await CatalogosAsync(ct);
        var preciosPorLista = new Dictionary<int, Dictionary<(int ProductId, int UnitId), PriceListItem>>();
        var pendientes = new List<Reclasificar>();

        foreach (var fila in filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var nombre = fila.Texto(P.Nombre);
            var categoria = fila.Referencia(P.Categoria, catalogos.Categorias, "una categoría", "en Inventario → Categorías");
            var marca = fila.Referencia(P.Marca, catalogos.Marcas, "una marca", "en Inventario → Marcas");
            var unidad = fila.Referencia(P.UnidadBase, catalogos.Unidades, "una unidad de medida", "en Inventario → Unidades");
            var grupo = fila.Referencia(P.GrupoContable, catalogos.Grupos, "un grupo contable", "en Inventario → Grupos contables");
            var estado = fila.Enumeracion(P.Estado, PlantillaDeProductos.EtiquetasDeEstado);
            var tratamiento = fila.Enumeracion(P.TratamientoIva, PlantillaDeProductos.EtiquetasDeIva);
            var tarifa = fila.Referencia(P.TarifaIva, catalogos.TarifasIva, "una tarifa de IVA", "en Maestros → Impuestos");
            var concepto = fila.Referencia(P.ConceptoRetencion, catalogos.Conceptos, "un concepto de retención", "en Maestros → Impuestos");
            var referencia = fila.Texto(P.Referencia);
            var lista = fila.Codigo(P.ListaDePrecios);
            var precio = fila.Monto(P.Precio);

            if (codigo is null || !ctx.Datos.LlaveUnica(fila, codigo, P.Codigo)) continue;
            if (!productos.TryGetValue(codigo, out var producto))
            {
                fila.Error(P.Codigo, ImportErrors.CellNotFound, $"No hay un producto «{codigo}». Esta plantilla no crea productos.");
                continue;
            }
            if ((lista is null) != (precio is null))
                fila.Error(lista is null ? P.ListaDePrecios : P.Precio, ImportErrors.CellRequired, "listaDePrecios y precio van juntos.");
            if (fila.TieneErrores) continue;

            var antes = Foto(producto, catalogos);
            var tieneMovimientos = conMovimientos.Contains(producto.Id);
            var grupoFinal = grupo ?? producto.AccountingGroup;
            var unidadFinal = unidad ?? producto.BaseUnit;
            var categoriaFinal = categoria ?? producto.Category;
            if (categoriaFinal is null || unidadFinal is null)
            {
                fila.Error(P.Codigo, CatalogErrors.CategoryNotFound().Code, $"El producto {codigo} cita una categoría o una unidad que ya no existe.");
                continue;
            }

            // Grupo de un producto con movimientos: la reclasificación, con permiso y motivo; las demás reglas corren con el grupo que tiene.
            Reclasificar? reclasifica = null;
            if (tieneMovimientos && grupo is not null && producto.AccountingGroupId != grupo.Id)
            {
                if (!ctx.TienePermiso(PlantillaDeProductos.PermisoDeReclasificar))
                {
                    fila.Error(P.GrupoContable, ImportErrors.CellPermissionRequired,
                        $"El producto {codigo} tiene movimientos: cambiar su grupo contable exige el permiso {PlantillaDeProductos.PermisoDeReclasificar}.");
                    continue;
                }
                ctx.PedirMotivo();
                reclasifica = new Reclasificar(fila, producto, grupo);
                grupoFinal = producto.AccountingGroup;
            }

            if (unidad is not null && unidad.Id != producto.BaseUnitId && producto.Units.Any(u => !u.IsDeleted && u.UnitId == unidad.Id))
            {
                fila.Error(P.UnidadBase, ImportErrors.CellFormat, $"{unidad.Code} ya es una unidad alterna de {codigo}: quítela antes de volverla la base.");
                continue;
            }

            var datos = new DatosDeProducto(nombre ?? producto.Name, producto.ShortName, producto.Description, producto.Kind, categoriaFinal.PublicId,
                (marca ?? producto.Brand)?.PublicId, unidadFinal.PublicId, grupoFinal?.PublicId, tratamiento ?? producto.VatSaleTreatment,
                (concepto ?? producto.WithholdingConcept)?.PublicId, referencia ?? producto.Reference, producto.Weight, producto.Volume,
                producto.TracksLot, producto.TracksSerial, producto.TracksExpiry, producto.IsPurchasable, producto.IsSellable);
            var reglas = ReglasDeProducto.Aplicar(producto, datos,
                new ReferenciasDeProducto(categoriaFinal, marca ?? producto.Brand, unidadFinal, grupoFinal, concepto ?? producto.WithholdingConcept),
                new HistoriaDeProducto(tieneMovimientos));
            if (reglas.IsFailure)
            {
                fila.Error(ColumnaDe(reglas.Error), reglas.Error.Code, reglas.Error.Message);
                continue;
            }

            if ((tratamiento is not null || tarifa is not null) && !FijarIva(fila, producto, tarifa, catalogos)) continue;
            if (estado is { } nuevoEstado && nuevoEstado != producto.Status)
            {
                ctx.PedirMotivo();
                producto.Status = nuevoEstado;
            }
            producto.SearchText = ReglasDeProducto.TextoDeBusqueda(producto, producto.Brand?.Name, producto.Barcodes.Where(b => !b.IsDeleted).Select(b => b.Barcode));

            if (unidad is not null && antes.Unidad != unidad.Code && conPrecios.Any(c => c.ProductId == producto.Id) && lista is null)
                fila.Aviso(P.UnidadBase, "Inventory.Product.PricesInOldUnit",
                    $"{codigo} tiene precios en la unidad anterior: revíselos en sus listas, porque no pasan solos a {unidad.Code}.");

            var despues = Foto(producto, catalogos) with { Grupo = reclasifica?.Destino.Code ?? producto.AccountingGroup?.Code };
            var campos = antes.Diferencias(despues).Select(d => new CampoCambiadoDto(d.Columna, d.Antes, d.Despues)).ToList();

            if (lista is not null && precio is { } valor)
            {
                var cambio = await PrecioAsync(fila, producto, lista, valor, unidadFinal, catalogos, preciosPorLista, ct);
                if (cambio is null) continue;
                if (cambio.Value.Cambio) campos.Add(new CampoCambiadoDto($"{P.Precio} {lista}", cambio.Value.Antes, Numero(valor)));
            }
            if (reclasifica is not null) pendientes.Add(reclasifica);
            ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
        }

        await ReclasificarAsync(ctx, pendientes, ct);
    }

    private async Task<Catalogos> CatalogosAsync(CancellationToken ct)
    {
        var definiciones = await db.TaxDefinitions.ToDictionaryAsync(d => d.Id, ct);
        var vigentes = (await db.TaxRates.ToListAsync(ct)).GroupBy(r => (r.TaxDefinitionId, r.Code)).Select(g => g.OrderByDescending(r => r.ValidFrom).First()).ToList();
        return new Catalogos(
            CatalogoCitado<ProductCategory>.Desde(await db.ProductCategories.ToListAsync(ct), c => c, c => c.Code),
            CatalogoCitado<Brand>.Desde(await db.Brands.ToListAsync(ct), b => b, b => b.Code),
            CatalogoCitado<UnitOfMeasure>.Desde(await db.UnitsOfMeasure.ToListAsync(ct), u => u, u => u.Code),
            CatalogoCitado<AccountingGroup>.Desde(await db.AccountingGroups.ToListAsync(ct), g => g, g => g.Code),
            CatalogoCitado<WithholdingConcept>.Desde(await db.WithholdingConcepts.ToListAsync(ct), c => c, c => c.Code),
            CatalogoCitado<TaxRate>.Desde(vigentes.Where(r => definiciones.GetValueOrDefault(r.TaxDefinitionId)?.Kind == TaxKind.Iva), r => r, r => r.Code),
            definiciones,
            vigentes.ToDictionary(r => (r.TaxDefinitionId, r.Code)),
            (await db.PriceLists.Where(l => !l.IsDeleted).ToListAsync(ct)).ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Deja el IVA que resulta y conserva los demás impuestos. Con <paramref name="tarifa"/> vacía, un gravado conserva su IVA; un exento o
    /// excluido lo pierde. Lo decide <see cref="ReglasDeProducto.ValidarIva"/>, como en el alta.
    /// </summary>
    private bool FijarIva(FilaDeImportacion fila, Product producto, TaxRate? tarifa, Catalogos c)
    {
        var finales = new List<ImpuestoResuelto>();
        foreach (var guardado in producto.Taxes.Where(t => !t.IsDeleted))
        {
            if (c.Definiciones.GetValueOrDefault(guardado.TaxDefinitionId) is not { } definicion) continue;
            if (definicion.Kind == TaxKind.Iva && (tarifa is not null || producto.VatSaleTreatment != VatSaleTreatment.Taxed)) continue;
            var vigente = guardado.TaxRateCode is { } codigo ? c.TarifaPorCodigo.GetValueOrDefault((definicion.Id, codigo)) : null;
            finales.Add(new ImpuestoResuelto(definicion, vigente, guardado.TaxableUnitsPerBaseUnit));
        }
        if (tarifa is not null && c.Definiciones.GetValueOrDefault(tarifa.TaxDefinitionId) is { } iva)
            finales.Add(new ImpuestoResuelto(iva, tarifa, null));

        var validacion = ReglasDeProducto.ValidarIva(producto.VatSaleTreatment, finales);
        if (validacion.IsFailure)
        {
            fila.Error(P.TarifaIva, validacion.Error.Code, validacion.Error.Message);
            return false;
        }
        ReglasDeProducto.FijarImpuestos(producto, finales, reloj.UtcNow);
        return true;
    }

    /// <summary>El precio en la unidad base, con las reglas de la plantilla 12. Nulo si la fila quedó con error.</summary>
    private async Task<(bool Cambio, string? Antes)?> PrecioAsync(FilaDeImportacion fila, Product producto, string codigoDeLista, decimal precio, UnitOfMeasure unidad,
        Catalogos c, Dictionary<int, Dictionary<(int ProductId, int UnitId), PriceListItem>> preciosPorLista, CancellationToken ct)
    {
        if (!c.Listas.TryGetValue(codigoDeLista, out var lista))
        {
            fila.Error(P.ListaDePrecios, ImportErrors.CellNotFound, $"No hay una lista de precios «{codigoDeLista}». Créela en Ventas → Listas de precios.");
            return null;
        }
        if (producto.Status == ProductStatus.Blocked)
        {
            fila.Error(P.Precio, ImportErrors.CellFormat, $"El producto {producto.Code} está bloqueado: no recibe precios.");
            return null;
        }
        var admitida = ReglasDeListaDePrecios.UnidadAdmitida(producto, unidad, esAlternaDeVenta: false);
        if (admitida.IsFailure)
        {
            fila.Error(P.Precio, admitida.Error.Code, admitida.Error.Message);
            return null;
        }
        if (!preciosPorLista.TryGetValue(lista.Id, out var actuales))
            preciosPorLista[lista.Id] = actuales = await ReglasDeListaDePrecios.PreciosVivosAsync(db, lista, ct);
        var antes = actuales.TryGetValue((producto.Id, unidad.Id), out var previo) ? previo.Price : (decimal?)null;
        var aplicado = ReglasDeListaDePrecios.AplicarPrecio(db, lista, new PrecioPedido(producto, unidad, precio), actuales);
        if (aplicado.IsFailure)
        {
            fila.Error(P.Precio, aplicado.Error.Code, aplicado.Error.Message);
            return null;
        }
        return (aplicado.Value != CambioDePrecio.Unchanged, antes is { } a ? Numero(a) : null);
    }

    /// <summary>Al revisar sólo valida; al aplicar reclasifica, con la transacción del ejecutor, con fecha de hoy y el motivo de la operación.</summary>
    private async Task ReclasificarAsync(ContextoDeImportacion ctx, List<Reclasificar> pendientes, CancellationToken ct)
    {
        foreach (var (fila, producto, destino) in pendientes)
        {
            if (reclasificacion is null)
            {
                fila.Error(P.GrupoContable, ImportErrors.CellNotYetAvailable, $"El producto {producto.Code} tiene movimientos: su grupo cambia por la reclasificación de grupo.");
                continue;
            }
            var error = ctx.Modo == ModoDeImportacion.Review
                ? (await reclasificacion.ValidarAsync(producto, destino, null, ct)) is { IsFailure: true } invalida ? invalida.Error : null
                : (await reclasificacion.AplicarAsync(producto, destino, null, ctx.Motivo ?? string.Empty, ct)) is { IsFailure: true } fallida ? fallida.Error : null;
            if (error is not null) fila.Error(P.GrupoContable, error.Code, error.Message);
        }
    }

    private static string ColumnaDe(Error error) => error.Code switch
    {
        var c when c == CatalogErrors.ProductBaseUnitLocked().Code => P.UnidadBase,
        var c when c == CatalogErrors.ProductAccountingGroupRequired().Code || c == CatalogErrors.ProductUseReclassifyAccountingGroup().Code => P.GrupoContable,
        var c when c == CatalogErrors.ProductWithholdingConceptRequired().Code => P.ConceptoRetencion,
        _ => P.Codigo,
    };

    private static string Numero(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    private Foto Foto(Product p, Catalogos c)
    {
        var tarifa = p.Taxes.Where(t => !t.IsDeleted && c.Definiciones.GetValueOrDefault(t.TaxDefinitionId)?.Kind == TaxKind.Iva).Select(t => t.TaxRateCode).FirstOrDefault();
        return new Foto(p.Name, p.Category?.Code, p.Brand?.Code, p.BaseUnit?.Code, p.AccountingGroup?.Code, p.Status.ToString(), p.VatSaleTreatment.ToString(),
            tarifa, p.WithholdingConcept?.Code, p.Reference);
    }
}

/// <summary>Lo que se compara antes y después de una fila. (nuevo)</summary>
internal sealed record Foto(string Nombre, string? Categoria, string? Marca, string? Unidad, string? Grupo, string Estado, string Iva, string? Tarifa, string? Concepto, string? Referencia)
{
    public IEnumerable<(string Columna, string? Antes, string? Despues)> Diferencias(Foto d)
    {
        (string, string?, string?)[] pares =
        [
            (P.Nombre, Nombre, d.Nombre), (P.Categoria, Categoria, d.Categoria), (P.Marca, Marca, d.Marca), (P.UnidadBase, Unidad, d.Unidad),
            (P.GrupoContable, Grupo, d.Grupo), (P.Estado, Estado, d.Estado), (P.TratamientoIva, Iva, d.Iva), (P.TarifaIva, Tarifa, d.Tarifa),
            (P.ConceptoRetencion, Concepto, d.Concepto), (P.Referencia, Referencia, d.Referencia),
        ];
        return pares.Where(x => !string.Equals(x.Item2 ?? string.Empty, x.Item3 ?? string.Empty, StringComparison.Ordinal));
    }
}

// ---------------------------------------------------------------------------------------------------- plantilla --

/// <summary><c>POST /api/inventory/products/bulk-update/import?mode=review|apply</c>. (nuevo)</summary>
public sealed record ImportProductUpdatesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportProductUpdatesCommandValidator : AbstractValidator<ImportProductUpdatesCommand>
{
    public ImportProductUpdatesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportProductUpdatesCommand>.LargoMaximo);
    }
}

public sealed class ImportProductUpdatesCommandHandler(EjecutorDeImportacion ejecutor, ActualizadorDeProductos actualizador)
    : IRequestHandler<ImportProductUpdatesCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportProductUpdatesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, actualizador.ProcesarAsync, ct);
}

/// <summary>La plantilla llena con lo que hoy tiene cada producto; lista y precio van vacíos. (nuevo)</summary>
public sealed record GetProductUpdatesTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetProductUpdatesTemplateDataQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductUpdatesTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetProductUpdatesTemplateDataQuery request, CancellationToken ct)
    {
        var productos = await db.Products.AsNoTracking()
            .Include(p => p.Category).Include(p => p.Brand).Include(p => p.BaseUnit).Include(p => p.AccountingGroup).Include(p => p.WithholdingConcept).Include(p => p.Taxes)
            .OrderBy(p => p.Code).ToListAsync(ct);
        var iva = (await db.TaxDefinitions.AsNoTracking().Where(d => d.Kind == TaxKind.Iva).Select(d => d.Id).ToListAsync(ct)).ToHashSet();
        var filas = productos.Select(p => (IReadOnlyList<object?>)
        [
            p.Code, p.Name, p.Category?.Code, p.Brand?.Code, p.BaseUnit?.Code, p.AccountingGroup?.Code, p.Status.ToString(), p.VatSaleTreatment.ToString(),
            p.Taxes.Where(t => iva.Contains(t.TaxDefinitionId)).Select(t => t.TaxRateCode).FirstOrDefault(), p.WithholdingConcept?.Code, p.Reference, null, null,
        ]).ToList();
        return Result.Success(new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase) { ["Datos"] = filas }));
    }
}

// --------------------------------------------------------------------------------------------------- formulario --

/// <summary>Los productos a los que aplica el formulario: los mismos filtros de la lista de productos. (nuevo)</summary>
public sealed record FiltroDeProductos(
    string? Search = null, Guid? CategoryPublicId = null, Guid? BrandPublicId = null, Guid? AccountingGroupPublicId = null,
    ProductKind? Kind = null, ProductStatus? Status = null);

/// <summary>El mismo valor para todos; nulo = no cambia. Por código, como en la plantilla. (nuevo)</summary>
public sealed record CambiosMasivos(
    string? Category = null, string? Brand = null, string? BaseUnit = null, string? AccountingGroup = null, string? Status = null,
    string? VatTreatment = null, string? VatRate = null, string? WithholdingConcept = null);

/// <summary>Un precio igual para todos (<see cref="Price"/>) o el actual ± <see cref="Percentage"/>, redondeado al múltiplo <see cref="RoundTo"/>. (nuevo)</summary>
public sealed record PrecioMasivo(string PriceList, decimal? Price = null, decimal? Percentage = null, int RoundTo = 1);

/// <summary>
/// <c>POST /api/inventory/products/bulk-update?mode=review|apply</c>: arma una fila de la plantilla por cada producto del filtro y corre por el
/// mismo <see cref="ActualizadorDeProductos"/>, con la misma revisión, el mismo todo o nada y la misma auditoría. (nuevo)
/// </summary>
public sealed record BulkUpdateProductsCommand(
    ModoDeImportacion? Mode, FiltroDeProductos? Filter, CambiosMasivos? Changes, PrecioMasivo? Price,
    IReadOnlyList<string>? ExcludedCodes = null, string? Reason = null)
    : IRequest<Result<ImportResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class BulkUpdateProductsCommandValidator : AbstractValidator<BulkUpdateProductsCommand>
{
    public BulkUpdateProductsCommandValidator()
    {
        RuleFor(x => x.Mode).NotNull().WithMessage("Indique si revisa o aplica.");
        RuleFor(x => x.Reason).NotEmpty().When(x => x.Mode == ModoDeImportacion.Apply).WithMessage("Escriba el motivo del cambio.");
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportProductUpdatesCommand>.LargoMaximo);
        RuleFor(x => x).Must(x => x.Price is not null || (x.Changes is { } c && new[] { c.Category, c.Brand, c.BaseUnit, c.AccountingGroup, c.Status, c.VatTreatment, c.VatRate, c.WithholdingConcept }.Any(v => !string.IsNullOrWhiteSpace(v))))
            .WithMessage("Elija al menos un cambio.");
        RuleFor(x => x.Price!).Must(p => (p.Price is null) != (p.Percentage is null)).When(x => x.Price is not null).WithMessage("Escriba un precio o un porcentaje, no los dos.");
        RuleFor(x => x.Price!.RoundTo).GreaterThan(0).When(x => x.Price is not null).WithMessage("El redondeo es un múltiplo mayor que cero.");
        RuleFor(x => x.Price!.Price).GreaterThanOrEqualTo(0m).When(x => x.Price?.Price is not null).WithMessage("El precio no puede ser negativo.");
    }
}

public sealed class BulkUpdateProductsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, ActualizadorDeProductos actualizador)
    : IRequestHandler<BulkUpdateProductsCommand, Result<ImportResultDto>>
{
    public const string ExtraConError = "productsWithErrors";
    public const string CodigoSinProductos = "Inventory.BulkUpdate.NoProducts";
    public const string AvisoSinPrecio = "Inventory.BulkUpdate.NoCurrentPrice";

    private static readonly string[] Encabezados =
        [P.Codigo, P.Categoria, P.Marca, P.UnidadBase, P.GrupoContable, P.Estado, P.TratamientoIva, P.TarifaIva, P.ConceptoRetencion, P.ListaDePrecios, P.Precio];

    public async Task<Result<ImportResultDto>> Handle(BulkUpdateProductsCommand request, CancellationToken ct)
    {
        if (request.Mode is not { } modo) return Result.Failure<ImportResultDto>(ImportErrors.ModeRequired);

        var f = request.Filter ?? new FiltroDeProductos();
        var consulta = await FiltroDeListaDeProductos.AplicarAsync(db, db.Products.AsNoTracking(), f.Search, f.CategoryPublicId, f.BrandPublicId,
            f.AccountingGroupPublicId, f.Kind, f.Status, ct);
        var excluidos = (request.ExcludedCodes ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var productos = (await consulta.OrderBy(p => p.Code).Select(p => new { p.Id, p.Code, p.BaseUnitId }).ToListAsync(ct))
            .Where(p => !excluidos.Contains(p.Code)).ToList();
        if (productos.Count == 0)
            return Result.Failure<ImportResultDto>(new Error(CodigoSinProductos, "Ningún producto cumple los filtros."));

        var actuales = await PreciosActualesAsync(request.Price, ct);
        var c = request.Changes ?? new CambiosMasivos();
        var sinPrecio = new List<ErrorDeFila>();
        var filas = new List<IReadOnlyList<string?>>(productos.Count);
        foreach (var p in productos)
        {
            var precio = PrecioDe(request.Price, actuales.GetValueOrDefault((p.Id, p.BaseUnitId)));
            if (request.Price is not null && precio is null)
                sinPrecio.Add(new ErrorDeFila(filas.Count + 2, P.Precio, AvisoSinPrecio, $"{p.Code} no tiene precio en {request.Price.PriceList}: el porcentaje no le aplica."));
            filas.Add([p.Code, c.Category, c.Brand, c.BaseUnit, c.AccountingGroup, c.Status, c.VatTreatment, c.VatRate, c.WithholdingConcept,
                precio is null ? null : request.Price!.PriceList, precio?.ToString("0.00", CultureInfo.InvariantCulture)]);
        }

        var resultado = await ejecutor.EjecutarFilasAsync(P.Definicion, modo, "formulario", request.Reason, Encabezados, filas, actualizador.ProcesarAsync, ct);
        if (resultado.IsFailure) return resultado;

        var conError = resultado.Value.Errors.Where(e => e.Row >= 2).Select(e => productos[e.Row - 2].Code).Distinct().ToList();
        return Result.Success(resultado.Value with
        {
            Warnings = [.. sinPrecio, .. resultado.Value.Warnings],
            Extra = new Dictionary<string, object?> { [ExtraConError] = conError },
        });
    }

    private async Task<Dictionary<(int ProductId, int UnitId), decimal>> PreciosActualesAsync(PrecioMasivo? precio, CancellationToken ct)
    {
        if (precio?.Percentage is null) return [];
        var filas = await db.PriceListItems.AsNoTracking()
            .Where(i => i.PriceList!.Code == precio.PriceList && !i.PriceList.IsDeleted)
            .Select(i => new { i.ProductId, i.UnitId, i.Price }).ToListAsync(ct);
        return filas.ToDictionary(i => (i.ProductId, i.UnitId), i => i.Price);
    }

    /// <summary>El precio de la fila; con porcentaje, nulo si el producto no tiene precio en esa lista.</summary>
    private static decimal? PrecioDe(PrecioMasivo? pedido, decimal actual)
    {
        if (pedido is null) return null;
        if (pedido.Price is { } fijo) return fijo;
        if (actual == 0m || pedido.Percentage is not { } porcentaje) return null;
        var bruto = actual * (1m + porcentaje / 100m);
        return Math.Round(bruto / pedido.RoundTo, 0, MidpointRounding.AwayFromZero) * pedido.RoundTo;
    }
}
