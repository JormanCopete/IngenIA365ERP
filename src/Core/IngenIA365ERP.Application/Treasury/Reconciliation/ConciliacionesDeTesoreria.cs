using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using IngenIA365ERP.Domain.Treasury.Conciliacion;
using Microsoft.EntityFrameworkCore;
using ConciliacionBancaria = IngenIA365ERP.Domain.Entities.Treasury.Reconciliation.Reconciliation;

namespace IngenIA365ERP.Application.Treasury.Reconciliation;

/// <summary>Un registro de los libros que se puede cruzar en la conciliación: su clase, su <c>PublicId</c> y el registro para el motor. (nuevo)</summary>
public sealed record RegistroDisponible(ReconciliationRecordKind Kind, Guid PublicId, string Number, DateOnly Date, StatementLineDirection Direction, decimal Amount,
    string? Reference, Guid? StatementLinePublicId, string Source)
{
    /// <summary>La clave con que el motor lo nombra: <c>{clase}:{PublicId:N}</c>.</summary>
    public string Clave => Clave_(Kind, PublicId);

    public static string Clave_(ReconciliationRecordKind kind, Guid publicId) => string.Create(CultureInfo.InvariantCulture, $"{(int)kind}:{publicId:N}");

    public RegistroACruzar ParaElMotor() => new(Clave, Date, Direction, Amount, Reference, StatementLinePublicId);
}

/// <summary>
/// Lo común de la conciliación mínima de Tesorería (feature 015, US25, T0278; FR-131, FR-209, FR-216, FR-217; data-model §12.2–§12.4): los registros de los libros de una
/// cuenta y mes —recibos y partidas de Recaudo por <see cref="IRecaudoParaConciliacion"/>, remesas, pagos girados, cheques girados, archivos de desembolsos de Cartera y los
/// movimientos contables que no nacieron de Recaudo ni de Tesorería (lo heredado y lo digitado en Contabilidad, por <c>MovimientosDeCuentaBancariaAsync</c> con su
/// <c>sourceModule</c>)—, las líneas del extracto que faltan por cruzar, el recálculo de los saldos y la diferencia, y la segregación del conciliador. Nada de aquí guarda.
/// (nuevo)
/// </summary>
public static class ConciliacionesDeTesoreria
{
    /// <summary>Los módulos cuyos movimientos contables ya están en los libros por su propio registro: no se cruzan dos veces.</summary>
    public static readonly IReadOnlySet<string> ModulosConRegistroPropio = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "REC", "TES" };

    /// <summary>La conciliación vigente de la cuenta y el mes: la última versión que no se reabrió ni quedó desactualizada.</summary>
    public static bool Vigente(ReconciliationState s) => s is ReconciliationState.Open or ReconciliationState.Closed;

    public static DateOnly Inicio(ConciliacionBancaria c) => new(c.PeriodYear, c.PeriodMonth, 1);

    public static DateOnly Fin(ConciliacionBancaria c) => Inicio(c).AddMonths(1).AddDays(-1);

    /// <summary>
    /// El signo con que una partida explica la diferencia: <c>Difference = StatementBalance − LedgerBalance + Σ signo × valor</c>. Lo que está en libros y no en el banco
    /// (cheque sin cobrar) resta del extracto; lo que está en el banco y no en libros (abono o cargo sin registrar) se suma o resta a los libros. El abono sin registro que ya
    /// se llevó a Recaudo como partida queda en los libros por su hecho: no explica nada (signo cero) y su línea se cruza con la partida.
    /// </summary>
    public static decimal Signo(ReconcilingItemKind clase, Guid? partidaDeRecaudo) => clase switch
    {
        ReconcilingItemKind.DepositInTransit => 1m,
        ReconcilingItemKind.RemittanceInTransit => 1m,
        ReconcilingItemKind.IssuedCheckNotCashed => -1m,
        ReconcilingItemKind.BankChargeNotRecorded => 1m,
        ReconcilingItemKind.UnidentifiedDebit => 1m,
        ReconcilingItemKind.BankCreditNotRecorded => -1m,
        ReconcilingItemKind.UnidentifiedCredit => partidaDeRecaudo is null ? -1m : 0m,
        _ => 0m,
    };

    /// <summary>Los registros de la cuenta en el mes (con la holgura hacia atrás) que no están cruzados en otra conciliación vigente.</summary>
    public static async Task<IReadOnlyList<RegistroDisponible>> RegistrosAsync(IApplicationDbContext db, IEnumerable<IRecaudoParaConciliacion> recaudo,
        IContabilidadParaTesoreria contabilidad, OwnBankAccount cuenta, ConciliacionBancaria conciliacion, int holgura, CancellationToken ct)
    {
        var desde = Inicio(conciliacion).AddDays(-Math.Max(0, holgura));
        var hasta = Fin(conciliacion);
        var lista = new List<RegistroDisponible>();

        foreach (var puerto in recaudo)
            foreach (var r in await puerto.RegistrosAsync(cuenta.PublicId, desde, hasta, ct))
                lista.Add(new RegistroDisponible(ReconciliationRecordKind.ReceiptsBankReceipt, r.SourcePublicId, r.Number, r.Date, StatementLineDirection.Credit, r.Amount,
                    r.Reference, r.StatementLinePublicId, $"Recaudo · {r.SourceKind}"));

        var remesas = await db.CashRemittances.AsNoTracking()
            .Where(r => r.BankAccountId == cuenta.Id && r.Status != RemittanceState.Voided && r.DispatchDate >= desde && r.DispatchDate <= hasta).ToListAsync(ct);
        lista.AddRange(remesas.Select(r => new RegistroDisponible(ReconciliationRecordKind.Remittance, r.PublicId, r.Number, r.ConfirmedOn ?? r.DispatchDate,
            r.Direction == RemittanceDirection.VaultToBank ? StatementLineDirection.Credit : StatementLineDirection.Debit, r.Amount, r.BagSeal ?? r.Number, null,
            "Tesorería · remesa")));

        var pagos = await (from res in db.DisbursementResults.AsNoTracking()
                           join o in db.PaymentOrders.AsNoTracking() on res.PaymentOrderId equals o.Id
                           join i in db.PaymentBatchItems.AsNoTracking() on o.Id equals i.PaymentOrderId
                           join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
                           where res.Outcome == DisbursementOutcome.Disbursed && o.Form != PaymentForm.Check && i.IsLive && b.BankAccountId == cuenta.Id
                                 && res.OccurredOn >= desde && res.OccurredOn <= hasta
                           select new { res.PublicId, o.Number, res.OccurredOn, res.Amount, res.TreasuryReference }).ToListAsync(ct);
        lista.AddRange(pagos.Select(p => new RegistroDisponible(ReconciliationRecordKind.TreasuryPayment, p.PublicId, p.Number, p.OccurredOn, StatementLineDirection.Debit,
            p.Amount, p.TreasuryReference, null, "Tesorería · pago")));

        var cheques = await (from c in db.IssuedChecks.AsNoTracking()
                             join k in db.Checkbooks.AsNoTracking() on c.CheckbookId equals k.Id
                             where k.BankAccountId == cuenta.Id && (c.Status == IssuedCheckState.Issued || c.Status == IssuedCheckState.Delivered
                                                                     || c.Status == IssuedCheckState.Cashed)
                                   && c.IssuedOn <= hasta
                             select c).ToListAsync(ct);
        lista.AddRange(cheques.Select(c => new RegistroDisponible(ReconciliationRecordKind.IssuedCheck, c.PublicId, c.CheckNumber.ToString(CultureInfo.InvariantCulture),
            c.CashedOn ?? c.DeliveredOn ?? c.IssuedOn, StatementLineDirection.Debit, c.Amount, c.CheckNumber.ToString(CultureInfo.InvariantCulture), null,
            "Tesorería · cheque girado")));

        // T1 (fase 33; FR-104): los traslados entre cuentas propias enviados o confirmados: el débito en la de origen (fecha del envío) y el abono en la de destino.
        var traslados = await db.TreasuryTransfers.AsNoTracking()
            .Where(t => (t.Status == TransferState.Sent || t.Status == TransferState.Confirmed) && (t.FromBankAccountId == cuenta.Id || t.ToBankAccountId == cuenta.Id)
                        && t.SentOn >= desde && t.SentOn <= hasta)
            .ToListAsync(ct);
        lista.AddRange(traslados.Select(t => t.FromBankAccountId == cuenta.Id
            ? new RegistroDisponible(ReconciliationRecordKind.Transfer, t.PublicId, t.Number, t.SentOn!.Value, StatementLineDirection.Debit, t.Amount, t.BankReference ?? t.Number,
                null, "Tesorería · traslado enviado")
            : new RegistroDisponible(ReconciliationRecordKind.Transfer, t.PublicId, t.Number, t.ConfirmedOn ?? t.SentOn!.Value, StatementLineDirection.Credit, t.Amount,
                t.BankReference ?? t.Number, t.ConfirmedByStatementLinePublicId, "Tesorería · traslado recibido")));

        var archivos = await db.LendingDisbursementFiles.AsNoTracking()
            .Where(f => f.BankAccountId == cuenta.Id && f.Status != LendingDisbursementFileState.Voided).ToListAsync(ct);
        lista.AddRange(archivos.Select(f => (f, Fecha: DateOnly.FromDateTime(f.ReceivedAt.ToLocalTime())))
            .Where(x => x.Fecha >= desde && x.Fecha <= hasta)
            .Select(x => new RegistroDisponible(ReconciliationRecordKind.LendingDisbursementFile, x.f.PublicId, x.f.FileNumber, x.Fecha, StatementLineDirection.Debit,
                x.f.Total, x.f.FileNumber, null, "Cartera · archivo de desembolsos")));

        // T2 (fase 41; FR-130, FR-168): las notas bancarias aprobadas (con su línea de origen, si nacieron de una) y las dispersiones de Nómina informadas.
        var notas = await db.TreasuryBankNotes.AsNoTracking()
            .Where(n => n.BankAccountId == cuenta.Id && n.Status == BankNoteState.Approved && n.NoteDate >= desde && n.NoteDate <= hasta).ToListAsync(ct);
        lista.AddRange(notas.Select(n => new RegistroDisponible(ReconciliationRecordKind.BankNote, n.PublicId, n.Number, n.NoteDate, n.Direction, n.Amount, n.Number,
            n.StatementLinePublicId, $"Tesorería · nota bancaria ({n.Kind})")));
        var dispersiones = await db.TreasuryPayrollDispersions.AsNoTracking()
            .Where(d => d.BankAccountId == cuenta.Id && d.PayDate >= desde && d.PayDate <= hasta).ToListAsync(ct);
        lista.AddRange(dispersiones.Select(d => new RegistroDisponible(ReconciliationRecordKind.PayrollDispersion, d.FilePublicId, d.PayDate.ToString("yyyyMMdd",
            CultureInfo.InvariantCulture), d.PayDate, StatementLineDirection.Debit, d.Total, null, null, "Nómina · dispersión")));

        foreach (var m in await contabilidad.MovimientosDeCuentaBancariaAsync(cuenta.PublicId, desde, hasta, ct))
        {
            if (ModulosConRegistroPropio.Contains(m.SourceModule)) continue;
            var abono = m.Debit > 0m;
            lista.Add(new RegistroDisponible(ReconciliationRecordKind.LedgerMovement, m.JournalEntryPublicId, m.VoucherNumber, m.Date,
                abono ? StatementLineDirection.Credit : StatementLineDirection.Debit, abono ? m.Debit : m.Credit, m.VoucherNumber, null,
                $"Contabilidad · {m.SourceModule}"));
        }

        // Fuera lo que ya está cruzado en otra conciliación vigente de la cuenta (nada se cruza dos veces).
        var otras = await db.TreasuryReconciliations.AsNoTracking()
            .Where(c => c.BankAccountId == cuenta.Id && c.Id != conciliacion.Id && (c.Status == ReconciliationState.Open || c.Status == ReconciliationState.Closed))
            .Select(c => c.Id).ToListAsync(ct);
        var cruzados = await db.TreasuryReconciliationMatches.AsNoTracking()
            .Where(x => x.IsLive && (otras.Contains(x.ReconciliationId) || x.ReconciliationId == conciliacion.Id))
            .Select(x => new { x.RecordKind, x.RecordPublicId }).ToListAsync(ct);
        var ocupados = cruzados.Select(x => RegistroDisponible.Clave_(x.RecordKind, x.RecordPublicId)).ToHashSet(StringComparer.Ordinal);
        return lista.Where(r => !ocupados.Contains(r.Clave)).ToList();
    }

    /// <summary>Las líneas del extracto del mes (sin los duplicados descartados) que no están cruzadas ni marcadas en una conciliación vigente.</summary>
    public static async Task<IReadOnlyList<BankStatementLine>> LineasPorCruzarAsync(IApplicationDbContext db, ConciliacionBancaria conciliacion, CancellationToken ct)
    {
        var desde = Inicio(conciliacion);
        var hasta = Fin(conciliacion);
        var lineas = await db.TreasuryBankStatementLines.AsNoTracking()
            .Where(l => l.BankAccountId == conciliacion.BankAccountId && l.MovementDate >= desde && l.MovementDate <= hasta
                        && l.DuplicateDecision != StatementLineDuplicateDecision.Duplicate)
            .OrderBy(l => l.MovementDate).ThenBy(l => l.Id).ToListAsync(ct);
        var usadas = await LineasUsadasAsync(db, conciliacion, ct);
        return lineas.Where(l => !usadas.Contains(l.Id)).ToList();
    }

    /// <summary>Las líneas cruzadas o explicadas por una partida en esta conciliación.</summary>
    public static async Task<HashSet<long>> LineasUsadasAsync(IApplicationDbContext db, ConciliacionBancaria conciliacion, CancellationToken ct)
    {
        var cruzadas = await db.TreasuryReconciliationMatches.AsNoTracking().Where(m => m.ReconciliationId == conciliacion.Id && m.IsLive)
            .Select(m => m.StatementLineId).ToListAsync(ct);
        var conPartida = await db.TreasuryReconcilingItems.AsNoTracking()
            .Where(i => i.ReconciliationId == conciliacion.Id && !i.IsVoided && i.StatementLineId != null).Select(i => i.StatementLineId!.Value).ToListAsync(ct);
        return [.. cruzadas, .. conPartida];
    }

    /// <summary>
    /// Recalcula el saldo del extracto (el que se fijó al abrir, o el último saldo del mes que trae el extracto), el saldo en libros a fin de mes por Contabilidad (E-04), el
    /// total de las partidas con su signo y la diferencia. Sin guardar.
    /// </summary>
    public static async Task RecalcularAsync(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad, OwnBankAccount cuenta, ConciliacionBancaria c,
        decimal? saldoDelExtracto, CancellationToken ct)
    {
        var fin = Fin(c);
        if (saldoDelExtracto is { } fijado) c.StatementBalance = fijado;
        else if (await SaldoDelExtractoAsync(db, c.BankAccountId, Inicio(c), fin, ct) is { } leido) c.StatementBalance = leido;
        c.LedgerBalance = await contabilidad.SaldoEnLibrosAsync(cuenta.PublicId, fin, ct);
        var partidas = db.TreasuryReconcilingItems.Local.Where(i => i.ReconciliationId == c.Id && !i.IsVoided)
            .Concat(await db.TreasuryReconcilingItems.AsNoTracking().Where(i => i.ReconciliationId == c.Id && !i.IsVoided).ToListAsync(ct))
            .GroupBy(i => i.PublicId).Select(g => g.First()).Where(i => !i.IsVoided).ToList();
        c.ReconcilingItemsTotal = partidas.Sum(i => Signo(i.Kind, i.UnidentifiedItemPublicId) * i.Amount);
        c.Difference = c.StatementBalance - c.LedgerBalance + c.ReconcilingItemsTotal;
    }

    /// <summary>El último saldo que el extracto informa en el mes: el de la última línea con saldo o el de cierre de la última carga que termina en el mes.</summary>
    public static async Task<decimal?> SaldoDelExtractoAsync(IApplicationDbContext db, int cuenta, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var deLinea = await db.TreasuryBankStatementLines.AsNoTracking()
            .Where(l => l.BankAccountId == cuenta && l.MovementDate >= desde && l.MovementDate <= hasta && l.Balance != null
                        && l.DuplicateDecision != StatementLineDuplicateDecision.Duplicate)
            .OrderByDescending(l => l.MovementDate).ThenByDescending(l => l.Id).Select(l => l.Balance).FirstOrDefaultAsync(ct);
        if (deLinea is not null) return deLinea;
        return await db.TreasuryBankStatements.AsNoTracking().Where(s => s.BankAccountId == cuenta && s.To >= desde && s.To <= hasta && s.ClosingBalance != null)
            .OrderByDescending(s => s.To).ThenByDescending(s => s.Id).Select(s => s.ClosingBalance).FirstOrDefaultAsync(ct);
    }

    /// <summary>Quienes prepararon o aprobaron lotes de pago de la cuenta en el mes (FR-217): los <c>SEC_Users.Id</c> que la segregación compara.</summary>
    public static async Task<IReadOnlyList<int>> ParticipantesDePagosAsync(IApplicationDbContext db, int cuenta, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var lotes = await db.PaymentBatches.AsNoTracking().Where(b => b.BankAccountId == cuenta && b.PayDate >= desde && b.PayDate <= hasta)
            .Select(b => new { b.PublicId, b.PreparedByUserId }).ToListAsync(ct);
        if (lotes.Count == 0) return [];
        var publicos = lotes.Select(l => l.PublicId).ToList();
        var aprobaron = await (from d in db.ApprovalDecisions.AsNoTracking()
                               join r in db.ApprovalRequests.AsNoTracking() on d.RequestId equals r.Id
                               where publicos.Contains(r.SourcePublicId)
                               select d.DecidedByUserId).ToListAsync(ct);
        return lotes.Select(l => l.PreparedByUserId).Concat(aprobaron).Distinct().ToList();
    }

    /// <summary>Los días de holgura del cruce (<c>Conciliacion.DiasDeHolgura</c>, vigente a fin de mes).</summary>
    public static async Task<int> HolguraAsync(ParametrosDeTesoreria parametros, DateOnly fecha, CancellationToken ct) =>
        await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ConciliacionDiasDeHolgura, fecha, ct) ?? 0;
}
