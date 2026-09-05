using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Plans.Queries;

public sealed record PayrollPlanDto(
    Guid PublicId,
    string Code,
    string Name,
    string Periodicity,
    bool IsDefault,
    bool IsActive,
    int EmployeeCount);

public sealed record ListPayrollPlansQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<PayrollPlanDto>>>;

public sealed class ListPayrollPlansQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListPayrollPlansQuery, Result<IReadOnlyList<PayrollPlanDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollPlanDto>>> Handle(ListPayrollPlansQuery request, CancellationToken ct)
    {
        var planes = db.PayrollPlans.AsNoTracking();
        if (!request.IncludeInactive) planes = planes.Where(p => p.IsActive);

        var lista = await planes
            .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Code)
            .Select(p => new PayrollPlanDto(
                p.PublicId, p.Code, p.Name, p.Periodicity.ToString(), p.IsDefault, p.IsActive,
                db.Employees.Count(e => e.PayrollPlanId == p.Id && e.Status != -1)))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<PayrollPlanDto>>(lista);
    }
}

/// <summary>Principio VIII: sin reglas, pero la consulta existe en el catálogo de validación.</summary>
public sealed class ListPayrollPlansQueryValidator : AbstractValidator<ListPayrollPlansQuery>;
