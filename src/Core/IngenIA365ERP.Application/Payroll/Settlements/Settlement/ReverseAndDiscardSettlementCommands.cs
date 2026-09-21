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

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

// ----------------------------------------------------------------- reversar --

/// <summary>
/// Reversa la definitiva aprobada (contracts/api.md §3.4 <c>POST /{runId}/reverse</c>) sobre el ciclo
/// común: asiento espejo y, en la misma transacción, la ficha vuelve a estar vigente (<c>Status</c>,
/// <c>TerminationDate = MaxValue</c>, <c>Person.IsEmployee = true</c>), la terminación queda
/// <c>Reinstated</c> con el motivo, los descuentos <c>Reverted</c> y el movimiento de vacaciones de la
/// definitiva anulado. Los recaudos de Cartera <b>no</b> se reversan solos: la respuesta los lista para
/// que Cartera los reverse por su propio flujo (FR-020, R7). Auditoría <c>Payroll.Employee.Reinstated</c>.
/// </summary>
public sealed record ReverseSettlementCommand(Guid RunPublicId, string Reason) : IRequest<Result<SettlementReversedWithPortfolioDto>>;

public sealed class ReverseSettlementCommandValidator : AbstractValidator<ReverseSettlementCommand>
{
    public ReverseSettlementCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo de la reversión.").MaximumLength(300);
    }
}

public sealed class ReverseSettlementCommandHandler(
    IApplicationDbContext db,
    SettlementRunWorkflow workflow,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<ReverseSettlementCommand, Result<SettlementReversedWithPortfolioDto>>
{
    public async Task<Result<SettlementReversedWithPortfolioDto>> Handle(ReverseSettlementCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementReversedWithPortfolioDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement || run.TerminationId is null)
            return Result.Failure<SettlementReversedWithPortfolioDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));

        var terminacion = await db.EmploymentTerminations.Include(t => t.Deductions).FirstOrDefaultAsync(t => t.Id == run.TerminationId, ct);
        if (terminacion is null) return Result.Failure<SettlementReversedWithPortfolioDto>(SettlementErrors.TerminationNotFound);
        var empleado = await db.Employees.FirstOrDefaultAsync(e => e.Id == terminacion.EmployeeId, ct);
        if (empleado is null) return Result.Failure<SettlementReversedWithPortfolioDto>(SettlementErrors.EmployeeNotFound);
        var persona = await db.People.FirstOrDefaultAsync(p => p.Id == empleado.PersonId, ct);

        var pagos = new List<PortfolioPaymentDto>();
        var reversado = await TransaccionDeLiquidacion.EjecutarAsync(db, () => workflow.ReverseAsync(run.PublicId, PayrollRunKind.Settlement, request.Reason,
            async (corrida, _, token) =>
            {
                var ahora = clock.UtcNow;
                var quien = user.UserName ?? string.Empty;

                foreach (var d in terminacion.Deductions.Where(d => !d.IsDeleted && d.Status == SettlementDeductionStatus.Applied))
                {
                    if (d.AppliedAmount > 0m && d.Kind == SettlementDeductionKind.CooperativeLoan)
                        pagos.Add(new PortfolioPaymentDto(d.PublicId, d.CarteraTransactionPublicId, d.AppliedAmount, d.RemainingBalanceAfter, d.Description));
                    d.Status = SettlementDeductionStatus.Reverted;
                    d.UpdatedAt = ahora;
                    d.UpdatedBy = quien;
                }

                // La ficha vuelve a estar vigente (FR-020).
                empleado.Status = 1;
                empleado.TerminationDate = DateTime.MaxValue.Date;
                empleado.TerminationCause = null;
                empleado.UpdatedAt = ahora;
                empleado.UpdatedBy = quien;
                if (persona is not null)
                {
                    persona.IsEmployee = true;
                    persona.UpdatedAt = ahora;
                    persona.UpdatedBy = quien;
                }

                var movimientos = await db.VacationMovements
                    .Where(m => m.PayrollRunId == corrida.Id && m.Kind == VacationMovementKind.SettlementPayout && m.Status != VacationMovementStatus.Cancelled)
                    .ToListAsync(token);
                foreach (var m in movimientos)
                {
                    m.Status = VacationMovementStatus.Cancelled;
                    m.CancelReason = $"Definitiva reversada: {request.Reason.Trim()}";
                    m.UpdatedAt = ahora;
                    m.UpdatedBy = quien;
                }

                terminacion.Status = TerminationStatus.Reinstated;
                terminacion.ReinstatedAt = ahora;
                terminacion.ReinstatedBy = quien;
                terminacion.ReinstateReason = request.Reason.Trim();
                terminacion.UpdatedAt = ahora;
                terminacion.UpdatedBy = quien;
                return Result.Success();
            }, ct), ct);
        if (reversado.IsFailure) return Result.Failure<SettlementReversedWithPortfolioDto>(reversado.Error);

        await audit.EmitAsync(AuditEventTypes.PayrollEmployeeReinstated, nameof(Employee), empleado.PublicId,
            new { status = -1, terminationDate = terminacion.TerminationDate, terminationPublicId = terminacion.PublicId },
            new { status = 1, reason = request.Reason.Trim(), runPublicId = run.PublicId, reversalDocument = reversado.Value.ReversalNumber, portfolioPayments = pagos }, ct);

        var mensaje = pagos.Count == 0
            ? "La ficha volvió a estar vigente. No había recaudos de Cartera que reversar."
            : $"La ficha volvió a estar vigente. Los {pagos.Count} recaudo(s) que la definitiva dejó en Cartera no se reversan desde aquí: revérselos en Cartera con su propio flujo.";
        return Result.Success(new SettlementReversedWithPortfolioDto(run.PublicId, terminacion.PublicId, reversado.Value.ReversalDocumentPublicId, reversado.Value.ReversalNumber, pagos, mensaje));
    }
}

// ---------------------------------------------------------------- descartar --

/// <summary>Descarta el borrador de la definitiva (contracts/api.md §3.4 <c>POST /{runId}/discard</c>): la corrida queda <c>Superseded</c> con motivo y la terminación <c>Cancelled</c>; la ficha nunca se tocó.</summary>
public sealed record DiscardSettlementCommand(Guid RunPublicId, string Reason) : IRequest<Result<SettlementDiscardedDto>>;

public sealed class DiscardSettlementCommandValidator : AbstractValidator<DiscardSettlementCommand>
{
    public DiscardSettlementCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo del descarte.").MaximumLength(300);
    }
}

public sealed class DiscardSettlementCommandHandler(IApplicationDbContext db, SettlementRunWorkflow workflow, IDateTimeService clock, ICurrentUserService user)
    : IRequestHandler<DiscardSettlementCommand, Result<SettlementDiscardedDto>>
{
    public async Task<Result<SettlementDiscardedDto>> Handle(DiscardSettlementCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementDiscardedDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement || run.TerminationId is null)
            return Result.Failure<SettlementDiscardedDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));
        var terminacion = await db.EmploymentTerminations.FirstOrDefaultAsync(t => t.Id == run.TerminationId, ct);
        if (terminacion is null) return Result.Failure<SettlementDiscardedDto>(SettlementErrors.TerminationNotFound);

        return await workflow.DiscardAsync(run.PublicId, PayrollRunKind.Settlement, request.Reason, (_, _, _) =>
        {
            terminacion.Status = TerminationStatus.Cancelled;
            var nota = string.IsNullOrWhiteSpace(terminacion.Notes) ? $"Descartada: {request.Reason.Trim()}" : $"{terminacion.Notes} · Descartada: {request.Reason.Trim()}";
            terminacion.Notes = nota.Length <= 500 ? nota : nota[..500];
            terminacion.UpdatedAt = clock.UtcNow;
            terminacion.UpdatedBy = user.UserName;
            return Task.FromResult(Result.Success());
        }, ct);
    }
}
