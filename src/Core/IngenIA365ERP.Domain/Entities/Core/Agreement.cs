using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Agreements] (sys_convenio).
/// </summary>
public class Agreement : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? AccountNumber { get; set; }

    [MaxLength(10)]
    public string? EntityCode { get; set; }

    public short Currency { get; set; }

    // Account type codes
    [MaxLength(4)]
    public string? SavingsCode { get; set; }

    [MaxLength(4)]
    public string? CheckingCode { get; set; }

    [MaxLength(4)]
    public string? BlockCode { get; set; }

    // Availability config
    public short AvailabilityOption { get; set; }
    public decimal AvailabilityLimit { get; set; }
    public decimal AvailabilityRate { get; set; }

    // ATM config
    public short AtmOption { get; set; }
    public decimal AtmLimit { get; set; }
    public decimal AtmRate { get; set; }
    public short AtmTransactions { get; set; }

    // POS config
    public short PosOption { get; set; }
    public decimal PosLimit { get; set; }
    public decimal PosRate { get; set; }
    public short PosTransactions { get; set; }

    // Balances and limits
    public short ShowBalances { get; set; }
    public int Bin { get; set; }
    public decimal AvailableLimit { get; set; }
    public decimal CashLimit { get; set; }

    // File paths
    [MaxLength(200)]
    public string? OutputPath { get; set; }

    [MaxLength(200)]
    public string? InputPath { get; set; }

    // Additional config
    public int AverageDays { get; set; }
    public int FreeTransactions { get; set; }
    public int HandlingFee { get; set; }
    public decimal AvailableLimit2 { get; set; }
    public decimal CashLimit2 { get; set; }
    public int ServiceType { get; set; }
}
