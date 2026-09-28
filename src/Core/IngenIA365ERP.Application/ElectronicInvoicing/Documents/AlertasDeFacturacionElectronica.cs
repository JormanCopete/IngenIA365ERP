using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.RaiseAlert;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>Lo que dejó una pasada de las alertas DIAN: cuántas pidió de cada tipo (nuevas o repetidas). (nuevo)</summary>
public sealed record ResultadoDeAlertasDian(int SinValidar, int Plazos, int PorAgotar, int PorVencer);

/// <summary>
/// Las alertas DIAN que dependen del paso del tiempo (feature 012, I4, T721; contracts/dian.md §6.3, §7.3, §9; SC-015, SC-022):
/// <list type="bullet">
/// <item><c>Dian.DocumentoSinValidar</c>: un <c>Pending</c> o <c>Sent</c> expedido hace más de <c>Dian.MinutosAlertaSinValidar</c>; una por documento
/// (<c>DedupKey</c>), que la emisión da por atendida al validarse;</item>
/// <item><c>Dian.PlazoDeContingencia</c>: por evento cerrado con documentos sin transmitir, <c>Dian.AlertaHorasAntesDelPlazo</c> horas antes del
/// plazo más próximo y otra vez al vencer;</item>
/// <item><c>Dian.ResolucionPorAgotar</c>: una resolución vigente con la fracción consumida en o sobre <c>Dian.AvisoResolucionPorcentaje</c>;</item>
/// <item><c>Dian.ResolucionPorVencer</c>: una resolución vigente a <c>Dian.AvisoResolucionDias</c> días o menos de vencer.</item>
/// </list>
/// <c>Dian.DocumentoRechazado</c> y <c>Dian.ContingenciaAbierta</c> las levanta la emisión en el acto. Todo por <see cref="RaiseAlertCommand"/>, con
/// los destinatarios por permiso del tipo; repetir la pasada sólo suma la ocurrencia de las pendientes. La corre la tarea programada
/// <c>einvoicing.alerts</c> (<see cref="TareaDeAlertasDeFacturacionElectronica"/>). (nuevo)
/// </summary>
public sealed class AlertasDeFacturacionElectronica(IApplicationDbContext db, ILectorDeParametros parametros, IDateTimeService reloj, ISender sender)
{
    public async Task<ResultadoDeAlertasDian> RevisarAsync(CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var hoy = reloj.HoyLocal;
        return new ResultadoDeAlertasDian(
            await SinValidarAsync(ahora, hoy, ct),
            await PlazosAsync(ahora, hoy, ct),
            await ResolucionesPorAgotarAsync(hoy, ct),
            await ResolucionesPorVencerAsync(hoy, ct));
    }

    private async Task<int> SinValidarAsync(DateTime ahora, DateOnly hoy, CancellationToken ct)
    {
        var minutos = await EnteroAsync(ParametrosDeFacturacionElectronica.MinutosAlertaSinValidar, hoy, ct);
        var limite = ahora.AddMinutes(-minutos);
        var documentos = await db.ElectronicDocuments.AsNoTracking()
            .Where(d => (d.Status == ElectronicDocumentStatus.Pending || d.Status == ElectronicDocumentStatus.Sent) && d.IssuedAt < limite)
            .OrderBy(d => d.IssuedAt)
            .Select(d => new { d.PublicId, d.Number, d.Status, d.IssuedAt })
            .ToListAsync(ct);
        var n = 0;
        foreach (var d in documentos)
            if (await LevantarAsync(new AlertaALevantar(
                    TiposDeAlerta.DocumentoSinValidar,
                    $"El documento electrónico {d.Number} sigue sin validación de la DIAN",
                    $"Se expidió hace más de {minutos} minutos y sigue {(d.Status == ElectronicDocumentStatus.Sent ? "enviado sin respuesta" : "pendiente de envío")}. " +
                    "Revíselo en Documentos electrónicos: consulte a la DIAN o reintente.",
                    EntityType: GuardadoDeArtefactos.EntidadDelDocumento,
                    EntityPublicId: d.PublicId,
                    DedupKey: GuardadoDeArtefactos.ClaveSinValidar(d.PublicId)), ct))
                n++;
        return n;
    }

    private async Task<int> PlazosAsync(DateTime ahora, DateOnly hoy, CancellationToken ct)
    {
        var horasAntes = await EnteroAsync(ParametrosDeFacturacionElectronica.AlertaHorasAntesDelPlazo, hoy, ct);
        var pendientes = await db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.ContingencyEventId != null && d.TransmissionDeadline != null
                        && (d.Status == ElectronicDocumentStatus.IssuerContingency || d.Status == ElectronicDocumentStatus.DianContingency))
            .Select(d => new { EventoId = d.ContingencyEventId!.Value, Plazo = d.TransmissionDeadline!.Value })
            .ToListAsync(ct);
        var n = 0;
        foreach (var grupo in pendientes.GroupBy(p => p.EventoId))
        {
            var plazo = grupo.Min(p => p.Plazo);
            var aviso = PlazoDeContingencia.InstanteDeLaAlerta(new DateTimeOffset(DateTime.SpecifyKind(plazo, DateTimeKind.Utc)), horasAntes).UtcDateTime;
            if (ahora < aviso) continue;
            var evento = await db.DianContingencyEvents.AsNoTracking().FirstAsync(e => e.Id == grupo.Key, ct);
            var vencido = ahora >= plazo;
            if (await LevantarAsync(new AlertaALevantar(
                    TiposDeAlerta.PlazoDeContingencia,
                    vencido
                        ? $"Venció el plazo para transmitir lo expedido en contingencia ({evento.ChannelCode})"
                        : $"Se acerca el plazo para transmitir lo expedido en contingencia ({evento.ChannelCode})",
                    $"{grupo.Count()} documento(s) del evento de contingencia siguen sin transmitir; el plazo más próximo es {plazo:yyyy-MM-dd HH:mm} UTC " +
                    $"({evento.DeadlineHoursApplied} horas, {evento.LegalSource}). Revíselos en Contingencias DIAN.",
                    EntityType: "DianContingencyEvent",
                    EntityPublicId: evento.PublicId,
                    DedupKey: $"{TiposDeAlerta.PlazoDeContingencia}:{evento.PublicId:N}:{(vencido ? "vencido" : "antes")}"), ct))
                n++;
        }
        return n;
    }

    private async Task<int> ResolucionesPorAgotarAsync(DateOnly hoy, CancellationToken ct)
    {
        var umbral = await parametros.LeerComoAsync<decimal>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.AvisoResolucionPorcentaje,
            hoy, ct: ct);
        if (umbral.IsFailure || umbral.Value <= 0m) return 0;
        var n = 0;
        foreach (var r in await VigentesAsync(hoy, ct))
        {
            var fraccion = ReglasDeResolucion.FraccionConsumida(r);
            if (fraccion < umbral.Value) continue;
            if (await LevantarAsync(new AlertaALevantar(
                    TiposDeAlerta.ResolucionPorAgotar,
                    $"La resolución {r.ResolutionNumber} (prefijo {r.Prefix}) está por agotarse",
                    $"Lleva consumido el {fraccion:P0} del rango {r.RangeFrom}–{r.RangeTo} (último número {r.LastIssuedNumber}). " +
                    "Solicite una nueva numeración a la DIAN y regístrela a tiempo.",
                    EntityType: "DianNumberingResolution",
                    EntityPublicId: r.PublicId,
                    DedupKey: $"{TiposDeAlerta.ResolucionPorAgotar}:{r.PublicId:N}"), ct))
                n++;
        }
        return n;
    }

    private async Task<int> ResolucionesPorVencerAsync(DateOnly hoy, CancellationToken ct)
    {
        var dias = await EnteroAsync(ParametrosDeFacturacionElectronica.AvisoResolucionDias, hoy, ct);
        var n = 0;
        foreach (var r in await VigentesAsync(hoy, ct))
        {
            var faltan = ReglasDeResolucion.DiasParaVencer(r, hoy);
            if (faltan > dias) continue;
            if (await LevantarAsync(new AlertaALevantar(
                    TiposDeAlerta.ResolucionPorVencer,
                    $"La resolución {r.ResolutionNumber} (prefijo {r.Prefix}) vence en {faltan} día(s)",
                    $"Vence el {r.ValidTo:yyyy-MM-dd}. Solicite una nueva numeración a la DIAN y regístrela antes de esa fecha.",
                    EntityType: "DianNumberingResolution",
                    EntityPublicId: r.PublicId,
                    DedupKey: $"{TiposDeAlerta.ResolucionPorVencer}:{r.PublicId:N}"), ct))
                n++;
        }
        return n;
    }

    private Task<List<Domain.Entities.ElectronicInvoicing.DianNumberingResolution>> VigentesAsync(DateOnly hoy, CancellationToken ct) =>
        db.DianNumberingResolutions.AsNoTracking()
            .Where(r => r.IsActive && r.ValidFrom <= hoy && r.ValidTo >= hoy)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

    private async Task<int> EnteroAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var r = await parametros.LeerComoAsync<int>(ParametrosDeFacturacionElectronica.Modulo, clave, fecha, ct: ct);
        return r.IsSuccess ? r.Value : 0;
    }

    private async Task<bool> LevantarAsync(AlertaALevantar alerta, CancellationToken ct) =>
        (await sender.Send(new RaiseAlertCommand(alerta), ct)).IsSuccess;
}

/// <summary>
/// La tarea programada <c>einvoicing.alerts</c> (feature 012, I4, T721): corre <see cref="AlertasDeFacturacionElectronica"/> en cada cooperativa
/// cada <see cref="Intervalo"/> (por defecto cinco minutos: el plazo de contingencia y los minutos sin validar se miden en horas y minutos). La
/// corre <c>ProgramadorDeTareas</c> por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración»; idempotente. Se registra como
/// singleton en <c>Program.cs</c>. (nuevo)
/// </summary>
public sealed class TareaDeAlertasDeFacturacionElectronica(TimeSpan intervalo) : ITareaProgramada
{
    public const string NombreDeLaTarea = "einvoicing.alerts";

    public static readonly TimeSpan IntervaloPorDefecto = TimeSpan.FromMinutes(5);

    public TareaDeAlertasDeFacturacionElectronica() : this(IntervaloPorDefecto) { }

    public TimeSpan Intervalo { get; } = intervalo;

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Intervalo;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<AlertasDeFacturacionElectronica>().RevisarAsync(ct);
}
