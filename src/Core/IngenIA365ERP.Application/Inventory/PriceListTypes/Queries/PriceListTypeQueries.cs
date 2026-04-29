using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.PriceListTypes.Queries;

// DTO
public record PriceListTypeDto
{
    public Guid PublicId { get; init; }
    public int TypeCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? PriceClass { get; init; }
}

// List Query
public record ListPriceListTypesQuery : IRequest<Result<PagedList<PriceListTypeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPriceListTypesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPriceListTypesQuery, Result<PagedList<PriceListTypeDto>>>
{
    public async Task<Result<PagedList<PriceListTypeDto>>> Handle(
        ListPriceListTypesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PriceListTypes
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
            .Select(e => new PriceListTypeDto
            {
                PublicId = e.PublicId,
                TypeCode = e.TypeCode,
                Name = e.Name,
                ShortName = e.ShortName,
                PriceClass = e.PriceClass
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PriceListTypeDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPriceListTypeByIdQuery(Guid PublicId) : IRequest<Result<PriceListTypeDto>>;

public class GetPriceListTypeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPriceListTypeByIdQuery, Result<PriceListTypeDto>>
{
    public async Task<Result<PriceListTypeDto>> Handle(
        GetPriceListTypeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PriceListTypes
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PriceListTypeDto
            {
                PublicId = e.PublicId,
                TypeCode = e.TypeCode,
                Name = e.Name,
                ShortName = e.ShortName,
                PriceClass = e.PriceClass
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PriceListTypeDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
