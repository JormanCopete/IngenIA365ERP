using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.ExternalEntities.Queries;

public record ExternalEntityDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public record ListExternalEntitiesQuery : IRequest<Result<PagedList<ExternalEntityDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListExternalEntitiesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListExternalEntitiesQuery, Result<PagedList<ExternalEntityDto>>>
{
    public async Task<Result<PagedList<ExternalEntityDto>>> Handle(
        ListExternalEntitiesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ExternalEntities
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
            .Select(e => new ExternalEntityDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ExternalEntityDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetExternalEntityByIdQuery(Guid PublicId) : IRequest<Result<ExternalEntityDto>>;

public class GetExternalEntityByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetExternalEntityByIdQuery, Result<ExternalEntityDto>>
{
    public async Task<Result<ExternalEntityDto>> Handle(
        GetExternalEntityByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ExternalEntities
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ExternalEntityDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ExternalEntityDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
