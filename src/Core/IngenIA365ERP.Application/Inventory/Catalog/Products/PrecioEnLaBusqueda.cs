using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Sales.Pricing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Feature 022 (T020; FR-030, plan §8.2): el precio que aplica en la búsqueda de productos, con el mismo contexto que deriva la
/// venta. Tres formas, una a la vez:
/// <list type="bullet">
/// <item><c>priceDraft</c>: un borrador de venta (POS u oficina): su cliente, su canal, su sucursal y su fecha de operación, como
/// <c>PrecificacionDeVenta</c> los recibe del documento;</item>
/// <item><c>priceCashSession</c>: una sesión de caja del POS sin borrador: el consumidor final, el canal y la sucursal del punto y la
/// fecha operativa de la sesión (como la lectura del POS);</item>
/// <item>explícito: <c>priceCustomer</c> (persona), <c>priceDocumentType</c> (su canal), <c>priceWarehouse</c> (su sucursal) y
/// <c>priceDate</c> (hoy si falta); cualquiera puede faltar.</item>
/// </list>
/// La lista se resuelve con <see cref="ResolucionDePrecios.CandidatasAsync"/> (una consulta para las listas y otra para sus precios,
/// sean uno o veinte productos) y <see cref="ResolutorDeListaDePrecios.Resolver"/> en la unidad base; las demás unidades toman el
/// precio de esa misma lista sólo si lo trae para (producto, unidad) exacto. Nunca pasa por <c>PrecificacionDeVenta</c>: no hay
/// descuentos, promociones ni impuestos en una búsqueda. Lo fuera del alcance responde como si no existiera. (nuevo)
/// </summary>
public static class PrecioEnLaBusqueda
{
    public const string VerPrecios = "Inventory.Prices.View";

    /// <summary>¿Pidió la búsqueda algún contexto de precio?</summary>
    public static bool Pedido(SearchProductsQuery q) =>
        q.PriceDraft is not null || q.PriceCashSession is not null || q.PriceCustomer is not null
        || q.PriceDocumentType is not null || q.PriceWarehouse is not null || q.PriceDate is not null;

    /// <summary>El contexto de precio que pide la búsqueda, o el error de la referencia que no existe (o está fuera del alcance).</summary>
    public static async Task<Result<ContextoDePrecio>> ContextoAsync(
        IApplicationDbContext db, AlcanceDeInventario alcance, DateOnly hoy, SearchProductsQuery q, CancellationToken ct)
    {
        int? persona;
        int? canal;
        int? sucursal;
        DateOnly fecha;

        if (q.PriceDraft is { } borrador)
        {
            var d = await db.InventoryDocuments.AsNoTracking().Where(x => x.PublicId == borrador)
                .Select(x => new { x.Class, x.CounterpartyPersonId, x.SalesChannelId, x.BranchId, x.WarehouseId, x.PointOfSaleId, x.OperationDate })
                .FirstOrDefaultAsync(ct);
            var visible = d is not null && ClasesDeDocumento.De(d.Class).Group == DocumentClassGroup.Sales
                && (d.PointOfSaleId is int p ? alcance.IncluyePunto(p) : d.WarehouseId is not int b || alcance.IncluyeBodega(b));
            if (!visible) return Result.Failure<ContextoDePrecio>(ErroresDelContexto.DraftNotFound());
            (persona, canal, sucursal, fecha) = (d!.CounterpartyPersonId, d.SalesChannelId, d.BranchId, d.OperationDate);
        }
        else if (q.PriceCashSession is { } sesionId)
        {
            var s = await db.CashSessions.AsNoTracking().Where(x => x.PublicId == sesionId)
                .Select(x => new { x.PointOfSaleId, x.OperatingDate }).FirstOrDefaultAsync(ct);
            if (s is null || !alcance.IncluyePunto(s.PointOfSaleId)) return Result.Failure<ContextoDePrecio>(ErroresDelContexto.CashSessionNotFound());
            var punto = await db.PointsOfSale.AsNoTracking().Where(x => x.Id == s.PointOfSaleId)
                .Select(x => new { x.SalesChannelId, x.BranchId }).FirstAsync(ct);
            var consumidor = BorradorDeVenta.PersonaDelConsumidorFinal(db.People.AsNoTracking(), s.OperatingDate);
            persona = consumidor is null ? null : await consumidor.Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            (canal, sucursal, fecha) = (punto.SalesChannelId, punto.BranchId, s.OperatingDate);
        }
        else
        {
            persona = null;
            if (q.PriceCustomer is { } cliente)
            {
                persona = await db.People.AsNoTracking().Where(x => x.PublicId == cliente && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
                if (persona is null) return Result.Failure<ContextoDePrecio>(ErroresDelContexto.CustomerNotFound());
            }
            canal = null;
            if (q.PriceDocumentType is { } tipo)
            {
                var t = await db.InventoryDocumentTypes.AsNoTracking().Where(x => x.PublicId == tipo).Select(x => new { x.SalesChannelId }).FirstOrDefaultAsync(ct);
                if (t is null) return Result.Failure<ContextoDePrecio>(ErroresDelContexto.DocumentTypeNotFound());
                canal = t.SalesChannelId;
            }
            sucursal = null;
            if (q.PriceWarehouse is { } bodega)
            {
                var w = await db.Warehouses.AsNoTracking().Where(x => x.PublicId == bodega).Select(x => new { x.Id, x.BranchId }).FirstOrDefaultAsync(ct);
                if (w is null || !alcance.IncluyeBodega(w.Id)) return Result.Failure<ContextoDePrecio>(ErroresDeAlcance.BodegaInexistente());
                sucursal = w.BranchId;
            }
            fecha = q.PriceDate ?? hoy;
        }

        return Result.Success(new ContextoDePrecio(persona, await ReglasDeListaDePrecios.SegmentoDeAsync(db, persona, ct), canal, sucursal, fecha));
    }

    /// <summary>Lo resuelto para un producto: precio de la base, la lista ganadora y el precio por unidad (Id de unidad → precio).</summary>
    public sealed record PrecioDeProducto(decimal? Price, bool? IncludesTaxes, string? ListCode, IReadOnlyDictionary<int, decimal> PorUnidad);

    /// <summary>Los precios de <paramref name="productos"/> (Id → Id de su unidad base) en el contexto, con dos consultas.</summary>
    public static async Task<IReadOnlyDictionary<int, PrecioDeProducto>> PreciosAsync(
        IApplicationDbContext db, ContextoDePrecio contexto, IReadOnlyDictionary<int, int> productos, CancellationToken ct)
    {
        var (candidatas, _) = await ResolucionDePrecios.CandidatasAsync(db, contexto, productos.Keys.ToList(), ct);
        var resultado = new Dictionary<int, PrecioDeProducto>();
        foreach (var (producto, unidadBase) in productos)
        {
            var r = ResolutorDeListaDePrecios.Resolver(candidatas, contexto, producto, unidadBase);
            if (!r.Found)
            {
                resultado[producto] = new PrecioDeProducto(null, null, null, new Dictionary<int, decimal>());
                continue;
            }
            var ganadora = candidatas.First(c => c.PriceListId == r.PriceListId);
            var porUnidad = ganadora.Items.Where(i => i.ProductId == producto)
                .GroupBy(i => i.UnitId).ToDictionary(g => g.Key, g => g.First().Price);
            resultado[producto] = new PrecioDeProducto(r.Price, r.IncludesTaxes, r.PriceListCode, porUnidad);
        }
        return resultado;
    }

    /// <summary>La unidad base y los empaques de venta vivos de cada producto, en orden de factor.</summary>
    public static async Task<ILookup<int, (Guid UnitPublicId, int UnitId, Guid? ProductUnitPublicId, string Code, decimal Factor, bool IsBase)>> UnidadesAsync(
        IApplicationDbContext db, IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        var bases = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, UnitPublicId = p.BaseUnit!.PublicId, UnitId = p.BaseUnitId, p.BaseUnit.Code }).ToListAsync(ct);
        var empaques = await db.ProductUnits.AsNoTracking().Where(u => productIds.Contains(u.ProductId) && !u.IsDeleted && u.UsedForSale)
            .Select(u => new { u.ProductId, UnitPublicId = u.Unit!.PublicId, u.UnitId, u.PublicId, u.Unit.Code, u.Factor }).ToListAsync(ct);
        return bases.Select(b => (b.Id, Fila: (b.UnitPublicId, b.UnitId, (Guid?)null, b.Code, 1m, true)))
            .Concat(empaques.OrderBy(e => e.Factor).ThenBy(e => e.Code)
                .Select(e => (Id: e.ProductId, Fila: (e.UnitPublicId, e.UnitId, (Guid?)e.PublicId, e.Code, e.Factor, false))))
            .ToLookup(x => x.Id, x => x.Fila);
    }
}

/// <summary>Feature 022 (T020): las referencias del contexto de precio que no existen (o están fuera del alcance). (nuevo)</summary>
public static class ErroresDelContexto
{
    public static Error DraftNotFound() => new("Inventory.PriceContext.Draft.NotFound", "El borrador de venta del precio no existe.");
    public static Error CashSessionNotFound() => new("Inventory.PriceContext.CashSession.NotFound", "La sesión de caja del precio no existe.");
    public static Error CustomerNotFound() => new("Inventory.PriceContext.Customer.NotFound", "El cliente del precio no existe.");
    public static Error DocumentTypeNotFound() => new("Inventory.PriceContext.DocumentType.NotFound", "El tipo de documento del precio no existe.");
}
