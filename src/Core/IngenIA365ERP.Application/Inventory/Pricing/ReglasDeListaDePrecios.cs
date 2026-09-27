using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Pricing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing;

/// <summary>Lo que se escribe de una lista, con las referencias ya resueltas a Id. (nuevo)</summary>
public sealed record DatosDeLista(
    string Code,
    string Name,
    bool IncludesTaxes,
    int? PersonId,
    string? Segment,
    int? SalesChannelId,
    int? BranchId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive,
    string? Notes = null);

/// <summary>Un precio que se escribe, con el producto y la unidad ya cargados. (nuevo)</summary>
public sealed record PrecioPedido(Product Product, UnitOfMeasure Unit, decimal Price);

/// <summary>Qué hizo la escritura de precios: por cada (producto, unidad), si se creó, cambió o quedó igual. (nuevo)</summary>
public enum CambioDePrecio { Created = 1, Updated = 2, Unchanged = 3 }

/// <summary>
/// La regla única de las listas de precios (feature 012, I3, T597; contracts/api.md §19.1; data-model §14; T51): la usan el
/// alta y la edición una a una y la plantilla 12 (<c>ImportPriceListsCommand</c>), así que responden los mismos códigos.
/// <list type="bullet">
/// <item>código único entre vivas (<c>Catalogo.CodigoDuplicado</c>); el ámbito normalizado en <c>ScopeKey</c> y
/// <c>DimensionCount</c> por <see cref="PriceList.FijarAmbito"/>;</item>
/// <item>el segmento es una clase de asociado existente (<c>COR_Associates.AssociateClass</c>), o
/// <c>Inventory.PriceList.SegmentUnknown</c> con <c>data.allowed</c>;</item>
/// <item>antes de mirar los cruces toma el candado del ámbito (<see cref="ICerrojoPorClave"/>): dos altas del mismo ámbito con
/// fechas distintas se serializan y la segunda ve la primera. Se cruza con otra lista viva y activa del mismo ámbito cuyas
/// fechas se solapan, o con cualquiera viva que empiece el mismo día (el índice único) →
/// <c>Inventory.PriceList.Overlaps</c> nombrándola;</item>
/// <item>un precio va en la unidad base o en una alterna de venta del producto, nunca en una plantilla, y no es negativo.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class ReglasDeListaDePrecios
{
    public const int LargoDeNombre = 80;
    public const int LargoDeSegmento = 4;
    public const int LargoDeNotas = 300;

    /// <summary>Las clases de asociado que existen hoy, normalizadas (los segmentos admitidos).</summary>
    public static async Task<IReadOnlyList<string>> SegmentosAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var clases = await db.Associates.AsNoTracking()
            .Where(a => !a.IsDeleted && a.AssociateClass != null && a.AssociateClass != "")
            .Select(a => a.AssociateClass!)
            .Distinct()
            .ToListAsync(ct);
        return clases.Select(AmbitoDeLista.NormalizarSegmento).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>El segmento de una persona: la clase de su fila de asociado viva, si la tiene.</summary>
    public static async Task<string?> SegmentoDeAsync(IApplicationDbContext db, int? personId, CancellationToken ct)
    {
        if (personId is not { } id) return null;
        var clase = await db.Associates.AsNoTracking()
            .Where(a => a.PersonId == id && !a.IsDeleted)
            .OrderByDescending(a => a.Id)
            .Select(a => a.AssociateClass)
            .FirstOrDefaultAsync(ct);
        return AmbitoDeLista.NormalizarSegmento(clase);
    }

    /// <summary>Crea la lista (sin guardar) si cumple todas las reglas.</summary>
    public static async Task<Result<PriceList>> AltaAsync(IApplicationDbContext db, ICerrojoPorClave cerrojo, DatosDeLista datos, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(datos.Code)!;
        var existente = db.PriceLists.Local.Where(l => !l.IsDeleted && l.Code == codigo).Select(l => new { l.PublicId, l.Name }).FirstOrDefault()
            ?? await db.PriceLists.AsNoTracking().Where(l => !l.IsDeleted && l.Code == codigo)
                .Select(l => new { l.PublicId, l.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return Result.Failure<PriceList>(CodigoDeCatalogo.Duplicado("una lista de precios", codigo, existente.Name, existente.PublicId));

        var segmento = await SegmentoValidoAsync(db, datos.Segment, ct);
        if (segmento.IsFailure) return Result.Failure<PriceList>(segmento.Error);

        var lista = new PriceList
        {
            Code = codigo,
            Name = datos.Name.Trim(),
            IncludesTaxes = datos.IncludesTaxes,
            ValidFrom = datos.ValidFrom,
            ValidTo = datos.ValidTo,
            IsActive = datos.IsActive,
            Notes = string.IsNullOrWhiteSpace(datos.Notes) ? null : datos.Notes.Trim(),
        };
        lista.FijarAmbito(datos.PersonId, segmento.Value, datos.SalesChannelId, datos.BranchId);

        var cruce = await CruceAsync(db, cerrojo, lista, ct);
        if (cruce.IsFailure) return Result.Failure<PriceList>(cruce.Error);

        db.PriceLists.Add(lista);
        return Result.Success(lista);
    }

    /// <summary>Cambia nombre, cierre de vigencia, activo y notas; el ámbito, el código e <c>IncludesTaxes</c> no cambian.</summary>
    public static async Task<Result> ActualizarAsync(IApplicationDbContext db, ICerrojoPorClave cerrojo, PriceList lista, string name, DateOnly? validTo,
        bool isActive, string? notes, CancellationToken ct)
    {
        var antes = (lista.ValidTo, lista.IsActive);
        lista.Name = name.Trim();
        lista.ValidTo = validTo;
        lista.IsActive = isActive;
        if (notes is not null) lista.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        // Cerrar o desactivar nunca crea un cruce; abrir la vigencia o reactivar, sí.
        if (antes == (lista.ValidTo, lista.IsActive)) return Result.Success();
        return await CruceAsync(db, cerrojo, lista, ct);
    }

    /// <summary>
    /// Si lo que pide la edición cambia el ámbito, el código o <c>IncludesTaxes</c> (<c>Inventory.PriceList.ScopeLocked</c>). Lo
    /// nulo no se compara (no se pidió).
    /// </summary>
    public static Error? AmbitoBloqueado(PriceList lista, string? code, bool? includesTaxes, int? personId, string? segment, int? salesChannelId,
        int? branchId, bool traeAmbito)
    {
        if (code is not null && !string.Equals(CodigoDeCatalogo.Normalizar(code), lista.Code, StringComparison.Ordinal))
            return ErroresDePrecios.ScopeLocked("code");
        if (includesTaxes is { } incluye && incluye != lista.IncludesTaxes)
            return ErroresDePrecios.ScopeLocked("includesTaxes");
        if (traeAmbito && AmbitoDeLista.Clave(personId, segment, salesChannelId, branchId) != lista.ScopeKey)
            return ErroresDePrecios.ScopeLocked("scope");
        return null;
    }

    /// <summary>
    /// Agrega o reemplaza precios por (producto, unidad) sobre la lista (sin guardar). Devuelve lo que hizo con cada pedido, en el
    /// mismo orden. <paramref name="actuales"/> son los precios vivos que ya tiene la lista (se cargan una vez).
    /// </summary>
    public static async Task<Result<CambioDePrecio>> AplicarPrecioAsync(IApplicationDbContext db, PriceList lista, PrecioPedido pedido,
        IDictionary<(int ProductId, int UnitId), PriceListItem> actuales, CancellationToken ct)
    {
        var admitida = await UnidadAdmitidaAsync(db, pedido.Product, pedido.Unit, ct);
        return admitida.IsFailure ? Result.Failure<CambioDePrecio>(admitida.Error) : AplicarPrecio(db, lista, pedido, actuales);
    }

    /// <summary>
    /// Lo mismo sin mirar la unidad: para quien ya la comprobó con <see cref="UnidadAdmitida"/> sobre las unidades de venta
    /// cargadas en bloque (la plantilla 12, que no consulta por fila).
    /// </summary>
    public static Result<CambioDePrecio> AplicarPrecio(IApplicationDbContext db, PriceList lista, PrecioPedido pedido,
        IDictionary<(int ProductId, int UnitId), PriceListItem> actuales)
    {
        if (pedido.Price < 0m)
            return Result.Failure<CambioDePrecio>(new Error(Error.Validation.Code, $"El precio de {pedido.Product.Code} no puede ser negativo."));

        var precio = Math.Round(pedido.Price, 2, MidpointRounding.AwayFromZero);
        if (actuales.TryGetValue((pedido.Product.Id, pedido.Unit.Id), out var item))
        {
            if (item.Price == precio) return Result.Success(CambioDePrecio.Unchanged);
            item.Price = precio;
            return Result.Success(CambioDePrecio.Updated);
        }

        var nuevo = new PriceListItem { PriceList = lista, PriceListId = lista.Id, ProductId = pedido.Product.Id, UnitId = pedido.Unit.Id, Price = precio };
        lista.Items.Add(nuevo);
        db.PriceListItems.Add(nuevo);
        actuales[(pedido.Product.Id, pedido.Unit.Id)] = nuevo;
        return Result.Success(CambioDePrecio.Created);
    }

    /// <summary>Los precios vivos de la lista por (producto, unidad), seguidos por el contexto.</summary>
    public static async Task<Dictionary<(int ProductId, int UnitId), PriceListItem>> PreciosVivosAsync(IApplicationDbContext db, PriceList lista, CancellationToken ct)
    {
        if (lista.Id == 0)
            return lista.Items.Where(i => !i.IsDeleted).ToDictionary(i => (i.ProductId, i.UnitId));
        return await db.PriceListItems.Where(i => i.PriceListId == lista.Id && !i.IsDeleted).ToDictionaryAsync(i => (i.ProductId, i.UnitId), ct);
    }

    /// <summary>La unidad base del producto o una alterna de venta; una plantilla no tiene precio.</summary>
    public static async Task<Result> UnidadAdmitidaAsync(IApplicationDbContext db, Product producto, UnitOfMeasure unidad, CancellationToken ct)
    {
        var deVenta = unidad.Id != producto.BaseUnitId && await db.ProductUnits.AsNoTracking()
            .AnyAsync(u => u.ProductId == producto.Id && u.UnitId == unidad.Id && u.UsedForSale && !u.IsDeleted, ct);
        return UnidadAdmitida(producto, unidad, deVenta);
    }

    /// <summary>La regla pura: <paramref name="esAlternaDeVenta"/> dice si (producto, unidad) está en <c>INV_ProductUnits</c> para venta.</summary>
    public static Result UnidadAdmitida(Product producto, UnitOfMeasure unidad, bool esAlternaDeVenta)
    {
        if (producto.Kind == ProductKind.Template) return Result.Failure(ErroresDePrecios.ProductNotPriceable(producto.Code));
        return unidad.Id == producto.BaseUnitId || esAlternaDeVenta
            ? Result.Success()
            : Result.Failure(ErroresDePrecios.UnitNotForSale(producto.Code, unidad.Code));
    }

    private static async Task<Result<string?>> SegmentoValidoAsync(IApplicationDbContext db, string? segment, CancellationToken ct)
    {
        var segmento = AmbitoDeLista.NormalizarSegmento(segment);
        if (segmento is null) return Result.Success<string?>(null);
        var admitidos = await SegmentosAsync(db, ct);
        return admitidos.Contains(segmento, StringComparer.Ordinal)
            ? Result.Success<string?>(segmento)
            : Result.Failure<string?>(ErroresDePrecios.SegmentUnknown(segmento, admitidos));
    }

    /// <summary>Toma el candado del ámbito y busca otra lista del mismo ámbito que se cruce con <paramref name="lista"/>.</summary>
    private static async Task<Result> CruceAsync(IApplicationDbContext db, ICerrojoPorClave cerrojo, PriceList lista, CancellationToken ct)
    {
        if (lista.ValidTo is { } hasta && hasta < lista.ValidFrom)
            return Result.Failure(new Error(Error.Validation.Code, "La vigencia termina antes de empezar."));

        await cerrojo.BloquearAsync(ClavesDeCerrojo.AmbitoDeLista(lista.ScopeKey), ct);

        // Las guardadas y las que este mismo contexto agregó sin guardar (la plantilla crea varias antes del único SaveChanges).
        var locales = db.PriceLists.Local.Where(l => !l.IsDeleted && l.ScopeKey == lista.ScopeKey && !ReferenceEquals(l, lista)).ToList();
        var guardadas = await db.PriceLists.AsNoTracking()
            .Where(l => !l.IsDeleted && l.ScopeKey == lista.ScopeKey && l.PublicId != lista.PublicId)
            .ToListAsync(ct);
        var otras = locales.Concat(guardadas.Where(g => locales.All(l => l.PublicId != g.PublicId))).ToList();

        var cruzada = otras
            .Where(o => o.ValidFrom == lista.ValidFrom || (o.IsActive && lista.IsActive && SeCruzan(o.ValidFrom, o.ValidTo, lista.ValidFrom, lista.ValidTo)))
            .OrderBy(o => o.ValidFrom)
            .FirstOrDefault();
        return cruzada is null
            ? Result.Success()
            : Result.Failure(ErroresDePrecios.Overlaps(cruzada.PublicId, cruzada.Code, cruzada.ValidFrom, cruzada.ValidTo));
    }

    /// <summary>Dos vigencias cerradas en los dos extremos (nulo = sin fin) se cruzan.</summary>
    public static bool SeCruzan(DateOnly desdeA, DateOnly? hastaA, DateOnly desdeB, DateOnly? hastaB) =>
        desdeA <= (hastaB ?? DateOnly.MaxValue) && desdeB <= (hastaA ?? DateOnly.MaxValue);
}
