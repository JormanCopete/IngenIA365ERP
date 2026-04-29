using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CollectionNoticeDetails] (cop_detcircobro).</summary>
public class CollectionNoticeDetail : AuditableEntityLong
{
    [MaxLength(3)]
    public string NoticeNumber { get; set; } = string.Empty;
    public int Period { get; set; }
    [MaxLength(2)]
    public string ConceptClass { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int Cycle { get; set; }
    public decimal CapitalBalance { get; set; }
    public decimal ExtraBalance { get; set; }
    public decimal InterestBalance { get; set; }
    public decimal DefaultBalance { get; set; }
    public decimal InsuranceBalance { get; set; }
    public decimal AdminBalance { get; set; }
    public decimal OtherBalance { get; set; }
    public int DaysOverdue { get; set; }
    public decimal TotalBalance { get; set; }
    public DateTime MaturityDate { get; set; }
    [MaxLength(20)]
    public string Codeudor1 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Phone1 { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Address1 { get; set; } = string.Empty;
    public int City1 { get; set; }
    [MaxLength(20)]
    public string Codeudor2 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Phone2 { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Address2 { get; set; } = string.Empty;
    public int City2 { get; set; }
    [MaxLength(20)]
    public string Codeudor3 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Phone3 { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Address3 { get; set; } = string.Empty;
    public int City3 { get; set; }
    [MaxLength(20)]
    public string Codeudor4 { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Phone4 { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Address4 { get; set; } = string.Empty;
    public int City4 { get; set; }
    [MaxLength(2)]
    public string OnlyDefaulted { get; set; } = string.Empty;

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
