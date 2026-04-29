using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_FiscalPeriods] (new table).</summary>
public class FiscalPeriod : AuditableEntity
{
    public int PeriodYear { get; set; }
    public byte PeriodMonth { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "O";
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
}
