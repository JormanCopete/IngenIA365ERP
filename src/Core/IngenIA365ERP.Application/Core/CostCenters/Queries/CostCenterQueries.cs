using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.CostCenters.Queries;

public record CostCenterDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? CompanyTaxId { get; init; }
    public short PayrollType { get; init; }
    public short Period { get; init; }
    public short PayrollPeriodicity { get; init; }
    public string? PayrollStatus { get; init; }
}

public record ListCostCentersQuery : IRequest<Result<PagedList<CostCenterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListCostCentersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCostCentersQuery, Result<PagedList<CostCenterDto>>>
{
    public async Task<Result<PagedList<CostCenterDto>>> Handle(
        ListCostCentersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.CostCenters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(term) ||
                (e.CompanyName != null && e.CompanyName.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            "companyname" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CompanyName)
                : query.OrderBy(e => e.CompanyName),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new CostCenterDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                CompanyName = e.CompanyName,
                CompanyTaxId = e.CompanyTaxId,
                PayrollType = e.PayrollType,
                Period = e.Period,
                PayrollPeriodicity = e.PayrollPeriodicity,
                PayrollStatus = e.PayrollStatus
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<CostCenterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

public record GetCostCenterByIdQuery(Guid PublicId) : IRequest<Result<CostCenterDto>>;

public class GetCostCenterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCostCenterByIdQuery, Result<CostCenterDto>>
{
    public async Task<Result<CostCenterDto>> Handle(
        GetCostCenterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.CostCenters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new CostCenterDto
            {
                PublicId = e.PublicId,
                Code = e.LegacyCode,
                Name = e.Name,
                CompanyName = e.CompanyName,
                CompanyTaxId = e.CompanyTaxId,
                PayrollType = e.PayrollType,
                Period = e.Period,
                PayrollPeriodicity = e.PayrollPeriodicity,
                PayrollStatus = e.PayrollStatus
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<CostCenterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
