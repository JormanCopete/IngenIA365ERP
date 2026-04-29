using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_ExchangeRateHistory] (cnt_estadosdecambio).</summary>
public class ExchangeRateHistory : AuditableEntityLong
{
    public string? CurrencyCode { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? SourceAccountCode { get; set; }
    public string? TargetAccountCode { get; set; }
    public int? GroupNumber { get; set; }
    public int? SubgroupNumber { get; set; }
    public int? PeriodCode { get; set; }
    public decimal Amount { get; set; }
    public string? StatementCode { get; set; }
}
