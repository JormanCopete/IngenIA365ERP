using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CollectionMasters] (cop_gesmaes).</summary>
public class CollectionMaster : AuditableEntity
{
    public int Period { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public int PortfolioNumber { get; set; }
    [MaxLength(20)]
    public string? UserId { get; set; }
    public DateOnly ManagementDate { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal OverdueCapital { get; set; }
    public decimal OverdueInterest { get; set; }
    public decimal OverdueInsurance { get; set; }
    public decimal OverdueAdmin { get; set; }
    public decimal AccumulatedDefault { get; set; }
    public decimal InstallmentAmount { get; set; }
    public decimal DaysOverdue { get; set; }
    public long CollectionCaseId { get; set; }
    public decimal OverdueExtras { get; set; }
    public decimal OverdueOther { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
