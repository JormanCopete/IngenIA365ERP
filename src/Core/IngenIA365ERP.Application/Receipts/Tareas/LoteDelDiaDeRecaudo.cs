using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions.Tareas;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Receipts.Tareas;

/// <summary>
/// El lote del día de Recaudo (feature 015, US8, T0263; FR-184, FR-185; TR43; contabilidad.md §7): pasada la hora de corte (<c>Recaudo.HoraDeCorte</c>), junta
/// los recibos de caja que esperan en lote (<c>InBatch</c>, sin lote, con <c>BatchScopeKey = CashSession:{sesión}</c> y la clave <c>REC.RCE|CierreDeTurno||PorDia</c>)
/// en un lote <c>CashSessionClose</c> por <b>sesión cerrada</b>; el despachador los consume y Contabilidad los resume en un comprobante por día y punto
/// (<c>PostReceiptsSummaryGroupCommand</c>). Un punto con el día abierto no detiene a los demás: lo de sus sesiones abiertas entra al siguiente y se alerta
/// <c>Recaudo.DiaDelPuntoAbierto</c>. Al terminar deja la marca <see cref="MarcaDelLoteDelDia"/> que espera <c>recaudo.cuadre-diario</c>. El orden con el ciclo
/// diario de Cartera no se declara aquí: lo garantiza la restricción de configuración de la hora de corte (<c>ReglasDeParametrosDeRecaudo</c>). (nuevo)
/// </summary>
public sealed class LoteDelDiaDeRecaudo(IApplicationDbContext db, IActorActual actorActual, IAlertas alertas, IDateTimeService reloj)
{
    /// <summary>Crea los lotes de las sesiones cerradas y deja la marca del día. Devuelve cuántos lotes creó.</summary>
    public async Task<int> CorrerAsync(DateOnly dia, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var candidatas = await db.IntegrationMessageDeliveries.Include(d => d.Message)
            .Where(d => d.Destination == IntegrationDestinations.Accounting && d.Status == DeliveryStatus.InBatch && d.BatchId == null
                        && d.BatchScopeKey != null && d.BatchScopeKey.StartsWith("CashSession:"))
            .OrderBy(d => d.MessageId)
            .ToListAsync(ct);
        var deRecaudo = candidatas.Where(d => ClavesDeLote.Leer(d.ScheduleKey) is { Disparador: ClavesDeLote.CierreDeTurno } h && h.Modulo == ModulosDeOrigen.Recaudo).ToList();
        var lotes = 0;
        var abiertas = new List<Guid>();
        foreach (var porSesion in deRecaudo.GroupBy(d => d.BatchScopeKey!))
        {
            if (!Guid.TryParse(porSesion.Key["CashSession:".Length..], out var sesionPublicId)) continue;
            var sesion = await db.ReceiptsCashSessions.AsNoTracking().Where(s => s.PublicId == sesionPublicId).Select(s => new { s.Status }).FirstOrDefaultAsync(ct);
            if (sesion is null) continue;
            if (sesion.Status != CashSessionState.Closed)
            {
                abiertas.Add(sesionPublicId);
                continue;
            }
            if (await db.IntegrationBatches.AnyAsync(b => b.CashSessionPublicId == sesionPublicId && b.Trigger == BatchTrigger.CashSessionClose, ct)) continue;
            var entregas = porSesion.ToList();
            var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting, BatchTrigger.CashSessionClose, actor,
                motivo: null, reloj.UtcNow);
            lote.CashSessionPublicId = sesionPublicId;
            var granularidades = entregas.Select(e => ClavesDeLote.Leer(e.ScheduleKey)!.GranularidadDelLote).Distinct().ToList();
            lote.Granularity = granularidades.Count == 1 ? granularidades[0] : null;
            lote.CutoffMessageId = entregas[^1].MessageId;
            lote.MessageCount = entregas.Count;
            lote.DocumentCount = entregas.Select(e => e.Message!.OriginPublicId).Distinct().Count();
            lote.DateFrom = entregas.Min(e => e.Message!.OperationDate);
            lote.DateTo = entregas.Max(e => e.Message!.OperationDate);
            db.IntegrationBatches.Add(lote);
            await db.SaveChangesAsync(ct);
            LotesDeIntegracion.Asignar(lote, entregas);
            lotes++;
        }
        await MarcaDelLoteDelDia.RegistrarAsync(db, dia, ct);
        await db.SaveChangesAsync(ct);

        foreach (var sesion in abiertas.Distinct())
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoDiaDelPuntoAbierto, "Día de un punto de recaudo abierto",
                string.Create(CultureInfo.InvariantCulture,
                    $"Pasada la hora de corte del {dia:dd/MM/yyyy} una sesión de caja sigue abierta: sus recibos entran al lote del siguiente día. Ciérrela para contabilizarlos."),
                "CashSession", sesion, DedupKey: string.Create(CultureInfo.InvariantCulture, $"Recaudo.DiaDelPuntoAbierto:{sesion:N}:{dia:yyyy-MM-dd}"),
                ModuloDeOrigen: "Receipts"), ct);
        return lotes;
    }
}

/// <summary>La tarea <c>recaudo.lote-del-dia</c>: cada media hora, por cooperativa; corre una vez por día pasada la hora de corte. Sin dependencias declaradas. (nuevo)</summary>
public sealed class TareaDeLoteDelDiaDeRecaudo : ITareaProgramada
{
    public const string NombreDeLaTarea = "recaudo.lote-del-dia";

    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(30);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Intervalo;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var reloj = servicios.GetRequiredService<IDateTimeService>();
        var parametros = servicios.GetRequiredService<ParametrosDeCaja>();
        var ahora = reloj.AhoraLocal;
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var corte = await parametros.HoraAsync(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, hoy, ct);
        if (TimeOnly.FromDateTime(ahora.DateTime) < corte) return;
        await servicios.GetRequiredService<LoteDelDiaDeRecaudo>().CorrerAsync(hoy, ct);
    }
}
