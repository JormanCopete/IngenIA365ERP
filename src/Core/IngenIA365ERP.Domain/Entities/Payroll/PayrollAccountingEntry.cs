using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_AccountingEntries] (nom_contpla).</summary>
public class PayrollAccountingEntry : AuditableEntityLong
{
    public int PlanId { get; set; }
    public int PayrollCompanyId { get; set; }
    public int ConceptId { get; set; }
    public int SequenceNumber { get; set; }

    [MaxLength(10)]
    public string CostCenterCode { get; set; } = string.Empty;

    [MaxLength(15)]
    public string AccountCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string TaxId { get; set; } = string.Empty;

    [MaxLength(5)]
    public string DocumentType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string DocumentNumber { get; set; } = string.Empty;

    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public int EmployeeId { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
