using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.PrimaryGroups.Queries;

// DTO
public record PrimaryGroupDto
{
    public Guid PublicId { get; init; }
    public int GroupCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

// List Query
public record ListPrimaryGroupsQuery : IRequest<Result<PagedList<PrimaryGroupDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPrimaryGroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPrimaryGroupsQuery, Result<PagedList<PrimaryGroupDto>>>
{
    public async Task<Result<PagedList<PrimaryGroupDto>>> Handle(
        ListPrimaryGroupsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PrimaryGroups
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
            .Select(e => new PrimaryGroupDto
            {
                PublicId = e.PublicId,
                GroupCode = e.GroupCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PrimaryGroupDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPrimaryGroupByIdQuery(Guid PublicId) : IRequest<Result<PrimaryGroupDto>>;

public class GetPrimaryGroupByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPrimaryGroupByIdQuery, Result<PrimaryGroupDto>>
{
    public async Task<Result<PrimaryGroupDto>> Handle(
        GetPrimaryGroupByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PrimaryGroups
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PrimaryGroupDto
            {
                PublicId = e.PublicId,
                GroupCode = e.GroupCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PrimaryGroupDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
