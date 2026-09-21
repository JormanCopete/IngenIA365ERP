using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Contabiliza una liquidación especial (feature 010, FR-004, research «Contabilización de las
/// liquidaciones») sobre <see cref="PayrollAccountingPoster"/>, que a su vez va por
/// <see cref="AccountingPoster"/>: el único camino al libro. Lo que agrega sobre la ordinaria:
/// <list type="bullet">
/// <item><c>SourceType</c> por tipo de corrida (<c>ServiceBonusRun</c>, <c>SeveranceRun</c>, <c>VacationRun</c>, <c>SettlementRun</c>).</item>
/// <item>La fecha del comprobante es la que pide quien aprueba (<c>postingDate</c>); por defecto la
/// fecha de corte (D-04: causar contra la provisión en el mes correcto), nunca anterior al corte ni
/// posterior a hoy (<c>Payroll.Settlement.PostingDateInvalid</c>).</item>
/// <item>Los conceptos pares provisión/ajuste ya vienen del motor como líneas: el rubro (<c>PRIMA</c>)
/// contra la provisión, el ajuste (<c>PRIMA_AJUSTE_PROV</c>) con signo; un ajuste negativo (liberación)
/// invierte débito y crédito, cosa que el poster de nómina ya hace con cualquier valor negativo.</item>
/// <item>En las cesantías anuales, <c>CESANTIAS</c> deja la cuenta por pagar <b>al fondo</b> (la persona
/// vinculada al fondo del empleado, FR-088) e <c>INT_CESANTIAS</c> al empleado; en la definitiva ambas
/// van al empleado.</item>
/// <item><c>DESC_CARTERA</c> no genera asiento (Cartera contabiliza el recaudo, D-08): viene con <c>AffectsAccounting = false</c>.</item>
/// <item>Antes de tocar el contexto comprueba que la contabilidad esté iniciada y que todo concepto con
/// asiento tenga cuentas; si falta una responde <c>Payroll.Settlement.ConceptAccountsMissing</c> con la
/// lista y no agrega nada al libro.</item>
/// </list>
/// </summary>
public sealed class SettlementAccountingPoster(IApplicationDbContext db, PayrollAccountingPoster poster, IDateTimeService clock)
{
    /// <summary>La fecha del comprobante que rige: la pedida, o el corte (D-04). Falla si sale del rango [corte, hoy].</summary>
    public Result<DateOnly> ResolverFecha(PayrollRun run, DateOnly? postingDate)
    {
        var corte = run.CutoffDate ?? throw new InvalidOperationException("Una liquidación especial siempre tiene fecha de corte.");
        var hoy = clock.TodayUtc;
        var fecha = postingDate ?? corte;
        if (fecha < corte || fecha > hoy)
            return Result.Failure<DateOnly>(SettlementErrors.PostingDateInvalid(fecha, corte, hoy));
        return Result.Success(fecha);
    }

    /// <summary>Los conceptos con asiento de la liquidación que no tienen cuentas (ni por centro de costo ni por defecto).</summary>
    public async Task<IReadOnlyList<string>> ConceptosSinCuentasAsync(IEnumerable<PayrollRunLine> lines, CancellationToken ct)
    {
        var codigos = lines.Where(l => l.AffectsAccounting && l.Amount != 0m)
            .Select(l => l.ConceptCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (codigos.Count == 0) return [];
        var conCuentas = await db.PayrollConceptDefinitionAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && codigos.Contains(a.ConceptCode))
            .Select(a => a.ConceptCode)
            .Distinct()
            .ToListAsync(ct);
        return codigos.Where(c => !conCuentas.Contains(c, StringComparer.OrdinalIgnoreCase)).OrderBy(c => c, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Arma y prepara el comprobante de la liquidación SIN guardar: quien aprueba lo guarda junto
    /// con la corrida en un solo <c>SaveChanges</c>, o nada.
    /// </summary>
    public async Task<Result<AccountingDocument>> PostAsync(
        PayrollRun run,
        IReadOnlyList<(PayrollRunEmployee RunEmployee, Employee Employee, IReadOnlyList<PayrollRunLine> Lines)> employees,
        DateOnly? postingDate,
        string detail,
        CancellationToken ct)
    {
        if (!run.EsEspecial)
            throw new InvalidOperationException("La nómina ordinaria se contabiliza con PayrollAccountingPoster; este contabilizador es de las liquidaciones especiales.");

        var fecha = ResolverFecha(run, postingDate);
        if (fecha.IsFailure) return Result.Failure<AccountingDocument>(fecha.Error);

        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct))
            return Result.Failure<AccountingDocument>(SettlementErrors.AccountingNotInitialized);

        var lineas = employees.SelectMany(e => e.Lines).ToList();
        var sinCuentas = await ConceptosSinCuentasAsync(lineas, ct);
        if (sinCuentas.Count > 0)
            return Result.Failure<AccountingDocument>(SettlementErrors.ConceptAccountsMissing(sinCuentas));
        if (!lineas.Any(l => l.AffectsAccounting && l.Amount != 0m))
            return Result.Failure<AccountingDocument>(SettlementErrors.NothingToPost);

        var resultado = await poster.PostAsync(PayrollAccountingPoster.OrigenDe(run), employees, fecha.Value, detail, TerceroPara(run.Kind), ct);
        if (resultado.IsFailure)
        {
            // El poster de nómina habla en los códigos de la ordinaria; aquí el contrato (§3.5) pide los propios.
            return resultado.Error.Code switch
            {
                "Payroll.ConceptWithoutAccounts" => Result.Failure<AccountingDocument>(SettlementErrors.ConceptAccountsMissing(sinCuentas)),
                "Payroll.NothingToPost" => Result.Failure<AccountingDocument>(SettlementErrors.NothingToPost),
                "Accounting.NotInitialized" => Result.Failure<AccountingDocument>(SettlementErrors.AccountingNotInitialized),
                _ => resultado,
            };
        }
        return resultado;
    }

    /// <summary>Espejo del comprobante de la liquidación (FR-032), con el mismo origen por tipo.</summary>
    public Task<Result<AccountingDocument>> ReverseAsync(PayrollRun run, AccountingDocument original, DateOnly documentDate, string reason, CancellationToken ct) =>
        poster.ReverseAsync(original, documentDate, reason, PayrollAccountingPoster.OrigenDe(run), ct);

    /// <summary>
    /// Con quién se contabiliza cada línea según el tipo: en las cesantías anuales la cuenta por pagar
    /// de <c>CESANTIAS</c> es del fondo (la consignación va allá, FR-011); en todo lo demás rige la regla
    /// de la ordinaria (empleado en devengos y deducciones; entidad en aportes y provisiones).
    /// </summary>
    public static Func<PayrollRunLine, EntidadInstitucional> TerceroPara(PayrollRunKind kind) => line =>
    {
        if (kind == PayrollRunKind.Severance && line.Nature == ConceptNature.Earning
            && line.ConceptCode.Equals(WellKnownConceptCodes.Severance, StringComparison.OrdinalIgnoreCase))
            return EntidadInstitucional.FondoDeCesantias;
        return TercerosDeNomina.EntidadDe(line.ConceptCode, line.Nature);
    };
}
