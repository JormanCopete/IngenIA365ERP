using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.IcaTaxLines.Queries;

// DTO
public record IcaTaxLineDto
{
    public Guid PublicId { get; init; }
    public string LineCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AccountCode { get; init; }
    public decimal TaxRate { get; init; }
    public string? BaseAccountCode { get; init; }
    public string? Sign { get; init; }
}

// List Query
public record ListIcaTaxLinesQuery : IRequest<Result<PagedList<IcaTaxLineDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListIcaTaxLinesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListIcaTaxLinesQuery, Result<PagedList<IcaTaxLineDto>>>
{
    public async Task<Result<PagedList<IcaTaxLineDto>>> Handle(
        ListIcaTaxLinesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.IcaTaxLines
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.LineCode.ToLower().Contains(term)
                || (e.Description != null && e.Description.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "description" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Description)
                : query.OrderBy(e => e.Description),
            _ => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.LineCode)
                : query.OrderBy(e => e.LineCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new IcaTaxLineDto
            {
                PublicId = e.PublicId,
                LineCode = e.LineCode,
                Description = e.Description,
                AccountCode = e.AccountCode,
                TaxRate = e.TaxRate,
                BaseAccountCode = e.BaseAccountCode,
                Sign = e.Sign
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<IcaTaxLineDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetIcaTaxLineByIdQuery(Guid PublicId) : IRequest<Result<IcaTaxLineDto>>;

public class GetIcaTaxLineByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetIcaTaxLineByIdQuery, Result<IcaTaxLineDto>>
{
    public async Task<Result<IcaTaxLineDto>> Handle(
        GetIcaTaxLineByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.IcaTaxLines
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new IcaTaxLineDto
            {
                PublicId = e.PublicId,
                LineCode = e.LineCode,
                Description = e.Description,
                AccountCode = e.AccountCode,
                TaxRate = e.TaxRate,
                BaseAccountCode = e.BaseAccountCode,
                Sign = e.Sign
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<IcaTaxLineDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
