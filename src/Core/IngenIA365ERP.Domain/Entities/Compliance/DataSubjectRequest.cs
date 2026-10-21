using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Domain.Entities.Compliance;

/// <summary>
/// <c>COR_DataSubjectRequests</c> (feature 013, T69 b, S10; data-model parte 3 §4.9): una solicitud del titular de los datos —conocer,
/// actualizar, rectificar, suprimir, revocar— con su plazo en días hábiles nacionales (<c>PlazosDelTitular</c>). La radica
/// <c>RegisterDataSubjectRequestCommand</c>; la prórroga (una sola) y la respuesta las escriben sus comandos. Una revocación o supresión
/// aplica <c>ReglaDeRevocacion</c> y sella la respuesta de la consulta de obligaciones en <see cref="DutyEvidenceJson"/>. No es un hecho:
/// cambia de estado; la respuesta se escribe una vez (<see cref="RespondedAt"/>). Migración <c>PlataformaParaCartera</c>. (nuevo)
/// </summary>
public class DataSubjectRequest : AuditableEntity
{
    /// <summary>El titular.</summary>
    public int PersonId { get; set; }

    public DataSubjectRequestKind Kind { get; set; }

    /// <summary>La finalidad sobre la que recae; obligatoria en revocación y supresión.</summary>
    public DataAuthorizationPurpose? Purpose { get; set; }

    /// <summary>El canal a revocar cuando <see cref="Purpose"/> es <c>ContactChannel</c>.</summary>
    public DataAuthorizationChannel? ContactChannel { get; set; }

    /// <summary><c>Presencial</c>, <c>Correo</c>, <c>Escrito</c>, <c>Telefono</c>.</summary>
    public string ReceivedVia { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    /// <summary>Fecha local del recibo: desde ella corre el plazo.</summary>
    public DateOnly ReceivedOn { get; set; }

    public string Description { get; set; } = string.Empty;

    public Guid? EvidenceAttachmentPublicId { get; set; }

    public DateOnly DueDate { get; set; }

    /// <summary>La prórroga, una sola vez.</summary>
    public DateOnly? ExtendedDueDate { get; set; }

    public string? ExtensionReason { get; set; }

    public DataSubjectRequestStatus Status { get; set; } = DataSubjectRequestStatus.Received;

    public DataSubjectRequestOutcome? Outcome { get; set; }

    /// <summary>El deber legal o contractual que impide revocar o suprimir (FR-006).</summary>
    public string? DutyBasis { get; set; }

    /// <summary>La respuesta sellada de la consulta de obligaciones que usó <c>ReglaDeRevocacion</c>.</summary>
    public string? DutyEvidenceJson { get; set; }

    public string? ResponseText { get; set; }

    public Guid? ResponseAttachmentPublicId { get; set; }

    [EscrituraUnica]
    public DateTime? RespondedAt { get; set; }

    public int? RespondedByUserId { get; set; }

    public string? RespondedByName { get; set; }

    public int RegisteredByUserId { get; set; }

    public string RegisteredByName { get; set; } = string.Empty;

    /// <summary>El vencimiento que rige: el prorrogado si lo hay.</summary>
    public DateOnly VenceEl => ExtendedDueDate ?? DueDate;

    /// <summary>Abierta: recibida o prorrogada, sin responder ni retirar.</summary>
    public bool Abierta => Status is DataSubjectRequestStatus.Received or DataSubjectRequestStatus.Extended;
}
