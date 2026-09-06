using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.EmployeeTax;

public sealed record WithholdingRateInput(decimal RatePercent, DateTime ValidFrom, DateTime? ValidTo);
public sealed record TaxDeductionInputDto(TaxDeductionKind Kind, decimal? MonthlyAmount, decimal? Percent, DateTime ValidFrom, DateTime? ValidTo);

public sealed record EmployeeWithholdingDto(
    Guid EmployeePublicId,
    byte Procedure,
    IReadOnlyList<WithholdingRateInput> Rates,
    IReadOnlyList<TaxDeductionInputDto> Deductions,
    Guid PlanPublicId,
    string PlanName,
    DateTime? PlanEffectiveFrom,
    string EmployeeClass);

// -------------------------------------------------------------------- consulta --

public sealed record GetEmployeeWithholdingQuery(Guid EmployeePublicId) : IRequest<Result<EmployeeWithholdingDto>>;

public sealed class GetEmployeeWithholdingQueryValidator : AbstractValidator<GetEmployeeWithholdingQuery>
{
    public GetEmployeeWithholdingQueryValidator() => RuleFor(x => x.EmployeePublicId).NotEmpty();
}

public sealed class GetEmployeeWithholdingQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeWithholdingQuery, Result<EmployeeWithholdingDto>>
{
    public async Task<Result<EmployeeWithholdingDto>> Handle(GetEmployeeWithholdingQuery request, CancellationToken ct)
    {
        var e = await db.Employees.AsNoTracking().Include(x => x.PayrollPlan).FirstOrDefaultAsync(x => x.PublicId == request.EmployeePublicId, ct);
        if (e is null) return Result.Failure<EmployeeWithholdingDto>(new Error("Payroll.EmployeeNotFound", "No existe el empleado indicado."));

        var tasas = await db.EmployeeWithholdingRates.AsNoTracking().Where(r => r.EmployeeId == e.Id).OrderByDescending(r => r.ValidFrom)
            .Select(r => new WithholdingRateInput(r.RatePercent, r.ValidFrom, r.ValidTo)).ToListAsync(ct);
        var deducciones = await db.EmployeeTaxDeductions.AsNoTracking().Where(d => d.EmployeeId == e.Id).OrderBy(d => d.Kind).ThenByDescending(d => d.ValidFrom)
            .Select(d => new TaxDeductionInputDto(d.Kind, d.MonthlyAmount, d.Percent, d.ValidFrom, d.ValidTo)).ToListAsync(ct);

        return Result.Success(new EmployeeWithholdingDto(e.PublicId, e.WithholdingProcedure, tasas, deducciones,
            e.PayrollPlan?.PublicId ?? Guid.Empty, e.PayrollPlan?.Name ?? string.Empty, e.PayrollPlanEffectiveFrom, e.EmployeeClass.ToString()));
    }
}

// --------------------------------------------------------------------- comando --

/// <summary>
/// FR-039 (D-11): procedimiento de retención, porcentajes con vigencia (procedimiento 2)
/// y deducciones declaradas con vigencia. Reemplaza el conjunto: lo anterior queda
/// marcado como retirado, no borrado. Evento explícito de auditoría con antes y después.
/// </summary>
public sealed record SetEmployeeWithholdingCommand(
    Guid EmployeePublicId,
    byte Procedure,
    IReadOnlyList<WithholdingRateInput> Rates,
    IReadOnlyList<TaxDeductionInputDto> Deductions,
    EmployeeClass? EmployeeClass = null) : IRequest<Result>;

public sealed class SetEmployeeWithholdingCommandValidator : AbstractValidator<SetEmployeeWithholdingCommand>
{
    public SetEmployeeWithholdingCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.Procedure).Must(p => p is 1 or 2).WithMessage("El procedimiento de retención es 1 ó 2.");
        RuleFor(x => x.Rates).NotNull();
        RuleFor(x => x.Deductions).NotNull();
        RuleForEach(x => x.Rates).ChildRules(r =>
        {
            r.RuleFor(x => x.RatePercent).InclusiveBetween(0m, 100m).WithMessage("El porcentaje va de 0 a 100.");
            r.RuleFor(x => x.ValidFrom).NotEmpty();
            r.RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null);
        });
        RuleForEach(x => x.Deductions).ChildRules(d =>
        {
            d.RuleFor(x => x.Kind).IsInEnum();
            d.RuleFor(x => x).Must(x => x.MonthlyAmount is not null || x.Percent is not null || x.Kind == TaxDeductionKind.Dependents)
                .WithMessage("Indique el valor mensual o el porcentaje de la deducción.");
            d.RuleFor(x => x.ValidFrom).NotEmpty();
            d.RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null);
        });
        RuleFor(x => x.Rates).Must(r => r.Count > 0).When(x => x.Procedure == 2)
            .WithMessage("El procedimiento 2 exige al menos un porcentaje con vigencia.");
    }
}

public sealed class SetEmployeeWithholdingCommandHandler(
    IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, IPayrollRunStaleMarker stale, PayrollAuditEmitter audit)
    : IRequestHandler<SetEmployeeWithholdingCommand, Result>
{
    public async Task<Result> Handle(SetEmployeeWithholdingCommand request, CancellationToken ct)
    {
        var e = await db.Employees.FirstOrDefaultAsync(x => x.PublicId == request.EmployeePublicId, ct);
        if (e is null) return Result.Failure(new Error("Payroll.EmployeeNotFound", "No existe el empleado indicado."));

        var tasas = request.Rates.OrderBy(r => r.ValidFrom).ToList();
        for (var i = 1; i < tasas.Count; i++)
        {
            var anterior = tasas[i - 1];
            if (anterior.ValidTo is null || anterior.ValidTo >= tasas[i].ValidFrom)
                return Result.Failure(new Error("Payroll.WithholdingRateOverlap",
                    $"Los porcentajes con vigencia desde {anterior.ValidFrom:dd/MM/yyyy} y {tasas[i].ValidFrom:dd/MM/yyyy} se solapan."));
        }

        var ahora = clock.UtcNow;
        var antes = new
        {
            procedure = e.WithholdingProcedure,
            employeeClass = e.EmployeeClass.ToString(),
            rates = await db.EmployeeWithholdingRates.Where(r => r.EmployeeId == e.Id).Select(r => new { r.RatePercent, r.ValidFrom, r.ValidTo }).ToListAsync(ct),
            deductions = await db.EmployeeTaxDeductions.Where(d => d.EmployeeId == e.Id).Select(d => new { d.Kind, d.MonthlyAmount, d.Percent, d.ValidFrom, d.ValidTo }).ToListAsync(ct),
        };

        foreach (var r in await db.EmployeeWithholdingRates.Where(r => r.EmployeeId == e.Id).ToListAsync(ct))
        {
            r.IsDeleted = true; r.DeletedAt = ahora; r.DeletedBy = user.UserName;
        }
        foreach (var d in await db.EmployeeTaxDeductions.Where(d => d.EmployeeId == e.Id).ToListAsync(ct))
        {
            d.IsDeleted = true; d.DeletedAt = ahora; d.DeletedBy = user.UserName;
        }
        foreach (var r in tasas)
            db.EmployeeWithholdingRates.Add(new EmployeeWithholdingRate
            {
                EmployeeId = e.Id, RatePercent = r.RatePercent, ValidFrom = r.ValidFrom.Date, ValidTo = r.ValidTo?.Date, CreatedAt = ahora, CreatedBy = user.UserName,
            });
        foreach (var d in request.Deductions)
            db.EmployeeTaxDeductions.Add(new EmployeeTaxDeduction
            {
                EmployeeId = e.Id, Kind = d.Kind, MonthlyAmount = d.MonthlyAmount, Percent = d.Percent, ValidFrom = d.ValidFrom.Date, ValidTo = d.ValidTo?.Date,
                CreatedAt = ahora, CreatedBy = user.UserName,
            });

        e.WithholdingProcedure = request.Procedure;
        if (request.EmployeeClass is { } clase) e.EmployeeClass = clase;
        e.UpdatedAt = ahora;
        e.UpdatedBy = user.UserName;

        var periodosCalculados = await db.PayPeriods.Where(p => p.PayrollPlanId == e.PayrollPlanId && p.Status == PayPeriodStatus.Calculated).Select(p => p.Id).ToListAsync(ct);
        foreach (var pid in periodosCalculados)
            await stale.MarkStaleAsync(pid, $"retención del empleado {e.PublicId} cambiada", ct);

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollEmployeeWithholdingChanged, nameof(Employee), e.PublicId, antes,
            new { procedure = request.Procedure, employeeClass = e.EmployeeClass.ToString(), rates = tasas, deductions = request.Deductions }, ct);
        return Result.Success();
    }
}
