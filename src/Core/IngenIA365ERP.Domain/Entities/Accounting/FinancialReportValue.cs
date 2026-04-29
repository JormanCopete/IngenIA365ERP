using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_FinancialReportValues] (cnt_parvalmedian).</summary>
public class FinancialReportValue : AuditableEntity
{
    public int FormatId { get; set; }
    public int ValueId { get; set; }
    public string? ValueCode { get; set; }
    public string Description { get; set; } = string.Empty;
}
