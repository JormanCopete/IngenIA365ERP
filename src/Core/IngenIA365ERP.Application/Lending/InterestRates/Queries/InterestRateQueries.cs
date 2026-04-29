using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.InterestRates.Queries;

// DTO
public record InterestRateDto
{
    public Guid PublicId { get; init; }
    public int Period { get; init; }
    public string CreditLineCode { get; init; } = string.Empty;
    public decimal PortfolioBalance { get; init; }
    public string CostCenterId { get; init; } = string.Empty;
    public int? PortfolioClass { get; init; }
}

// List Query
public record ListInterestRatesQuery : IRequest<Result<PagedList<InterestRateDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListInterestRatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListInterestRatesQuery, Result<PagedList<InterestRateDto>>>
{
    public async Task<Result<PagedList<InterestRateDto>>> Handle(
        ListInterestRatesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.InterestRates
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.CreditLineCode.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "creditlinecode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CreditLineCode)
                : query.OrderBy(e => e.CreditLineCode),
            "period" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Period)
                : query.OrderBy(e => e.Period),
            _ => query.OrderByDescending(e => e.Period)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new InterestRateDto
            {
                PublicId = e.PublicId,
                Period = e.Period,
                CreditLineCode = e.CreditLineCode,
                PortfolioBalance = e.PortfolioBalance,
                CostCenterId = e.CostCenterId,
                PortfolioClass = e.PortfolioClass
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<InterestRateDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetInterestRateByIdQuery(Guid PublicId) : IRequest<Result<InterestRateDto>>;

public class GetInterestRateByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetInterestRateByIdQuery, Result<InterestRateDto>>
{
    public async Task<Result<InterestRateDto>> Handle(
        GetInterestRateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.InterestRates
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new InterestRateDto
            {
                PublicId = e.PublicId,
                Period = e.Period,
                CreditLineCode = e.CreditLineCode,
                PortfolioBalance = e.PortfolioBalance,
                CostCenterId = e.CostCenterId,
                PortfolioClass = e.PortfolioClass
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<InterestRateDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
