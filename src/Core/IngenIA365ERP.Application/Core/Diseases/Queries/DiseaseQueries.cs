using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Diseases.Queries;

public record DiseaseDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
}

public record ListDiseasesQuery : IRequest<Result<PagedList<DiseaseDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListDiseasesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDiseasesQuery, Result<PagedList<DiseaseDto>>>
{
    public async Task<Result<PagedList<DiseaseDto>>> Handle(
        ListDiseasesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Diseases
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
            .Select(e => new DiseaseDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<DiseaseDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetDiseaseByIdQuery(Guid PublicId) : IRequest<Result<DiseaseDto>>;

public class GetDiseaseByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDiseaseByIdQuery, Result<DiseaseDto>>
{
    public async Task<Result<DiseaseDto>> Handle(
        GetDiseaseByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Diseases
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new DiseaseDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<DiseaseDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
