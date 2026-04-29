using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PortfolioAccounts.Queries;

// DTO
public record PortfolioAccountDto
{
    public Guid PublicId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string RecordClass { get; init; } = string.Empty;
    public int Category { get; init; }
    public int GuaranteeType { get; init; }
    public int DeductionClass { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
}

// List Query
public record ListPortfolioAccountsQuery : IRequest<Result<PagedList<PortfolioAccountDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPortfolioAccountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPortfolioAccountsQuery, Result<PagedList<PortfolioAccountDto>>>
{
    public async Task<Result<PagedList<PortfolioAccountDto>>> Handle(
        ListPortfolioAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PortfolioAccounts
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.AccountCode.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "accountcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.AccountCode)
                : query.OrderBy(e => e.AccountCode),
            "category" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Category)
                : query.OrderBy(e => e.Category),
            _ => query.OrderBy(e => e.AccountCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new PortfolioAccountDto
            {
                PublicId = e.PublicId,
                AccountCode = e.AccountCode,
                RecordClass = e.RecordClass,
                Category = e.Category,
                GuaranteeType = e.GuaranteeType,
                DeductionClass = e.DeductionClass,
                RiskLevel = e.RiskLevel
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PortfolioAccountDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPortfolioAccountByIdQuery(Guid PublicId) : IRequest<Result<PortfolioAccountDto>>;

public class GetPortfolioAccountByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPortfolioAccountByIdQuery, Result<PortfolioAccountDto>>
{
    public async Task<Result<PortfolioAccountDto>> Handle(
        GetPortfolioAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PortfolioAccounts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PortfolioAccountDto
            {
                PublicId = e.PublicId,
                AccountCode = e.AccountCode,
                RecordClass = e.RecordClass,
                Category = e.Category,
                GuaranteeType = e.GuaranteeType,
                DeductionClass = e.DeductionClass,
                RiskLevel = e.RiskLevel
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PortfolioAccountDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
