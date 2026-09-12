using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs.DiscardPayrollRun;

public sealed record DiscardRunResultDto(Guid RunPublicId, Guid PeriodPublicId, int RecurrentesAnuladas);

/// <summary>
/// Feature 006 (FR-001): descarta un borrador. Hasta hoy un período Calculado sólo volvía a
/// Abierto aprobando y reversando, que deja un asiento contable espejo por un error de
/// captura. Descartar deja la corrida <c>Superseded</c> con quién, cuándo y por qué (nada se
/// borra: Principio XI), devuelve el período a <c>Open</c>, anula las novedades que las
/// recurrentes generaron para ese período —su cuota nunca se contó; se cuenta al aprobar— y
/// conserva las manuales. No toca contabilidad porque un borrador no tiene.
/// </summary>
public sealed record DiscardPayrollRunCommand(Guid RunPublicId, string Reason) : IRequest<Result<DiscardRunResultDto>>;

public sealed class DiscardPayrollRunCommandValidator : AbstractValidator<DiscardPayrollRunCommand>
{
    public DiscardPayrollRunCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo del descarte es obligatorio.").MaximumLength(300);
    }
}

public sealed class DiscardPayrollRunCommandHandler(
    IApplicationDbContext db,
    IDistributedLock distributedLock,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<DiscardPayrollRunCommand, Result<DiscardRunResultDto>>
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(2);

    public async Task<Result<DiscardRunResultDto>> Handle(DiscardPayrollRunCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Fallo("Payroll.ReasonRequired", "El descarte exige un motivo.");

        var run = await db.PayrollRuns.Include(r => r.PayPeriod)
            .FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Fallo("Payroll.RunNotFound", "No existe la corrida indicada.");
        if (run.Status != PayrollRunStatus.Draft)
            return Fallo("Payroll.RunNotDraft",
                $"La corrida está {run.Status}: sólo se descarta un borrador. Una liquidación aprobada se reversa.");

        var period = run.PayPeriod!;

        // Mismo candado que Calcular: no se descarta lo que alguien está recalculando.
        var lockKey = $"payroll:run:{user.TenantId ?? "-"}:{period.PublicId}";
        await using var handle = await distributedLock.TryAcquireAsync(lockKey, LockTtl, ct);
        if (handle is null)
            return Fallo("Payroll.RunInProgress", "Hay un cálculo en curso para este período. Espere a que termine y refresque.");

        var ahora = clock.UtcNow;
        var yo = user.UserName ?? string.Empty;
        var motivo = request.Reason.Trim();

        run.Status = PayrollRunStatus.Superseded;
        run.DiscardedAt = ahora;
        run.DiscardedBy = yo;
        run.DiscardReason = motivo;
        run.UpdatedAt = ahora;
        run.UpdatedBy = yo;

        period.Status = PayPeriodStatus.Open;
        period.RunPublicId = null;
        period.StatusMessage = PayPeriod.Mensaje($"Borrador descartado por {yo} el {ahora:dd/MM/yyyy HH:mm} UTC: {motivo}");
        period.UpdatedAt = ahora;
        period.UpdatedBy = yo;

        // Las recurrentes se regeneran en el próximo cálculo; sin anularlas quedarían dobles.
        var generadas = await db.PayrollNovelties
            .Where(n => n.PayPeriodId == period.Id && n.Status == NoveltyStatus.Active && n.Origin == NoveltyOrigin.Recurring)
            .ToListAsync(ct);
        foreach (var n in generadas)
        {
            n.Status = NoveltyStatus.Cancelled;
            n.StatusReason = "Borrador descartado: se regenera en el próximo cálculo.";
            n.UpdatedAt = ahora;
            n.UpdatedBy = yo;
        }

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollRunDiscarded, nameof(PayrollRun), run.PublicId,
            new { status = "Draft", version = run.Version, periodStatus = "Calculated" },
            new { status = "Superseded", reason = motivo, period = period.PublicId, periodStatus = "Open", recurrentesAnuladas = generadas.Count }, ct);

        return Result.Success(new DiscardRunResultDto(run.PublicId, period.PublicId, generadas.Count));
    }

    private static Result<DiscardRunResultDto> Fallo(string code, string message) =>
        Result.Failure<DiscardRunResultDto>(new Error(code, message));
}
