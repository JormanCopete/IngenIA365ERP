using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Audit;

/// <summary>
/// Un evento de auditoría de un <b>módulo encadenado</b> escrito en la misma transacción que el cambio
/// (<c>COR_AuditOutbox</c>; feature 012, T37, T38; data-model §23). Lo agregan
/// <c>AuditableEntityInterceptor</c> (las diferencias de entidad), <c>AuditBehavior</c> (el evento del
/// comando), un contexto aparte para los rechazos, y quien registra ingresar, exportar o imprimir.
///
/// <para>
/// <b>Nunca DELETE</b> (Principio VII). Lo único que cambia, y sólo lo cambia <c>AuditOutboxForwarder</c>,
/// es el reenvío (<see cref="Forwarded"/>, <see cref="ForwardedAt"/>, intentos), el sello
/// (<see cref="Seq"/>, <see cref="PrevHash"/>, <see cref="Hash"/>) y el vaciado de
/// <see cref="PayloadJson"/>: queda la fila delgada como testigo en SQL de lo que se llevó a Mongo.
/// <c>[SinDiffDeAuditoria]</c>: auditar la auditoría sería un bucle.
/// </para>
/// </summary>
[SinDiffDeAuditoria]
public class AuditOutboxEntry : AuditableEntityLong
{
    /// <summary>Identificador del evento; en Mongo es el <c>_id</c> (un duplicado se trata como hecho).</summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>Flujo de la cadena: <c>{tenantPublicId:N}:10y</c>.</summary>
    public string Stream { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    /// <summary>Instante UTC del evento, a milisegundos (la precisión de Mongo: con más, el hash no se podría recalcular).</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>El evento en JSON; se vacía (<c>UPDATE … = NULL</c>) tras reenviarlo.</summary>
    public string? PayloadJson { get; set; }

    public bool Forwarded { get; set; }

    public DateTime? ForwardedAt { get; set; }

    public int ForwardAttempts { get; set; }

    public string? LastForwardError { get; set; }

    /// <summary>Posición en la cadena; la asigna el reenviador, en el orden en que sella.</summary>
    public long? Seq { get; set; }

    public string? PrevHash { get; set; }

    /// <summary>SHA-256(<see cref="PrevHash"/> ‖ JSON canónico de <c>SelloDeIntegridad</c>), en hexadecimal.</summary>
    public string? Hash { get; set; }
}
