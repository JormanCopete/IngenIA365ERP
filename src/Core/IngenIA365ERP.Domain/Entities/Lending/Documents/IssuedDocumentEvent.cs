using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocumentEvents</c> (feature 013, C.3; data-model §27.3): emisión, entrega y anulación de un
/// <see cref="IssuedLendingDocument"/>. Hecho de sólo inserción (<see cref="IHechoInmutable"/>); una sola anulación por documento.
/// La reimpresión, la descarga y la verificación son lecturas auditadas, no eventos.
/// </summary>
public class IssuedDocumentEvent : AuditableEntity, IHechoInmutable
{
    public int IssuedDocumentId { get; init; }

    public IssuedDocumentEventKind Kind { get; init; }

    public DateTime OccurredAt { get; init; }

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    /// <summary>Obligatorio en la anulación manual (<c>Lending.Documents.Void</c>).</summary>
    public string? Reason { get; init; }

    /// <summary>El reverso que lo anuló (<c>VigenciaDeDocumentos</c>).</summary>
    public long? CausingOperationId { get; init; }

    /// <summary><c>Delivered</c> por correo.</summary>
    public int? DebtorMessageId { get; init; }
}
