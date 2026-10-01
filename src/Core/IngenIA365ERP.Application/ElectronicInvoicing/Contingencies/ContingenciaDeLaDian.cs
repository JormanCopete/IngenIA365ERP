using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;

/// <summary>Quién abre o cierra un evento de contingencia: el proceso o una persona. (nuevo)</summary>
public sealed record QuienDeclara(ActorKind Kind, int? UserId, string Name);

/// <summary>
/// El evento de contingencia 04 (falla la DIAN) de un canal (feature 012, I4; contracts/dian.md §7.1 y §7.3): <b>sólo lo declara el canal</b>,
/// devolviendo <c>DianUnavailable</c> con el documento firmado y su código único. <see cref="UnirOAbrirAsync"/> une el documento al evento 04
/// abierto de su canal o lo abre (y quien lo llama levanta <c>Dian.ContingenciaAbierta</c> después de guardar, con
/// <see cref="AvisarAperturaAsync"/>); <see cref="CerrarPorRespuestaAsync"/> cierra los eventos 04 del canal cuando un documento recibe una
/// respuesta definitiva de la DIAN posterior a su inicio; <see cref="CerrarAsync"/> fija el fin, copia las horas de
/// <c>Dian.PlazoContingenciaHoras</c> vigentes con su norma y el plazo de cada documento del evento con <see cref="PlazoDeContingencia"/>, y los
/// deja listos para el procesador. Nada de esto guarda: lo guarda la transacción de quien llama. Lo usa la emisión (T712); el cierre manual
/// y la contingencia 03 (<c>OpenContingencyCommand</c>, <c>CloseContingencyCommand</c>) son de T726, que reusa <see cref="CerrarAsync"/>.
/// (nuevo)
/// </summary>
public sealed class ContingenciaDeLaDian(IApplicationDbContext db, ILectorDeParametros parametros, IAlertas alertas)
{
    /// <summary>La norma que se anota si <c>Dian.PlazoContingenciaHoras</c> no tiene vigencia guardada (se usa su defecto seguro).</summary>
    public const string FuenteDelDefecto = "Defecto seguro del parámetro Dian.PlazoContingenciaHoras (sin vigencia registrada)";

    /// <summary>Une <paramref name="documento"/> al evento 04 abierto de su canal, o abre uno. Devuelve el evento y si lo abrió.</summary>
    public async Task<(DianContingencyEvent Evento, bool Abierto)> UnirOAbrirAsync(ElectronicDocument documento, DateTime ahora, QuienDeclara quien,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var abierto = await db.DianContingencyEvents
            .Where(e => e.Type == ContingencyType.Dian04 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == documento.ChannelCode)
            .OrderBy(e => e.StartedAt)
            .FirstOrDefaultAsync(ct);
        var nuevo = abierto is null;
        var evento = abierto ?? new DianContingencyEvent
        {
            Type = ContingencyType.Dian04,
            ChannelCode = documento.ChannelCode,
            StartedAt = ahora,
            DetectedByKind = quien.Kind,
            DetectedByUserId = quien.UserId,
            DetectedByName = Recortar(quien.Name, 150),
            Reason = "El canal informó que la DIAN no está disponible y entregó el documento firmado con su código único.",
            Status = ContingencyEventStatus.Open,
        };
        if (nuevo) db.DianContingencyEvents.Add(evento);
        documento.ContingencyEvent = evento;
        return (evento, nuevo);
    }

    /// <summary>
    /// Abre una contingencia 03 (falla el facturador, su conexión o su proveedor; contracts/dian.md §7.2) en <paramref name="canal"/>: la
    /// declara una persona (<c>OpenContingencyCommand</c>) o el circuito del canal (<c>CircuitoDeCanal</c>, T730, con el proceso). Un solo
    /// evento 03 abierto por canal (<c>ElectronicInvoicing.Contingency.AlreadyOpen</c>). No guarda; quien llama levanta la alerta con
    /// <see cref="AvisarAperturaAsync"/> después de guardar. (nuevo, T726)
    /// </summary>
    public async Task<Common.Models.Result<DianContingencyEvent>> AbrirDeFacturacionAsync(string canal, DateTime inicio, QuienDeclara quien, string motivo,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canal);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        var abierta = await db.DianContingencyEvents
            .Where(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == canal)
            .Select(e => (Guid?)e.PublicId)
            .FirstOrDefaultAsync(ct);
        if (abierta is { } ya) return Common.Models.Result.Failure<DianContingencyEvent>(ErroresDeContingencias.AlreadyOpen(ya));

        var evento = new DianContingencyEvent
        {
            Type = ContingencyType.Issuer03,
            ChannelCode = canal,
            StartedAt = inicio,
            DetectedByKind = quien.Kind,
            DetectedByUserId = quien.UserId,
            DetectedByName = Recortar(quien.Name, 150),
            Reason = Recortar(motivo.Trim(), 500),
            Status = ContingencyEventStatus.Open,
        };
        db.DianContingencyEvents.Add(evento);
        return Common.Models.Result.Success(evento);
    }

    /// <summary>Levanta <c>Dian.ContingenciaAbierta</c> por el evento (una por evento). Después de guardar.</summary>
    public Task AvisarAperturaAsync(DianContingencyEvent evento, CancellationToken ct) =>
        alertas.LevantarAsync(new AlertaALevantar(
            TiposDeAlerta.ContingenciaAbierta,
            evento.Type == ContingencyType.Dian04 ? $"Contingencia de la DIAN en el canal {evento.ChannelCode}" : $"Contingencia de facturación en el canal {evento.ChannelCode}",
            evento.Type == ContingencyType.Dian04
                ? "El canal informó que la DIAN no está disponible. Los documentos se entregan con la leyenda de contingencia y se transmiten al cerrarse el evento."
                : "Se abrió una contingencia de facturación: las ventas nuevas se numeran con la resolución de contingencia y se transmiten al cerrarse.",
            EntityType: "DianContingencyEvent",
            EntityPublicId: evento.PublicId,
            DedupKey: $"{TiposDeAlerta.ContingenciaAbierta}:{evento.PublicId:N}"), ct);

    /// <summary>
    /// Cierra los eventos 04 abiertos de <paramref name="canal"/> que empezaron antes de <paramref name="ahora"/>: la DIAN volvió a dar una
    /// respuesta definitiva. Devuelve los eventos cerrados.
    /// </summary>
    public async Task<IReadOnlyList<DianContingencyEvent>> CerrarPorRespuestaAsync(string canal, DateTime ahora, CancellationToken ct)
    {
        var abiertos = await db.DianContingencyEvents
            .Where(e => e.Type == ContingencyType.Dian04 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == canal && e.StartedAt < ahora)
            .ToListAsync(ct);
        foreach (var evento in abiertos)
            await CerrarAsync(evento, ahora, new QuienDeclara(ActorKind.Process, null, Common.Execution.Actor.NombreDelProceso),
                "La DIAN volvió a responder: un documento del canal recibió una respuesta definitiva.", ct);
        return abiertos;
    }

    /// <summary>
    /// Cierra <paramref name="evento"/> en <paramref name="fin"/>: copia las horas y la norma de <c>Dian.PlazoContingenciaHoras</c> vigentes a
    /// la fecha local del fin, fija el plazo del evento y el <c>TransmissionDeadline</c> de cada documento pendiente del evento, y los deja
    /// para el procesador (<c>NextAttemptAt = fin</c>, en orden de consecutivo). (nuevo)
    /// </summary>
    public async Task CerrarAsync(DianContingencyEvent evento, DateTime fin, QuienDeclara quien, string motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var local = new DateTimeOffset(DateTime.SpecifyKind(fin, DateTimeKind.Utc)).ToOffset(IDateTimeService.DesfaseColombia);
        var leido = await parametros.LeerAsync(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.PlazoContingenciaHoras,
            DateOnly.FromDateTime(local.DateTime), ct: ct);
        var horas = leido.IsSuccess ? leido.Value.Como<int>() : 0;
        if (horas <= 0)
            horas = int.Parse(ParametrosDeFacturacionElectronica.Definiciones.First(d => d.Clave == ParametrosDeFacturacionElectronica.PlazoContingenciaHoras)
                .DefectoSeguro, System.Globalization.CultureInfo.InvariantCulture);
        var fuente = leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Vigencia?.LegalSource) ? leido.Value.Vigencia!.LegalSource! : FuenteDelDefecto;

        evento.EndedAt = fin;
        evento.Status = ContingencyEventStatus.Closed;
        evento.ClosedByKind = quien.Kind;
        evento.ClosedByUserId = quien.UserId;
        evento.ClosedByName = Recortar(quien.Name, 150);
        evento.CloseReason = Recortar(motivo, 300);
        evento.DeadlineHoursApplied = (short)horas;
        evento.LegalSource = Recortar(fuente, 200);
        evento.DeadlineAt = PlazoDeContingencia.Calcular(ElectronicDocumentKind.Invoice, local, horas, fuente).Plazo.UtcDateTime;

        var eventoId = evento.Id;
        var documentos = eventoId == 0
            ? db.ElectronicDocuments.Local.Where(d => d.ContingencyEvent == evento).ToList()
            : await db.ElectronicDocuments.Where(d => d.ContingencyEventId == eventoId).ToListAsync(ct);
        foreach (var d in documentos.Where(d => d.Status is ElectronicDocumentStatus.IssuerContingency or ElectronicDocumentStatus.DianContingency))
        {
            d.TransmissionDeadline = PlazoDeContingencia.Calcular(d.Kind, local, horas, fuente, IDateTimeService.DesfaseColombia).Plazo.UtcDateTime;
            d.NextAttemptAt = fin;
        }
    }

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
