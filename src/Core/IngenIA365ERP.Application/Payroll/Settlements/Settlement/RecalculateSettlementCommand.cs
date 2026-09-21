using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>
/// Recalcula la definitiva (contracts/api.md §3.4 <c>POST /{runId}/recalculate</c>): versión nueva,
/// la anterior <c>Superseded</c>; vuelve a leer Cartera y libranzas y <b>conserva los ajustes</b> cuyo
/// propuesto no cambió (los demás vuelven al saldo y se avisa, <c>Payroll.Settlement.DeductionReproposed</c>).
/// Sólo sobre el borrador vigente de una terminación registrada.
/// </summary>
public sealed record RecalculateSettlementCommand(Guid RunPublicId) : IRequest<Result<TerminationRegisteredDto>>;

public sealed class RecalculateSettlementCommandValidator : AbstractValidator<RecalculateSettlementCommand>
{
    public RecalculateSettlementCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class RecalculateSettlementCommandHandler(
    IApplicationDbContext db,
    SettlementInputLoader loader,
    SettlementRunPersister persister,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<RecalculateSettlementCommand, Result<TerminationRegisteredDto>>
{
    public async Task<Result<TerminationRegisteredDto>> Handle(RecalculateSettlementCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.Settlement || run.TerminationId is null) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.Settlement));
        if (!run.IsEditableDraft) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.NotDraft(run.Status));

        var terminacion = await db.EmploymentTerminations.Include(t => t.Deductions).FirstOrDefaultAsync(t => t.Id == run.TerminationId, ct);
        if (terminacion is null) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.TerminationNotFound);
        if (terminacion.Status != TerminationStatus.Registered)
            return Result.Failure<TerminationRegisteredDto>(SettlementErrors.NotDraft(run.Status));
        var empleado = await db.Employees.FirstOrDefaultAsync(e => e.Id == terminacion.EmployeeId, ct);
        if (empleado is null) return Result.Failure<TerminationRegisteredDto>(SettlementErrors.EmployeeNotFound);

        var calculador = new DefinitivaCalculator(db, loader, persister, clock, user);
        var calculo = await TransaccionDeLiquidacion.EjecutarAsync(db, () => calculador.CalcularAsync(terminacion, empleado, recalculo: true, ct), ct);
        if (calculo.IsFailure) return Result.Failure<TerminationRegisteredDto>(calculo.Error);

        await audit.EmitAsync(AuditEventTypes.PayrollSettlementCalculated, nameof(EmploymentTermination), terminacion.PublicId,
            new { runPublicId = run.PublicId, version = run.Version },
            new
            {
                action = "Recalculated", runPublicId = calculo.Value.Run.PublicId, version = calculo.Value.Run.Version, net = calculo.Value.Run.TotalNet,
                warnings = calculo.Value.Avisos.Select(w => w.Code).ToList(),
            }, ct);

        return Result.Success(RegisterTerminationCommandHandler.Respuesta(terminacion, calculo.Value));
    }
}
