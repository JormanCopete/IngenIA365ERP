using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Cities.Queries;

public record CityDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid DepartmentPublicId { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
}

public record ListCitiesQuery : IRequest<Result<PagedList<CityDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
    public Guid? DepartmentPublicId { get; init; }
}

public class ListCitiesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCitiesQuery, Result<PagedList<CityDto>>>
{
    public async Task<Result<PagedList<CityDto>>> Handle(
        ListCitiesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Cities
            .AsNoTracking()
            .Include(e => e.Department)
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term));
        }

        if (request.DepartmentPublicId.HasValue)
        {
            query = query.Where(e => e.Department.PublicId == request.DepartmentPublicId.Value);
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            "department" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Department.Name)
                : query.OrderBy(e => e.Department.Name),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new CityDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                DepartmentPublicId = e.Department.PublicId,
                DepartmentName = e.Department.Name
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<CityDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetCityByIdQuery(Guid PublicId) : IRequest<Result<CityDto>>;

public class GetCityByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCityByIdQuery, Result<CityDto>>
{
    public async Task<Result<CityDto>> Handle(
        GetCityByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Cities
            .AsNoTracking()
            .Include(e => e.Department)
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new CityDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                DepartmentPublicId = e.Department.PublicId,
                DepartmentName = e.Department.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<CityDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
