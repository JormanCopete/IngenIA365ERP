using IngenIA365ERP.Application.Common.Integration.Payroll;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Integration;

/// <summary>
/// La implementación de Nómina de <see cref="IDescuentosDeNominaParaCartera"/> (feature 028, T064a; R6 «Nómina propia»). Sólo lee tablas de Nómina:
/// <list type="bullet">
/// <item><b>Calculado</b>: una fila <c>PAY_PayrollLoanDeductions</c> calculada (<c>Calculated</c>) de la corrida ordinaria en borrador (<c>Draft</c> o
/// <c>Stale</c>) de un período que no se ha aprobado; el valor es lo que la corrida descuenta;</item>
/// <item><b>Generado</b>: la novedad <c>LoanDeduction</c> activa de un período que no se ha aprobado, sin corrida que la calcule; el valor es el de la
/// novedad;</item>
/// <item><b>Aprobado</b>: la fila aplicada (total o parcialmente) de la corrida ordinaria aprobada de un período que no había terminado a la fecha; el valor
/// es lo que Cartera aplicó.</item>
/// </list>
/// Los vivos no se acotan por la fecha (una nómina atrasada sigue descontando); los aprobados, sí: lo aprobado de un período ya terminado es historia. No
/// escribe ni audita. (nuevo)
/// </summary>
public sealed class DescuentosDeNominaParaCartera(IApplicationDbContext db) : IDescuentosDeNominaParaCartera
{
    private static readonly PayPeriodStatus[] Abiertos = [PayPeriodStatus.Open, PayPeriodStatus.Calculated];
    private static readonly PayrollRunStatus[] EnBorrador = [PayrollRunStatus.Draft, PayrollRunStatus.Stale];
    private static readonly PayrollLoanDeductionStatus[] Aplicadas = [PayrollLoanDeductionStatus.Applied, PayrollLoanDeductionStatus.PartiallyApplied];

    public async Task<IReadOnlyList<DescuentoDeNominaVivoDto>> VivosAsync(IReadOnlyCollection<Guid> obligaciones, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(obligaciones);
        if (obligaciones.Count == 0) return [];
        var ids = obligaciones.Distinct().ToList();
        var corte = fecha.ToDateTime(TimeOnly.MinValue);

        var deCorridas = await (
                from d in db.PayrollLoanDeductions.AsNoTracking()
                join r in db.PayrollRuns.AsNoTracking() on d.PayrollRunId equals r.Id
                join p in db.PayPeriods.AsNoTracking() on r.PayPeriodId equals p.Id
                where ids.Contains(d.ObligationPublicId) && !d.IsDeleted && !r.IsDeleted && !p.IsDeleted && r.Kind == PayrollRunKind.Ordinary
                      && ((EnBorrador.Contains(r.Status) && d.Status == PayrollLoanDeductionStatus.Calculated && Abiertos.Contains(p.Status))
                          || (r.Status == PayrollRunStatus.Approved && Aplicadas.Contains(d.Status) && p.EndDate >= corte))
                select new
                {
                    d.ObligationPublicId, PeriodoId = p.Id, PeriodoPublico = p.PublicId, p.Description, p.StartDate, p.EndDate, Corrida = r.PublicId, r.Version,
                    Aprobada = r.Status == PayrollRunStatus.Approved, d.DeductedAmount, d.AppliedAmount,
                })
            .ToListAsync(ct);
        var novedades = await (
                from n in db.PayrollNovelties.AsNoTracking()
                join p in db.PayPeriods.AsNoTracking() on n.PayPeriodId equals p.Id
                where n.ObligationPublicId != null && ids.Contains(n.ObligationPublicId.Value) && !n.IsDeleted && !p.IsDeleted
                      && n.Origin == NoveltyOrigin.LoanDeduction && n.Status == NoveltyStatus.Active && Abiertos.Contains(p.Status)
                select new
                {
                    Obligacion = n.ObligationPublicId!.Value, PeriodoId = p.Id, PeriodoPublico = p.PublicId, p.Description, p.StartDate, p.EndDate,
                    n.InstallmentNumber, n.Amount,
                })
            .ToListAsync(ct);
        var cuotaDeLaNovedad = novedades.GroupBy(n => (n.Obligacion, n.PeriodoId)).ToDictionary(g => g.Key, g => g.First().InstallmentNumber);

        var vivos = new List<DescuentoDeNominaVivoDto>();
        // La corrida en borrador o aprobada de cada obligación y período (la de versión mayor).
        foreach (var g in deCorridas.GroupBy(x => (x.ObligationPublicId, x.PeriodoId, x.Aprobada)))
        {
            var x = g.MaxBy(y => y.Version)!;
            vivos.Add(new DescuentoDeNominaVivoDto(x.ObligationPublicId,
                x.Aprobada ? EstadoDelDescuentoDeNomina.Approved : EstadoDelDescuentoDeNomina.Calculated,
                x.PeriodoPublico, Periodo(x.Description, x.StartDate, x.EndDate), DateOnly.FromDateTime(x.StartDate), DateOnly.FromDateTime(x.EndDate),
                x.Corrida, x.Version, cuotaDeLaNovedad.GetValueOrDefault((x.ObligationPublicId, x.PeriodoId)),
                x.Aprobada ? x.AppliedAmount ?? x.DeductedAmount : x.DeductedAmount));
        }
        // La novedad sin corrida que la calcule.
        var calculadas = vivos.Select(v => (v.ObligationPublicId, v.PeriodPublicId)).ToHashSet();
        foreach (var g in novedades.GroupBy(n => (n.Obligacion, n.PeriodoId)))
        {
            var n = g.First();
            if (calculadas.Contains((n.Obligacion, n.PeriodoPublico))) continue;
            vivos.Add(new DescuentoDeNominaVivoDto(n.Obligacion, EstadoDelDescuentoDeNomina.Generated, n.PeriodoPublico,
                Periodo(n.Description, n.StartDate, n.EndDate), DateOnly.FromDateTime(n.StartDate), DateOnly.FromDateTime(n.EndDate), null, null,
                g.Count() == 1 ? n.InstallmentNumber : null, g.Sum(y => y.Amount ?? 0m)));
        }
        return vivos.OrderBy(v => v.ObligationPublicId).ThenBy(v => v.PeriodStart).ThenBy(v => v.State).ToList();
    }

    /// <summary>El nombre del período: su descripción o, sin ella, sus fechas.</summary>
    private static string Periodo(string? descripcion, DateTime desde, DateTime hasta) =>
        string.IsNullOrWhiteSpace(descripcion) ? $"{desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}" : descripcion.Trim();
}
