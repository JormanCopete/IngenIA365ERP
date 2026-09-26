using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Queries;

/// <summary>
/// Un vendedor (feature 012, T425; contracts/api.md §31): el rol y la persona que lo tiene. Los datos personales vienen de
/// <c>COR_People</c>; <c>INV_Salespeople</c> sólo guarda el tipo y si aplica comisión. <see cref="IsActive"/> es falso en un
/// rol retirado (baja lógica). (nuevo)
/// </summary>
public sealed record SalespersonDto(
    Guid SalespersonPublicId,
    SalespersonPersonDto Person,
    int? SalespersonType,
    bool AppliesCommission,
    bool IsActive);

public sealed record SalespersonPersonDto(Guid PersonPublicId, string Name, string IdNumber);

/// <summary>
/// La lista paginada de vendedores (§31, <c>GET /api/inventory/salespeople?search=&amp;includeRetired=&amp;page=&amp;pageSize=</c>,
/// <c>Inventory.Salespeople.View</c>). <see cref="Search"/> busca en nombre, apellido y documento; sin
/// <see cref="IncludeRetired"/> sólo los vivos. No filtra por alcance: un vendedor es una persona, sin bodega ni punto
/// (FR-031; <c>LasConsultasDeInventarioRespetanElAlcance</c>).
/// </summary>
public sealed record ListSalespeopleQuery(string? Search = null, bool IncludeRetired = false, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<SalespersonDto>>>;

public sealed class ListSalespeopleQueryHandler(IApplicationDbContext db) : IRequestHandler<ListSalespeopleQuery, Result<PagedResult<SalespersonDto>>>
{
    public async Task<Result<PagedResult<SalespersonDto>>> Handle(ListSalespeopleQuery request, CancellationToken ct)
    {
        var incluirRetirados = request.IncludeRetired;
        var consulta = from s in db.Salespeople.IgnoreQueryFilters().AsNoTracking()
                       where incluirRetirados || !s.IsDeleted
                       join p in db.People.IgnoreQueryFilters().AsNoTracking() on s.PersonId equals p.Id
                       select new { s, p };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var termino = request.Search.Trim();
            consulta = consulta.Where(x => x.p.FirstName.Contains(termino) || x.p.LastName.Contains(termino) || x.p.TaxId.Contains(termino));
        }

        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var items = await consulta
            .OrderBy(x => x.s.IsDeleted).ThenBy(x => x.p.LastName).ThenBy(x => x.p.FirstName).ThenBy(x => x.s.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize)
            .Take(pagina.SafePageSize)
            .Select(x => new SalespersonDto(
                x.s.PublicId,
                new SalespersonPersonDto(x.p.PublicId, (x.p.FirstName + " " + x.p.LastName).Trim(), x.p.TaxId),
                x.s.SalespersonType,
                x.s.AppliesCommission,
                !x.s.IsDeleted))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<SalespersonDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}
