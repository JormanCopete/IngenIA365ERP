using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// El <b>único lector</b> de <c>LND_PrepaymentPreferences</c> por crédito o por cupo rotativo (feature 013, US5, T0541; FR-035, FR-075, D24, T24):
/// la que rige a un instante es la última con <c>RegisteredAt ≤ R</c> (<see cref="PrepaymentPreference.Vigente"/>). Lo usan el contrato (abono sin
/// interacción), la simulación de pantalla, la consulta de la preferencia y el nacimiento de la obligación de una venta (<c>InheritedFromLimit</c>,
/// US6). Una preferencia con <c>Choice</c> nulo está revocada: el abono queda como saldo a favor. No escribe. (nuevo)
/// </summary>
public sealed class PreferenciaDePrepagoVigente(IApplicationDbContext db)
{
    /// <summary>La versión que rige para el crédito a <paramref name="hasta"/>; nula si nunca tuvo.</summary>
    public async Task<PrepaymentPreference?> DelCreditoAsync(int loanId, DateTime hasta, CancellationToken ct) =>
        PrepaymentPreference.Vigente(await HistoriaDelCreditoAsync(loanId, ct), hasta);

    /// <summary>La elección que rige para el crédito (nula si no tiene o la revocó).</summary>
    public async Task<PrepaymentChoice?> EleccionDelCreditoAsync(int loanId, DateTime hasta, CancellationToken ct) =>
        (await DelCreditoAsync(loanId, hasta, ct))?.Choice;

    /// <summary>Todas las versiones del crédito, de la más vieja a la más nueva.</summary>
    public async Task<IReadOnlyList<PrepaymentPreference>> HistoriaDelCreditoAsync(int loanId, CancellationToken ct) =>
        await db.PrepaymentPreferences.AsNoTracking().Where(p => p.LoanId == loanId && !p.IsDeleted)
            .OrderBy(p => p.RegisteredAt).ThenBy(p => p.VersionNumber).ToListAsync(ct);

    /// <summary>La que rige para un cupo rotativo (las filas de todas sus versiones) a <paramref name="hasta"/>.</summary>
    public async Task<PrepaymentPreference?> DelCupoAsync(IReadOnlyCollection<int> creditLimitIds, DateTime hasta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(creditLimitIds);
        var ids = creditLimitIds.ToList();
        var versiones = await db.PrepaymentPreferences.AsNoTracking()
            .Where(p => p.CreditLimitId != null && ids.Contains(p.CreditLimitId.Value) && !p.IsDeleted).ToListAsync(ct);
        return PrepaymentPreference.Vigente(versiones, hasta);
    }
}
