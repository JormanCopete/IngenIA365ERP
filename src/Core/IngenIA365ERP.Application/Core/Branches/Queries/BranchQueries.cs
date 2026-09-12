using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Branches.Queries;

// DTO
public record BranchDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

// List Query
public record ListBranchesQuery : IRequest<Result<PagedList<BranchDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListBranchesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListBranchesQuery, Result<PagedList<BranchDto>>>
{
    public async Task<Result<PagedList<BranchDto>>> Handle(
        ListBranchesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Branches
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
            .Select(e => new BranchDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<BranchDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetBranchByIdQuery(Guid PublicId) : IRequest<Result<BranchDto>>;

public class GetBranchByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBranchByIdQuery, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(
        GetBranchByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Branches
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new BranchDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<BranchDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
