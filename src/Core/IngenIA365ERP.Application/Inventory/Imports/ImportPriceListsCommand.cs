using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeListasDePrecios;

namespace IngenIA365ERP.Application.Inventory.Imports;

/// <summary>
/// Plantilla 12 — listas de precios (feature 012, I3, T600; contracts/plantillas.md §12). Escribe <c>INV_PriceLists</c> e
/// <c>INV_PriceListItems</c>. Reemplaza la definición que I1 publicaba sólo para descargar. El nombre admite 80 caracteres, el
/// largo de la columna (§12 decía 120). (nuevo)
/// </summary>
public static class PlantillaDeListasDePrecios
{
    public const string Clave = CatalogoDePlantillas.ListasDePreciosClave;
    public const string HojaListas = "Listas";
    public const string HojaPrecios = "Precios";

    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string IncluyeImpuestos = "incluyeImpuestos";
    public const string Cliente = "cliente";
    public const string Segmento = "segmento";
    public const string Canal = "canal";
    public const string Sucursal = "sucursal";
    public const string VigenteDesde = "vigenteDesde";
    public const string VigenteHasta = "vigenteHasta";
    public const string Activa = "activa";

    public const string Lista = "lista";
    public const string Producto = "producto";
    public const string Unidad = "unidad";
    public const string Precio = "precio";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Listas de precios", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla(HojaListas,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "llave; no cambia", Ejemplo: "GENERAL"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDeListaDePrecios.LargoDeNombre, Ejemplo: "LISTA GENERAL"),
            new(IncluyeImpuestos, TipoDeValor.SiNo, Obligatoria: true, Reglas: "el precio trae los impuestos incluidos; no cambia", Ejemplo: "sí"),
            new(Cliente, TipoDeValor.Persona, Reglas: "documento de la persona; dimensión de ámbito", Ejemplo: ""),
            new(Segmento, TipoDeValor.Texto, Largo: ReglasDeListaDePrecios.LargoDeSegmento, Reglas: "una clase de asociado existente", Ejemplo: ""),
            new(Canal, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de canal de venta", Ejemplo: ""),
            new(Sucursal, TipoDeValor.Sucursal, Reglas: "código o nombre de la sucursal; las cuatro dimensiones vacías = lista general", Ejemplo: ""),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "no cambia; dos listas del mismo ámbito no se cruzan", Ejemplo: "AAAA-MM-01"),
            new(VigenteHasta, TipoDeValor.Fecha, Reglas: "vacío = sin cierre", Ejemplo: ""),
            new(Activa, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ]),
        new HojaDePlantilla(HojaPrecios,
        [
            new(Lista, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "código de una lista de la hoja Listas o existente", Ejemplo: "GENERAL"),
            new(Producto, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoLargo, Reglas: "activo o inactivo (aviso); no bloqueado ni plantilla", Ejemplo: "ARZ-001"),
            new(Unidad, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "vacío = la base; si no, una unidad de venta del producto", Ejemplo: ""),
            new(Precio, TipoDeValor.Monto, Obligatoria: true, Reglas: "≥ 0; con o sin impuestos según la lista", Ejemplo: "2100,00"),
        ], Obligatoria: false),
    ]);
}

/// <summary>
/// La plantilla 12 (feature 012, I3, T600; contracts/plantillas.md §12, §0.5; <c>POST /api/inventory/price-lists/import</c>): listas y
/// precios con <b>las mismas reglas que el alta</b> (<see cref="ReglasDeListaDePrecios"/>), todo o nada. El código de la lista es la
/// llave: si existe, actualiza nombre, cierre y activo; su ámbito, <c>incluyeImpuestos</c> y <c>vigenteDesde</c> no cambian. Una
/// lista nueva, un cierre de vigencia o una reactivación piden motivo. Los precios se agregan o reemplazan por (lista, producto,
/// unidad); la plantilla nunca borra. (nuevo)
/// </summary>
public sealed record ImportPriceListsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportPriceListsCommandValidator : AbstractValidator<ImportPriceListsCommand>
{
    public ImportPriceListsCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportPriceListsCommand>.LargoMaximo);
    }
}

public sealed class ImportPriceListsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, ICerrojoPorClave cerrojo)
    : IRequestHandler<ImportPriceListsCommand, Result<ImportResultDto>>
{
    public Task<Result<ImportResultDto>> Handle(ImportPriceListsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var listas = (await db.PriceLists.Where(l => !l.IsDeleted).ToListAsync(ct)).ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);
        await ListasAsync(ctx, listas, ct);
        await PreciosAsync(ctx, listas, ct);
    }

    private async Task ListasAsync(ContextoDeImportacion ctx, Dictionary<string, PriceList> listas, CancellationToken ct)
    {
        var hoja = ctx.Hoja(P.HojaListas);
        if (hoja.Filas.Count == 0) return;

        var documentos = hoja.Filas.Select(f => f.Crudo(P.Cliente)?.Trim()).OfType<string>().Distinct().ToList();
        var personas = CatalogoCitado<Person>.Desde(
            await db.People.Where(p => !p.IsDeleted && documentos.Contains(p.TaxId)).ToListAsync(ct), p => p, p => p.TaxId);
        var canales = CatalogoCitado<SalesChannel>.Desde(await db.SalesChannels.Where(c => !c.IsDeleted).ToListAsync(ct), c => c, c => c.Code);
        var sucursales = CatalogoCitado<Branch>.Desde(await db.Branches.Where(b => !b.IsDeleted).ToListAsync(ct), b => b, b => b.LegacyCode, b => b.Name);

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var nombre = fila.Texto(P.Nombre);
            var incluye = fila.EstaVacia(P.IncluyeImpuestos) ? (bool?)null : fila.SiNo(P.IncluyeImpuestos);
            var persona = fila.Referencia(P.Cliente, personas, "una persona con documento", "en Maestros → Personas (esta plantilla no crea personas)");
            var segmento = fila.Texto(P.Segmento);
            var canal = fila.Referencia(P.Canal, canales, "un canal de venta", "en Ventas → Canales");
            var sucursal = fila.Referencia(P.Sucursal, sucursales, "una sucursal", "en Maestros → Agencias");
            var desde = fila.Fecha(P.VigenteDesde);
            var hasta = fila.Fecha(P.VigenteHasta);
            var activa = fila.SiNo(P.Activa, porDefecto: true);
            if (!hoja.LlaveUnica(fila, codigo, P.Codigo) || codigo is null || nombre is null || incluye is null || desde is null || fila.TieneErrores) continue;
            if (hasta is { } h && h < desde)
            {
                fila.Error(P.VigenteHasta, ImportErrors.CellFormat, "La vigencia termina antes de empezar.");
                continue;
            }

            if (!listas.TryGetValue(codigo, out var lista))
            {
                var alta = await ReglasDeListaDePrecios.AltaAsync(db, cerrojo,
                    new DatosDeLista(codigo, nombre, incluye.Value, persona?.Id, segmento, canal?.Id, sucursal?.Id, desde.Value, hasta, activa), ct);
                if (alta.IsFailure)
                {
                    Error(fila, alta.Error);
                    continue;
                }
                listas[codigo] = alta.Value;
                ctx.PedirMotivo();
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create,
                    [new(P.Nombre, null, nombre), new(P.VigenteDesde, null, desde.Value.ToString("yyyy-MM-dd")), new(P.IncluyeImpuestos, null, SiNo(incluye.Value))]);
                continue;
            }

            if (ReglasDeListaDePrecios.AmbitoBloqueado(lista, null, incluye, persona?.Id, segmento, canal?.Id, sucursal?.Id, traeAmbito: true) is { } bloqueado)
            {
                Error(fila, bloqueado);
                continue;
            }
            if (lista.ValidFrom != desde.Value)
            {
                fila.Error(P.VigenteDesde, ErroresDePrecios.ScopeLockedCode,
                    $"La lista {lista.Code} empieza el {lista.ValidFrom:yyyy-MM-dd}: la fecha de inicio no cambia. Un cambio programado es otra lista del mismo ámbito.");
                continue;
            }

            var campos = new List<CampoCambiadoDto>();
            Diferencia(campos, P.Nombre, lista.Name, nombre);
            Diferencia(campos, P.VigenteHasta, lista.ValidTo?.ToString("yyyy-MM-dd"), hasta?.ToString("yyyy-MM-dd"));
            Diferencia(campos, P.Activa, SiNo(lista.IsActive), SiNo(activa));
            if (lista.ValidTo != hasta || lista.IsActive != activa) ctx.PedirMotivo();
            var r = await ReglasDeListaDePrecios.ActualizarAsync(db, cerrojo, lista, nombre, hasta, activa, null, ct);
            if (r.IsFailure)
            {
                Error(fila, r.Error);
                continue;
            }
            ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
        }
    }

    private async Task PreciosAsync(ContextoDeImportacion ctx, Dictionary<string, PriceList> listas, CancellationToken ct)
    {
        var hoja = ctx.Hoja(P.HojaPrecios);
        if (hoja.Filas.Count == 0) return;

        var codigos = hoja.Filas.Select(f => CodigoDeCatalogo.Normalizar(f.Crudo(P.Producto))).OfType<string>().Distinct().ToList();
        var productos = CatalogoCitado<Product>.Desde(
            await db.Products.AsNoTracking().Where(p => !p.IsDeleted && codigos.Contains(p.Code)).ToListAsync(ct), p => p, p => p.Code);
        var ids = await db.Products.AsNoTracking().Where(p => !p.IsDeleted && codigos.Contains(p.Code)).Select(p => p.Id).ToListAsync(ct);
        var deVenta = (await db.ProductUnits.AsNoTracking().Where(u => ids.Contains(u.ProductId) && u.UsedForSale && !u.IsDeleted)
                .Select(u => new { u.ProductId, u.UnitId }).ToListAsync(ct))
            .Select(u => (u.ProductId, u.UnitId)).ToHashSet();
        var todasLasUnidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => !u.IsDeleted).ToListAsync(ct);
        var unidades = CatalogoCitado<UnitOfMeasure>.Desde(todasLasUnidades, u => u, u => u.Code);
        var unidadPorId = todasLasUnidades.ToDictionary(u => u.Id);
        var actualesPorLista = new Dictionary<PriceList, Dictionary<(int ProductId, int UnitId), PriceListItem>>(ReferenceEqualityComparer.Instance);

        foreach (var fila in hoja.Filas)
        {
            var codigoLista = fila.Codigo(P.Lista);
            var producto = fila.Referencia(P.Producto, productos, "un producto", "en la plantilla de productos o en Inventario → Productos");
            var unidad = fila.Referencia(P.Unidad, unidades, "una unidad de medida", "en la plantilla de unidades o en Inventario → Unidades");
            var precio = fila.Monto(P.Precio);
            if (codigoLista is null || producto is null || precio is null || fila.TieneErrores) continue;
            unidad ??= unidadPorId.GetValueOrDefault(producto.BaseUnitId);
            if (unidad is null) continue;
            if (!hoja.LlaveUnica(fila, $"{codigoLista}|{producto.Code}|{unidad.Code}", P.Producto)) continue;
            if (!listas.TryGetValue(codigoLista, out var lista))
            {
                fila.Error(P.Lista, ImportErrors.CellNotFound,
                    $"No hay una lista de precios «{codigoLista}». Créela en la hoja Listas de este archivo o en Ventas → Listas de precios.");
                continue;
            }
            if (producto.Status == ProductStatus.Blocked)
            {
                fila.Error(P.Producto, ImportErrors.CellFormat, $"El producto {producto.Code} está bloqueado: no recibe precios.");
                continue;
            }
            if (precio < 0m)
            {
                fila.Error(P.Precio, ImportErrors.CellFormat, "El precio no puede ser negativo.");
                continue;
            }
            var admitida = ReglasDeListaDePrecios.UnidadAdmitida(producto, unidad, deVenta.Contains((producto.Id, unidad.Id)));
            if (admitida.IsFailure)
            {
                fila.Error(admitida.Error.Code == ErroresDePrecios.UnitNotForSaleCode ? P.Unidad : P.Producto, admitida.Error.Code, admitida.Error.Message);
                continue;
            }
            if (producto.Status == ProductStatus.Inactive)
                fila.Aviso(P.Producto, "Import.Product.Inactive", $"El producto {producto.Code} está inactivo: el precio queda, pero no se vende mientras siga así.");

            if (!actualesPorLista.TryGetValue(lista, out var actuales))
                actualesPorLista[lista] = actuales = await ReglasDeListaDePrecios.PreciosVivosAsync(db, lista, ct);
            var antes = actuales.TryGetValue((producto.Id, unidad.Id), out var previo) ? previo.Price : (decimal?)null;
            var r = ReglasDeListaDePrecios.AplicarPrecio(db, lista, new PrecioPedido(producto, unidad, precio.Value), actuales);
            if (r.IsFailure)
            {
                fila.Error(P.Precio, r.Error.Code, r.Error.Message);
                continue;
            }
            var clave = $"{lista.Code}|{producto.Code}|{unidad.Code}";
            ctx.Registrar(fila, clave, r.Value switch
            {
                CambioDePrecio.Created => AccionDeImportacion.Create,
                CambioDePrecio.Updated => AccionDeImportacion.Update,
                _ => AccionDeImportacion.Unchanged,
            }, r.Value == CambioDePrecio.Unchanged ? [] : [new(P.Precio, antes?.ToString("0.##"), precio.Value.ToString("0.##"))]);
        }
    }

    /// <summary>La columna de un error de regla: el mismo código que el alta una a una, en la columna que lo causó.</summary>
    private static void Error(FilaDeImportacion fila, Error error)
    {
        var columna = error.Code switch
        {
            "Catalogo.CodigoDuplicado" => P.Codigo,
            ErroresDePrecios.SegmentUnknownCode => P.Segmento,
            ErroresDePrecios.OverlapsCode => P.VigenteDesde,
            ErroresDePrecios.ScopeLockedCode => CampoBloqueado(error) switch
            {
                "includesTaxes" => P.IncluyeImpuestos,
                "code" => P.Codigo,
                _ => P.Cliente,
            },
            "Validation.Invalid" => P.VigenteHasta,
            _ => P.Codigo,
        };
        fila.Error(columna, error.Code, error.Message);
    }

    private static string? CampoBloqueado(Error error) =>
        error is ErrorConDatos { Data: { } data } ? data.GetType().GetProperty("field")?.GetValue(data) as string : null;

    private static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        if (!string.Equals(antes ?? string.Empty, despues ?? string.Empty, StringComparison.Ordinal)) campos.Add(new(columna, antes, despues));
    }

    private static string SiNo(bool valor) => valor ? "sí" : "no";
}
