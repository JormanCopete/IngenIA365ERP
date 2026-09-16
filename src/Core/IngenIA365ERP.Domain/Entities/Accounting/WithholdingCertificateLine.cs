using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Una línea del certificado: concepto, cuenta, base, tarifa y valor retenido.</summary>
public class WithholdingCertificateLine : AuditableEntity
{
    public int CertificateId { get; set; }
    public WithholdingCertificate? Certificate { get; set; }
    public int AccountId { get; set; }
    public ChartOfAccount? Account { get; set; }
    public string ConceptCode { get; set; } = string.Empty;
    public decimal Base { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
}
