using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.ScoringParameters.Queries;

// DTO
public record ScoringParameterDto
{
    public Guid PublicId { get; init; }
    public string CriterionCode { get; init; } = string.Empty;
    public string SubItemCode { get; init; } = string.Empty;
    public string CriterionName { get; init; } = string.Empty;
    public string SubItemName { get; init; } = string.Empty;
    public decimal Percentage { get; init; }
}

// List Query
public record ListScoringParametersQuery : IRequest<Result<PagedList<ScoringParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListScoringParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListScoringParametersQuery, Result<PagedList<ScoringParameterDto>>>
{
    public async Task<Result<PagedList<ScoringParameterDto>>> Handle(
        ListScoringParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ScoringParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.CriterionName.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "criterionname" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CriterionName)
                : query.OrderBy(e => e.CriterionName),
            "criterioncode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CriterionCode)
                : query.OrderBy(e => e.CriterionCode),
            _ => query.OrderBy(e => e.CriterionName)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ScoringParameterDto
            {
                PublicId = e.PublicId,
                CriterionCode = e.CriterionCode,
                SubItemCode = e.SubItemCode,
                CriterionName = e.CriterionName,
                SubItemName = e.SubItemName,
                Percentage = e.Percentage
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ScoringParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetScoringParameterByIdQuery(Guid PublicId) : IRequest<Result<ScoringParameterDto>>;

public class GetScoringParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetScoringParameterByIdQuery, Result<ScoringParameterDto>>
{
    public async Task<Result<ScoringParameterDto>> Handle(
        GetScoringParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ScoringParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ScoringParameterDto
            {
                PublicId = e.PublicId,
                CriterionCode = e.CriterionCode,
                SubItemCode = e.SubItemCode,
                CriterionName = e.CriterionName,
                SubItemName = e.SubItemName,
                Percentage = e.Percentage
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ScoringParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
