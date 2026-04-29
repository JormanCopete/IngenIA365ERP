using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Banks] (sys_banco03).
/// </summary>
public class Bank : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? ShortName { get; set; }

    [MaxLength(20)]
    public string? AccountCode { get; set; }

    [MaxLength(10)]
    public string? VoucherTypeCode { get; set; }

    [MaxLength(20)]
    public string? TransferCode { get; set; }

    [MaxLength(2)]
    public string? AccountClass { get; set; }

    public bool CheckDigitRequired { get; set; }

    public int? LastCheckNumber { get; set; }

    [MaxLength(20)]
    public string? AccountingAccountCode { get; set; }

    [MaxLength(2)]
    public string? PrintFormat { get; set; }

    public short? Copies { get; set; }

    public decimal FinancialTaxRate { get; set; }

    [MaxLength(4)]
    public string? FileStructure { get; set; }

    public bool ChargesCommission { get; set; }

    [MaxLength(20)]
    public string? CommissionAccount { get; set; }

    public int? CommissionType { get; set; }

    public decimal? CommissionAmount { get; set; }

    public bool PromptForPrinter { get; set; }

    [MaxLength(2)]
    public string? ControlSequential { get; set; }
}
