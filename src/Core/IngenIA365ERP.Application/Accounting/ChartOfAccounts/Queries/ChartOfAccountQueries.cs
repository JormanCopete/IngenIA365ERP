using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.ChartOfAccounts.Queries;

// DTO
public record ChartOfAccountDto
{
    public Guid PublicId { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public byte Level { get; init; }
    public string Nature { get; init; } = string.Empty;
    public decimal Rate { get; init; }
}

// List Query
public record ListChartOfAccountsQuery : IRequest<Result<PagedList<ChartOfAccountDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListChartOfAccountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListChartOfAccountsQuery, Result<PagedList<ChartOfAccountDto>>>
{
    public async Task<Result<PagedList<ChartOfAccountDto>>> Handle(
        ListChartOfAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ChartOfAccounts
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.AccountCode.ToLower().Contains(term)
                || e.Name.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            _ => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.AccountCode)
                : query.OrderBy(e => e.AccountCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ChartOfAccountDto
            {
                PublicId = e.PublicId,
                AccountCode = e.AccountCode,
                Name = e.Name,
                Level = e.Level,
                Nature = e.Nature,
                Rate = e.Rate
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ChartOfAccountDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetChartOfAccountByIdQuery(Guid PublicId) : IRequest<Result<ChartOfAccountDto>>;

public class GetChartOfAccountByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetChartOfAccountByIdQuery, Result<ChartOfAccountDto>>
{
    public async Task<Result<ChartOfAccountDto>> Handle(
        GetChartOfAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ChartOfAccounts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ChartOfAccountDto
            {
                PublicId = e.PublicId,
                AccountCode = e.AccountCode,
                Name = e.Name,
                Level = e.Level,
                Nature = e.Nature,
                Rate = e.Rate
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ChartOfAccountDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
