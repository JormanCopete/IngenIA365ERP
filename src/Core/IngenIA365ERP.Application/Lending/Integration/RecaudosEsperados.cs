using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// <see cref="IRecaudosEsperadosDeCartera"/> (feature 015, T2, fase 43, T0459; E-01 d; FR-164 d): lo programado de las cuotas de los créditos activos que vencen entre
/// dos fechas, por día, del plan vigente de cada crédito (la última versión del plan). Es lo **programado**, no lo pendiente: un pago anticipado parcial no lo descuenta
/// (la posición es una proyección; el saldo exacto lo da <c>ReconstruccionDeCredito</c> y no se recalcula aquí crédito por crédito). Sin datos personales. (nuevo)
/// </summary>
public sealed class RecaudosEsperadosDeCartera(IApplicationDbContext db) : IRecaudosEsperadosDeCartera
{
    public async Task<IReadOnlyList<RecaudoEsperadoDelDiaDto>> PorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) return [];
        var vigentes = from v in db.RepaymentScheduleVersions.AsNoTracking()
                       join l in db.Loans.AsNoTracking() on v.LoanId equals l.Id
                       where l.Status == LoanStatus.Active
                             && v.VersionNumber == db.RepaymentScheduleVersions.Where(x => x.LoanId == v.LoanId).Max(x => x.VersionNumber)
                       select v.Id;
        var cuotas = await db.RepaymentInstallments.AsNoTracking()
            .Where(c => vigentes.Contains(c.ScheduleVersionId) && c.DueDate >= desde && c.DueDate <= hasta)
            .Select(c => new { c.DueDate, c.Total })
            .ToListAsync(ct);
        return cuotas.GroupBy(c => c.DueDate).OrderBy(g => g.Key).Select(g => new RecaudoEsperadoDelDiaDto(g.Key, g.Count(), g.Sum(c => c.Total))).ToList();
    }
}

/// <summary>
/// <see cref="IRecaudosEsperadosDeCaptaciones"/> (feature 015, T2, fase 43, T0459; E-02 f; FR-165 f): lo programado de las cuotas de aportes y ahorros (no anuladas) de las
/// cuentas activas que vencen entre dos fechas, por día. Lo programado, no lo pendiente (como en Cartera). Sin datos personales. (nuevo)
/// </summary>
public sealed class RecaudosEsperadosDeCaptaciones(IApplicationDbContext db) : IRecaudosEsperadosDeCaptaciones
{
    public async Task<IReadOnlyList<RecaudoEsperadoDelDiaDto>> PorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) return [];
        var cuotas = await (from c in db.AccountInstallments.AsNoTracking()
                            join a in db.MemberAccounts.AsNoTracking() on c.MemberAccountId equals a.Id
                            where a.Status == MemberAccountStatus.Active && c.CancelledAt == null && c.DueDate >= desde && c.DueDate <= hasta
                            select new { c.DueDate, c.Amount }).ToListAsync(ct);
        return cuotas.GroupBy(c => c.DueDate).OrderBy(g => g.Key).Select(g => new RecaudoEsperadoDelDiaDto(g.Key, g.Count(), g.Sum(c => c.Amount))).ToList();
    }
}
