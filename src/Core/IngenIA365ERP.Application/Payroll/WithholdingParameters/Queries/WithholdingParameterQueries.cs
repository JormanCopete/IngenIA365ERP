using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingParameters.Queries;

// DTO
public record WithholdingParameterDto
{
    public Guid PublicId { get; init; }
    public Guid PayrollPlanPublicId { get; init; }
    public string PayrollPlanCode { get; init; } = string.Empty;
    public string PayrollPlanName { get; init; } = string.Empty;
    public int UvtRangeStart { get; init; }
    public int UvtRangeEnd { get; init; }
    public decimal Rate { get; init; }
    public int AdditionalUvt { get; init; }
}

// List Query
public record ListWithholdingParametersQuery : IRequest<Result<PagedList<WithholdingParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
    /// <summary>Sólo los tramos de ese plan.</summary>
    public Guid? PlanPublicId { get; init; }
}

public class ListWithholdingParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWithholdingParametersQuery, Result<PagedList<WithholdingParameterDto>>>
{
    public async Task<Result<PagedList<WithholdingParameterDto>>> Handle(
        ListWithholdingParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.WithholdingParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (request.PlanPublicId is { } planId)
            query = query.Where(e => e.PayrollPlan!.PublicId == planId);
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToUpperInvariant();
            query = query.Where(e => e.PayrollPlan!.Code.Contains(term) || e.PayrollPlan.Name.ToUpper().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "uvtrangestart" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.UvtRangeStart)
                : query.OrderBy(e => e.UvtRangeStart),
            "rate" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Rate)
                : query.OrderBy(e => e.Rate),
            _ => query.OrderBy(e => e.PayrollPlan!.Code).ThenBy(e => e.UvtRangeStart)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new WithholdingParameterDto
            {
                PublicId = e.PublicId,
                PayrollPlanPublicId = e.PayrollPlan!.PublicId,
                PayrollPlanCode = e.PayrollPlan.Code,
                PayrollPlanName = e.PayrollPlan.Name,
                UvtRangeStart = e.UvtRangeStart,
                UvtRangeEnd = e.UvtRangeEnd,
                Rate = e.Rate,
                AdditionalUvt = e.AdditionalUvt
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<WithholdingParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetWithholdingParameterByIdQuery(Guid PublicId) : IRequest<Result<WithholdingParameterDto>>;

public class GetWithholdingParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWithholdingParameterByIdQuery, Result<WithholdingParameterDto>>
{
    public async Task<Result<WithholdingParameterDto>> Handle(
        GetWithholdingParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.WithholdingParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WithholdingParameterDto
            {
                PublicId = e.PublicId,
                PayrollPlanPublicId = e.PayrollPlan!.PublicId,
                PayrollPlanCode = e.PayrollPlan.Code,
                PayrollPlanName = e.PayrollPlan.Name,
                UvtRangeStart = e.UvtRangeStart,
                UvtRangeEnd = e.UvtRangeEnd,
                Rate = e.Rate,
                AdditionalUvt = e.AdditionalUvt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<WithholdingParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
