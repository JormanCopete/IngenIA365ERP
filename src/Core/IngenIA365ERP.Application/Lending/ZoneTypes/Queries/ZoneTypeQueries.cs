using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.ZoneTypes.Queries;

// DTO
public record ZoneTypeDto
{
    public Guid PublicId { get; init; }
    public int ZoneTypeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

// List Query
public record ListZoneTypesQuery : IRequest<Result<PagedList<ZoneTypeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListZoneTypesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListZoneTypesQuery, Result<PagedList<ZoneTypeDto>>>
{
    public async Task<Result<PagedList<ZoneTypeDto>>> Handle(
        ListZoneTypesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ZoneTypes
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            "zonetypeid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.ZoneTypeId)
                : query.OrderBy(e => e.ZoneTypeId),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ZoneTypeDto
            {
                PublicId = e.PublicId,
                ZoneTypeId = e.ZoneTypeId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ZoneTypeDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetZoneTypeByIdQuery(Guid PublicId) : IRequest<Result<ZoneTypeDto>>;

public class GetZoneTypeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetZoneTypeByIdQuery, Result<ZoneTypeDto>>
{
    public async Task<Result<ZoneTypeDto>> Handle(
        GetZoneTypeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ZoneTypes
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ZoneTypeDto
            {
                PublicId = e.PublicId,
                ZoneTypeId = e.ZoneTypeId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ZoneTypeDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
