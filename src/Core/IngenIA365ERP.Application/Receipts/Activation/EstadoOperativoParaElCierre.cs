using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Common;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Activation;

/// <summary>
/// Lo que Recaudo ofrece al cierre del mes contable (feature 015, US24, T0321; FR-167, FR-228; interfaces.md §3): si la activación vigente de Recaudo tomó un cuadre de
/// apertura provisional y todavía no tiene su definitivo, el cierre del mes de la activación se niega (<c>Accounting.Period.OpeningSquarePending</c>). La tarea pedía
/// una sola clase para Recaudo y Tesorería en este archivo; son dos (<see cref="EstadoOperativoDeRecaudoParaElCierre"/> y su par en <c>Treasury/Activation</c>) para que
/// ningún módulo lea las tablas del otro. Sólo lee. (nuevo)
/// </summary>
public sealed class EstadoOperativoDeRecaudoParaElCierre(IApplicationDbContext db) : IEstadoOperativoParaElCierre
{
    public async Task<IReadOnlyList<PendienteDeCierreDto>> PendientesAsync(int anio, int mes, CancellationToken ct) =>
        CuadreProvisionalPendiente.PendienteDelCierre(ClavesDeRecaudo.Codigo, "Recaudo", await PendienteAsync(db, ct), anio, mes);

    /// <summary>El cuadre provisional pendiente de la activación vigente de Recaudo, si lo hay.</summary>
    public static async Task<CuadreProvisionalPendienteDto?> PendienteAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var activacion = await db.ReceiptsActivations.AsNoTracking().OrderByDescending(a => a.Id)
            .Select(a => new { a.ActivatedOn, a.OpeningSquareId }).FirstOrDefaultAsync(ct);
        return activacion is null ? null : await CuadreProvisionalPendiente.PendienteAsync(db.ReceiptsOpeningSquares, activacion.ActivatedOn, activacion.OpeningSquareId, ct);
    }
}
