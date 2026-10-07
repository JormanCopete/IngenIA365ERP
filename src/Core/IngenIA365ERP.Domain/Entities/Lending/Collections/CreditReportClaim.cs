using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_CreditReportClaims</c> (feature 013, C.3; data-model §26.12; FR-006, FR-137; T69): reclamo o suplantación sobre el
/// reporte en centrales. No es hecho: avanza de <c>InProcess</c> a respondido, aceptado tácitamente o retirado. Mientras está en
/// trámite, el archivo de centrales lleva la marca «reclamo en trámite».
/// </summary>
public class CreditReportClaim : AuditableEntity
{
    public int PersonId { get; set; }

    public int? LoanId { get; set; }

    public CreditReportClaimKind Kind { get; set; }

    public CreditReportClaimStatus Status { get; set; } = CreditReportClaimStatus.InProcess;

    public string? BureauCode { get; set; }

    public DateOnly ReceivedOn { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>15 días hábiles, prorrogables 8 (festivos nacionales).</summary>
    public DateOnly DueDate { get; set; }

    public DateOnly? ExtendedDueDate { get; set; }

    public DateOnly? ExtensionNotifiedOn { get; set; }

    public DateOnly? AnsweredOn { get; set; }

    public bool? AnswerUpholdsClaim { get; set; }

    public string? AnswerSummary { get; set; }

    public Guid? AnswerAttachmentPublicId { get; set; }

    /// <summary>Lo escribe el ciclo diario (paso 7) con alerta.</summary>
    public DateOnly? TacitAcceptanceOn { get; set; }

    public DateOnly? WithdrawnOn { get; set; }

    /// <summary>Si entró como solicitud del titular.</summary>
    public int? DataSubjectRequestId { get; set; }

    public int? MigrationLoadId { get; set; }
}
