using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Professions.Queries;

public record ProfessionDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
}

public record ListProfessionsQuery : IRequest<Result<PagedList<ProfessionDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListProfessionsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListProfessionsQuery, Result<PagedList<ProfessionDto>>>
{
    public async Task<Result<PagedList<ProfessionDto>>> Handle(
        ListProfessionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Professions
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
            .Select(e => new ProfessionDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ProfessionDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetProfessionByIdQuery(Guid PublicId) : IRequest<Result<ProfessionDto>>;

public class GetProfessionByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetProfessionByIdQuery, Result<ProfessionDto>>
{
    public async Task<Result<ProfessionDto>> Handle(
        GetProfessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Professions
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ProfessionDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                ShortName = e.ShortName
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ProfessionDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
