using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_StampTaxes] (cnt_estampilla).</summary>
public class StampTax : AuditableEntity
{
    public string? LegacyCode { get; set; }
    public string Grade { get; set; } = string.Empty;
    public string? DebitAccountCode { get; set; }
    public string? CreditAccountCode { get; set; }
}
