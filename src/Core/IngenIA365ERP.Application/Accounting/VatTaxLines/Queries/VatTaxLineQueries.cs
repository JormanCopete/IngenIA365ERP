using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.VatTaxLines.Queries;

// DTO
public record VatTaxLineDto
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
public record ListVatTaxLinesQuery : IRequest<Result<PagedList<VatTaxLineDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListVatTaxLinesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListVatTaxLinesQuery, Result<PagedList<VatTaxLineDto>>>
{
    public async Task<Result<PagedList<VatTaxLineDto>>> Handle(
        ListVatTaxLinesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.VatTaxLines
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
            .Select(e => new VatTaxLineDto
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

        var pagedList = new PagedList<VatTaxLineDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetVatTaxLineByIdQuery(Guid PublicId) : IRequest<Result<VatTaxLineDto>>;

public class GetVatTaxLineByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetVatTaxLineByIdQuery, Result<VatTaxLineDto>>
{
    public async Task<Result<VatTaxLineDto>> Handle(
        GetVatTaxLineByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.VatTaxLines
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new VatTaxLineDto
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
            ? Result.Failure<VatTaxLineDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
