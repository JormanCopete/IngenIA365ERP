using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

public enum MfaResetStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Executed = 3,
    Expired = 4
}

/// <summary>
/// Solicitud de reset administrativo del MFA (FR-013, doble aprobación).
/// El solicitante no puede aprobarse a sí mismo; los dos aprobadores deben
/// ser distintos; ventana de 24h.
/// </summary>
public class MfaResetRequest : AuditableEntity
{
    public int UserId { get; set; }
    public int RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? EvidenceAttachmentId { get; set; }

    public MfaResetStatus Status { get; set; } = MfaResetStatus.Pending;
    public int? FirstApproverId { get; set; }
    public DateTime? FirstApprovalAt { get; set; }
    public int? SecondApproverId { get; set; }
    public DateTime? SecondApprovalAt { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public User? User { get; set; }
}
