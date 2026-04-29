using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.RiskCategories.Queries;

// DTO
public record RiskCategoryDto
{
    public Guid PublicId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string PeriodCode { get; init; } = string.Empty;
    public decimal InitialBalance { get; init; }
    public decimal AverageBalance { get; init; }
}

// List Query
public record ListRiskCategoriesQuery : IRequest<Result<PagedList<RiskCategoryDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListRiskCategoriesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListRiskCategoriesQuery, Result<PagedList<RiskCategoryDto>>>
{
    public async Task<Result<PagedList<RiskCategoryDto>>> Handle(
        ListRiskCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.RiskCategories
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.AccountCode.ToLower().Contains(term)
                || e.PeriodCode.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "periodcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.PeriodCode)
                : query.OrderBy(e => e.PeriodCode),
            _ => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.AccountCode)
                : query.OrderBy(e => e.AccountCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new RiskCategoryDto
            {
                PublicId = e.PublicId,
                AccountCode = e.AccountCode,
                PeriodCode = e.PeriodCode,
                InitialBalance = e.InitialBalance,
                AverageBalance = e.AverageBalance
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<RiskCategoryDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetRiskCategoryByIdQuery(Guid PublicId) : IRequest<Result<RiskCategoryDto>>;

public class GetRiskCategoryByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetRiskCategoryByIdQuery, Result<RiskCategoryDto>>
{
    public async Task<Result<RiskCategoryDto>> Handle(
        GetRiskCategoryByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.RiskCategories
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new RiskCategoryDto
            {
                PublicId = e.PublicId,
                AccountCode = e.AccountCode,
                PeriodCode = e.PeriodCode,
                InitialBalance = e.InitialBalance,
                AverageBalance = e.AverageBalance
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<RiskCategoryDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
