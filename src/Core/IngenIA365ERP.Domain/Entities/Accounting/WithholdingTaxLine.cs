using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_WithholdingTaxLines] (cnt_linretefuente).</summary>
public class WithholdingTaxLine : AuditableEntity
{
    public string? LegacyCode { get; set; }
    public string LineCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AccountCode { get; set; }
    public decimal TaxRate { get; set; }
    public string? BaseAccountCode { get; set; }
    public string? Sign { get; set; }
}
