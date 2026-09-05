using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Plans;

// ------------------------------------------------------------------ crear plan --

/// <summary>Crea un plan de nómina (FR-037). El código es inmutable y único por cooperativa.</summary>
public sealed record CreatePayrollPlanCommand(string Code, string Name, PayrollPeriodicity Periodicity) : IRequest<Result<Guid>>;

public sealed class CreatePayrollPlanCommandValidator : AbstractValidator<CreatePayrollPlanCommand>
{
    public CreatePayrollPlanCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.")
            .MaximumLength(20).WithMessage("El código no puede pasar de 20 caracteres.")
            .Matches("^[A-Z0-9_]+$").WithMessage("El código sólo admite mayúsculas, dígitos y guion bajo.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede pasar de 100 caracteres.");
        RuleFor(x => x.Periodicity).IsInEnum().WithMessage("La periodicidad debe ser Monthly o Biweekly.");
    }
}

public sealed class CreatePayrollPlanCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<CreatePayrollPlanCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePayrollPlanCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var existe = await db.PayrollPlans.IgnoreQueryFilters().AnyAsync(p => p.Code == code, ct);
        if (existe)
            return Result.Failure<Guid>(new Error("Payroll.PlanCodeDuplicate", $"Ya existe un plan de nómina con el código {code}."));

        var hayPorDefecto = await db.PayrollPlans.AnyAsync(p => p.IsDefault, ct);
        var plan = new PayrollPlan
        {
            Code = code,
            Name = request.Name.Trim(),
            Periodicity = request.Periodicity,
            IsDefault = !hayPorDefecto,
            IsActive = true,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName,
        };
        db.PayrollPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return Result.Success(plan.PublicId);
    }
}

// -------------------------------------------------------------- actualizar plan --

/// <summary>Cambia nombre y estado. Un plan con empleados no se desactiva; el código no cambia.</summary>
public sealed record UpdatePayrollPlanCommand(Guid PublicId, string Name, bool IsActive) : IRequest<Result>;

public sealed class UpdatePayrollPlanCommandValidator : AbstractValidator<UpdatePayrollPlanCommand>
{
    public UpdatePayrollPlanCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede pasar de 100 caracteres.");
    }
}

public sealed class UpdatePayrollPlanCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<UpdatePayrollPlanCommand, Result>
{
    public async Task<Result> Handle(UpdatePayrollPlanCommand request, CancellationToken ct)
    {
        var plan = await db.PayrollPlans.FirstOrDefaultAsync(p => p.PublicId == request.PublicId, ct);
        if (plan is null)
            return Result.Failure(new Error("Payroll.PlanNotFound", "No existe el plan de nómina indicado."));

        if (!request.IsActive && plan.IsActive)
        {
            if (plan.IsDefault)
                return Result.Failure(new Error("Payroll.PlanHasEmployees", "El plan por defecto no se puede desactivar."));
            var conEmpleados = await db.Employees.AnyAsync(e => e.PayrollPlanId == plan.Id && e.Status != -1, ct);
            if (conEmpleados)
                return Result.Failure(new Error("Payroll.PlanHasEmployees",
                    $"El plan «{plan.Name}» tiene empleados vigentes: cámbielos de plan antes de desactivarlo."));
        }

        plan.Name = request.Name.Trim();
        plan.IsActive = request.IsActive;
        plan.UpdatedAt = clock.UtcNow;
        plan.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ------------------------------------------------------- cambiar plan del empleado --

/// <summary>
/// Cambia el plan de nómina de un empleado con fecha de efecto (edge case de la spec): el
/// empleado termina el período abierto de su plan actual y entra al plan nuevo desde la
/// fecha de efecto, que no puede caer dentro de un período abierto o calculado del plan
/// actual (así nunca aparece en dos períodos abiertos ni se le liquidan días dos veces).
/// </summary>
public sealed record ChangeEmployeePlanCommand(Guid EmployeePublicId, Guid PlanPublicId, DateTime EffectiveFrom) : IRequest<Result>;

public sealed class ChangeEmployeePlanCommandValidator : AbstractValidator<ChangeEmployeePlanCommand>
{
    public ChangeEmployeePlanCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.PlanPublicId).NotEmpty();
        RuleFor(x => x.EffectiveFrom).NotEmpty().WithMessage("La fecha de efecto es obligatoria.");
    }
}

public sealed class ChangeEmployeePlanCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<ChangeEmployeePlanCommand, Result>
{
    public async Task<Result> Handle(ChangeEmployeePlanCommand request, CancellationToken ct)
    {
        var empleado = await db.Employees.FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId, ct);
        if (empleado is null)
            return Result.Failure(new Error("Payroll.EmployeeNotFound", "No existe el empleado indicado."));

        var plan = await db.PayrollPlans.FirstOrDefaultAsync(p => p.PublicId == request.PlanPublicId, ct);
        if (plan is null)
            return Result.Failure(new Error("Payroll.PlanNotFound", "No existe el plan de nómina indicado."));
        if (!plan.IsActive)
            return Result.Failure(new Error("Payroll.PlanInactive", $"El plan «{plan.Name}» está inactivo."));
        if (plan.Id == empleado.PayrollPlanId)
            return Result.Success();

        var efecto = request.EffectiveFrom.Date;
        var periodoAbierto = await db.PayPeriods
            .Where(p => p.PayrollPlanId == empleado.PayrollPlanId
                        && (p.Status == PayPeriodStatus.Open || p.Status == PayPeriodStatus.Calculated)
                        && p.EndDate >= efecto)
            .OrderBy(p => p.EndDate)
            .FirstOrDefaultAsync(ct);
        if (periodoAbierto is not null)
        {
            return Result.Failure(new Error("Payroll.PlanChangeInsideOpenPeriod",
                $"El plan actual tiene el período {periodoAbierto.StartDate:dd/MM/yyyy}–{periodoAbierto.EndDate:dd/MM/yyyy} abierto: " +
                $"el cambio rige desde el siguiente período. Use una fecha de efecto posterior al {periodoAbierto.EndDate:dd/MM/yyyy} " +
                "o apruebe ese período antes."));
        }

        empleado.PayrollPlanId = plan.Id;
        empleado.PayrollPlanEffectiveFrom = efecto;
        empleado.UpdatedAt = clock.UtcNow;
        empleado.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
