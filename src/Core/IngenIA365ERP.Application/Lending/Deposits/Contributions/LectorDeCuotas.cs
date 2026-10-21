using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

/// <summary>
/// Una cuota exigible leída con lo pagado (Σ de las filas del libro con su <c>InstallmentId</c>, espejos incluidos) y el último no descontado que
/// informó un recaudador (feature 014, US3, T082–T084). El estado y lo pendiente se calculan al leer (data-model §3.5). (nuevo)
/// </summary>
public sealed record CuotaLeida(AccountInstallment Cuota, decimal Pagado, NonDeductionReason? MotivoDeNoDescuento, DateOnly? NoDescontadaEl)
{
    public decimal Pendiente => Cuota.Pendiente(Pagado);

    public EstadoDeCuotaDeCuenta Estado => Cuota.Estado(Pagado, MotivoDeNoDescuento is not null);

    /// <summary>¿Vencida y con algo pendiente a <paramref name="fecha"/>? (la mora de una cuota de cuenta no causa nada: sólo se cuenta).</summary>
    public bool EnMoraEl(DateOnly fecha) => Pendiente > 0m && Cuota.DueDate < fecha;
}

/// <summary>
/// El único lector de las cuotas exigibles de las cuentas (feature 014, US3): en bloque, con lo pagado por cuota en una sola consulta agrupada y el
/// último no descontado de cada una. Lo usan el contrato de cuentas (exigibles y consignar), la consulta de aportes (cuotas en mora), las pantallas y
/// los informes. Sólo lee, sin seguimiento salvo que se pida. (nuevo)
/// </summary>
public static class LectorDeCuotas
{
    /// <summary>
    /// Las cuotas de las cuentas, con lo pagado según lo registrado hasta <paramref name="registradoHasta"/> (nulo = todo). Sin las anuladas salvo
    /// <paramref name="incluirAnuladas"/>. En orden de cuenta, vencimiento y período.
    /// </summary>
    public static async Task<List<CuotaLeida>> DeCuentasAsync(IApplicationDbContext db, IReadOnlyCollection<int> cuentas, CancellationToken ct,
        bool incluirAnuladas = false, DateTime? registradoHasta = null, bool seguir = false)
    {
        if (cuentas.Count == 0) return [];
        var ids = cuentas.Distinct().ToArray();
        var consulta = seguir ? db.AccountInstallments : db.AccountInstallments.AsNoTracking();
        var cuotas = await consulta.Where(c => ids.Contains(c.MemberAccountId) && !c.IsDeleted && (incluirAnuladas || c.CancelledAt == null))
            .ToListAsync(ct);
        return await ConPagadoAsync(db, cuotas, ct, registradoHasta);
    }

    /// <summary>Las cuotas nombradas por su PublicId (consignar a cuotas específicas, no descontados).</summary>
    public static async Task<List<CuotaLeida>> PorPublicIdAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> publicos, CancellationToken ct,
        bool seguir = false)
    {
        if (publicos.Count == 0) return [];
        var ids = publicos.Distinct().ToArray();
        var consulta = seguir ? db.AccountInstallments : db.AccountInstallments.AsNoTracking();
        var cuotas = await consulta.Where(c => ids.Contains(c.PublicId) && !c.IsDeleted).ToListAsync(ct);
        return await ConPagadoAsync(db, cuotas, ct, null);
    }

    private static async Task<List<CuotaLeida>> ConPagadoAsync(IApplicationDbContext db, List<AccountInstallment> cuotas, CancellationToken ct,
        DateTime? registradoHasta)
    {
        if (cuotas.Count == 0) return [];
        var ids = cuotas.Select(c => c.Id).ToArray();
        var hasta = registradoHasta ?? DateTime.MaxValue;
        var pagado = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.InstallmentId != null && ids.Contains(t.InstallmentId.Value) && !t.IsDeleted && t.RegisteredAt <= hasta)
            .GroupBy(t => t.InstallmentId!.Value)
            .Select(g => new { Cuota = g.Key, Total = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.Cuota, x => x.Total, ct);
        var noDescontados = (await db.AccountInstallmentNonDeductions.AsNoTracking()
                .Where(n => ids.Contains(n.InstallmentId) && !n.IsDeleted)
                .Select(n => new { n.InstallmentId, n.Reason, n.OccurredOn, n.Id }).ToListAsync(ct))
            .GroupBy(n => n.InstallmentId).ToDictionary(g => g.Key, g => g.OrderByDescending(n => n.OccurredOn).ThenByDescending(n => n.Id).First());
        return cuotas
            .OrderBy(c => c.MemberAccountId).ThenBy(c => c.DueDate).ThenBy(c => c.PeriodYear).ThenBy(c => c.PeriodMonth).ThenBy(c => c.SubPeriodNumber)
            .Select(c =>
            {
                var nd = noDescontados.GetValueOrDefault(c.Id);
                return new CuotaLeida(c, pagado.GetValueOrDefault(c.Id), nd?.Reason, nd?.OccurredOn);
            })
            .ToList();
    }
}
