using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Certificado de retención expedido a un tercero (feature 009, FR-067..FR-069): consecutivo por
/// tipo, fecha de expedición, líneas por concepto. <see cref="LedgerFingerprint"/> resume las
/// líneas contables que lo alimentaron; si el libro cambia después, el certificado queda
/// <c>Outdated</c> y se reexpide con número nuevo dejando este <c>Voided</c> (R20).
/// </summary>
public class WithholdingCertificate : AuditableEntity
{
    public int PersonId { get; set; }
    public Person? Person { get; set; }
    public TaxKind TaxKind { get; set; }
    public int Year { get; set; }
    public byte? PeriodFrom { get; set; }
    public byte? PeriodTo { get; set; }
    public long Number { get; set; }
    public DateTime IssuedAt { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
    public CertificateStatus Status { get; set; } = CertificateStatus.Current;
    public int? VoidedByCertificateId { get; set; }
    public string LedgerFingerprint { get; set; } = string.Empty;
    public decimal TotalBase { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? LastSentAt { get; set; }
    public string? LastSentTo { get; set; }

    public ICollection<WithholdingCertificateLine> Lines { get; set; } = [];
}
