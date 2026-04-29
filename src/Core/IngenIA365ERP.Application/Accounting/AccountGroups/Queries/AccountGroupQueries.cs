using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.AccountGroups.Queries;

// DTO
public record AccountGroupDto
{
    public Guid PublicId { get; init; }
    public int? GroupNumber { get; init; }
    public int? GroupType { get; init; }
    public string? AccountCode { get; init; }
    public string? Description { get; init; }
    public int? ReportOrder { get; init; }
    public int? Level { get; init; }
    public int? ParentGroupId { get; init; }
}

// List Query
public record ListAccountGroupsQuery : IRequest<Result<PagedList<AccountGroupDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListAccountGroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListAccountGroupsQuery, Result<PagedList<AccountGroupDto>>>
{
    public async Task<Result<PagedList<AccountGroupDto>>> Handle(
        ListAccountGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AccountGroups
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => (e.Description != null && e.Description.ToLower().Contains(term))
                || (e.AccountCode != null && e.AccountCode.ToLower().Contains(term)));
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
            .Select(e => new AccountGroupDto
            {
                PublicId = e.PublicId,
                GroupNumber = e.GroupNumber,
                GroupType = e.GroupType,
                AccountCode = e.AccountCode,
                Description = e.Description,
                ReportOrder = e.ReportOrder,
                Level = e.Level,
                ParentGroupId = e.ParentGroupId
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<AccountGroupDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetAccountGroupByIdQuery(Guid PublicId) : IRequest<Result<AccountGroupDto>>;

public class GetAccountGroupByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAccountGroupByIdQuery, Result<AccountGroupDto>>
{
    public async Task<Result<AccountGroupDto>> Handle(
        GetAccountGroupByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.AccountGroups
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new AccountGroupDto
            {
                PublicId = e.PublicId,
                GroupNumber = e.GroupNumber,
                GroupType = e.GroupType,
                AccountCode = e.AccountCode,
                Description = e.Description,
                ReportOrder = e.ReportOrder,
                Level = e.Level,
                ParentGroupId = e.ParentGroupId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<AccountGroupDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
