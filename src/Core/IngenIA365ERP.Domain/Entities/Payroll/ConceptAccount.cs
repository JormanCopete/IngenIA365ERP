using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_ConceptAccounts] (nom_cuentas).</summary>
public class ConceptAccount : AuditableEntity
{
    public int ConceptId { get; set; }

    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;

    [MaxLength(15)]
    public string ExpenseAccountCode { get; set; } = string.Empty;

    [MaxLength(15)]
    public string CounterAccountCode { get; set; } = string.Empty;

    [MaxLength(15)]
    public string ProvisionAccountCode { get; set; } = string.Empty;

    // Navigation
    public PayrollConcept? Concept { get; set; }
}
