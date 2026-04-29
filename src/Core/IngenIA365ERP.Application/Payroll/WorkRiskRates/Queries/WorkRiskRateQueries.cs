using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WorkRiskRates.Queries;

// DTO
public record WorkRiskRateDto
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public decimal Rate { get; init; }
}

// List Query
public record ListWorkRiskRatesQuery : IRequest<Result<PagedList<WorkRiskRateDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListWorkRiskRatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWorkRiskRatesQuery, Result<PagedList<WorkRiskRateDto>>>
{
    public async Task<Result<PagedList<WorkRiskRateDto>>> Handle(
        ListWorkRiskRatesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.WorkRiskRates
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
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Code)
                : query.OrderBy(e => e.Code),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new WorkRiskRateDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                Rate = e.Rate
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<WorkRiskRateDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetWorkRiskRateByIdQuery(Guid PublicId) : IRequest<Result<WorkRiskRateDto>>;

public class GetWorkRiskRateByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWorkRiskRateByIdQuery, Result<WorkRiskRateDto>>
{
    public async Task<Result<WorkRiskRateDto>> Handle(
        GetWorkRiskRateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.WorkRiskRates
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WorkRiskRateDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                Rate = e.Rate
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<WorkRiskRateDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
