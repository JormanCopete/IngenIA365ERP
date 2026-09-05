using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_RecurringNovelties]. Plantilla que genera una
/// <see cref="PayrollNovelty"/> por período hasta agotar las cuotas o llegar a la
/// fecha final (FR-030). Se materializa al calcular; la cuota se cuenta al aprobar.
/// </summary>
public class PayrollRecurringNovelty : AuditableEntity
{
    public int EmployeeId { get; set; }

    [MaxLength(30)]
    public string ConceptCode { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }
    public decimal? Amount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? TotalInstallments { get; set; }
    public int InstallmentsIssued { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(300)]
    public string? DeactivationReason { get; set; }

    public Employee? Employee { get; set; }

    public bool HasInstallmentsLeft => TotalInstallments is null || InstallmentsIssued < TotalInstallments;

    public bool CoversPeriod(DateTime periodStart, DateTime periodEnd) =>
        IsActive && StartDate <= periodEnd && (EndDate is null || EndDate >= periodStart) && HasInstallmentsLeft;
}
