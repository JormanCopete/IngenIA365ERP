using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DiscountTypes.Queries;

// DTO
public record DiscountTypeDto
{
    public Guid PublicId { get; init; }
    public int TypeCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? DiscountClass { get; init; }
}

// List Query
public record ListDiscountTypesQuery : IRequest<Result<PagedList<DiscountTypeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListDiscountTypesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDiscountTypesQuery, Result<PagedList<DiscountTypeDto>>>
{
    public async Task<Result<PagedList<DiscountTypeDto>>> Handle(
        ListDiscountTypesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.DiscountTypes
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
            .Select(e => new DiscountTypeDto
            {
                PublicId = e.PublicId,
                TypeCode = e.TypeCode,
                Name = e.Name,
                ShortName = e.ShortName,
                DiscountClass = e.DiscountClass
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<DiscountTypeDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetDiscountTypeByIdQuery(Guid PublicId) : IRequest<Result<DiscountTypeDto>>;

public class GetDiscountTypeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDiscountTypeByIdQuery, Result<DiscountTypeDto>>
{
    public async Task<Result<DiscountTypeDto>> Handle(
        GetDiscountTypeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.DiscountTypes
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new DiscountTypeDto
            {
                PublicId = e.PublicId,
                TypeCode = e.TypeCode,
                Name = e.Name,
                ShortName = e.ShortName,
                DiscountClass = e.DiscountClass
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<DiscountTypeDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
