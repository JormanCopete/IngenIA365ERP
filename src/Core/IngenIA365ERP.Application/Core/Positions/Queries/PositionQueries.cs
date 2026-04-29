using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Positions.Queries;

public record PositionDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public record ListPositionsQuery : IRequest<Result<PagedList<PositionDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPositionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPositionsQuery, Result<PagedList<PositionDto>>>
{
    public async Task<Result<PagedList<PositionDto>>> Handle(
        ListPositionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Positions
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
            .Select(e => new PositionDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PositionDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetPositionByIdQuery(Guid PublicId) : IRequest<Result<PositionDto>>;

public class GetPositionByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPositionByIdQuery, Result<PositionDto>>
{
    public async Task<Result<PositionDto>> Handle(
        GetPositionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Positions
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PositionDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PositionDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
