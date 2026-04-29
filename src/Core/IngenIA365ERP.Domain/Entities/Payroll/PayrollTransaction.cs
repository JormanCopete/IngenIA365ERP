using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PayrollTransactions] (nom_movtos).</summary>
public class PayrollTransaction : AuditableEntityLong
{
    public int PayPeriodId { get; set; }
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int ConceptId { get; set; }
    public decimal SequenceNumber { get; set; }
    public decimal? Time { get; set; }
    public decimal? Amount { get; set; }
    public decimal? PaymentMethod { get; set; }

    [MaxLength(50)]
    public string? UserName { get; set; }

    public DateTime? TransactionDate { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    // Navigation
    public Employee? Employee { get; set; }
    public PayrollConcept? Concept { get; set; }
}
