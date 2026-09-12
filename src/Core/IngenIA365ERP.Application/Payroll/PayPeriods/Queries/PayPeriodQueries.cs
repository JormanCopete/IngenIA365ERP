using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayPeriods.Queries;

/// <summary>
/// Período de pago hacia afuera: el plan al que pertenece y el estado en texto
/// (<c>Open</c>, <c>Calculated</c>, <c>Approved</c>, <c>Reversed</c>), nunca el número.
/// </summary>
public record PayPeriodDto
{
    public Guid PublicId { get; init; }
    public Guid PlanPublicId { get; init; }
    public string PlanCode { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public string PlanPeriodicity { get; init; } = string.Empty;
    public int PlanId { get; init; }
    public int PayrollCompanyId { get; init; }
    public string? Description { get; init; }
    public string? PayDate { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int? Periodicity { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusMessage { get; init; } = string.Empty;
    public DateTime? ApprovedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public Guid? CurrentRunPublicId { get; init; }
    public int PeriodId { get; init; }
    public byte SubPeriodNumber { get; init; }
    public short ImputationYear { get; init; }
    public byte ImputationMonth { get; init; }
    /// <summary>«Quincena 2», «Semana 3», «Mes»…</summary>
    public string SubPeriodLabel => Enum.TryParse<PayrollPeriodicity>(PlanPeriodicity, out var p)
        ? PeriodCalendar.Etiqueta(p, SubPeriodNumber)
        : SubPeriodNumber.ToString();
}

public record ListPayPeriodsQuery : IRequest<Result<PagedList<PayPeriodDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }

    /// <summary>Plan de nómina; nulo = todos.</summary>
    public Guid? PlanId { get; init; }

    /// <summary>Open | Calculated | Approved | Reversed; nulo = todos.</summary>
    public string? Status { get; init; }
}

public class ListPayPeriodsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPayPeriodsQuery, Result<PagedList<PayPeriodDto>>>
{
    public async Task<Result<PagedList<PayPeriodDto>>> Handle(
        ListPayPeriodsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PayPeriods.AsNoTracking().AsQueryable();

        if (request.PlanId is Guid planId)
            query = query.Where(e => e.PayrollPlan!.PublicId == planId);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<PayPeriodStatus>(request.Status, ignoreCase: true, out var status))
            {
                return Result.Failure<PagedList<PayPeriodDto>>(new Error("Payroll.InvalidStatus",
                    "Estado no válido. Valores: Open, Calculated, Approved, Reversed."));
            }
            query = query.Where(e => e.Status == status);
        }

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
            .Select(Proyeccion)
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PayPeriodDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<Domain.Entities.Payroll.PayPeriod, PayPeriodDto>> Proyeccion =
        e => new PayPeriodDto
        {
            PublicId = e.PublicId,
            PlanPublicId = e.PayrollPlan!.PublicId,
            PlanCode = e.PayrollPlan.Code,
            PlanName = e.PayrollPlan.Name,
            PlanPeriodicity = e.PayrollPlan.Periodicity.ToString(),
            PlanId = e.PlanId,
            PayrollCompanyId = e.PayrollCompanyId,
            Description = e.Description,
            PayDate = e.PayDate,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Periodicity = e.Periodicity,
            SubPeriodNumber = e.SubPeriodNumber,
            ImputationYear = e.ImputationYear,
            ImputationMonth = e.ImputationMonth,
            Status = e.Status.ToString(),
            StatusMessage = e.StatusMessage,
            ApprovedAt = e.ApprovedAt,
            ApprovedBy = e.ApprovedBy,
            CurrentRunPublicId = e.RunPublicId,
            PeriodId = e.PeriodId
        };
}

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
            .Where(e => e.PublicId == request.PublicId)
            .Select(ListPayPeriodsQueryHandler.Proyeccion)
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PayPeriodDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
