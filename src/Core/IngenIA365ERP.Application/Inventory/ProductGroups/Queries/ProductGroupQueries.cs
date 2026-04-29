using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.ProductGroups.Queries;

// DTO
public record ProductGroupDto
{
    public Guid PublicId { get; init; }
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? SecondaryGroupId { get; init; }
    public bool RestrictsLimit { get; init; }
    public int MaxSalesQuantity { get; init; }
}

// List Query
public record ListProductGroupsQuery : IRequest<Result<PagedList<ProductGroupDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListProductGroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListProductGroupsQuery, Result<PagedList<ProductGroupDto>>>
{
    public async Task<Result<PagedList<ProductGroupDto>>> Handle(
        ListProductGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ProductGroups
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
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ProductGroupDto
            {
                PublicId = e.PublicId,
                GroupCode = e.GroupCode,
                Name = e.Name,
                ShortName = e.ShortName,
                SecondaryGroupId = e.SecondaryGroupId,
                RestrictsLimit = e.RestrictsLimit,
                MaxSalesQuantity = e.MaxSalesQuantity
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ProductGroupDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetProductGroupByIdQuery(Guid PublicId) : IRequest<Result<ProductGroupDto>>;

public class GetProductGroupByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetProductGroupByIdQuery, Result<ProductGroupDto>>
{
    public async Task<Result<ProductGroupDto>> Handle(
        GetProductGroupByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ProductGroups
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ProductGroupDto
            {
                PublicId = e.PublicId,
                GroupCode = e.GroupCode,
                Name = e.Name,
                ShortName = e.ShortName,
                SecondaryGroupId = e.SecondaryGroupId,
                RestrictsLimit = e.RestrictsLimit,
                MaxSalesQuantity = e.MaxSalesQuantity
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ProductGroupDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
