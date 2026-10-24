using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Activation;

/// <summary>
/// Lo que Tesorería ofrece al cierre del mes contable (feature 015, US24, T0321; FR-167, FR-228; interfaces.md §3): el par de
/// <c>EstadoOperativoDeRecaudoParaElCierre</c> sobre <c>TES_Activations</c> y <c>TES_OpeningSquares</c>. Sólo lee. (nuevo)
/// </summary>
/// <remarks>
/// T1 (fase 35, T0422; FR-132; D29): además, las conciliaciones bancarias del mes que siguen abiertas (o desactualizadas) —<c>Accounting.Period.ReconciliationsOpen</c>—,
/// un aviso que bloquea sólo con <c>Contabilidad.CierreExigeConciliaciones</c>.
/// </remarks>
public sealed class EstadoOperativoDeTesoreriaParaElCierre(IApplicationDbContext db, ILectorDeParametros? lector = null) : IEstadoOperativoParaElCierre
{
    public const string Modulo = "TES";

    public async Task<IReadOnlyList<PendienteDeCierreDto>> PendientesAsync(int anio, int mes, CancellationToken ct)
    {
        var lista = CuadreProvisionalPendiente.PendienteDelCierre(Modulo, "Tesorería", await PendienteAsync(db, ct), anio, mes).ToList();
        var abiertas = await (from c in db.TreasuryReconciliations.AsNoTracking()
                              join a in db.OwnBankAccounts.AsNoTracking() on c.BankAccountId equals a.Id
                              where c.PeriodYear == anio && c.PeriodMonth == mes && (c.Status == ReconciliationState.Open || c.Status == ReconciliationState.Outdated)
                              select a.Code).Distinct().ToListAsync(ct);
        if (abiertas.Count > 0)
            lista.Add(new PendienteDeCierreDto(Modulo, AvisoOperativoDelCierre.CodigoConciliacionesAbiertas,
                $"Tesorería tiene {abiertas.Count} conciliación(es) bancaria(s) del mes sin cerrar ({string.Join(", ", abiertas.Order(StringComparer.Ordinal))}): ciérrelas antes de cerrar el mes.",
                Blocks: await AvisoOperativoDelCierre.BloqueaAsync(lector, anio, mes, ct)));
        return lista;
    }

    /// <summary>El cuadre provisional pendiente de la activación vigente de Tesorería, si lo hay.</summary>
    public static async Task<CuadreProvisionalPendienteDto?> PendienteAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var activacion = await db.TreasuryActivations.AsNoTracking().OrderByDescending(a => a.Id)
            .Select(a => new { a.ActivatedOn, a.OpeningSquareId }).FirstOrDefaultAsync(ct);
        return activacion is null ? null : await CuadreProvisionalPendiente.PendienteAsync(db.TreasuryOpeningSquares, activacion.ActivatedOn, activacion.OpeningSquareId, ct);
    }
}
