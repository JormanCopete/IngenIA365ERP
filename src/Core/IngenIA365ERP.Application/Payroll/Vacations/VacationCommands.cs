using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Vacations;

// ------------------------------------------------------------------ ajuste --

/// <summary>
/// Un ajuste al saldo de vacaciones con signo y motivo (contracts/api.md §5): días reconocidos por
/// acuerdo, una corrección de lo migrado, un disfrute que no pasó por aquí. Es un movimiento
/// <c>Adjustment</c> que entra al saldo derivado de inmediato; nunca se edita el saldo a mano.
/// </summary>
public sealed record AddVacationAdjustmentCommand(Guid EmployeePublicId, decimal Days, string Reason) : IRequest<Result<Guid>>;

public sealed class AddVacationAdjustmentCommandValidator : AbstractValidator<AddVacationAdjustmentCommand>
{
    public AddVacationAdjustmentCommandValidator()
    {
        RuleFor(x => x.EmployeePublicId).NotEmpty().WithMessage("El empleado es obligatorio.");
        RuleFor(x => x.Days).NotEqual(0m).WithMessage("Indique los días del ajuste, con signo: positivos suman al saldo, negativos lo restan.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo del ajuste es obligatorio.").MaximumLength(300);
    }
}

public sealed class AddVacationAdjustmentCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<AddVacationAdjustmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddVacationAdjustmentCommand request, CancellationToken ct)
    {
        var empleado = await db.Employees.FirstOrDefaultAsync(e => e.PublicId == request.EmployeePublicId && !e.IsDeleted, ct);
        if (empleado is null) return Result.Failure<Guid>(SettlementErrors.EmployeeNotFound);

        var ahora = clock.UtcNow;
        var movimiento = new VacationMovement
        {
            EmployeeId = empleado.Id,
            Kind = VacationMovementKind.Adjustment,
            StartDate = clock.TodayUtc,
            EndDate = null,
            BusinessDays = request.Days,
            CalendarDays = 0,
            WeekPolicyUsed = string.Empty,
            SkippedDaysJson = "[]",
            Status = VacationMovementStatus.Registered,
            Notes = request.Reason.Trim(),
            CreatedAt = ahora,
            CreatedBy = user.UserName,
        };
        db.VacationMovements.Add(movimiento);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollVacationRegistered, nameof(VacationMovement), movimiento.PublicId, null,
            new { kind = "Adjustment", employeePublicId = empleado.PublicId, days = request.Days, reason = movimiento.Notes }, ct);
        return Result.Success(movimiento.PublicId);
    }
}

// ----------------------------------------------------------------- cancelar --

/// <summary>
/// Anula un movimiento registrado con motivo (contracts/api.md §5). Uno ya liquidado por una
/// corrida aprobada no se anula: se reversa la corrida (<c>MovementConfirmed</c>). Si el
/// movimiento tiene un borrador de liquidación esperando, el borrador queda descartado en la
/// misma acción por el ciclo común (<see cref="SettlementRunWorkflow"/>). Las novedades
/// <c>VacationLeave</c> que hubiera dejado se anulan en los períodos aún abiertos; una en período
/// aprobado no se toca y se rechaza con <c>PeriodApproved</c> y el período abierto siguiente.
/// </summary>
public sealed record CancelVacationMovementCommand(Guid MovementPublicId, string Reason) : IRequest<Result>;

public sealed class CancelVacationMovementCommandValidator : AbstractValidator<CancelVacationMovementCommand>
{
    public CancelVacationMovementCommandValidator()
    {
        RuleFor(x => x.MovementPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo de la anulación es obligatorio.").MaximumLength(300);
    }
}

public sealed class CancelVacationMovementCommandHandler(
    IApplicationDbContext db,
    SettlementRunWorkflow workflow,
    VacationNoveltyPlanner planner,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<CancelVacationMovementCommand, Result>
{
    public async Task<Result> Handle(CancelVacationMovementCommand request, CancellationToken ct)
    {
        var movimiento = await db.VacationMovements.Include(m => m.Employee).FirstOrDefaultAsync(m => m.PublicId == request.MovementPublicId, ct);
        if (movimiento is null) return Result.Failure(SettlementErrors.VacationMovementNotFound);
        if (movimiento.Status == VacationMovementStatus.Liquidated) return Result.Failure(SettlementErrors.VacationMovementConfirmed);
        if (movimiento.Status == VacationMovementStatus.Cancelled)
            return Result.Failure(new Error("Payroll.Vacation.MovementCancelled", "El movimiento ya está anulado."));

        var motivo = request.Reason.Trim();

        // Una novedad que ya se pagó en un período aprobado no se puede deshacer desde aquí.
        var enAprobados = await db.PayrollNovelties.AsNoTracking().Include(n => n.PayPeriod)
            .Where(n => n.VacationMovementId == movimiento.Id && n.Status == NoveltyStatus.Active && n.PayPeriod!.Status == PayPeriodStatus.Approved)
            .Select(n => n.PayPeriod!)
            .FirstOrDefaultAsync(ct);
        if (enAprobados is not null)
        {
            var destino = await db.PayPeriods.AsNoTracking()
                .Where(x => x.PayrollPlanId == enAprobados.PayrollPlanId && x.StartDate > enAprobados.EndDate && (x.Status == PayPeriodStatus.Open || x.Status == PayPeriodStatus.Calculated))
                .OrderBy(x => x.StartDate).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct);
            return Result.Failure(SettlementErrors.VacationPeriodApproved(enAprobados.PublicId, destino));
        }

        var borrador = await db.PayrollRuns
            .Where(r => r.VacationMovementId == movimiento.Id && (r.Status == PayrollRunStatus.Draft || r.Status == PayrollRunStatus.Stale))
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(ct);

        async Task<Result> Anular(CancellationToken token)
        {
            var (anuladas, _) = await planner.AnularAsync(movimiento, $"{VacationNoveltyPlanner.AnuladaPorCancelacion}: {motivo}", token);
            movimiento.Status = VacationMovementStatus.Cancelled;
            movimiento.CancelReason = motivo;
            movimiento.PayrollRunId = null;
            movimiento.UpdatedAt = clock.UtcNow;
            movimiento.UpdatedBy = user.UserName;
            return Result.Success();
        }

        if (borrador is not null)
        {
            // El borrador y el movimiento caen juntos, en el SaveChanges del ciclo común.
            var descartado = await workflow.DiscardAsync(borrador.PublicId, PayrollRunKind.Vacation, motivo, (_, _, token) => Anular(token), ct);
            if (descartado.IsFailure) return Result.Failure(descartado.Error);
        }
        else
        {
            await Anular(ct);
            await db.SaveChangesAsync(ct);
        }

        await audit.EmitAsync(AuditEventTypes.PayrollVacationCancelled, nameof(VacationMovement), movimiento.PublicId,
            new { status = "Registered", kind = movimiento.Kind.ToString() },
            new { status = "Cancelled", reason = motivo, employeePublicId = movimiento.Employee?.PublicId, discardedRunPublicId = borrador?.PublicId }, ct);
        return Result.Success();
    }
}
