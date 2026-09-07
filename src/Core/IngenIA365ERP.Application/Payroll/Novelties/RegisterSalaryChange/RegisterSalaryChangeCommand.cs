using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Novelties.RegisterSalaryChange;

/// <summary>
/// Cambio de salario con fecha de efecto (FR-004): una fila más en el historial
/// (<c>PAY_SalaryChanges</c>), el salario de la ficha como espejo del último vigente, y el
/// borrador de los períodos que tocan la fecha marcado como desactualizado. No se acepta
/// una fecha dentro de un período aprobado del plan del empleado: eso se liquida como
/// retroactivo. Si el empleado no tenía historial, se siembra primero la línea base con el
/// salario de la ficha desde su ingreso, para que los tramos siempre sepan cuánto ganaba
/// antes del cambio.
/// </summary>
public sealed record RegisterSalaryChangeCommand(Guid EmployeePublicId, decimal NewSalary, DateTime EffectiveFrom, string Reason)
    : IRequest<Result<long>>;

public sealed class RegisterSalaryChangeCommandValidator : AbstractValidator<RegisterSalaryChangeCommand>
{
    public RegisterSalaryChangeCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty();
        RuleFor(x => x.NewSalary).GreaterThan(0m).WithMessage("El salario debe ser mayor que cero.");
        RuleFor(x => x.EffectiveFrom).NotEmpty().WithMessage("La fecha de efecto es obligatoria.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(300);
    }
}

public sealed class RegisterSalaryChangeCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    IPayrollRunStaleMarker staleMarker,
    PayrollAuditEmitter audit)
    : IRequestHandler<RegisterSalaryChangeCommand, Result<long>>
{
    public async Task<Result<long>> Handle(RegisterSalaryChangeCommand request, CancellationToken ct)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId, ct);
        if (employee is null)
            return Result.Failure<long>(new Error("Payroll.EmployeeNotFound", "No existe el empleado indicado."));

        var efecto = request.EffectiveFrom.Date;
        if (efecto < employee.JoinDate.Date)
            return Result.Failure<long>(new Error("Payroll.SalaryChangeBeforeJoin",
                $"La fecha de efecto ({efecto:dd/MM/yyyy}) es anterior al ingreso del empleado ({employee.JoinDate:dd/MM/yyyy})."));

        var aprobado = await db.PayPeriods.AsNoTracking()
            .Where(p => p.PayrollPlanId == employee.PayrollPlanId && p.Status == PayPeriodStatus.Approved
                        && p.StartDate <= efecto && p.EndDate >= efecto)
            .Select(p => new { p.StartDate, p.EndDate })
            .FirstOrDefaultAsync(ct);
        if (aprobado is not null)
            return Result.Failure<long>(new Error("Payroll.SalaryChangeInApprovedPeriod",
                $"La fecha de efecto cae en el período aprobado {aprobado.StartDate:dd/MM/yyyy}–{aprobado.EndDate:dd/MM/yyyy}. " +
                "Registre el cambio con efecto en el período abierto y la diferencia como ajuste retroactivo."));

        var yaExiste = await db.SalaryChanges.AnyAsync(s => s.EmployeeId == employee.Id && s.EffectiveDate == efecto, ct);
        if (yaExiste)
            return Result.Failure<long>(new Error("Payroll.SalaryChangeDuplicate",
                $"Ya hay un cambio de salario con efecto el {efecto:dd/MM/yyyy}; corríjalo desde el historial."));

        var ahora = clock.UtcNow;
        var salarioAnterior = employee.Salary;

        var hayHistorial = await db.SalaryChanges.AnyAsync(s => s.EmployeeId == employee.Id, ct);
        if (!hayHistorial && employee.Salary > 0m && efecto > employee.JoinDate.Date)
        {
            db.SalaryChanges.Add(new SalaryChange
            {
                PayrollCompanyId = employee.PayrollCompanyId,
                EmployeeId = employee.Id,
                EffectiveDate = employee.JoinDate.Date,
                NewSalary = employee.Salary,
                UserName = SalaryChange.RecortarUsuario(user.UserName),
                EntryDate = ahora,
                CreatedAt = ahora,
                CreatedBy = "system:baseline",
            });
        }

        var cambio = new SalaryChange
        {
            PayrollCompanyId = employee.PayrollCompanyId,
            EmployeeId = employee.Id,
            EffectiveDate = efecto,
            NewSalary = request.NewSalary,
            UserName = SalaryChange.RecortarUsuario(user.UserName),
            EntryDate = ahora,
            CreatedAt = ahora,
            CreatedBy = user.UserName,
        };
        db.SalaryChanges.Add(cambio);

        // Espejo: el salario de la ficha es el último vigente a hoy.
        var ultimoVigente = await db.SalaryChanges.AsNoTracking()
            .Where(s => s.EmployeeId == employee.Id && s.EffectiveDate <= clock.TodayUtc.ToDateTime(TimeOnly.MinValue))
            .OrderByDescending(s => s.EffectiveDate)
            .Select(s => (decimal?)s.NewSalary)
            .FirstOrDefaultAsync(ct);
        if (efecto <= clock.TodayUtc.ToDateTime(TimeOnly.MinValue))
            employee.Salary = request.NewSalary;
        else if (ultimoVigente is { } v)
            employee.Salary = v;
        employee.EffectiveDate = efecto;
        employee.UpdatedAt = ahora;
        employee.UpdatedBy = user.UserName;

        // Borradores de los períodos del plan que incluyen la fecha o son posteriores.
        var periodosAfectados = await db.PayPeriods
            .Where(p => p.PayrollPlanId == employee.PayrollPlanId && p.EndDate >= efecto && p.Status == PayPeriodStatus.Calculated)
            .Select(p => p.Id)
            .ToListAsync(ct);
        foreach (var periodId in periodosAfectados)
            await staleMarker.MarkStaleAsync(periodId, $"cambio de salario de {employee.PublicId} con efecto {efecto:yyyy-MM-dd}", ct);

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollSalaryChanged, nameof(Employee), employee.PublicId,
            new { salary = salarioAnterior },
            new { salary = request.NewSalary, effectiveFrom = efecto, reason = request.Reason.Trim(), salaryChangeId = cambio.Id }, ct);

        return Result.Success(cambio.Id);
    }
}
