using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Contabiliza una corrida aprobada (D-07, FR-019, FR-023) replicando el patrón de
/// <c>CreateDocumentCommand</c> —tipo de comprobante, período contable abierto,
/// numeración, documento, movimientos y saldos— pero SIN guardar: el comando de
/// aprobación guarda todo en una sola transacción. Si algo falta (cuentas de un concepto,
/// comprobante <c>NM</c>, período contable), devuelve el error y no toca el contexto.
///
/// <para>
/// Cada línea que afecta contabilidad se agrupa por concepto y centro de costo del
/// empleado, y toma las cuentas de <c>PAY_ConceptDefinitionAccounts</c> (la fila del
/// centro de costo, o la fila por defecto): el valor va al débito de la cuenta débito y al
/// crédito de la cuenta crédito. La naturaleza del concepto ya está en la configuración
/// de cuentas, así que el comprobante cuadra por construcción. Un valor negativo (ajuste
/// de redondeo en contra) invierte débito y crédito.
/// </para>
/// </summary>
public sealed class PayrollAccountingPoster(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService currentUser)
{
    public const string VoucherCode = "NM";
    public const string ModuleCode = "NOM";
    private const int DetailMaxLength = 200;
    private const string AccountingPeriodModule = "CNT";

    public sealed record Posting(AccountingDocument Document, IReadOnlyList<JournalEntry> Entries);

    public async Task<Result<Posting>> PostAsync(
        PayrollRun run,
        IReadOnlyList<(PayrollRunEmployee RunEmployee, Employee Employee, IReadOnlyList<PayrollRunLine> Lines)> employees,
        DateOnly documentDate,
        string detail,
        CancellationToken ct)
    {
        var voucher = await db.VoucherTypes.FirstOrDefaultAsync(v => v.Code == VoucherCode && !v.IsDeleted, ct);
        if (voucher is null)
            return Result.Failure<Posting>(new Error("Payroll.VoucherTypeMissing",
                $"No existe el tipo de comprobante {VoucherCode} (Nómina). Reaplique la semilla de nómina o créelo en Contabilidad."));

        var periodoAbierto = await PeriodoContableAbiertoAsync(documentDate, ct);
        if (!periodoAbierto)
            return Result.Failure<Posting>(new Error("Payroll.AccountingPeriodClosed",
                $"El período contable {documentDate:yyyy-MM} está cerrado o no existe: no se puede contabilizar la nómina."));

        // --- centro de costo de cada empleado (código legado → Id) ---
        var codigosCc = employees.Select(e => e.Employee.CostCenterId).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var centros = await db.CostCenters.AsNoTracking()
            .Where(c => !c.IsDeleted && c.LegacyCode != null && codigosCc.Contains(c.LegacyCode))
            .Select(c => new { c.Id, c.LegacyCode })
            .ToListAsync(ct);
        var ccPorCodigo = centros.ToDictionary(c => c.LegacyCode!, c => c.Id, StringComparer.OrdinalIgnoreCase);
        var ccPorDefecto = await db.CostCenters.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct) ?? 0;
        var sucursal = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct) ?? 0;

        // --- agrupar por (concepto, centro de costo) ---
        var grupos = new Dictionary<(string Code, int CostCenterId), decimal>();
        foreach (var (_, employee, lines) in employees)
        {
            var cc = !string.IsNullOrWhiteSpace(employee.CostCenterId) && ccPorCodigo.TryGetValue(employee.CostCenterId, out var id) ? id : ccPorDefecto;
            foreach (var line in lines.Where(l => l.AffectsAccounting && l.Amount != 0m))
            {
                var key = (line.ConceptCode, cc);
                grupos[key] = grupos.GetValueOrDefault(key) + line.Amount;
            }
        }

        // --- cuentas por concepto ---
        var codigos = grupos.Keys.Select(k => k.Code).Distinct().ToList();
        var cuentas = await db.PayrollConceptDefinitionAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && codigos.Contains(a.ConceptCode))
            .ToListAsync(ct);
        var sinCuentas = new List<string>();
        var asientos = new List<(string Code, int CostCenterId, int DebitAccountId, int CreditAccountId, decimal Amount)>();
        foreach (var ((code, cc), amount) in grupos.OrderBy(g => g.Key.Code, StringComparer.Ordinal).ThenBy(g => g.Key.CostCenterId))
        {
            var fila = cuentas.FirstOrDefault(a => a.ConceptCode.Equals(code, StringComparison.OrdinalIgnoreCase) && a.CostCenterId == cc)
                    ?? cuentas.FirstOrDefault(a => a.ConceptCode.Equals(code, StringComparison.OrdinalIgnoreCase) && a.CostCenterId == null);
            if (fila is null) { if (!sinCuentas.Contains(code)) sinCuentas.Add(code); continue; }
            asientos.Add((code, cc, fila.DebitAccountId, fila.CreditAccountId, amount));
        }
        if (sinCuentas.Count > 0)
            return Result.Failure<Posting>(new Error("Payroll.ConceptWithoutAccounts",
                $"Conceptos liquidados sin cuentas contables configuradas: {string.Join(", ", sinCuentas)}. " +
                "Configúrelas en Nómina › Conceptos › Cuentas antes de aprobar."));

        if (asientos.Count == 0)
            return Result.Failure<Posting>(new Error("Payroll.NothingToPost",
                "La corrida no tiene líneas que afecten contabilidad."));

        var lineas = new List<(int AccountId, int CostCenterId, decimal Debit, decimal Credit, string Description)>();
        foreach (var a in asientos)
        {
            var valor = Math.Abs(a.Amount);
            var (debito, credito) = a.Amount >= 0m ? (a.DebitAccountId, a.CreditAccountId) : (a.CreditAccountId, a.DebitAccountId);
            lineas.Add((debito, a.CostCenterId, valor, 0m, $"Nómina {a.Code}"));
            lineas.Add((credito, a.CostCenterId, 0m, valor, $"Nómina {a.Code}"));
        }

        return Result.Success(Crear(voucher, documentDate, detail, lineas, sucursal));
    }

    /// <summary>Comprobante reverso del original (FR-032): mismos movimientos con débito y crédito invertidos.</summary>
    public async Task<Result<Posting>> ReverseAsync(AccountingDocument original, DateOnly documentDate, string reason, CancellationToken ct)
    {
        var voucher = await db.VoucherTypes.FirstOrDefaultAsync(v => v.Code == VoucherCode && !v.IsDeleted, ct);
        if (voucher is null)
            return Result.Failure<Posting>(new Error("Payroll.VoucherTypeMissing", $"No existe el tipo de comprobante {VoucherCode}."));

        if (!await PeriodoContableAbiertoAsync(documentDate, ct))
            return Result.Failure<Posting>(new Error("Payroll.AccountingPeriodClosedForReversal",
                $"El período contable {documentDate:yyyy-MM} está cerrado: la reversión no se puede contabilizar."));

        var movimientos = await db.JournalEntries.AsNoTracking()
            .Where(j => j.VoucherTypeCode == original.VoucherTypeCode && j.DocumentNumber == original.DocumentNumber && !j.IsDeleted)
            .OrderBy(j => j.Id)
            .ToListAsync(ct);
        if (movimientos.Count == 0)
            return Result.Failure<Posting>(new Error("Payroll.NothingToPost",
                $"El comprobante {original.VoucherTypeCode}-{original.DocumentNumber} no tiene movimientos que reversar."));

        var lineas = movimientos
            .Select(m => (m.AccountId, m.CostCenterId, m.CreditAmount, m.DebitAmount, $"Reversión {original.VoucherTypeCode}-{original.DocumentNumber}: {m.Description}"))
            .ToList();
        var sucursal = movimientos[0].BranchId;
        var detalle = $"Reversión del comprobante {original.VoucherTypeCode}-{original.DocumentNumber}: {reason}";

        return Result.Success(Crear(voucher, documentDate, detalle, lineas, sucursal));
    }

    private async Task<bool> PeriodoContableAbiertoAsync(DateOnly fecha, CancellationToken ct)
    {
        var periodo = await db.AccountingPeriods.AsNoTracking().FirstOrDefaultAsync(
            p => p.Year == fecha.Year && p.PeriodNumber == (byte)fecha.Month && p.ModuleCode == AccountingPeriodModule && !p.IsDeleted, ct);
        return periodo is not null && periodo.Status != "C";
    }

    private Posting Crear(VoucherType voucher, DateOnly fecha, string detalle,
        List<(int AccountId, int CostCenterId, decimal Debit, decimal Credit, string Description)> lineas, int sucursal)
    {
        var numero = voucher.NextSequenceNumber + 1;
        voucher.NextSequenceNumber = numero;
        var periodCode = fecha.Year * 100 + fecha.Month;
        var ahora = clock.UtcNow;
        var usuario = currentUser.UserName;

        // VoucherType se asigna además del código: el mapeo de VoucherType.Documents no
        // enlaza la navegación AccountingDocument.VoucherType, así que EF le creó una segunda
        // relación con FK sombra VoucherTypeId (NOT NULL en la base). Sin la navegación, el
        // INSERT viola esa FK; lo detectó la prueba e2e de aprobación.
        var documento = new AccountingDocument
        {
            VoucherType = voucher,
            VoucherTypeCode = voucher.Code,
            DocumentNumber = numero,
            Detail = detalle.Length <= DetailMaxLength ? detalle : detalle[..DetailMaxLength], // ACC_Documents.Detail: varchar(200)
            TotalDebit = lineas.Sum(l => l.Debit),
            TotalCredit = lineas.Sum(l => l.Credit),
            DocumentDate = fecha,
            IsClosed = false,
            IsVoided = false,
            PeriodCode = periodCode,
            ModuleCode = ModuleCode,
            CreatedAt = ahora,
            CreatedBy = usuario,
        };
        db.AccountingDocuments.Add(documento);

        var asientos = new List<JournalEntry>(lineas.Count);
        foreach (var l in lineas)
        {
            var asiento = new JournalEntry
            {
                VoucherTypeCode = voucher.Code,
                DocumentNumber = numero,
                AccountId = l.AccountId,
                BranchId = sucursal,
                CostCenterId = l.CostCenterId,
                PeriodCode = periodCode.ToString(),
                TransactionDate = fecha,
                Description = l.Description,
                DebitAmount = l.Debit,
                CreditAmount = l.Credit,
                Status = 0,
                UserName = usuario,
                DocumentType = ModuleCode,
                CreatedAt = ahora,
                CreatedBy = usuario,
            };
            db.JournalEntries.Add(asiento);
            asientos.Add(asiento);
        }

        // Saldos por cuenta y período, como CreateDocumentCommand. Se acumulan en memoria
        // antes de tocar la tabla para no repetir la misma clave dentro del comprobante.
        foreach (var grupo in lineas.GroupBy(l => (l.AccountId, l.CostCenterId)))
        {
            var (accountId, costCenterId) = grupo.Key;
            var debito = grupo.Sum(g => g.Debit);
            var credito = grupo.Sum(g => g.Credit);
            var saldo = db.AccountBalances.Local.FirstOrDefault(b =>
                            b.AccountId == accountId && b.PeriodYear == fecha.Year && b.PeriodMonth == (byte)fecha.Month
                            && b.BranchId == sucursal && b.CostCenterId == costCenterId)
                        ?? db.AccountBalances.FirstOrDefault(b =>
                            b.AccountId == accountId && b.PeriodYear == fecha.Year && b.PeriodMonth == (byte)fecha.Month
                            && b.BranchId == sucursal && b.CostCenterId == costCenterId);
            if (saldo is null)
            {
                db.AccountBalances.Add(new AccountBalance
                {
                    AccountId = accountId,
                    PeriodYear = fecha.Year,
                    PeriodMonth = (byte)fecha.Month,
                    BranchId = sucursal,
                    CostCenterId = costCenterId,
                    DebitAmount = debito,
                    CreditAmount = credito,
                    CreatedAt = ahora,
                    CreatedBy = usuario,
                });
            }
            else
            {
                saldo.DebitAmount += debito;
                saldo.CreditAmount += credito;
                saldo.UpdatedAt = ahora;
                saldo.UpdatedBy = usuario;
            }
        }

        return new Posting(documento, asientos);
    }
}
