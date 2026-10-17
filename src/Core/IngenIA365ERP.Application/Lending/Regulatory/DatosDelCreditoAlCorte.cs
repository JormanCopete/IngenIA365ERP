using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Regulatory;

/// <summary>
/// Lo que los archivos regulatorios leen de un crédito a un corte, además del cierre o de la posición (feature 013, K5, US16; T1002, T1003): las
/// condiciones vigentes (la última versión con fecha efectiva hasta el corte), el plan vigente (su vencimiento final, la cuota siguiente al corte y
/// cuántas cuotas vencieron hasta el corte), el último pago no reversado y la última posición diaria. (nuevo)
/// </summary>
public sealed record CreditoAlCorte(
    RepaymentPeriodicity Periodicidad,
    int CuotasPactadas,
    decimal? TasaEfectivaAnual,
    int VecesReestructurado,
    DateOnly? ReestructuradoEl,
    DateOnly? FechaDeVencimiento,
    DateOnly? ProximoVencimiento,
    decimal? ValorDeLaCuota,
    int CuotasVencidasAlCorte,
    DateOnly? FechaDelUltimoPago,
    LoanDailyPosition? Posicion)
{
    /// <summary>Las cuotas vencidas menos las que la posición dice que siguen sin pagar.</summary>
    public int CuotasPagadas => Math.Max(0, CuotasVencidasAlCorte - (Posicion?.InstallmentsPastDue ?? 0));
}

/// <summary>Las lecturas de <see cref="CreditoAlCorte"/> para varios créditos a la vez (pocas consultas, no una por crédito). (nuevo)</summary>
public static class DatosDelCreditoAlCorte
{
    public static async Task<IReadOnlyDictionary<int, CreditoAlCorte>> LeerAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos, DateOnly corte,
        CancellationToken ct)
    {
        var ids = creditos.Distinct().ToList();
        var resultado = new Dictionary<int, CreditoAlCorte>(ids.Count);
        if (ids.Count == 0) return resultado;

        var condiciones = (await db.LoanTermsVersions.AsNoTracking().Where(v => ids.Contains(v.LoanId))
                .Select(v => new { v.LoanId, v.VersionNumber, v.EffectiveValueDate, v.AnnualRate, v.ResultingEffectiveRate, v.Periodicity, v.Installments,
                    v.RestructuringCount, v.RestructuredOn })
                .ToListAsync(ct))
            .GroupBy(v => v.LoanId)
            .ToDictionary(g => g.Key, g => g.Where(v => v.EffectiveValueDate <= corte).OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                                           ?? g.OrderBy(v => v.VersionNumber).First());
        var planes = (await db.RepaymentScheduleVersions.AsNoTracking().Where(v => ids.Contains(v.LoanId))
                .Select(v => new { v.Id, v.LoanId, v.VersionNumber, v.EffectiveValueDate, v.FirstRegeneratedInstallment }).ToListAsync(ct))
            .GroupBy(v => v.LoanId)
            .ToDictionary(g => g.Key, g => g.Where(v => v.EffectiveValueDate <= corte).OrderByDescending(v => v.VersionNumber).FirstOrDefault()
                                           ?? g.OrderBy(v => v.VersionNumber).First());
        var planIds = planes.Values.Select(p => p.Id).ToList();
        var cuotas = (await db.RepaymentInstallments.AsNoTracking().Where(i => planIds.Contains(i.ScheduleVersionId))
                .Select(i => new { i.ScheduleVersionId, i.InstallmentNumber, i.DueDate, i.Total }).ToListAsync(ct))
            .ToLookup(i => i.ScheduleVersionId);
        var pagos = (await db.LoanOperations.AsNoTracking()
                .Where(o => ids.Contains(o.LoanId) && o.Kind == LoanOperationKind.Payment && o.ValueDate <= corte && o.ReversedByOperationId == null)
                .Select(o => new { o.LoanId, o.ValueDate }).ToListAsync(ct))
            .GroupBy(o => o.LoanId).ToDictionary(g => g.Key, g => g.Max(o => o.ValueDate));
        var posiciones = await UltimasPosicionesAsync(db, ids, corte, ct);

        foreach (var id in ids)
        {
            var c = condiciones.GetValueOrDefault(id);
            var plan = planes.GetValueOrDefault(id);
            var delPlan = plan is null ? [] : cuotas[plan.Id].OrderBy(i => i.InstallmentNumber).ToList();
            var proxima = delPlan.FirstOrDefault(i => i.DueDate > corte);
            var vencidas = delPlan.Where(i => i.DueDate <= corte).Select(i => (int?)i.InstallmentNumber).Max()
                           ?? (plan is null ? 0 : Math.Max(0, plan.FirstRegeneratedInstallment - 1));
            resultado[id] = new CreditoAlCorte(
                c?.Periodicity ?? RepaymentPeriodicity.Monthly,
                c?.Installments ?? 0,
                c?.ResultingEffectiveRate ?? c?.AnnualRate,
                c?.RestructuringCount ?? 0,
                c?.RestructuredOn,
                delPlan.Count == 0 ? null : delPlan.Max(i => i.DueDate),
                proxima?.DueDate,
                (proxima ?? delPlan.LastOrDefault())?.Total,
                vencidas,
                pagos.TryGetValue(id, out var pago) ? pago : null,
                posiciones.GetValueOrDefault(id));
        }
        return resultado;
    }

    /// <summary>La última posición diaria (la de mayor versión del último día medido) de cada crédito hasta el corte.</summary>
    public static async Task<IReadOnlyDictionary<int, LoanDailyPosition>> UltimasPosicionesAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos,
        DateOnly corte, CancellationToken ct)
    {
        var ids = creditos.Distinct().ToList();
        var dias = await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.PositionDate <= corte)
            .GroupBy(p => p.LoanId).Select(g => new { LoanId = g.Key, Dia = g.Max(p => p.PositionDate) }).ToListAsync(ct);
        if (dias.Count == 0) return new Dictionary<int, LoanDailyPosition>();
        var fechas = dias.Select(d => d.Dia).Distinct().ToList();
        var porCredito = dias.ToDictionary(d => d.LoanId, d => d.Dia);
        var filas = await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId) && fechas.Contains(p.PositionDate)).ToListAsync(ct);
        return filas.Where(p => porCredito[p.LoanId] == p.PositionDate).GroupBy(p => p.LoanId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Version).First());
    }
}
