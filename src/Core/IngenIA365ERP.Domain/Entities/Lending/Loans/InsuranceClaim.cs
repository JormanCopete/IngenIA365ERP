using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Loans;

/// <summary>
/// <c>LND_InsuranceClaims</c> (feature 013; data-model §25.7; FR-085, FR-097; T102; datos en K1, proceso en K3): reclamación al seguro
/// de vida deudores tras el fallecimiento del deudor. Una sola en trámite por crédito. Mientras está <c>InProcess</c> no hay cobranza,
/// mensajes de cobro ni reporte negativo; el pago de la aseguradora por el contrato la pasa a <c>Paid</c> en la misma transacción.
/// No es hecho. Forma sin comportamiento.
/// </summary>
public class InsuranceClaim : AuditableEntity
{
    /// <summary>Único filtrado a <c>InProcess</c> y viva.</summary>
    public int LoanId { get; set; }

    /// <summary>La aseguradora (persona).</summary>
    public int InsurerPersonId { get; set; }

    /// <summary>El deudor fallecido.</summary>
    public int DeceasedPersonId { get; set; }

    public DateOnly DeathDate { get; set; }

    public Guid? DeathCertificateAttachmentPublicId { get; set; }

    /// <summary>Radicado ante la aseguradora.</summary>
    public string? ClaimNumber { get; set; }

    public DateOnly? FiledOn { get; set; }

    public decimal? ClaimedAmount { get; set; }

    public decimal? PaidAmount { get; set; }

    public InsuranceClaimStatus Status { get; set; } = InsuranceClaimStatus.InProcess;

    /// <summary>El pago de la aseguradora que la cerró (<c>LND_LoanOperations</c>, bigint).</summary>
    public long? PaidOperationId { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string? CloseReason { get; set; }

    /// <summary>Nacida de la migración.</summary>
    public int? MigrationLoadId { get; set; }
}
