using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core.NationalValues;

/// <summary>
/// <c>COR_RegulatoryDocuments</c> (feature 013, T37, T39; data-model parte 3 §4.2): copia sin modificar de un documento
/// normativo de <c>ADM_RegulatoryDocuments</c>. La escribe sólo la réplica y la lee sólo <c>INormativaVigente</c>, que
/// valida el JSON con el lector tipado, lo memoriza por <see cref="ContentSha256"/> y entrega el documento con su
/// <c>PublicId</c> y su huella para sellarlos en el cierre (T47). Misma identidad bitemporal que <see cref="NationalValue"/>.
/// </summary>
public class RegulatoryDocument : AuditableEntity, IHechoInmutable, IVersionBitemporal
{
    public string Kind { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public string Content { get; init; } = string.Empty;
    public string ContentSha256 { get; init; } = string.Empty;
    public short SchemaVersion { get; init; }
    public bool Verified { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? Reason { get; init; }

    public Guid SourcePublicId { get; init; }
    public long SourceSequence { get; init; }
    public Guid? SupersedesSourcePublicId { get; init; }
    public DateTime CentralRegisteredAt { get; init; }
    public DateTime KnownAt { get; init; }
    public int ReplicationId { get; init; }
    public NationalValueReplication? Replication { get; init; }

    Guid IVersionBitemporal.PublicId => SourcePublicId;
    Guid? IVersionBitemporal.SupersedesPublicId => SupersedesSourcePublicId;
    long IVersionBitemporal.Sequence => SourceSequence;
    DateTime IVersionBitemporal.ConocidaEn => KnownAt;
}
