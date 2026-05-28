using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Countries.Queries;

public record CountryDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
}

// List Query (paginada, por consistencia con el resto)
public record ListCountriesQuery : IRequest<Result<PagedList<CountryDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListCountriesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCountriesQuery, Result<PagedList<CountryDto>>>
{
    public async Task<Result<PagedList<CountryDto>>> Handle(
        ListCountriesQuery request, CancellationToken ct)
    {
        var query = context.Countries
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c => c.Name.Contains(term));
        }

        query = query.OrderBy(c => c.Name);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(c => new CountryDto { PublicId = c.PublicId, Name = c.Name })
            .ToListAsync(ct);

        return Result.Success(new PagedList<CountryDto>(
            items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize));
    }
}

// GetById Query
public record GetCountryByIdQuery(Guid PublicId) : IRequest<Result<CountryDto>>;

public class GetCountryByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCountryByIdQuery, Result<CountryDto>>
{
    public async Task<Result<CountryDto>> Handle(
        GetCountryByIdQuery request, CancellationToken ct)
    {
        var dto = await context.Countries
            .AsNoTracking()
            .Where(c => c.PublicId == request.PublicId && !c.IsDeleted)
            .Select(c => new CountryDto { PublicId = c.PublicId, Name = c.Name })
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<CountryDto>(new Error("Country.NotFound", "Pais no encontrado."))
            : Result.Success(dto);
    }
}
