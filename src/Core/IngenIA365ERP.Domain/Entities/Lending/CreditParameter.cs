using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CreditParameters].</summary>
public class CreditParameter : AuditableEntity
{
    [MaxLength(5)]
    public string BankCode { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public int AdvanceType { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal AdvanceRate { get; set; }
    public int CreditLineId { get; set; }
    public int AdvanceCreditLineId { get; set; }
    [MaxLength(2)]
    public string DeductionClass { get; set; } = string.Empty;
    public int ExpirationDays { get; set; }
    public int CutoffDay { get; set; }
    public decimal? Amount1 { get; set; }
    public int? Term1 { get; set; }
    public int? Period1 { get; set; }
    public decimal? Amount2 { get; set; }
    public int? Term2 { get; set; }
    public int? Period2 { get; set; }
    public decimal? Amount3 { get; set; }
    public int? Term3 { get; set; }
    public int? Period3 { get; set; }
    public decimal? Amount4 { get; set; }
    public int? Term4 { get; set; }
    public int? Period4 { get; set; }
    public decimal? Amount5 { get; set; }
    public int? Term5 { get; set; }
    public int? Period5 { get; set; }
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    public decimal Amount6 { get; set; }
    public int Term6 { get; set; }
    public int Period6 { get; set; }
    public decimal Amount7 { get; set; }
    public int Term7 { get; set; }
    public int Period7 { get; set; }
    public int AdvanceTerm1 { get; set; }
    public int AdvanceTerm2 { get; set; }
    public int AdvanceTerm3 { get; set; }
    public int AdvanceTerm4 { get; set; }
    public int AdvanceTerm5 { get; set; }
    public int AdvanceTerm6 { get; set; }
    public int AdvanceTerm7 { get; set; }
    public int MaintenanceLineId { get; set; }
    public decimal MaintenanceAmount { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
