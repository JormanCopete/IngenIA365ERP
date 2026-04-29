using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayPeriods.Queries;

// DTO
public record PayPeriodDto
{
    public Guid PublicId { get; init; }
    public int PlanId { get; init; }
    public int PayrollCompanyId { get; init; }
    public string? Description { get; init; }
    public string? PayDate { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? Periodicity { get; init; }
    public int Status { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public int PeriodId { get; init; }
}

// List Query
public record ListPayPeriodsQuery : IRequest<Result<PagedList<PayPeriodDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPayPeriodsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPayPeriodsQuery, Result<PagedList<PayPeriodDto>>>
{
    public async Task<Result<PagedList<PayPeriodDto>>> Handle(
        ListPayPeriodsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PayPeriods
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.StartDate.Year.ToString().Contains(term)
                || (e.Description != null && e.Description.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "startdate" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.StartDate)
                : query.OrderBy(e => e.StartDate),
            "periodid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.PeriodId)
                : query.OrderBy(e => e.PeriodId),
            _ => query.OrderByDescending(e => e.StartDate)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new PayPeriodDto
            {
                PublicId = e.PublicId,
                PlanId = e.PlanId,
                PayrollCompanyId = e.PayrollCompanyId,
                Description = e.Description,
                PayDate = e.PayDate,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Periodicity = e.Periodicity,
                Status = e.Status,
                StatusMessage = e.StatusMessage,
                PeriodId = e.PeriodId
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PayPeriodDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPayPeriodByIdQuery(Guid PublicId) : IRequest<Result<PayPeriodDto>>;

public class GetPayPeriodByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayPeriodByIdQuery, Result<PayPeriodDto>>
{
    public async Task<Result<PayPeriodDto>> Handle(
        GetPayPeriodByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PayPeriods
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PayPeriodDto
            {
                PublicId = e.PublicId,
                PlanId = e.PlanId,
                PayrollCompanyId = e.PayrollCompanyId,
                Description = e.Description,
                PayDate = e.PayDate,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                Periodicity = e.Periodicity,
                Status = e.Status,
                StatusMessage = e.StatusMessage,
                PeriodId = e.PeriodId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PayPeriodDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
