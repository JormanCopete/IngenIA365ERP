using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.CDT;

/// <summary>Maps to [dbo].[CDT_CertificateEntries] (cdt_novcdats).</summary>
public class CertificateEntry : AuditableEntityLong
{
    public int CertificateId { get; set; }
    public int PersonId { get; set; }
    public int CreditLineId { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly? OpeningDate { get; set; }

    [MaxLength(5)]
    public string EntryType { get; set; } = string.Empty;

    public decimal PreviousRate { get; set; }
    public decimal CurrentRate { get; set; }
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    // Navigation
    public Certificate? Certificate { get; set; }
}
