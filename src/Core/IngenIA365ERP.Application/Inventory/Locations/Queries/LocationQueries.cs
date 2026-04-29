using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Locations.Queries;

// DTO
public record LocationDto
{
    public Guid PublicId { get; init; }
    public int LocationCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
}

// List Query
public record ListLocationsQuery : IRequest<Result<PagedList<LocationDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListLocationsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListLocationsQuery, Result<PagedList<LocationDto>>>
{
    public async Task<Result<PagedList<LocationDto>>> Handle(
        ListLocationsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Locations
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Description.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "description" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Description)
                : query.OrderBy(e => e.Description),
            _ => query.OrderBy(e => e.Description)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new LocationDto
            {
                PublicId = e.PublicId,
                LocationCode = e.LocationCode,
                Description = e.Description,
                ShortDescription = e.ShortDescription
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<LocationDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetLocationByIdQuery(Guid PublicId) : IRequest<Result<LocationDto>>;

public class GetLocationByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLocationByIdQuery, Result<LocationDto>>
{
    public async Task<Result<LocationDto>> Handle(
        GetLocationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Locations
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new LocationDto
            {
                PublicId = e.PublicId,
                LocationCode = e.LocationCode,
                Description = e.Description,
                ShortDescription = e.ShortDescription
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<LocationDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
