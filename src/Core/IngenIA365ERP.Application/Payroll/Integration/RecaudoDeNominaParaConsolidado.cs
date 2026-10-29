using IngenIA365ERP.Application.Common.Integration.Payroll;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Integration;

/// <summary>
/// La implementación de Nómina de <see cref="IRecaudoDeNominaParaConsolidado"/> (feature 015, R1, T0400; E-05): las deducciones de crédito y de cuenta aplicadas (total o
/// parcialmente) de las corridas aprobadas, por la fecha de pago de la corrida. Sólo lee tablas de Nómina. (nuevo)
/// </summary>
public sealed class RecaudoDeNominaParaConsolidado(IApplicationDbContext db) : IRecaudoDeNominaParaConsolidado
{
    private static readonly PayrollLoanDeductionStatus[] Aplicadas = [PayrollLoanDeductionStatus.Applied, PayrollLoanDeductionStatus.PartiallyApplied];

    public async Task<IReadOnlyList<RecaudoDeNominaDelDiaDto>> PorDiaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var creditos = await (from d in db.PayrollLoanDeductions.AsNoTracking()
                              join r in db.PayrollRuns.AsNoTracking() on d.PayrollRunId equals r.Id
                              where r.Status == PayrollRunStatus.Approved && r.PayDate != null && r.PayDate >= desde && r.PayDate <= hasta && Aplicadas.Contains(d.Status)
                              select new { Dia = r.PayDate!.Value, Valor = d.AppliedAmount ?? d.DeductedAmount }).ToListAsync(ct);
        var cuentas = await (from d in db.PayrollAccountDeductions.AsNoTracking()
                             join r in db.PayrollRuns.AsNoTracking() on d.PayrollRunId equals r.Id
                             where r.Status == PayrollRunStatus.Approved && r.PayDate != null && r.PayDate >= desde && r.PayDate <= hasta && Aplicadas.Contains(d.Status)
                             select new { Dia = r.PayDate!.Value, Valor = d.AppliedAmount ?? d.DeductedAmount }).ToListAsync(ct);
        return creditos.Select(c => (c.Dia, Credito: c.Valor, Cuenta: 0m)).Concat(cuentas.Select(c => (c.Dia, Credito: 0m, Cuenta: c.Valor)))
            .GroupBy(x => x.Dia).OrderBy(g => g.Key)
            .Select(g => new RecaudoDeNominaDelDiaDto(g.Key, g.Sum(x => x.Credito), g.Sum(x => x.Cuenta), g.Count())).ToList();
    }
}
