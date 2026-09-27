using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Sales.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing;

/// <summary>
/// El precio de un producto en una unidad para un cliente, canal, sucursal y fecha (feature 012, I3, T598; contracts/api.md
/// §19.2, <c>GET /api/inventory/prices/resolve</c>). Sin unidad, la base; sin fecha, hoy local. Sin precio en ninguna lista
/// aplicable → 422 <c>Inventory.Price.NotFound</c>. La consulta de precio y la pantalla de listas la usan; el POS, la factura, el
/// retiro gravado y la vista de deterioro van directo a <see cref="ResolucionDePrecios"/>. (nuevo)
/// </summary>
public sealed record ResolvePriceQuery(
    Guid ProductPublicId,
    Guid? UnitPublicId = null,
    Guid? PersonPublicId = null,
    Guid? SalesChannelPublicId = null,
    Guid? BranchPublicId = null,
    DateOnly? Date = null) : IRequest<Result<ResolvedPriceDto>>;

public sealed class ResolvePriceQueryHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<ResolvePriceQuery, Result<ResolvedPriceDto>>
{
    public async Task<Result<ResolvedPriceDto>> Handle(ResolvePriceQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Where(p => p.PublicId == request.ProductPublicId && !p.IsDeleted)
            .Select(p => new { p.Id, p.Code, p.BaseUnitId }).FirstOrDefaultAsync(ct);
        if (producto is null) return Result.Failure<ResolvedPriceDto>(Error.NotFound);

        var unidad = request.UnitPublicId is { } u
            ? await db.UnitsOfMeasure.AsNoTracking().Where(x => x.PublicId == u && !x.IsDeleted).Select(x => new { x.Id, x.Code }).FirstOrDefaultAsync(ct)
            : await db.UnitsOfMeasure.AsNoTracking().Where(x => x.Id == producto.BaseUnitId).Select(x => new { x.Id, x.Code }).FirstOrDefaultAsync(ct);
        if (unidad is null) return Result.Failure<ResolvedPriceDto>(Error.NotFound);

        var ambito = await ReferenciasDeLista.ResolverAsync(db,
            new PriceListScopeInput(request.PersonPublicId, null, request.SalesChannelPublicId, request.BranchPublicId), ct);
        if (ambito.IsFailure) return Result.Failure<ResolvedPriceDto>(ambito.Error);
        var (persona, canal, sucursal) = ambito.Value;

        var contexto = new ContextoDePrecio(persona, await ReglasDeListaDePrecios.SegmentoDeAsync(db, persona, ct), canal, sucursal,
            request.Date ?? reloj.HoyLocal);
        var resuelto = await ResolucionDePrecios.ResolverAsync(db, contexto, producto.Id, unidad.Id, ct);
        if (!resuelto.Precio.Found) return Result.Failure<ResolvedPriceDto>(ErroresDePrecios.PriceNotFound(producto.Code, unidad.Code));
        return Result.Success(resuelto.ComoDto());
    }
}

/// <summary>Lo que resolvió el motor, con el nombre y el <c>PublicId</c> de cada lista para la respuesta. (nuevo)</summary>
public sealed record PrecioResueltoConListas(PrecioResuelto Precio, IReadOnlyDictionary<int, (Guid PublicId, string Name)> Listas)
{
    public Guid? PriceListPublicId => Precio.PriceListId is int id ? Listas[id].PublicId : null;

    public ResolvedPriceDto ComoDto() => new(
        Precio.Price ?? 0m,
        Precio.IncludesTaxes,
        new ResolvedPriceListDto(Listas[Precio.PriceListId!.Value].PublicId, Precio.PriceListCode!, Listas[Precio.PriceListId.Value].Name,
            Precio.MatchedDimensions.Select(d => d.ToString()).ToList()),
        Precio.FallbackUsed,
        Precio.Candidates.Select(c => new PriceCandidateDto(Listas[c.PriceListId].PublicId, c.Code, c.MatchedDimensions.Select(d => d.ToString()).ToList(), c.HasProduct))
            .ToList());
}

/// <summary>
/// La carga de las listas para <see cref="ResolutorDeListaDePrecios"/> (feature 012, I3, T598): sólo las vivas, activas, vigentes
/// a la fecha del contexto y cuyas dimensiones no nulas coinciden con él —las demás no pueden ganar—, con sus precios de los
/// productos pedidos y nada más. Una consulta para las listas y otra para los precios, sea uno o muchos productos. (nuevo)
/// </summary>
public static class ResolucionDePrecios
{
    /// <summary>Las candidatas compatibles con el contexto, con los precios de <paramref name="productIds"/>.</summary>
    public static async Task<(IReadOnlyList<ListaDePreciosCandidata> Candidatas, IReadOnlyDictionary<int, (Guid PublicId, string Name)> Listas)> CandidatasAsync(
        IApplicationDbContext db, ContextoDePrecio contexto, IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        var fecha = contexto.Date;
        var segmento = AmbitoDeLista.NormalizarSegmento(contexto.Segment);
        var listas = await db.PriceLists.AsNoTracking()
            .Where(l => !l.IsDeleted && l.IsActive && l.ValidFrom <= fecha && (l.ValidTo == null || l.ValidTo >= fecha)
                && (l.PersonId == null || l.PersonId == contexto.PersonId)
                && (l.Segment == null || l.Segment == segmento)
                && (l.SalesChannelId == null || l.SalesChannelId == contexto.SalesChannelId)
                && (l.BranchId == null || l.BranchId == contexto.BranchId))
            .ToListAsync(ct);
        if (listas.Count == 0) return ([], new Dictionary<int, (Guid, string)>());

        var ids = listas.Select(l => l.Id).ToList();
        var precios = await db.PriceListItems.AsNoTracking()
            .Where(i => ids.Contains(i.PriceListId) && productIds.Contains(i.ProductId) && !i.IsDeleted)
            .Select(i => new { i.PriceListId, i.ProductId, i.UnitId, i.Price })
            .ToListAsync(ct);
        var porLista = precios.ToLookup(p => p.PriceListId);

        var candidatas = listas.Select(l => new ListaDePreciosCandidata(l.Id, l.Code, l.PersonId, l.Segment, l.SalesChannelId, l.BranchId, l.ValidFrom,
                l.ValidTo, l.IsActive, l.IncludesTaxes, porLista[l.Id].Select(p => new PrecioDeLista(p.ProductId, p.UnitId, p.Price)).ToList()))
            .ToList();
        return (candidatas, listas.ToDictionary(l => l.Id, l => (l.PublicId, l.Name)));
    }

    /// <summary>El precio de un producto en una unidad.</summary>
    public static async Task<PrecioResueltoConListas> ResolverAsync(IApplicationDbContext db, ContextoDePrecio contexto, int productId, int unitId, CancellationToken ct)
    {
        var (candidatas, listas) = await CandidatasAsync(db, contexto, [productId], ct);
        return new PrecioResueltoConListas(ResolutorDeListaDePrecios.Resolver(candidatas, contexto, productId, unitId), listas);
    }

    /// <summary>El precio en la lista <b>general</b> vigente (el retiro gravado de consumo interno y la vista de deterioro).</summary>
    public static Task<PrecioResueltoConListas> GeneralAsync(IApplicationDbContext db, DateOnly fecha, int productId, int unitId, CancellationToken ct) =>
        ResolverAsync(db, new ContextoDePrecio(null, null, null, null, fecha), productId, unitId, ct);
}
