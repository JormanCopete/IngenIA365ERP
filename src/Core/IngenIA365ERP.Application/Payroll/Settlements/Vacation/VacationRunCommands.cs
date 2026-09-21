using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Vacation;

/// <summary>Lo que los comandos de la corrida de vacaciones comparten: la corrida con su movimiento y su empleado.</summary>
internal static class VacationRunLookup
{
    public static async Task<Result<(Domain.Entities.Payroll.Transactions.PayrollRun Run, VacationMovement Movimiento, Employee Empleado)>> BuscarAsync(
        IApplicationDbContext db, Guid runPublicId, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.PublicId == runPublicId, ct);
        if (run is null) return Result.Failure<(Domain.Entities.Payroll.Transactions.PayrollRun, VacationMovement, Employee)>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Vacation)
            return Result.Failure<(Domain.Entities.Payroll.Transactions.PayrollRun, VacationMovement, Employee)>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Vacation));
        var movimiento = run.VacationMovementId is { } id ? await db.VacationMovements.FirstOrDefaultAsync(m => m.Id == id, ct) : null;
        if (movimiento is null)
            return Result.Failure<(Domain.Entities.Payroll.Transactions.PayrollRun, VacationMovement, Employee)>(SettlementErrors.VacationMovementNotFound);
        var empleado = await db.Employees.Include(e => e.Person).FirstOrDefaultAsync(e => e.Id == movimiento.EmployeeId, ct);
        if (empleado is null)
            return Result.Failure<(Domain.Entities.Payroll.Transactions.PayrollRun, VacationMovement, Employee)>(SettlementErrors.EmployeeNotFound);
        return Result.Success((run, movimiento, empleado));
    }
}

// -------------------------------------------------------------- recalcular --

/// <summary>Versión nueva del borrador de una liquidación de vacaciones; la anterior queda <c>Superseded</c> (Principio XI). El movimiento no cambia.</summary>
public sealed record RecalculateVacationCommand(Guid RunPublicId, bool AcceptRetroactive = false) : IRequest<Result<VacationCalculatedDto>>;

public sealed class RecalculateVacationCommandValidator : AbstractValidator<RecalculateVacationCommand>
{
    public RecalculateVacationCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class RecalculateVacationCommandHandler(
    IApplicationDbContext db,
    VacationNoveltyPlanner planner,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    PayrollAuditEmitter audit)
    : IRequestHandler<RecalculateVacationCommand, Result<VacationCalculatedDto>>
{
    public async Task<Result<VacationCalculatedDto>> Handle(RecalculateVacationCommand request, CancellationToken ct)
    {
        var busqueda = await VacationRunLookup.BuscarAsync(db, request.RunPublicId, ct);
        if (busqueda.IsFailure) return Result.Failure<VacationCalculatedDto>(busqueda.Error);
        var (anterior, movimiento, empleado) = busqueda.Value;
        if (!anterior.IsEditableDraft) return Result.Failure<VacationCalculatedDto>(SettlementErrors.NotDraft(anterior.Status));
        if (movimiento.Status != VacationMovementStatus.Registered)
            return Result.Failure<VacationCalculatedDto>(new Error("Payroll.Vacation.MovementCancelled", "El movimiento de este borrador está anulado: registre el disfrute de nuevo."));

        var corte = anterior.CutoffDate!.Value;
        var key = SettlementRunKey.Vacaciones(empleado.Id, corte, movimiento.Id);
        var anteriores = await persister.CorridasDeAsync(key, ct);
        if (SettlementRunPersister.Duplicado(anteriores, key, recalculo: true) is { } duplicado)
            return Result.Failure<VacationCalculatedDto>(duplicado);

        var plan = movimiento.Kind == VacationMovementKind.Enjoyment
            ? await planner.PlanearAsync(empleado, movimiento.StartDate, movimiento.EndDate!.Value, corte, request.AcceptRetroactive, ct)
            : Result.Success(new PlanDeNovedades([], [], string.Empty));
        if (plan.IsFailure) return Result.Failure<VacationCalculatedDto>(plan.Error);

        var calculo = await VacationRunCalculator.CalcularAsync(loader, persister, empleado, movimiento, corte, key, anteriores, recalculo: true, ct);
        if (calculo.IsFailure) return Result.Failure<VacationCalculatedDto>(calculo.Error);
        var (run, batch, calculados) = calculo.Value;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollSettlementCalculated, nameof(Domain.Entities.Payroll.Transactions.PayrollRun), run.PublicId,
            new { version = anterior.Version, status = anterior.Status.ToString() },
            new { kind = "Vacation", cutoffDate = corte, version = run.Version, employeePublicId = empleado.PublicId, movementPublicId = movimiento.PublicId, totalNet = run.TotalNet }, ct);

        var avisos = new List<WarningDto>(plan.Value.Warnings);
        avisos.AddRange(batch.Warnings);
        return Result.Success(VacationRunCalculator.Respuesta(run, movimiento, SkippedDayDto.Leer(movimiento.SkippedDaysJson), plan.Value, calculados, avisos));
    }
}

// ----------------------------------------------------------------- aprobar --

/// <summary>
/// Aprueba la liquidación de vacaciones por el ciclo común (<see cref="SettlementRunWorkflow"/>):
/// comprobante <c>VacationRun</c> contra <c>PROV_VACACIONES</c> fechado al corte (D-04), y en la
/// misma transacción el movimiento pasa a <c>Liquidated</c> y queda la novedad en cada período que
/// cubre el disfrute (<c>AUSENCIA_VACACIONES</c> o <c>VACACIONES</c> según la política, D-01).
/// Un disfrute con días que ningún período del plan cubre —ni por traslado del último existente
/// (FR-003)— no se aprueba: <c>Payroll.Vacation.PeriodMissing</c> con los tramos (D-31), porque
/// nadie crearía después esa novedad y la ordinaria pagaría los días como salario. Registrar sólo avisa.
/// </summary>
public sealed record ApproveVacationCommand(
    Guid RunPublicId,
    bool Confirm,
    DateOnly? PostingDate = null,
    bool ConfirmEmpty = false,
    bool ConfirmWithoutSegregation = false,
    bool AcceptRetroactive = false) : IRequest<Result<SettlementApprovedDto>>;

public sealed class ApproveVacationCommandValidator : AbstractValidator<ApproveVacationCommand>
{
    public ApproveVacationCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ApproveVacationCommandHandler(
    IApplicationDbContext db,
    SettlementRunWorkflow workflow,
    VacationNoveltyPlanner planner,
    PayrollPolicyReader policies,
    IDateTimeService clock,
    ICurrentUserService user)
    : IRequestHandler<ApproveVacationCommand, Result<SettlementApprovedDto>>
{
    public async Task<Result<SettlementApprovedDto>> Handle(ApproveVacationCommand request, CancellationToken ct)
    {
        var busqueda = await VacationRunLookup.BuscarAsync(db, request.RunPublicId, ct);
        if (busqueda.IsFailure) return Result.Failure<SettlementApprovedDto>(busqueda.Error);
        var (run, movimiento, empleado) = busqueda.Value;
        if (movimiento.Status != VacationMovementStatus.Registered)
            return Result.Failure<SettlementApprovedDto>(new Error("Payroll.Vacation.MovementCancelled", "El movimiento de esta liquidación está anulado: no se aprueba."));

        // D-01: con «paga la nómina ordinaria» la liquidación del disfrute sólo registra el movimiento y la
        // novedad; no tiene líneas contables y se aprueba sin comprobante.
        var politicas = await policies.LeerAsync(run.CutoffDate!.Value, ct);
        if (politicas.IsFailure) return Result.Failure<SettlementApprovedDto>(politicas.Error);
        var sinContabilidad = movimiento.Kind == VacationMovementKind.Enjoyment && !politicas.Value.VacacionesPagoAnticipado;

        return await workflow.ApproveAsync(
            new SettlementApprovalRequest(request.RunPublicId, PayrollRunKind.Vacation, request.Confirm, request.PostingDate, request.ConfirmEmpty, request.ConfirmWithoutSegregation, AllowNothingToPost: sinContabilidad),
            async (corrida, _, token) =>
            {
                if (movimiento.Kind == VacationMovementKind.Enjoyment)
                {
                    var plan = await planner.PlanearAsync(empleado, movimiento.StartDate, movimiento.EndDate!.Value, corrida.CutoffDate!.Value, request.AcceptRetroactive, token);
                    if (plan.IsFailure) return Result.Failure(plan.Error);
                    if (plan.Value.SinPeriodo.Count > 0)
                        return Result.Failure(SettlementErrors.VacationPeriodMissing(plan.Value.SinPeriodo.Select(h => (h.From, h.To))));
                    var creadas = await planner.CrearAsync(empleado, movimiento, plan.Value, token);
                    if (creadas.IsFailure) return Result.Failure(creadas.Error);
                }
                movimiento.Status = VacationMovementStatus.Liquidated;
                movimiento.PayrollRunId = corrida.Id;
                movimiento.UpdatedAt = clock.UtcNow;
                movimiento.UpdatedBy = user.UserName;
                return Result.Success();
            }, ct);
    }
}

// ---------------------------------------------------------------- reversar --

/// <summary>
/// Asiento espejo por el ciclo común; el movimiento vuelve a <c>Registered</c> y sus novedades se
/// anulan. Si la nómina ordinaria de un período cubierto ya está aprobada —ya descontó los días—,
/// la reversión se rechaza con <c>Payroll.Vacation.NoveltyAlreadyPaid</c> (D-32): reversada, el
/// disfrute quedaría con la novedad viva en un período inmutable y sin salida (no se puede anular,
/// recalcular ni registrar de nuevo). El camino es reversar antes esa nómina, que reabre el período.
/// </summary>
public sealed record ReverseVacationCommand(Guid RunPublicId, string Reason) : IRequest<Result<SettlementReversedDto>>;

public sealed class ReverseVacationCommandValidator : AbstractValidator<ReverseVacationCommand>
{
    public ReverseVacationCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo de la reversión es obligatorio.").MaximumLength(300);
    }
}

public sealed class ReverseVacationCommandHandler(
    IApplicationDbContext db,
    SettlementRunWorkflow workflow,
    VacationNoveltyPlanner planner,
    IDateTimeService clock,
    ICurrentUserService user)
    : IRequestHandler<ReverseVacationCommand, Result<SettlementReversedDto>>
{
    public async Task<Result<SettlementReversedDto>> Handle(ReverseVacationCommand request, CancellationToken ct)
    {
        var busqueda = await VacationRunLookup.BuscarAsync(db, request.RunPublicId, ct);
        if (busqueda.IsFailure) return Result.Failure<SettlementReversedDto>(busqueda.Error);
        var (run, movimiento, _) = busqueda.Value;

        if (run.Status == PayrollRunStatus.Approved)
        {
            var pagada = await db.PayrollNovelties.AsNoTracking()
                .Where(n => n.VacationMovementId == movimiento.Id && n.Status == NoveltyStatus.Active
                            && n.PayPeriod!.Status != PayPeriodStatus.Open && n.PayPeriod.Status != PayPeriodStatus.Calculated)
                .OrderBy(n => n.PayPeriod!.StartDate)
                .Select(n => new { n.PayPeriodId, n.PayPeriod!.PublicId })
                .FirstOrDefaultAsync(ct);
            if (pagada is not null)
            {
                var ordinaria = await db.PayrollRuns.AsNoTracking()
                    .Where(r => r.PayPeriodId == pagada.PayPeriodId && r.Status == PayrollRunStatus.Approved)
                    .Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct);
                return Result.Failure<SettlementReversedDto>(SettlementErrors.VacationNoveltyAlreadyPaid(pagada.PublicId, ordinaria));
            }
        }

        return await workflow.ReverseAsync(request.RunPublicId, PayrollRunKind.Vacation, request.Reason,
            async (_, _, token) =>
            {
                await planner.AnularAsync(movimiento, $"{VacationNoveltyPlanner.AnuladaPorReversion}: {request.Reason.Trim()}", token);
                movimiento.Status = VacationMovementStatus.Registered;
                movimiento.PayrollRunId = null;
                movimiento.UpdatedAt = clock.UtcNow;
                movimiento.UpdatedBy = user.UserName;
                return Result.Success();
            }, ct);
    }
}

// --------------------------------------------------------------- descartar --

/// <summary>El borrador queda <c>Superseded</c> sin contabilidad y el movimiento <c>Cancelled</c> con el motivo (contracts/api.md §3.3).</summary>
public sealed record DiscardVacationCommand(Guid RunPublicId, string Reason) : IRequest<Result<SettlementDiscardedDto>>;

public sealed class DiscardVacationCommandValidator : AbstractValidator<DiscardVacationCommand>
{
    public DiscardVacationCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(300);
    }
}

public sealed class DiscardVacationCommandHandler(
    IApplicationDbContext db,
    SettlementRunWorkflow workflow,
    VacationNoveltyPlanner planner,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<DiscardVacationCommand, Result<SettlementDiscardedDto>>
{
    public async Task<Result<SettlementDiscardedDto>> Handle(DiscardVacationCommand request, CancellationToken ct)
    {
        var busqueda = await VacationRunLookup.BuscarAsync(db, request.RunPublicId, ct);
        if (busqueda.IsFailure) return Result.Failure<SettlementDiscardedDto>(busqueda.Error);
        var (_, movimiento, empleado) = busqueda.Value;
        var motivo = request.Reason.Trim();

        var resultado = await workflow.DiscardAsync(request.RunPublicId, PayrollRunKind.Vacation, motivo,
            async (_, _, token) =>
            {
                if (movimiento.Status == VacationMovementStatus.Registered)
                {
                    await planner.AnularAsync(movimiento, $"{VacationNoveltyPlanner.AnuladaPorCancelacion}: {motivo}", token);
                    movimiento.Status = VacationMovementStatus.Cancelled;
                    movimiento.CancelReason = motivo;
                    movimiento.PayrollRunId = null;
                    movimiento.UpdatedAt = clock.UtcNow;
                    movimiento.UpdatedBy = user.UserName;
                }
                return Result.Success();
            }, ct);
        if (resultado.IsFailure) return resultado;

        await audit.EmitAsync(AuditEventTypes.PayrollVacationCancelled, nameof(VacationMovement), movimiento.PublicId,
            new { status = "Registered", kind = movimiento.Kind.ToString() },
            new { status = "Cancelled", reason = motivo, employeePublicId = empleado.PublicId, discardedRunPublicId = request.RunPublicId }, ct);
        return resultado;
    }
}
