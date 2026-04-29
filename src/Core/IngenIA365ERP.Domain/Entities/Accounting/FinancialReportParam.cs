using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_FinancialReportParams] (cnt_parinfmedian).</summary>
public class FinancialReportParam : AuditableEntity
{
    public int FormatId { get; set; }
    public int ConceptId { get; set; }
    public int ParameterOption { get; set; }
    public string ParameterValue { get; set; } = string.Empty;
    public int ValueOption { get; set; }
    public int CalculationBase { get; set; }
    public int Formula { get; set; }
}
