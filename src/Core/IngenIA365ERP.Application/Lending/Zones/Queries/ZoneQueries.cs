using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Zones.Queries;

// DTO
public record ZoneDto
{
    public Guid PublicId { get; init; }
    public int ZoneId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public int SubZoneId { get; init; }
}

// List Query
public record ListZonesQuery : IRequest<Result<PagedList<ZoneDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListZonesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListZonesQuery, Result<PagedList<ZoneDto>>>
{
    public async Task<Result<PagedList<ZoneDto>>> Handle(
        ListZonesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Zones
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
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Code)
                : query.OrderBy(e => e.Code),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ZoneDto
            {
                PublicId = e.PublicId,
                ZoneId = e.ZoneId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                Address = e.Address,
                Phone = e.Phone,
                SubZoneId = e.SubZoneId
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ZoneDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetZoneByIdQuery(Guid PublicId) : IRequest<Result<ZoneDto>>;

public class GetZoneByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetZoneByIdQuery, Result<ZoneDto>>
{
    public async Task<Result<ZoneDto>> Handle(
        GetZoneByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Zones
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ZoneDto
            {
                PublicId = e.PublicId,
                ZoneId = e.ZoneId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                Address = e.Address,
                Phone = e.Phone,
                SubZoneId = e.SubZoneId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ZoneDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
