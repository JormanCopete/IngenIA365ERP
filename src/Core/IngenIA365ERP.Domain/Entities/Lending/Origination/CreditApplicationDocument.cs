using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_CreditApplicationDocuments</c> (feature 013; data-model §2.3; FR-025, FR-035): un documento soporte de la solicitud. Se sube
/// mientras la solicitud no esté decidida; después el adjunto queda bloqueado (<c>Attachments.OwnerLocked</c>). No es hecho.
/// </summary>
public class CreditApplicationDocument : AuditableEntity
{
    public int ApplicationId { get; set; }

    /// <summary>Requisito documental de la versión de la línea; nulo = documento adicional.</summary>
    public int? RequirementId { get; set; }

    /// <summary>De qué parte de la solicitud es; nulo = de la solicitud.</summary>
    public int? PartyId { get; set; }

    /// <summary>Adjunto <c>LoanApplicationDocument</c> (T81), sin FK.</summary>
    public Guid AttachmentPublicId { get; set; }

    /// <summary>Fecha del documento, para <c>MaxAgeDays</c> del requisito.</summary>
    public DateOnly? DocumentDate { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public int? VerifiedByUserId { get; set; }

    /// <summary>Un documento rechazado no cuenta para los obligatorios.</summary>
    public string? RejectionReason { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0388) --

    /// <summary>Un documento adjuntado a la solicitud (T81): el adjunto ya subido con dueño <c>LoanApplicationDocument</c>.</summary>
    public static CreditApplicationDocument Nuevo(int applicationId, int? requirementId, int? partyId, Guid attachmentPublicId, DateOnly? fecha) => new()
    {
        ApplicationId = applicationId,
        RequirementId = requirementId,
        PartyId = partyId,
        AttachmentPublicId = attachmentPublicId,
        DocumentDate = fecha,
    };

    /// <summary>
    /// ¿Cuenta para un requisito obligatorio a <paramref name="hoy"/>? (FR-035): no está de baja ni rechazado y, si el requisito tiene
    /// antigüedad máxima, el documento trae su fecha y no la pasa.
    /// </summary>
    public bool Cuenta(DateOnly hoy, int? maxAgeDays) =>
        !IsDeleted && string.IsNullOrWhiteSpace(RejectionReason)
        && (maxAgeDays is not { } dias || DocumentDate is { } fecha && fecha <= hoy && fecha.AddDays(dias) >= hoy);
}
