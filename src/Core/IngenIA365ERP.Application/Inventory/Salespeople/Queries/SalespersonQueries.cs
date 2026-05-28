using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Queries;

/// <summary>
/// DTO de Vendedor. Datos personales (nombre, documento, contacto)
/// vienen de COR_People via JOIN. INV_Salespeople solo guarda
/// SalespersonType y AppliesCommission.
/// </summary>
public record SalespersonDto
{
    public Guid PublicId { get; init; }
    public Guid PersonPublicId { get; init; }
    public string IdNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? LastName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public string? CityName { get; init; }
    public int? SalespersonType { get; init; }
    public bool AppliesCommission { get; init; }
}

public record ListSalespeopleQuery : IRequest<Result<PagedList<SalespersonDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSalespeopleQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSalespeopleQuery, Result<PagedList<SalespersonDto>>>
{
    public async Task<Result<PagedList<SalespersonDto>>> Handle(
        ListSalespeopleQuery request, CancellationToken ct)
    {
        var query = from s in context.Salespeople.AsNoTracking().Where(s => !s.IsDeleted)
                    join p in context.People.AsNoTracking() on s.PersonId equals p.Id
                    where !p.IsDeleted
                    select new { s, p };

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(x =>
                x.p.FirstName.Contains(term) ||
                x.p.LastName.Contains(term) ||
                x.p.TaxId.Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(x => x.p.FirstName)
                : query.OrderBy(x => x.p.FirstName),
            _ => query.OrderBy(x => x.p.LastName).ThenBy(x => x.p.FirstName)
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(x => new SalespersonDto
            {
                PublicId = x.s.PublicId,
                PersonPublicId = x.p.PublicId,
                IdNumber = x.p.TaxId,
                Name = x.p.FirstName,
                LastName = x.p.LastName,
                Address = x.p.Address,
                Phone = x.p.Phone1,
                Mobile = x.p.Mobile,
                CityName = x.p.City != null ? x.p.City.Name : null,
                SalespersonType = x.s.SalespersonType,
                AppliesCommission = x.s.AppliesCommission
            })
            .ToListAsync(ct);

        return Result.Success(new PagedList<SalespersonDto>(
            items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize));
    }
}

public record GetSalespersonByIdQuery(Guid PublicId) : IRequest<Result<SalespersonDto>>;

public class GetSalespersonByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSalespersonByIdQuery, Result<SalespersonDto>>
{
    public async Task<Result<SalespersonDto>> Handle(
        GetSalespersonByIdQuery request, CancellationToken ct)
    {
        var dto = await (
            from s in context.Salespeople.AsNoTracking().Where(s => !s.IsDeleted && s.PublicId == request.PublicId)
            join p in context.People.AsNoTracking() on s.PersonId equals p.Id
            where !p.IsDeleted
            select new SalespersonDto
            {
                PublicId = s.PublicId,
                PersonPublicId = p.PublicId,
                IdNumber = p.TaxId,
                Name = p.FirstName,
                LastName = p.LastName,
                Address = p.Address,
                Phone = p.Phone1,
                Mobile = p.Mobile,
                CityName = p.City != null ? p.City.Name : null,
                SalespersonType = s.SalespersonType,
                AppliesCommission = s.AppliesCommission
            })
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<SalespersonDto>(new Error("Salesperson.NotFound", "Vendedor no encontrado."))
            : Result.Success(dto);
    }
}
