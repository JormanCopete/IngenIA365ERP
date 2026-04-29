using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_DirectDebits] (nom_libranzas).</summary>
public class DirectDebit : AuditableEntityLong
{
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int ConceptId { get; set; }
    public decimal SequenceNumber { get; set; }

    [MaxLength(10)]
    public string VoucherCode { get; set; } = string.Empty;

    public DateTime? DebitDate { get; set; }
    public DateTime? DiscountDate { get; set; }
    public decimal InitialAmount { get; set; }
    public int DiscountCycle { get; set; }
    public decimal InterestRate { get; set; }
    public decimal InstallmentAmount { get; set; }
    public int InstallmentType { get; set; }
    public int LiquidationBase { get; set; }
    public int NumberOfInstallments { get; set; }

    [MaxLength(20)]
    public string UserName { get; set; } = string.Empty;

    public DateTime SystemDate { get; set; }
    public DateTime EntryDate { get; set; }

    [MaxLength(20)]
    public string EntryUser { get; set; } = string.Empty;

    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;

    // Navigation
    public Employee? Employee { get; set; }
    public PayrollConcept? Concept { get; set; }
}
