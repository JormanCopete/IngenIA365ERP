using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Debit;

/// <summary>Maps to [dbo].[DEB_Cards] (deb_maetarj).</summary>
public class DebitCard : AuditableEntity
{
    public int BankId { get; set; }

    [MaxLength(25)]
    public string CardNumber { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? BinCode { get; set; }

    public int? AccountNumber { get; set; }
    public int? CreditLineId { get; set; }

    [MaxLength(5)]
    public string? AccountType { get; set; }

    [MaxLength(5)]
    public string? ErrorCode { get; set; }

    public int? PersonId { get; set; }

    [MaxLength(5)]
    public string? OperationType { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }

    [MaxLength(20)]
    public string? InitialConcept { get; set; }

    public decimal? AvailableBalance { get; set; }
    public decimal? DailyAtmLimit { get; set; }
    public int? DailyAtmTransactions { get; set; }
    public decimal? DailyPosLimit { get; set; }
    public int? DailyPosTransactions { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public DateOnly? LastEventDate { get; set; }
    public int? ExecutionTime { get; set; }
    public DateOnly? DownloadDate { get; set; }

    [MaxLength(100)]
    public string? Pin { get; set; }

    public int Mark { get; set; }
    public int AvailableLimitType { get; set; }
    public int? AtmLimitType { get; set; }

    [MaxLength(2)]
    public string IsDebitOrCredit { get; set; } = "D";

    [MaxLength(20)]
    public string? CoSigner1 { get; set; }

    [MaxLength(20)]
    public string? CoSigner2 { get; set; }

    public int CutoffDay { get; set; }
    public decimal CreditLimit { get; set; }

    [MaxLength(30)]
    public string? TerminalId { get; set; }

    public int BlockReasonId { get; set; }

    [MaxLength(20)]
    public string? BlockedByUserId { get; set; }

    public decimal DomesticAvailableBalance { get; set; }
    public decimal DomesticAtmLimit { get; set; }
    public int DomesticAtmTransactions { get; set; }
    public decimal DomesticPosLimit { get; set; }
    public int DomesticPosTransactions { get; set; }
    public int DomesticAccountNumber { get; set; }
    public bool ChargesManagement { get; set; }
    public bool ChargesManagementDs { get; set; }
    public decimal LegacyLineId { get; set; }
    public DateOnly? LimitAssignmentDate { get; set; }

    // Navigation
    public ICollection<DebitTransaction> Transactions { get; set; } = [];
}
