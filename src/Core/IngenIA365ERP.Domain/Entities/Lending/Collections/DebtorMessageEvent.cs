using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_DebtorMessageEvents</c> (feature 013, C.3; data-model §26.2; T74, T75): cada paso de un <see cref="DebtorMessage"/>
/// (encolado, diferido, excluido, enviado, entregado, fallido, simulado, cancelado). Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>) de alto volumen (<c>bigint</c>). La dirección usada vive aquí y no en el mensaje (C57).
///
/// <para>
/// T0305: nace sólo por <see cref="Registrar"/>, que aplica las transiciones de adaptadores-y-archivos.md §4.2 sobre el evento anterior
/// (<see cref="PuedeSeguir"/>: <c>Excluded</c>, <c>Failed</c>, <c>Cancelled</c> y <c>Delivered</c> son finales; <c>Simulated</c> lo es
/// en producción) y la forma de cada clase. Los nombres del contrato caen en las columnas que ya existen, sin regenerar
/// <c>CarteraNucleo</c>: <c>Reasons</c> → <see cref="ReasonCode"/> (los <see cref="ContactWindowReason"/> por nombre separados por coma;
/// <see cref="Motivos"/> los lee), <c>NextAttemptAt</c> → <see cref="DueAt"/> y <c>Attempt</c> → <see cref="AttemptNumber"/>.
/// <see cref="DestinationAddress"/> va obligatoria en <c>Sent</c> y <c>Simulated</c>, admitida en <c>Failed</c> y prohibida en las demás.
/// </para>
/// </summary>
public class DebtorMessageEvent : AuditableEntityLong, IHechoInmutable
{
    /// <summary>El largo de <see cref="ReasonCode"/> (nvarchar(80)).</summary>
    public const int LargoDelMotivo = 80;

    public int DebtorMessageId { get; init; }

    /// <summary>
    /// Navegación para que la cola enlace en el mismo guardado el primer evento con su mensaje todavía sin Id (T0306: la cola nunca
    /// guarda por su cuenta). Misma FK; no agrega columnas.
    /// </summary>
    public DebtorMessage? DebtorMessage { get; init; }

    public int Sequence { get; init; }

    public DebtorMessageEventKind Kind { get; init; }

    public DateTime OccurredAt { get; init; }

    /// <summary><c>Queued</c> y <c>Deferred</c>: instante desde el que puede salir.</summary>
    public DateTime? DueAt { get; init; }

    /// <summary><c>OutsideHours</c>, <c>Holiday</c>, <c>DailyCap</c>, <c>WeeklyChannelUsed</c>, <c>ChannelNotAuthorized</c>…</summary>
    public string? ReasonCode { get; init; }

    public string? ReasonText { get; init; }

    /// <summary>Copia del correo o número usado al enviar (I-1); en <c>Sent</c>, <c>Simulated</c> y <c>Failed</c>.</summary>
    public string? DestinationAddress { get; init; }

    public int? AttemptNumber { get; init; }

    public string? ProviderReference { get; init; }

    public ExternalOutcome? ExternalOutcome { get; init; }

    /// <summary>Constancia del proveedor o guía de una carta (<c>LendingPriorNoticeEvidence</c>).</summary>
    public Guid? ProviderArtifactAttachmentPublicId { get; init; }

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    // ------------------------------------------------------------------------------------------------- comportamiento --

    /// <summary>Las clases con las que nace un mensaje (según el motor al programar).</summary>
    public static IReadOnlySet<DebtorMessageEventKind> Iniciales { get; } =
        new HashSet<DebtorMessageEventKind> { DebtorMessageEventKind.Queued, DebtorMessageEventKind.Deferred, DebtorMessageEventKind.Excluded };

    /// <summary>
    /// Si <paramref name="siguiente"/> puede seguir a <paramref name="anterior"/> (nulo = el mensaje está naciendo). Desde
    /// <c>Queued</c>/<c>Deferred</c>: cualquiera salvo <c>Queued</c> y <c>Delivered</c>; desde <c>Sent</c>, y desde <c>Simulated</c>
    /// fuera de producción: <c>Delivered</c> o <c>Failed</c>; el resto es final.
    /// </summary>
    public static bool PuedeSeguir(DebtorMessageEventKind? anterior, DebtorMessageEventKind siguiente, bool enProduccion) => anterior switch
    {
        null => Iniciales.Contains(siguiente),
        DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred =>
            siguiente is not (DebtorMessageEventKind.Queued or DebtorMessageEventKind.Delivered),
        DebtorMessageEventKind.Sent => siguiente is DebtorMessageEventKind.Delivered or DebtorMessageEventKind.Failed,
        DebtorMessageEventKind.Simulated => !enProduccion && siguiente is DebtorMessageEventKind.Delivered or DebtorMessageEventKind.Failed,
        _ => false,
    };

    /// <summary>
    /// El evento número <c>anterior.Sequence + 1</c> (o 1 si nace) del mensaje <paramref name="debtorMessageId"/>. Lanza si la
    /// transición no existe o el evento no tiene la forma de su clase: los llama la cola, que ya decidió con el motor.
    /// </summary>
    public static DebtorMessageEvent Registrar(int debtorMessageId, DebtorMessageEvent? anterior, EventoDeMensaje e, bool enProduccion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(debtorMessageId, 1);
        if (anterior is not null && anterior.DebtorMessageId != debtorMessageId)
            throw new ArgumentException("El evento anterior es de otro mensaje.", nameof(anterior));
        return Construir(debtorMessageId, null, anterior, e, enProduccion);
    }

    /// <summary>
    /// Lo mismo sobre el mensaje (T0306): si todavía no tiene Id —nació en esta unidad de trabajo—, el evento lo enlaza por la navegación y
    /// el guardado de quien lo motivó pone la FK; si ya lo tiene, por su Id.
    /// </summary>
    public static DebtorMessageEvent Registrar(DebtorMessage mensaje, DebtorMessageEvent? anterior, EventoDeMensaje e, bool enProduccion)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (mensaje.Id > 0) return Registrar(mensaje.Id, anterior, e, enProduccion);
        if (anterior is not null && !ReferenceEquals(anterior.DebtorMessage, mensaje))
            throw new ArgumentException("El evento anterior es de otro mensaje.", nameof(anterior));
        return Construir(0, mensaje, anterior, e, enProduccion);
    }

    private static DebtorMessageEvent Construir(int debtorMessageId, DebtorMessage? mensaje, DebtorMessageEvent? anterior, EventoDeMensaje e,
        bool enProduccion)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!PuedeSeguir(anterior?.Kind, e.Kind, enProduccion))
            throw new InvalidOperationException($"Un mensaje al deudor no pasa de {anterior?.Kind.ToString() ?? "(nuevo)"} a {e.Kind}.");
        ValidarForma(e);

        return new DebtorMessageEvent
        {
            DebtorMessageId = debtorMessageId,
            DebtorMessage = mensaje,
            Sequence = (anterior?.Sequence ?? 0) + 1,
            Kind = e.Kind,
            OccurredAt = e.OccurredAt,
            DueAt = e.DueAt,
            ReasonCode = CodigoDeMotivos(e.Motivos) ?? (string.IsNullOrWhiteSpace(e.CodigoDeMotivo) ? null : e.CodigoDeMotivo.Trim()),
            ReasonText = string.IsNullOrWhiteSpace(e.TextoDelMotivo) ? null : e.TextoDelMotivo.Trim(),
            DestinationAddress = string.IsNullOrWhiteSpace(e.DestinationAddress) ? null : e.DestinationAddress.Trim(),
            AttemptNumber = e.AttemptNumber,
            ProviderReference = e.ProviderReference,
            ExternalOutcome = e.ExternalOutcome,
            ProviderArtifactAttachmentPublicId = e.ProviderArtifactAttachmentPublicId,
            ActorKind = e.ActorKind,
            ActorUserId = e.ActorUserId,
        };
    }

    /// <summary>Los motivos de la ventana legal guardados en <see cref="ReasonCode"/> (vacío si el código no es una lista de motivos).</summary>
    public IReadOnlyList<ContactWindowReason> Motivos()
    {
        if (string.IsNullOrWhiteSpace(ReasonCode)) return [];
        var motivos = new List<ContactWindowReason>();
        foreach (var parte in ReasonCode.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<ContactWindowReason>(parte, ignoreCase: false, out var motivo) || !Enum.IsDefined(motivo)) return [];
            motivos.Add(motivo);
        }
        return motivos;
    }

    /// <summary>
    /// Los motivos por nombre separados por coma, en orden y sin repetir; si no caben en <see cref="LargoDelMotivo"/>, por número (los
    /// números de Cartera no se renumeran, C65). Nulo sin motivos.
    /// </summary>
    public static string? CodigoDeMotivos(IReadOnlyCollection<ContactWindowReason>? motivos)
    {
        if (motivos is null || motivos.Count == 0) return null;
        var unicos = motivos.Distinct().ToList();
        var porNombre = string.Join(",", unicos.Select(m => m.ToString()));
        return porNombre.Length <= LargoDelMotivo ? porNombre : string.Join(",", unicos.Select(m => ((int)m).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static void ValidarForma(EventoDeMensaje e)
    {
        if (!Enum.IsDefined(e.Kind)) throw new ArgumentException("Clase de evento desconocida.", nameof(e));
        if ((e.ActorKind == ActorKind.Person) != e.ActorUserId.HasValue)
            throw new ArgumentException("Actúa una persona con su usuario, o el proceso sin usuario.", nameof(e));
        if (e.AttemptNumber is < 1) throw new ArgumentException("El número de intento empieza en 1.", nameof(e));

        var conMotivos = e.Kind is DebtorMessageEventKind.Deferred or DebtorMessageEventKind.Excluded;
        if (conMotivos && (e.Motivos is null || e.Motivos.Count == 0))
            throw new ArgumentException($"Un evento {e.Kind} lleva los motivos de la ventana legal.", nameof(e));
        if (!conMotivos && e.Motivos is { Count: > 0 })
            throw new ArgumentException($"Sólo los eventos diferido y excluido llevan motivos de la ventana legal; {e.Kind} no.", nameof(e));

        if (e.Kind is DebtorMessageEventKind.Queued or DebtorMessageEventKind.Deferred)
        {
            if (e.DueAt is null) throw new ArgumentException($"Un evento {e.Kind} lleva el instante desde el que puede salir.", nameof(e));
        }
        else if (e.DueAt is not null)
        {
            throw new ArgumentException($"Sólo los eventos encolado y diferido llevan instante de salida; {e.Kind} no.", nameof(e));
        }

        var conDestino = e.Kind is DebtorMessageEventKind.Sent or DebtorMessageEventKind.Simulated;
        if (conDestino && string.IsNullOrWhiteSpace(e.DestinationAddress))
            throw new ArgumentException($"Un evento {e.Kind} copia la dirección a la que salió (C57).", nameof(e));
        if (!conDestino && e.Kind != DebtorMessageEventKind.Failed && !string.IsNullOrWhiteSpace(e.DestinationAddress))
            throw new ArgumentException($"Un evento {e.Kind} no lleva dirección de destino.", nameof(e));
    }
}
