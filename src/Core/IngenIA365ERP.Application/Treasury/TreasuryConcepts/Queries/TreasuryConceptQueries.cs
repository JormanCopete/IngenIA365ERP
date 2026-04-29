using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.TreasuryConcepts.Queries;

// DTO
public record TreasuryConceptDto
{
    public Guid PublicId { get; init; }
    public string ConceptCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? ConceptType { get; init; }
}

// List Query
public record ListTreasuryConceptsQuery : IRequest<Result<PagedList<TreasuryConceptDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListTreasuryConceptsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListTreasuryConceptsQuery, Result<PagedList<TreasuryConceptDto>>>
{
    public async Task<Result<PagedList<TreasuryConceptDto>>> Handle(
        ListTreasuryConceptsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.TreasuryConcepts
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term) ||
                                     e.ConceptCode.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            _ => query.OrderBy(e => e.ConceptCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new TreasuryConceptDto
            {
                PublicId = e.PublicId,
                ConceptCode = e.ConceptCode,
                Name = e.Name,
                ShortName = e.ShortName,
                ConceptType = e.ConceptType
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<TreasuryConceptDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetTreasuryConceptByIdQuery(Guid PublicId) : IRequest<Result<TreasuryConceptDto>>;

public class GetTreasuryConceptByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTreasuryConceptByIdQuery, Result<TreasuryConceptDto>>
{
    public async Task<Result<TreasuryConceptDto>> Handle(
        GetTreasuryConceptByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.TreasuryConcepts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new TreasuryConceptDto
            {
                PublicId = e.PublicId,
                ConceptCode = e.ConceptCode,
                Name = e.Name,
                ShortName = e.ShortName,
                ConceptType = e.ConceptType
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<TreasuryConceptDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
