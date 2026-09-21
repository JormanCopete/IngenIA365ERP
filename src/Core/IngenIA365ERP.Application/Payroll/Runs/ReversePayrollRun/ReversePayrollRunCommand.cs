using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Runs.ReversePayrollRun;

public sealed record ReverseRunResultDto(
    Guid RunPublicId,
    Guid PeriodPublicId,
    Guid ReversalAccountingDocumentPublicId,
    string ReversalAccountingDocumentNumber);

/// <summary>
/// FR-032: reversa una liquidación aprobada. Nada se borra: la corrida queda <c>Reversed</c>
/// con quién, cuándo y por qué; el asiento <c>NM</c> se reversa con un comprobante espejo que
/// referencia al original; el período vuelve a <c>Open</c> en el mismo acto y las novedades
/// siguen como estaban, listas para corregirse y recalcular. Si algún empleado tiene marca de
/// pago vigente no se reversa: primero se retiran las marcas, con motivo.
/// </summary>
public sealed record ReversePayrollRunCommand(Guid RunPublicId, string Reason) : IRequest<Result<ReverseRunResultDto>>, IReintentableAnteConcurrencia;

public sealed class ReversePayrollRunCommandValidator : AbstractValidator<ReversePayrollRunCommand>
{
    public ReversePayrollRunCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo de la reversión es obligatorio.").MaximumLength(500);
    }
}

public sealed class ReversePayrollRunCommandHandler(
    IApplicationDbContext db,
    PayrollAccountingPoster poster,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
    : IRequestHandler<ReversePayrollRunCommand, Result<ReverseRunResultDto>>
{
    public async Task<Result<ReverseRunResultDto>> Handle(ReversePayrollRunCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Fallo("Payroll.ReasonRequired", "La reversión exige un motivo.");

        var run = await db.PayrollRuns.Include(r => r.PayPeriod).Include(r => r.AccountingDocument)
            .FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Fallo("Payroll.RunNotFound", "No existe la corrida indicada.");
        // Feature 010: las liquidaciones especiales se reversan por su ruta con su permiso.
        if (run.EsEspecial) return Result.Failure<ReverseRunResultDto>(Settlements.Common.SettlementErrors.UseSettlementRoute(run.Kind));
        if (run.Status != PayrollRunStatus.Approved)
            return Fallo("Payroll.RunNotApproved", $"La corrida está {run.Status}: sólo se reversa una liquidación aprobada.");
        var period = run.PayPeriod!;

        // --- pagos vigentes bloquean (FR-032): la constancia de pago manda ---
        var pagados = await (
            from p in db.PayrollPayments.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on p.PayrollRunEmployeeId equals re.Id
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join per in db.People.AsNoTracking() on e.PersonId equals per.Id
            where re.PayrollRunId == run.Id && !p.IsReverted
            orderby per.LastName, per.FirstName
            select (per.FirstName + " " + per.LastName).Trim()).ToListAsync(ct);
        if (pagados.Count > 0)
            return Fallo("Payroll.PaymentBlocksReversal",
                $"{pagados.Count} empleado(s) tienen marca de pago vigente: {string.Join(", ", pagados)}. Retire las marcas (con motivo) antes de reversar.");

        var original = run.AccountingDocument
                       ?? (run.AccountingDocumentId is { } docId ? await db.AccountingDocuments.FirstOrDefaultAsync(d => d.Id == docId, ct) : null);
        if (original is null)
            return Fallo("Payroll.NothingToPost", "La corrida aprobada no tiene comprobante contable asociado: no hay asiento que reversar.");

        var hoy = clock.TodayUtc;
        var posting = await poster.ReverseAsync(original, hoy, request.Reason.Trim(), ct);
        if (posting.IsFailure) return Result.Failure<ReverseRunResultDto>(posting.Error);

        var ahora = clock.UtcNow;
        var yo = user.UserName ?? string.Empty;

        run.Status = PayrollRunStatus.Reversed;
        run.ReversedAt = ahora;
        run.ReversedBy = yo;
        run.ReversalReason = request.Reason.Trim();
        run.ReversalAccountingDocument = posting.Value;
        run.UpdatedAt = ahora;
        run.UpdatedBy = yo;

        period.Status = PayPeriodStatus.Open;
        period.ApprovedAt = null;
        period.ApprovedBy = null;
        period.RunPublicId = null;
        period.StatusMessage = PayPeriod.Mensaje($"Reversado por {yo} el {ahora:dd/MM/yyyy HH:mm} UTC. Reabierto: {request.Reason.Trim()}");
        period.UpdatedAt = ahora;
        period.UpdatedBy = yo;

        // La cuota emitida por las recurrentes de este período se devuelve: la volverá a contar la próxima aprobación.
        var recurrentesIds = await db.PayrollNovelties.AsNoTracking()
            .Where(n => n.PayPeriodId == period.Id && n.Status == NoveltyStatus.Active && n.RecurringNoveltyId != null)
            .Select(n => n.RecurringNoveltyId!.Value).Distinct().ToListAsync(ct);
        if (recurrentesIds.Count > 0)
        {
            foreach (var r in await db.PayrollRecurringNovelties.Where(r => recurrentesIds.Contains(r.Id)).ToListAsync(ct))
            {
                if (r.InstallmentsIssued > 0) r.InstallmentsIssued--;
                r.UpdatedAt = ahora;
                r.UpdatedBy = yo;
            }
        }

        await db.SaveChangesAsync(ct);

        var numero = posting.Value.Referencia();
        await audit.EmitAsync(AuditEventTypes.PayrollRunReversed, nameof(PayrollRun), run.PublicId,
            new { status = "Approved", accountingDocument = original.Referencia() },
            new { status = "Reversed", reason = request.Reason.Trim(), reversalDocument = numero, period = period.PublicId, periodStatus = "Open" }, ct);

        return Result.Success(new ReverseRunResultDto(run.PublicId, period.PublicId, posting.Value.PublicId, numero));
    }

    private static Result<ReverseRunResultDto> Fallo(string code, string message) => Result.Failure<ReverseRunResultDto>(new Error(code, message));
}
