using System.Globalization;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Periods;

/// <summary>
/// Lo que el cierre contable de un mes tiene que saber de Cartera (feature 013, T0652; contracts/contabilidad.md §12; T57, FR-146), leído
/// <b>sin tocar <c>LND_</c> y sin un puerto inverso</b>: de los recibos de los cuatro informativos en <c>ACC_LendingPostings</c> y de las
/// entregas de la bandeja.
/// <list type="number">
///   <item><b>Bloqueo</b> (<c>Accounting.Period.LendingNotClosed</c>, sin reconocimiento): hay una <c>CarteraActivada</c> con fecha ≤ fin del mes y
///         no hay un <c>CierreDeCarteraRealizado</c> del mes que siga vigente (sin un <c>CierreDeCarteraReabierto</c> que lo nombre);</item>
///   <item><b>Aviso</b> (<c>Accounting.Period.LendingPending</c>): entregas de Cartera a Contabilidad <c>Pending</c>, <c>InBatch</c> o
///         <c>Rejected</c> con fecha contable en el mes (<see cref="ListPendingIntegrationMessagesQueryHandler"/> con <c>CAR</c>);</item>
///   <item><b>Aviso</b> (<c>Accounting.Period.LendingLoadWillExpire</c>): la última <c>CargaDeCarteraAplicada</c>, sin una <c>CarteraActivada</c>
///         posterior, tiene el día siguiente a su corte dentro del mes.</item>
/// </list>
/// Sin <c>CarteraActivada</c>, Contabilidad cierra como hoy: sólo pueden saltar los avisos. (nuevo)
/// </summary>
public static class CarteraEnElCierre
{
    public sealed record CargaSinActivar(DateOnly CutoffDate, Guid LoadPublicId);

    public sealed record Evaluacion(Error? Bloqueo, PendientesDeIntegracionDto Pendientes, CargaSinActivar? CargaQueVence);

    public static async Task<Evaluacion> EvaluarAsync(IApplicationDbContext db, DateOnly inicio, DateOnly fin, CancellationToken ct)
    {
        var informativos = await (
                from p in db.LendingPostings.AsNoTracking()
                join m in db.IntegrationMessages.AsNoTracking() on p.MessagePublicId equals m.PublicId into mensajes
                from m in mensajes.DefaultIfEmpty()
                where !p.IsDeleted && (p.MessageType == CarteraActivadaV1.Type || p.MessageType == CargaDeCarteraAplicadaV1.Type
                                       || p.MessageType == CierreDeCarteraRealizadoV1.Type || p.MessageType == CierreDeCarteraReabiertoV1.Type)
                select new { p.Id, p.MessagePublicId, p.MessageType, p.OperationDate, p.SourcePublicId, p.RelatedMessagePublicId, Emitido = (long?)m.Id })
            .ToListAsync(ct);
        long Orden(long? emitido, long id) => emitido ?? id;

        // (a) El bloqueo.
        Error? bloqueo = null;
        var activaciones = informativos.Where(i => i.MessageType == CarteraActivadaV1.Type && i.OperationDate <= fin).OrderBy(i => i.OperationDate).ToList();
        if (activaciones.Count > 0)
        {
            var reabiertos = informativos.Where(i => i.MessageType == CierreDeCarteraReabiertoV1.Type).ToList();
            var cierres = informativos
                .Where(i => i.MessageType == CierreDeCarteraRealizadoV1.Type
                            && !reabiertos.Any(r => r.RelatedMessagePublicId == i.MessagePublicId
                                                    || (r.RelatedMessagePublicId is null && r.SourcePublicId == i.SourcePublicId
                                                        && Orden(r.Emitido, r.Id) > Orden(i.Emitido, i.Id))))
                .OrderByDescending(i => i.OperationDate).ThenByDescending(i => Orden(i.Emitido, i.Id))
                .ToList();
            if (!cierres.Any(c => c.OperationDate >= inicio && c.OperationDate <= fin))
            {
                var ultimo = cierres.FirstOrDefault();
                bloqueo = AccountingErrors.PeriodLendingNotClosed(inicio.ToString("yyyy-MM", CultureInfo.InvariantCulture), activaciones[0].OperationDate,
                    ultimo is null ? null : new { month = ultimo.OperationDate.ToString("yyyy-MM", CultureInfo.InvariantCulture), date = ultimo.OperationDate });
            }
        }

        // (b) Lo pendiente de Cartera en el mes, informativos incluidos.
        var pendientes = await ListPendingIntegrationMessagesQueryHandler.ContarAsync(db, ModulosDeOrigen.Cartera, inicio, fin, ct);

        // (c) La última carga aplicada, sin activación posterior, que vence dentro del mes.
        CargaSinActivar? vence = null;
        var carga = informativos.Where(i => i.MessageType == CargaDeCarteraAplicadaV1.Type).MaxBy(i => Orden(i.Emitido, i.Id));
        if (carga is not null
            && !informativos.Any(i => i.MessageType == CarteraActivadaV1.Type && Orden(i.Emitido, i.Id) > Orden(carga.Emitido, carga.Id)))
        {
            var siguiente = carga.OperationDate.AddDays(1);
            if (siguiente >= inicio && siguiente <= fin) vence = new CargaSinActivar(carga.OperationDate, carga.SourcePublicId);
        }

        return new Evaluacion(bloqueo, pendientes, vence);
    }
}
