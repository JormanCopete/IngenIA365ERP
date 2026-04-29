using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountSubgroups.Queries;

// DTO
public record AccountSubgroupDto
{
    public Guid PublicId { get; init; }
    public int? GroupId { get; init; }
    public int SubgroupNumber { get; init; }
    public string AccountCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? ReportOrder { get; init; }
    public int? Level { get; init; }
}

// List Query
public record ListAccountSubgroupsQuery : IRequest<Result<PagedList<AccountSubgroupDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListAccountSubgroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListAccountSubgroupsQuery, Result<PagedList<AccountSubgroupDto>>>
{
    public async Task<Result<PagedList<AccountSubgroupDto>>> Handle(
        ListAccountSubgroupsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AccountSubgroups
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Description.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "accountcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.AccountCode)
                : query.OrderBy(e => e.AccountCode),
            _ => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Description)
                : query.OrderBy(e => e.Description)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new AccountSubgroupDto
            {
                PublicId = e.PublicId,
                GroupId = e.GroupId,
                SubgroupNumber = e.SubgroupNumber,
                AccountCode = e.AccountCode,
                Description = e.Description,
                ReportOrder = e.ReportOrder,
                Level = e.Level
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<AccountSubgroupDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetAccountSubgroupByIdQuery(Guid PublicId) : IRequest<Result<AccountSubgroupDto>>;

public class GetAccountSubgroupByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAccountSubgroupByIdQuery, Result<AccountSubgroupDto>>
{
    public async Task<Result<AccountSubgroupDto>> Handle(
        GetAccountSubgroupByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.AccountSubgroups
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new AccountSubgroupDto
            {
                PublicId = e.PublicId,
                GroupId = e.GroupId,
                SubgroupNumber = e.SubgroupNumber,
                AccountCode = e.AccountCode,
                Description = e.Description,
                ReportOrder = e.ReportOrder,
                Level = e.Level
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<AccountSubgroupDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
