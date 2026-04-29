using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.SecondaryGroups.Queries;

// DTO
public record SecondaryGroupDto
{
    public Guid PublicId { get; init; }
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? PrimaryGroupId { get; init; }
}

// List Query
public record ListSecondaryGroupsQuery : IRequest<Result<PagedList<SecondaryGroupDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSecondaryGroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSecondaryGroupsQuery, Result<PagedList<SecondaryGroupDto>>>
{
    public async Task<Result<PagedList<SecondaryGroupDto>>> Handle(
        ListSecondaryGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.SecondaryGroups
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new SecondaryGroupDto
            {
                PublicId = e.PublicId,
                GroupCode = e.GroupCode,
                Name = e.Name,
                ShortName = e.ShortName,
                PrimaryGroupId = e.PrimaryGroupId
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SecondaryGroupDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetSecondaryGroupByIdQuery(Guid PublicId) : IRequest<Result<SecondaryGroupDto>>;

public class GetSecondaryGroupByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSecondaryGroupByIdQuery, Result<SecondaryGroupDto>>
{
    public async Task<Result<SecondaryGroupDto>> Handle(
        GetSecondaryGroupByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.SecondaryGroups
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SecondaryGroupDto
            {
                PublicId = e.PublicId,
                GroupCode = e.GroupCode,
                Name = e.Name,
                ShortName = e.ShortName,
                PrimaryGroupId = e.PrimaryGroupId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SecondaryGroupDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
