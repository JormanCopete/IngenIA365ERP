using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Sales.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing;

// Listas de precios (feature 012, I3, T597; contracts/api.md §19.1; FR-053, T51). La regla es ReglasDeListaDePrecios, que
// comparte la plantilla 12. Toda escritura lleva motivo (IConMotivo) y clave de operación. (nuevo)

/// <summary>Alta de una lista (§19.1, <c>POST /api/inventory/price-lists</c> → 201 <c>{ priceListPublicId }</c>). (nuevo)</summary>
public sealed record CreatePriceListCommand(
    string Code,
    string Name,
    bool IncludesTaxes,
    PriceListScopeInput? Scope,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? Notes = null)
    : IRequest<Result<Guid>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePriceListCommandValidator : ValidadorConMotivo<CreatePriceListCommand>
{
    public CreatePriceListCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDeListaDePrecios.LargoDeNombre);
        RuleFor(x => x.Scope!.Segment).MaximumLength(ReglasDeListaDePrecios.LargoDeSegmento).When(x => x.Scope is not null);
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia termina antes de empezar.");
        RuleFor(x => x.Notes).MaximumLength(ReglasDeListaDePrecios.LargoDeNotas);
    }
}

public sealed class CreatePriceListCommandHandler(IApplicationDbContext db, ICerrojoPorClave cerrojo) : IRequestHandler<CreatePriceListCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePriceListCommand request, CancellationToken ct)
    {
        var ambito = await ReferenciasDeLista.ResolverAsync(db, request.Scope, ct);
        if (ambito.IsFailure) return Result.Failure<Guid>(ambito.Error);
        var (persona, canal, sucursal) = ambito.Value;
        var lista = await ReglasDeListaDePrecios.AltaAsync(db, cerrojo, new DatosDeLista(request.Code, request.Name, request.IncludesTaxes,
            persona, request.Scope?.Segment, canal, sucursal, request.ValidFrom, request.ValidTo, true, request.Notes), ct);
        if (lista.IsFailure) return Result.Failure<Guid>(lista.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(lista.Value.PublicId);
    }
}

/// <summary>
/// Edición de una lista (§19.1, <c>PUT /{id}</c>): nombre, cierre de vigencia y activo. Si el cuerpo trae código, ámbito o
/// <c>includesTaxes</c> distintos → <c>Inventory.PriceList.ScopeLocked</c>: un cambio programado es otra lista. (nuevo)
/// </summary>
public sealed record UpdatePriceListCommand(
    Guid PriceListPublicId,
    string Name,
    DateOnly? ValidTo,
    bool IsActive,
    string Reason,
    string? Notes = null,
    string? Code = null,
    bool? IncludesTaxes = null,
    PriceListScopeInput? Scope = null)
    : IRequest<Result<PriceListDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdatePriceListCommandValidator : ValidadorConMotivo<UpdatePriceListCommand>
{
    public UpdatePriceListCommandValidator()
    {
        RuleFor(x => x.PriceListPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ReglasDeListaDePrecios.LargoDeNombre);
        RuleFor(x => x.Notes).MaximumLength(ReglasDeListaDePrecios.LargoDeNotas);
    }
}

public sealed class UpdatePriceListCommandHandler(IApplicationDbContext db, ICerrojoPorClave cerrojo) : IRequestHandler<UpdatePriceListCommand, Result<PriceListDto>>
{
    public async Task<Result<PriceListDto>> Handle(UpdatePriceListCommand request, CancellationToken ct)
    {
        var lista = await db.PriceLists.FirstOrDefaultAsync(l => l.PublicId == request.PriceListPublicId && !l.IsDeleted, ct);
        if (lista is null) return Result.Failure<PriceListDto>(ErroresDePrecios.PriceListNotFound());

        int? persona = null, canal = null, sucursal = null;
        if (request.Scope is not null)
        {
            var ambito = await ReferenciasDeLista.ResolverAsync(db, request.Scope, ct);
            if (ambito.IsFailure) return Result.Failure<PriceListDto>(ambito.Error);
            (persona, canal, sucursal) = ambito.Value;
        }
        if (ReglasDeListaDePrecios.AmbitoBloqueado(lista, request.Code, request.IncludesTaxes, persona, request.Scope?.Segment, canal, sucursal,
                request.Scope is not null) is { } bloqueado)
            return Result.Failure<PriceListDto>(bloqueado);
        if (request.ValidTo is { } hasta && hasta < lista.ValidFrom)
            return Result.Failure<PriceListDto>(new Error(Error.Validation.Code, "La vigencia termina antes de empezar."));

        var r = await ReglasDeListaDePrecios.ActualizarAsync(db, cerrojo, lista, request.Name, request.ValidTo, request.IsActive, request.Notes, ct);
        if (r.IsFailure) return Result.Failure<PriceListDto>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDeListasDePrecios.ListasAsync(db, db.PriceLists.AsNoTracking().Where(l => l.Id == lista.Id), ct))[0]);
    }
}

/// <summary>
/// Precios de una lista (§19.1, <c>PUT /{id}/items</c>): agrega o reemplaza por (producto, unidad) y quita los productos de
/// <see cref="RemoveProductPublicIds"/> (todas sus unidades). La unidad es la base o una alterna de venta; una plantilla no
/// tiene precio. La línea de cada documento ya guardó su precio: cambiar uno aquí no toca lo vendido. (nuevo)
/// </summary>
public sealed record SetPriceListItemsCommand(
    Guid PriceListPublicId,
    IReadOnlyList<PriceListItemInput> Items,
    string Reason,
    IReadOnlyList<Guid>? RemoveProductPublicIds = null)
    : IRequest<Result<PriceListItemsResultDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetPriceListItemsCommandValidator : ValidadorConMotivo<SetPriceListItemsCommand>
{
    public SetPriceListItemsCommandValidator()
    {
        RuleFor(x => x.PriceListPublicId).NotEmpty();
        RuleFor(x => x.Items).NotNull();
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(y => y.ProductPublicId).NotEmpty();
            i.RuleFor(y => y.UnitPublicId).NotEmpty();
            i.RuleFor(y => y.Price).GreaterThanOrEqualTo(0m).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        });
    }
}

public sealed class SetPriceListItemsCommandHandler(IApplicationDbContext db) : IRequestHandler<SetPriceListItemsCommand, Result<PriceListItemsResultDto>>
{
    public async Task<Result<PriceListItemsResultDto>> Handle(SetPriceListItemsCommand request, CancellationToken ct)
    {
        var lista = await db.PriceLists.FirstOrDefaultAsync(l => l.PublicId == request.PriceListPublicId && !l.IsDeleted, ct);
        if (lista is null) return Result.Failure<PriceListItemsResultDto>(ErroresDePrecios.PriceListNotFound());

        var productoIds = request.Items.Select(i => i.ProductPublicId).Concat(request.RemoveProductPublicIds ?? []).Distinct().ToList();
        var productos = await db.Products.Where(p => productoIds.Contains(p.PublicId) && !p.IsDeleted).ToDictionaryAsync(p => p.PublicId, ct);
        var unidadIds = request.Items.Select(i => i.UnitPublicId).Distinct().ToList();
        var unidades = await db.UnitsOfMeasure.Where(u => unidadIds.Contains(u.PublicId) && !u.IsDeleted).ToDictionaryAsync(u => u.PublicId, ct);

        var repetido = request.Items.GroupBy(i => (i.ProductPublicId, i.UnitPublicId)).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            return Result.Failure<PriceListItemsResultDto>(new Error(Error.Validation.Code, "Un producto viene dos veces con la misma unidad."));

        var actuales = await ReglasDeListaDePrecios.PreciosVivosAsync(db, lista, ct);
        int creados = 0, cambiados = 0, iguales = 0, quitados = 0;
        foreach (var item in request.Items)
        {
            if (!productos.TryGetValue(item.ProductPublicId, out var producto) || !unidades.TryGetValue(item.UnitPublicId, out var unidad))
                return Result.Failure<PriceListItemsResultDto>(Error.NotFound);
            var r = await ReglasDeListaDePrecios.AplicarPrecioAsync(db, lista, new PrecioPedido(producto, unidad, item.Price), actuales, ct);
            if (r.IsFailure) return Result.Failure<PriceListItemsResultDto>(r.Error);
            switch (r.Value)
            {
                case CambioDePrecio.Created: creados++; break;
                case CambioDePrecio.Updated: cambiados++; break;
                default: iguales++; break;
            }
        }

        foreach (var publicId in request.RemoveProductPublicIds ?? [])
        {
            if (!productos.TryGetValue(publicId, out var producto)) return Result.Failure<PriceListItemsResultDto>(Error.NotFound);
            foreach (var item in actuales.Values.Where(i => i.ProductId == producto.Id && !i.IsDeleted))
            {
                item.IsDeleted = true;
                quitados++;
            }
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(new PriceListItemsResultDto(creados, cambiados, iguales, quitados));
    }
}

// ================================================================================================ consultas --

/// <summary>
/// Las listas (§19.1, <c>GET /price-lists?asOf=&amp;scope=&amp;active=&amp;search=</c>), paginadas. <see cref="Scope"/> filtra por
/// la dimensión que la lista exige: <c>General</c> (ninguna), <c>Person</c>, <c>Segment</c>, <c>Channel</c> o <c>Branch</c>.
/// (nuevo)
/// </summary>
public sealed record ListPriceListsQuery(DateOnly? AsOf = null, string? Scope = null, bool? Active = null, string? Search = null, PageRequest? Pagina = null)
    : IRequest<Result<PagedResult<PriceListDto>>>;

public sealed class ListPriceListsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListPriceListsQuery, Result<PagedResult<PriceListDto>>>
{
    public async Task<Result<PagedResult<PriceListDto>>> Handle(ListPriceListsQuery request, CancellationToken ct)
    {
        var consulta = db.PriceLists.AsNoTracking().Where(l => !l.IsDeleted);
        if (request.AsOf is { } fecha) consulta = consulta.Where(l => l.ValidFrom <= fecha && (l.ValidTo == null || l.ValidTo >= fecha));
        if (request.Active is { } activa) consulta = consulta.Where(l => l.IsActive == activa);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var texto = request.Search.Trim().ToUpperInvariant();
            consulta = consulta.Where(l => l.Code.Contains(texto) || l.Name.ToUpper().Contains(texto));
        }
        if (!string.IsNullOrWhiteSpace(request.Scope))
        {
            if (!Enum.TryParse<DimensionDePrecio>(request.Scope, ignoreCase: true, out var dimension) || !Enum.IsDefined(dimension))
            {
                if (!string.Equals(request.Scope, "General", StringComparison.OrdinalIgnoreCase))
                    return Result.Failure<PagedResult<PriceListDto>>(new Error(Error.Validation.Code,
                        "scope admite General, Person, Segment, Channel o Branch."));
                consulta = consulta.Where(l => l.DimensionCount == 0);
            }
            else
            {
                consulta = dimension switch
                {
                    DimensionDePrecio.Person => consulta.Where(l => l.PersonId != null),
                    DimensionDePrecio.Segment => consulta.Where(l => l.Segment != null),
                    DimensionDePrecio.Channel => consulta.Where(l => l.SalesChannelId != null),
                    _ => consulta.Where(l => l.BranchId != null),
                };
            }
        }

        var pagina = request.Pagina ?? new PageRequest();
        var total = await consulta.LongCountAsync(ct);
        var filas = consulta.OrderBy(l => l.Code).ThenBy(l => l.ValidFrom)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize);
        var listas = await VistaDeListasDePrecios.ListasAsync(db, filas, ct);
        return Result.Success(new PagedResult<PriceListDto>(listas, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Una lista con sus precios paginados (§19.1, <c>GET /{id}?search=&amp;page=</c>). (nuevo)</summary>
public sealed record GetPriceListQuery(Guid PriceListPublicId, string? Search = null, PageRequest? Pagina = null) : IRequest<Result<PriceListDetailDto>>;

public sealed class GetPriceListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPriceListQuery, Result<PriceListDetailDto>>
{
    public async Task<Result<PriceListDetailDto>> Handle(GetPriceListQuery request, CancellationToken ct)
    {
        var consulta = db.PriceLists.AsNoTracking().Where(l => l.PublicId == request.PriceListPublicId && !l.IsDeleted);
        var listas = await VistaDeListasDePrecios.ListasAsync(db, consulta, ct);
        if (listas.Count == 0) return Result.Failure<PriceListDetailDto>(ErroresDePrecios.PriceListNotFound());
        var id = await consulta.Select(l => l.Id).FirstAsync(ct);

        var items = from i in db.PriceListItems.AsNoTracking()
                    join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
                    join u in db.UnitsOfMeasure.AsNoTracking() on i.UnitId equals u.Id
                    where i.PriceListId == id && !i.IsDeleted
                    select new { i.PublicId, ProductPublicId = p.PublicId, ProductCode = p.Code, ProductName = p.Name, UnitPublicId = u.PublicId, UnitCode = u.Code, i.Price };
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var texto = request.Search.Trim().ToUpperInvariant();
            items = items.Where(i => i.ProductCode.Contains(texto) || i.ProductName.ToUpper().Contains(texto));
        }

        var pagina = request.Pagina ?? new PageRequest(1, 50);
        var total = await items.LongCountAsync(ct);
        var filas = await items.OrderBy(i => i.ProductCode).ThenBy(i => i.UnitCode)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PriceListDetailDto(listas[0],
            filas.Select(i => new PriceListItemDto(i.PublicId, i.ProductPublicId, i.ProductCode, i.ProductName, i.UnitPublicId, i.UnitCode, i.Price)).ToList(),
            pagina.SafePage, pagina.SafePageSize, total));
    }
}

// ================================================================================================ apoyo --

/// <summary>Resuelve las referencias del ámbito (persona, canal, sucursal) a Id; una inexistente es 404. (nuevo)</summary>
public static class ReferenciasDeLista
{
    public static async Task<Result<(int? PersonId, int? SalesChannelId, int? BranchId)>> ResolverAsync(IApplicationDbContext db, PriceListScopeInput? ambito,
        CancellationToken ct)
    {
        if (ambito is null) return Result.Success<(int?, int?, int?)>((null, null, null));
        int? persona = null, canal = null, sucursal = null;
        if (ambito.PersonPublicId is { } p)
        {
            persona = await db.People.AsNoTracking().Where(x => x.PublicId == p && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (persona is null) return Result.Failure<(int?, int?, int?)>(Error.NotFound);
        }
        if (ambito.SalesChannelPublicId is { } c)
        {
            canal = await db.SalesChannels.AsNoTracking().Where(x => x.PublicId == c && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (canal is null) return Result.Failure<(int?, int?, int?)>(Error.NotFound);
        }
        if (ambito.BranchPublicId is { } b)
        {
            sucursal = await db.Branches.AsNoTracking().Where(x => x.PublicId == b && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (sucursal is null) return Result.Failure<(int?, int?, int?)>(Error.NotFound);
        }
        return Result.Success<(int?, int?, int?)>((persona, canal, sucursal));
    }
}

/// <summary>Arma los <see cref="PriceListDto"/> de una consulta de listas, con las referencias en bloque. (nuevo)</summary>
public static class VistaDeListasDePrecios
{
    public static async Task<IReadOnlyList<PriceListDto>> ListasAsync(IApplicationDbContext db, IQueryable<PriceList> consulta, CancellationToken ct)
    {
        var listas = await consulta.ToListAsync(ct);
        if (listas.Count == 0) return [];
        var ids = listas.Select(l => l.Id).ToList();
        var conteos = await db.PriceListItems.AsNoTracking().Where(i => ids.Contains(i.PriceListId) && !i.IsDeleted)
            .GroupBy(i => i.PriceListId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var personaIds = listas.Select(l => l.PersonId).OfType<int>().Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName })
            .ToDictionaryAsync(p => p.Id, ct);
        var canalIds = listas.Select(l => l.SalesChannelId).OfType<int>().Distinct().ToList();
        var canales = await db.SalesChannels.AsNoTracking().IgnoreQueryFilters().Where(c => canalIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);
        var sucursalIds = listas.Select(l => l.BranchId).OfType<int>().Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => sucursalIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);

        return listas.Select(l =>
        {
            var persona = l.PersonId is int pid ? personas.GetValueOrDefault(pid) : null;
            return new PriceListDto(
                l.PublicId, l.Code, l.Name, l.IncludesTaxes,
                new PriceListScopeDto(
                    persona?.PublicId,
                    persona is null ? null : PersonFactory.NombreVisible(persona.FirstName, persona.OtherNames, persona.LastName, persona.SecondLastName, persona.BusinessName),
                    l.Segment,
                    l.SalesChannelId is int c ? canales.GetValueOrDefault(c) : null,
                    l.BranchId is int b ? sucursales.GetValueOrDefault(b) : null),
                l.ScopeKey, l.DimensionCount, l.ValidFrom, l.ValidTo, l.IsActive, conteos.GetValueOrDefault(l.Id));
        }).ToList();
    }
}
