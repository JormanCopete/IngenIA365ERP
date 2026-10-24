using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Activation;

/// <summary>
/// Lo que Recaudo ofrece al cierre del mes contable (feature 015, US24, T0321; FR-167, FR-228; interfaces.md §3): si la activación vigente de Recaudo tomó un cuadre de
/// apertura provisional y todavía no tiene su definitivo, el cierre del mes de la activación se niega (<c>Accounting.Period.OpeningSquarePending</c>). La tarea pedía
/// una sola clase para Recaudo y Tesorería en este archivo; son dos (<see cref="EstadoOperativoDeRecaudoParaElCierre"/> y su par en <c>Treasury/Activation</c>) para que
/// ningún módulo lea las tablas del otro. Sólo lee. (nuevo)
/// </summary>
/// <remarks>
/// T1 (fase 35, T0422; FR-132; D29): además, las sesiones de caja de días del mes que no se han cerrado —<c>Accounting.Period.CashSessionsOpen</c>—, un aviso que bloquea
/// sólo con <c>Contabilidad.CierreExigeConciliaciones</c>.
/// </remarks>
public sealed class EstadoOperativoDeRecaudoParaElCierre(IApplicationDbContext db, ILectorDeParametros? lector = null) : IEstadoOperativoParaElCierre
{
    public async Task<IReadOnlyList<PendienteDeCierreDto>> PendientesAsync(int anio, int mes, CancellationToken ct)
    {
        var lista = CuadreProvisionalPendiente.PendienteDelCierre(ClavesDeRecaudo.Codigo, "Recaudo", await PendienteAsync(db, ct), anio, mes).ToList();
        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var abiertas = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                              join d in db.CollectionPointDays.AsNoTracking() on s.CollectionPointDayId equals d.Id
                              where d.BusinessDate >= desde && d.BusinessDate <= hasta && s.Status != CashSessionState.Closed
                              select s.Id).CountAsync(ct);
        if (abiertas > 0)
            lista.Add(new PendienteDeCierreDto(ClavesDeRecaudo.Codigo, AvisoOperativoDelCierre.CodigoSesionesSinCerrar,
                $"Recaudo tiene {abiertas} sesión(es) de caja de días del mes sin cerrar: ciérrelas (o el supervisor las olvidadas) antes de cerrar el mes.",
                Blocks: await AvisoOperativoDelCierre.BloqueaAsync(lector, anio, mes, ct)));
        return lista;
    }

    /// <summary>El cuadre provisional pendiente de la activación vigente de Recaudo, si lo hay.</summary>
    public static async Task<CuadreProvisionalPendienteDto?> PendienteAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var activacion = await db.ReceiptsActivations.AsNoTracking().OrderByDescending(a => a.Id)
            .Select(a => new { a.ActivatedOn, a.OpeningSquareId }).FirstOrDefaultAsync(ct);
        return activacion is null ? null : await CuadreProvisionalPendiente.PendienteAsync(db.ReceiptsOpeningSquares, activacion.ActivatedOn, activacion.OpeningSquareId, ct);
    }
}
