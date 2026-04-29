using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_DianReportFormats] (cnt_parfordian).</summary>
public class DianReportFormat : AuditableEntity
{
    public int FormatId { get; set; }
    public int ConceptId { get; set; }
    public string? FormatCode { get; set; }
    public string? Description { get; set; }
    public decimal Threshold { get; set; }
    public string? MinorTaxId { get; set; }
    public decimal BalanceThreshold { get; set; }
    public string? DianTaxId { get; set; }
}
