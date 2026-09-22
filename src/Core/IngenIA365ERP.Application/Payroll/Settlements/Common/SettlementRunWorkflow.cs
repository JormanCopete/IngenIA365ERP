using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Settlements.Common;

/// <summary>Lo que pide quien aprueba una liquidación especial (contracts/api.md §3.1 <c>approve</c>).</summary>
public sealed record SettlementApprovalRequest(
    Guid RunPublicId,
    PayrollRunKind ExpectedKind,
    bool Confirm,
    DateOnly? PostingDate = null,
    bool ConfirmEmpty = false,
    bool ConfirmWithoutSegregation = false,
    bool AllowNothingToPost = false);

/// <summary>
/// El ciclo de vida común a las cuatro liquidaciones especiales (feature 010, T028): aprobar,
/// reversar y descartar. Cada comando por tipo (prima, cesantías, vacaciones, definitiva) le
/// pasa su <c>Kind</c> esperado y, si tiene algo propio que dejar en la misma transacción
/// (cerrar la ficha, confirmar el movimiento, aplicar pagos en Cartera), lo entrega en el gancho
/// <c>antesDeGuardar</c>: todo cae en UN <c>SaveChanges</c>, o nada.
///
/// <para>
/// Aprobar: el <c>Kind</c> de la ruta debe ser el de la corrida (<c>KindMismatch</c>), la corrida
/// debe estar en borrador (<c>NotDraft</c>), sin bloqueos, con confirmación; la segregación compara
/// aprobador con <c>CalculatedBy</c> —lo admite la política <c>AllowSameUserApproval</c> vigente al
/// corte con la segunda confirmación—; contabiliza por <see cref="SettlementAccountingPoster"/>
/// (fecha = <c>postingDate</c>, por defecto el corte, D-04), marca <c>Approved</c>, fija
/// <c>PayDate</c> y deja <c>ConsumedByRunId</c> en el saldo inicial que usó (R3). Reversar: sin
/// marcas de pago vigentes, espejo por el contrato, libera el saldo inicial —o lo deja a nombre de
/// la siguiente liquidación aprobada que también lo usó— y queda <c>Reversed</c>. Descartar: el borrador queda <c>Superseded</c> con quién, cuándo y por qué.
/// Todo auditado con los eventos <c>Payroll.Settlement.*</c>.
/// </para>
/// </summary>
public sealed class SettlementRunWorkflow(
    IApplicationDbContext db,
    SettlementAccountingPoster poster,
    PayrollPolicyReader policies,
    IDateTimeService clock,
    ICurrentUserService user,
    PayrollAuditEmitter audit)
{
    /// <summary>Gancho para lo propio de cada tipo, dentro de la misma transacción. Devolver un fallo aborta sin guardar nada.</summary>
    public delegate Task<Result> Gancho(PayrollRun run, IReadOnlyList<PayrollRunEmployee> employees, CancellationToken ct);

    // ---------------------------------------------------------------------- aprobar --

    public async Task<Result<SettlementApprovedDto>> ApproveAsync(SettlementApprovalRequest request, Gancho? antesDeGuardar, CancellationToken ct)
    {
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<SettlementApprovedDto>(SettlementErrors.RunNotFound);
        if (run.Kind != request.ExpectedKind) return Result.Failure<SettlementApprovedDto>(SettlementErrors.KindMismatch(run.Kind, request.ExpectedKind));
        if (run.Status != PayrollRunStatus.Draft) return Result.Failure<SettlementApprovedDto>(SettlementErrors.NotDraft(run.Status));
        if (!request.Confirm) return Result.Failure<SettlementApprovedDto>(SettlementErrors.ConfirmationRequired);

        var empleados = await db.PayrollRunEmployees.Include(e => e.Lines).Include(e => e.Employee)
            .Where(e => e.PayrollRunId == run.Id).ToListAsync(ct);
        if (empleados.Count == 0 && !request.ConfirmEmpty) return Result.Failure<SettlementApprovedDto>(SettlementErrors.EmptyRunRequiresConfirmation);
        if (empleados.Any(e => SettlementRunPersister.SinAvisos(e.Flags) != RunEmployeeFlag.None))
            return Result.Failure<SettlementApprovedDto>(SettlementErrors.HasBlockers);

        // --- segregación: quien calculó no aprueba, salvo política y segunda confirmación (R12) ---
        var yo = user.UserName ?? string.Empty;
        var sinSegregacion = false;
        if (!string.IsNullOrEmpty(run.CalculatedBy) && run.CalculatedBy.Equals(yo, StringComparison.OrdinalIgnoreCase))
        {
            var politica = await policies.LeerAsync(run.CutoffDate!.Value, ct);
            if (politica.IsFailure) return Result.Failure<SettlementApprovedDto>(politica.Error);
            if (!politica.Value.AllowSameUserApproval) return Result.Failure<SettlementApprovedDto>(SettlementErrors.SegregationViolation(run.CalculatedBy));
            if (!request.ConfirmWithoutSegregation) return Result.Failure<SettlementApprovedDto>(SettlementErrors.ConfirmWithoutSegregationRequired);
            sinSegregacion = true;
        }

        // --- comprobante, sin guardar ---
        var fecha = poster.ResolverFecha(run, request.PostingDate);
        if (fecha.IsFailure) return Result.Failure<SettlementApprovedDto>(fecha.Error);
        var detalle = $"{SettlementLabels.Etiqueta(run, empleados.Count == 1 ? await NombreAsync(empleados[0].EmployeeId, ct) : null)} v{run.Version}";
        AccountingDocument? documento = null;
        if (empleados.Count > 0)
        {
            var posting = await poster.PostAsync(run,
                empleados.Select(e => (e, e.Employee!, (IReadOnlyList<PayrollRunLine>)e.Lines.ToList())).ToList(),
                fecha.Value, detalle, ct);
            // US4 (D-01): una liquidación de vacaciones con la política «paga la nómina ordinaria» sólo
            // registra el disfrute y no tiene nada que contabilizar; su comando lo declara y se aprueba sin comprobante.
            if (posting.IsFailure && !(request.AllowNothingToPost && posting.Error.Code == SettlementErrors.NothingToPost.Code))
                return Result.Failure<SettlementApprovedDto>(posting.Error);
            documento = posting.IsSuccess ? posting.Value : null;
        }

        var ahora = clock.UtcNow;
        run.Status = PayrollRunStatus.Approved;
        run.ApprovedAt = ahora;
        run.ApprovedBy = yo;
        run.ApprovedWithoutSegregation = sinSegregacion;
        run.PayDate ??= fecha.Value;
        run.AccountingDocument = documento;
        run.UpdatedAt = ahora;
        run.UpdatedBy = yo;

        // --- el saldo inicial que esta liquidación usó queda consumido (R3): ya no se edita, se ajusta ---
        var idsEmpleados = empleados.Select(e => e.EmployeeId).ToList();
        var saldos = await db.EmployeeBenefitOpeningBalances
            .Where(b => idsEmpleados.Contains(b.EmployeeId) && b.ConsumedByRunId == null && b.AsOfDate <= run.CutoffDate)
            .ToListAsync(ct);
        foreach (var b in saldos)
        {
            b.ConsumedByRun = run;
            b.UpdatedAt = ahora;
            b.UpdatedBy = yo;
        }

        if (antesDeGuardar is not null)
        {
            var propio = await antesDeGuardar(run, empleados, ct);
            if (propio.IsFailure) return Result.Failure<SettlementApprovedDto>(propio.Error);
        }

        await db.SaveChangesAsync(ct);

        var numero = documento?.Referencia() ?? string.Empty;
        await audit.EmitAsync(AuditEventTypes.PayrollSettlementApproved, nameof(PayrollRun), run.PublicId,
            new { status = "Draft" },
            new
            {
                status = "Approved", kind = run.Kind.ToString(), cutoffDate = run.CutoffDate, postingDate = fecha.Value, version = run.Version,
                employees = run.EmployeeCount, totalNet = run.TotalNet, document = numero, documentPublicId = documento?.PublicId,
                approvedWithoutSegregation = sinSegregacion, openingBalancesConsumed = saldos.Count,
            }, ct);

        return Result.Success(new SettlementApprovedDto(run.PublicId, documento?.PublicId ?? Guid.Empty, numero, run.TotalNet, fecha.Value, sinSegregacion));
    }

    // --------------------------------------------------------------------- reversar --

    public async Task<Result<SettlementReversedDto>> ReverseAsync(Guid runPublicId, PayrollRunKind expectedKind, string reason, Gancho? antesDeGuardar, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure<SettlementReversedDto>(SettlementErrors.ReasonRequired);
        var run = await db.PayrollRuns.Include(r => r.AccountingDocument).FirstOrDefaultAsync(r => r.PublicId == runPublicId, ct);
        if (run is null) return Result.Failure<SettlementReversedDto>(SettlementErrors.RunNotFound);
        if (run.Kind != expectedKind) return Result.Failure<SettlementReversedDto>(SettlementErrors.KindMismatch(run.Kind, expectedKind));
        if (run.Status == PayrollRunStatus.Reversed) return Result.Failure<SettlementReversedDto>(SettlementErrors.AlreadyReversed(run.Status));
        if (run.Status != PayrollRunStatus.Approved) return Result.Failure<SettlementReversedDto>(SettlementErrors.NotApproved(run.Status));

        var empleados = await db.PayrollRunEmployees.Include(e => e.Lines).Where(e => e.PayrollRunId == run.Id).ToListAsync(ct);
        var idsFilas = empleados.Select(e => e.Id).ToList();
        if (await db.PayrollPayments.AsNoTracking().AnyAsync(p => idsFilas.Contains(p.PayrollRunEmployeeId) && !p.IsReverted, ct))
            return Result.Failure<SettlementReversedDto>(SettlementErrors.PaymentBlocksReversal);

        var original = run.AccountingDocument
                       ?? (run.AccountingDocumentId is { } docId ? await db.AccountingDocuments.FirstOrDefaultAsync(d => d.Id == docId, ct) : null);
        var motivo = reason.Trim();
        AccountingDocument? espejo = null;
        if (original is not null)
        {
            var posting = await poster.ReverseAsync(run, original, clock.TodayUtc, motivo, ct);
            if (posting.IsFailure) return Result.Failure<SettlementReversedDto>(posting.Error);
            espejo = posting.Value;
        }

        var ahora = clock.UtcNow;
        var yo = user.UserName ?? string.Empty;
        run.Status = PayrollRunStatus.Reversed;
        run.ReversedAt = ahora;
        run.ReversedBy = yo;
        run.ReversalReason = motivo;
        run.ReversalAccountingDocument = espejo;
        run.UpdatedAt = ahora;
        run.UpdatedBy = yo;

        // El saldo inicial vuelve a ser editable sólo si ninguna otra liquidación aprobada lo usa (R3); si otra
        // lo leyó (cesantías tras la prima, por ejemplo), la marca pasa a ella (revisión N1 de la feature 010).
        var saldos = await db.EmployeeBenefitOpeningBalances.Where(b => b.ConsumedByRunId == run.Id).ToListAsync(ct);
        var liberados = 0;
        foreach (var grupo in saldos.GroupBy(b => b.EmployeeId))
        {
            var otra = (await OpeningBalances.BenefitBalanceRules.ConsumidoresAsync(db, grupo.Key, grupo, run.Id, ct)).FirstOrDefault();
            foreach (var b in grupo)
            {
                b.ConsumedByRunId = otra?.Id;
                b.ConsumedByRun = null;
                b.UpdatedAt = ahora;
                b.UpdatedBy = yo;
                if (otra is null) liberados++;
            }
        }

        if (antesDeGuardar is not null)
        {
            var propio = await antesDeGuardar(run, empleados, ct);
            if (propio.IsFailure) return Result.Failure<SettlementReversedDto>(propio.Error);
        }

        await db.SaveChangesAsync(ct);

        var numero = espejo?.Referencia() ?? string.Empty;
        await audit.EmitAsync(AuditEventTypes.PayrollSettlementReversed, nameof(PayrollRun), run.PublicId,
            new { status = "Approved", accountingDocument = original?.Referencia() },
            new { status = "Reversed", kind = run.Kind.ToString(), reason = motivo, reversalDocument = numero, openingBalancesReleased = liberados }, ct);

        return Result.Success(new SettlementReversedDto(run.PublicId, espejo?.PublicId ?? Guid.Empty, numero));
    }

    // -------------------------------------------------------------------- descartar --

    public async Task<Result<SettlementDiscardedDto>> DiscardAsync(Guid runPublicId, PayrollRunKind expectedKind, string reason, Gancho? antesDeGuardar, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure<SettlementDiscardedDto>(SettlementErrors.ReasonRequired);
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.PublicId == runPublicId, ct);
        if (run is null) return Result.Failure<SettlementDiscardedDto>(SettlementErrors.RunNotFound);
        if (run.Kind != expectedKind) return Result.Failure<SettlementDiscardedDto>(SettlementErrors.KindMismatch(run.Kind, expectedKind));
        if (!run.IsEditableDraft) return Result.Failure<SettlementDiscardedDto>(SettlementErrors.NotDraft(run.Status));

        var ahora = clock.UtcNow;
        var yo = user.UserName ?? string.Empty;
        var motivo = reason.Trim();
        run.Status = PayrollRunStatus.Superseded;
        run.DiscardedAt = ahora;
        run.DiscardedBy = yo;
        run.DiscardReason = motivo;
        run.UpdatedAt = ahora;
        run.UpdatedBy = yo;

        if (antesDeGuardar is not null)
        {
            var empleados = await db.PayrollRunEmployees.Where(e => e.PayrollRunId == run.Id).ToListAsync(ct);
            var propio = await antesDeGuardar(run, empleados, ct);
            if (propio.IsFailure) return Result.Failure<SettlementDiscardedDto>(propio.Error);
        }

        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollSettlementDiscarded, nameof(PayrollRun), run.PublicId,
            new { status = "Draft", version = run.Version },
            new { status = "Superseded", kind = run.Kind.ToString(), reason = motivo }, ct);

        return Result.Success(new SettlementDiscardedDto(run.PublicId, motivo));
    }

    private async Task<string?> NombreAsync(int employeeId, CancellationToken ct) =>
        await (from e in db.Employees.AsNoTracking()
               join p in db.People.AsNoTracking() on e.PersonId equals p.Id
               where e.Id == employeeId
               select (p.FirstName + " " + p.LastName).Trim()).FirstOrDefaultAsync(ct);
}

/// <summary>Cómo se llama cada liquidación en comprobantes, asientos y pantallas («Prima de servicios 2026-II», «Liquidación definitiva · Ana Prueba»).</summary>
public static class SettlementLabels
{
    public static string Etiqueta(PayrollRun run, string? employeeName = null)
    {
        var quien = string.IsNullOrWhiteSpace(employeeName) ? string.Empty : $" · {employeeName}";
        return run.Kind switch
        {
            PayrollRunKind.ServiceBonus => $"Prima de servicios {run.Year}-{(run.Semester == 1 ? "I" : "II")}",
            PayrollRunKind.Severance => $"Cesantías e intereses {run.Year}",
            PayrollRunKind.Vacation => $"Vacaciones{quien} (corte {run.CutoffDate:dd/MM/yyyy})",
            PayrollRunKind.Settlement => $"Liquidación definitiva{quien} ({run.CutoffDate:dd/MM/yyyy})",
            _ => "Nómina",
        };
    }

    /// <summary>La etiqueta corta del comprobante del empleado (FR-011a): sin nombre, porque el comprobante ya es de él.</summary>
    public static string EtiquetaDelComprobante(PayrollRun run) => run.Kind switch
    {
        PayrollRunKind.ServiceBonus => $"Prima de servicios {run.Year}-{(run.Semester == 1 ? "I" : "II")}",
        PayrollRunKind.Severance => $"Intereses a las cesantías {run.Year}",
        PayrollRunKind.Vacation => "Vacaciones",
        PayrollRunKind.Settlement => "Liquidación definitiva",
        _ => "Nómina",
    };
}
