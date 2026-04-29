using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses.Queries;

// DTO
public record WarehouseDto
{
    public Guid PublicId { get; init; }
    public int WarehouseCode { get; init; }
    public int LocationId { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
}

// List Query
public record ListWarehousesQuery : IRequest<Result<PagedList<WarehouseDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListWarehousesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWarehousesQuery, Result<PagedList<WarehouseDto>>>
{
    public async Task<Result<PagedList<WarehouseDto>>> Handle(
        ListWarehousesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Warehouses
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
            .Select(e => new WarehouseDto
            {
                PublicId = e.PublicId,
                WarehouseCode = e.WarehouseCode,
                LocationId = e.LocationId,
                Description = e.Description,
                ShortDescription = e.ShortDescription
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<WarehouseDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetWarehouseByIdQuery(Guid PublicId) : IRequest<Result<WarehouseDto>>;

public class GetWarehouseByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWarehouseByIdQuery, Result<WarehouseDto>>
{
    public async Task<Result<WarehouseDto>> Handle(
        GetWarehouseByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Warehouses
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WarehouseDto
            {
                PublicId = e.PublicId,
                WarehouseCode = e.WarehouseCode,
                LocationId = e.LocationId,
                Description = e.Description,
                ShortDescription = e.ShortDescription
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<WarehouseDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
