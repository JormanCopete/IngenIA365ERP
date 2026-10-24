using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Activation;

/// <summary>
/// Lo que Tesorería ofrece al cierre del mes contable (feature 015, US24, T0321; FR-167, FR-228; interfaces.md §3): el par de
/// <c>EstadoOperativoDeRecaudoParaElCierre</c> sobre <c>TES_Activations</c> y <c>TES_OpeningSquares</c>. Sólo lee. (nuevo)
/// </summary>
public sealed class EstadoOperativoDeTesoreriaParaElCierre(IApplicationDbContext db) : IEstadoOperativoParaElCierre
{
    public const string Modulo = "TES";

    public async Task<IReadOnlyList<PendienteDeCierreDto>> PendientesAsync(int anio, int mes, CancellationToken ct) =>
        CuadreProvisionalPendiente.PendienteDelCierre(Modulo, "Tesorería", await PendienteAsync(db, ct), anio, mes);

    /// <summary>El cuadre provisional pendiente de la activación vigente de Tesorería, si lo hay.</summary>
    public static async Task<CuadreProvisionalPendienteDto?> PendienteAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var activacion = await db.TreasuryActivations.AsNoTracking().OrderByDescending(a => a.Id)
            .Select(a => new { a.ActivatedOn, a.OpeningSquareId }).FirstOrDefaultAsync(ct);
        return activacion is null ? null : await CuadreProvisionalPendiente.PendienteAsync(db.TreasuryOpeningSquares, activacion.ActivatedOn, activacion.OpeningSquareId, ct);
    }
}
