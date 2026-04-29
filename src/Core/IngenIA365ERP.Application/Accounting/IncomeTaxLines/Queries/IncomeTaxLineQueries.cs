using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.IncomeTaxLines.Queries;

// DTO
public record IncomeTaxLineDto
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
public record ListIncomeTaxLinesQuery : IRequest<Result<PagedList<IncomeTaxLineDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListIncomeTaxLinesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListIncomeTaxLinesQuery, Result<PagedList<IncomeTaxLineDto>>>
{
    public async Task<Result<PagedList<IncomeTaxLineDto>>> Handle(
        ListIncomeTaxLinesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.IncomeTaxLines
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
            .Select(e => new IncomeTaxLineDto
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

        var pagedList = new PagedList<IncomeTaxLineDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetIncomeTaxLineByIdQuery(Guid PublicId) : IRequest<Result<IncomeTaxLineDto>>;

public class GetIncomeTaxLineByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetIncomeTaxLineByIdQuery, Result<IncomeTaxLineDto>>
{
    public async Task<Result<IncomeTaxLineDto>> Handle(
        GetIncomeTaxLineByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.IncomeTaxLines
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new IncomeTaxLineDto
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
            ? Result.Failure<IncomeTaxLineDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
