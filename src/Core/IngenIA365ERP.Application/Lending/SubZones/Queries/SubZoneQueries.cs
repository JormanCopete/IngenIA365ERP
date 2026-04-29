using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SubZones.Queries;

// DTO
public record SubZoneDto
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
}

// List Query
public record ListSubZonesQuery : IRequest<Result<PagedList<SubZoneDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSubZonesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSubZonesQuery, Result<PagedList<SubZoneDto>>>
{
    public async Task<Result<PagedList<SubZoneDto>>> Handle(
        ListSubZonesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.SubZones
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
            .Select(e => new SubZoneDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                Address = e.Address,
                Phone = e.Phone
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SubZoneDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetSubZoneByIdQuery(Guid PublicId) : IRequest<Result<SubZoneDto>>;

public class GetSubZoneByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSubZoneByIdQuery, Result<SubZoneDto>>
{
    public async Task<Result<SubZoneDto>> Handle(
        GetSubZoneByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.SubZones
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SubZoneDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                Address = e.Address,
                Phone = e.Phone
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SubZoneDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
