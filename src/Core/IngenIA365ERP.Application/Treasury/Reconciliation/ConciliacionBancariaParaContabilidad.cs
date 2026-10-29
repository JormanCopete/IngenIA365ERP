using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Reconciliation;

/// <summary>
/// Lo que Tesorería ofrece a Contabilidad sobre la conciliación bancaria (feature 015, US25, T0278a; E-04; TR41; FR-167, FR-216): al reabrir un período contable, las
/// <c>TES_Reconciliations</c> cerradas de ese mes quedan <see cref="ReconciliationState.Outdated"/> con su motivo y un evento de auditoría por cada una; se vuelven a
/// conciliar con una versión nueva. Valida y audita (desviación III), escribe en la unidad de trabajo de quien llama y no guarda. Sin conciliaciones, cero. (nuevo)
/// </summary>
public sealed class ConciliacionBancariaParaContabilidad(IApplicationDbContext db, AuditoriaDeTesoreria auditoria, IDateTimeService reloj)
    : IConciliacionBancariaParaContabilidad
{
    public async Task<int> MarcarDesactualizadasAsync(int anio, int mes, string motivo, CancellationToken ct)
    {
        ValidarPedido(anio, mes, motivo);
        var cerradas = await db.TreasuryReconciliations.Where(c => c.PeriodYear == anio && c.PeriodMonth == mes && c.Status == ReconciliationState.Closed).ToListAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var c in cerradas)
        {
            c.Status = ReconciliationState.Outdated;
            c.OutdatedAt = ahora;
            c.OutdatedReason = motivo.Trim().Length <= 500 ? motivo.Trim() : motivo.Trim()[..500];
            await AuditarAsync(c.PublicId, anio, mes, c.OutdatedReason, ct);
        }
        return cerradas.Count;
    }

    /// <summary>El validador del puerto (desviación III).</summary>
    public static void ValidarPedido(int anio, int mes, string motivo)
    {
        if (anio is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(anio), anio, "El año del período no es válido.");
        if (mes is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(mes), mes, "El mes del período no es válido.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("El motivo de la reapertura es obligatorio.", nameof(motivo));
    }

    private Task AuditarAsync(Guid conciliacion, int anio, int mes, string motivo, CancellationToken ct) =>
        auditoria.AgregarAsync("Treasury.Reconciliation.Outdated", "Reconciliation", conciliacion, new { status = "Closed" },
            new { status = "Outdated", year = anio, month = mes, reason = motivo }, ct);
}
