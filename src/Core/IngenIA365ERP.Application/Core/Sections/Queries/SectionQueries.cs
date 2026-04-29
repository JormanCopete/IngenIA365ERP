using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Sections.Queries;

public record SectionDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public record ListSectionsQuery : IRequest<Result<PagedList<SectionDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSectionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSectionsQuery, Result<PagedList<SectionDto>>>
{
    public async Task<Result<PagedList<SectionDto>>> Handle(
        ListSectionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Sections
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
            .Select(e => new SectionDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SectionDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetSectionByIdQuery(Guid PublicId) : IRequest<Result<SectionDto>>;

public class GetSectionByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSectionByIdQuery, Result<SectionDto>>
{
    public async Task<Result<SectionDto>> Handle(
        GetSectionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Sections
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SectionDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SectionDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
