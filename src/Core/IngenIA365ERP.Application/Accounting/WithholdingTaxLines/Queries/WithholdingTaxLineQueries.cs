using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.WithholdingTaxLines.Queries;

// DTO
public record WithholdingTaxLineDto
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
public record ListWithholdingTaxLinesQuery : IRequest<Result<PagedList<WithholdingTaxLineDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListWithholdingTaxLinesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWithholdingTaxLinesQuery, Result<PagedList<WithholdingTaxLineDto>>>
{
    public async Task<Result<PagedList<WithholdingTaxLineDto>>> Handle(
        ListWithholdingTaxLinesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.WithholdingTaxLines
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
            .Select(e => new WithholdingTaxLineDto
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

        var pagedList = new PagedList<WithholdingTaxLineDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetWithholdingTaxLineByIdQuery(Guid PublicId) : IRequest<Result<WithholdingTaxLineDto>>;

public class GetWithholdingTaxLineByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWithholdingTaxLineByIdQuery, Result<WithholdingTaxLineDto>>
{
    public async Task<Result<WithholdingTaxLineDto>> Handle(
        GetWithholdingTaxLineByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.WithholdingTaxLines
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WithholdingTaxLineDto
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
            ? Result.Failure<WithholdingTaxLineDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
