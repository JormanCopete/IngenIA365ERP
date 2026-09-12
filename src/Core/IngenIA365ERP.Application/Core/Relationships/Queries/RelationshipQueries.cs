using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Relationships.Queries;

public record RelationshipDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public record ListRelationshipsQuery : IRequest<Result<PagedList<RelationshipDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListRelationshipsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListRelationshipsQuery, Result<PagedList<RelationshipDto>>>
{
    public async Task<Result<PagedList<RelationshipDto>>> Handle(
        ListRelationshipsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Relationships
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
            .Select(e => new RelationshipDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<RelationshipDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetRelationshipByIdQuery(Guid PublicId) : IRequest<Result<RelationshipDto>>;

public class GetRelationshipByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetRelationshipByIdQuery, Result<RelationshipDto>>
{
    public async Task<Result<RelationshipDto>> Handle(
        GetRelationshipByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Relationships
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new RelationshipDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<RelationshipDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
