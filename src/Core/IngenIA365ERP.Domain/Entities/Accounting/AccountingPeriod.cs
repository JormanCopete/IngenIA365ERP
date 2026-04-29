using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_AccountingPeriods] (sys_periodo — normalized).</summary>
public class AccountingPeriod : AuditableEntity
{
    public string ModuleCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public byte PeriodNumber { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "O";
}
