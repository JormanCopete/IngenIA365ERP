using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core.NationalValues;

/// <summary>
/// <c>COR_NationalValues</c> (feature 013, T37, T39, R1; data-model parte 3 §4.1): copia sin modificar de una fila de
/// <c>ADM_NationalValues</c> en la base de la cooperativa (el prefijo es <c>COR_</c> porque la constitución prohíbe
/// replicar tablas <c>ADM_</c>). La escribe <b>sólo</b> <c>ReplicacionDeValoresNacionales.AplicarAsync</c> y la leen
/// <b>sólo</b> <c>IValoresNacionales</c> e <c>ICalendarioHabil</c> (T40).
///
/// <para>Para la regla bitemporal la identidad es la de la central (<see cref="SourcePublicId"/>,
/// <see cref="SupersedesSourcePublicId"/>, <see cref="SourceSequence"/>) y R es <see cref="KnownAt"/>, el instante de la
/// réplica en esta cooperativa: una liquidación anterior a la réplica no pudo usar el valor.</para>
///
/// <para>«Pendiente de validar» es derivado, nunca columna: la última <c>COR_NationalValueReviews</c> sobre esta copia.</para>
/// </summary>
public class NationalValue : AuditableEntity, IHechoInmutable, IVersionBitemporal
{
    public string Code { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public DateOnly? ReferenceDate { get; init; }
    public decimal? Value { get; init; }
    public string? Unit { get; init; }
    public string? Label { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? Reason { get; init; }

    /// <summary>El <c>PublicId</c> de la fila central; único (guarda de idempotencia de la réplica).</summary>
    public Guid SourcePublicId { get; init; }

    /// <summary>La <c>Sequence</c> central; única. <c>MAX</c> de esta columna y la de los documentos es la marca de agua.</summary>
    public long SourceSequence { get; init; }

    /// <summary>Copia de <c>SupersedesPublicId</c> de la central.</summary>
    public Guid? SupersedesSourcePublicId { get; init; }

    /// <summary><c>RegisteredAt</c> de la central.</summary>
    public DateTime CentralRegisteredAt { get; init; }

    /// <summary>Instante UTC de la réplica en esta cooperativa: es R aquí.</summary>
    public DateTime KnownAt { get; init; }

    /// <summary>La réplica que la trajo (<c>COR_NationalValueReplications</c>).</summary>
    public int ReplicationId { get; init; }

    public NationalValueReplication? Replication { get; init; }

    Guid IVersionBitemporal.PublicId => SourcePublicId;
    Guid? IVersionBitemporal.SupersedesPublicId => SupersedesSourcePublicId;
    long IVersionBitemporal.Sequence => SourceSequence;
    DateTime IVersionBitemporal.ConocidaEn => KnownAt;
}
