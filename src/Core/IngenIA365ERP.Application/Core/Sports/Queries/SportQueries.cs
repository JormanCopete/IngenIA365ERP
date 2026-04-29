using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Sports.Queries;

public record SportDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public Guid? CommitteePublicId { get; init; }
    public string? CommitteeName { get; init; }
}

public record ListSportsQuery : IRequest<Result<PagedList<SportDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSportsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSportsQuery, Result<PagedList<SportDto>>>
{
    public async Task<Result<PagedList<SportDto>>> Handle(
        ListSportsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Sports
            .AsNoTracking()
            .Include(e => e.Committee)
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
            .Select(e => new SportDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName,
                CommitteePublicId = e.Committee != null ? e.Committee.PublicId : null,
                CommitteeName = e.Committee != null ? e.Committee.Name : null
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SportDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetSportByIdQuery(Guid PublicId) : IRequest<Result<SportDto>>;

public class GetSportByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSportByIdQuery, Result<SportDto>>
{
    public async Task<Result<SportDto>> Handle(
        GetSportByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Sports
            .AsNoTracking()
            .Include(e => e.Committee)
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SportDto
            {
                PublicId = e.PublicId,
                Name = e.Name,
                ShortName = e.ShortName,
                CommitteePublicId = e.Committee != null ? e.Committee.PublicId : null,
                CommitteeName = e.Committee != null ? e.Committee.Name : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SportDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
