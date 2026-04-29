using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Treasury;

/// <summary>Maps to [dbo].[TRS_Checks] (TES_CHEQUES).</summary>
public class Check : AuditableEntityLong
{
    [MaxLength(5)]
    public string ConceptCode { get; set; } = string.Empty;

    public int BankId { get; set; }
    public int SequentialNumber { get; set; }
    public int? PersonId { get; set; }

    [MaxLength(5)]
    public string? VoucherCode { get; set; }

    public int? VoucherNumber { get; set; }
    public DateOnly CheckDate { get; set; }
    public int CheckNumber { get; set; }
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? VoidDetail { get; set; }

    [MaxLength(20)]
    public string? VoidUserId { get; set; }

    [MaxLength(5)]
    public string? VoidVoucherCode { get; set; }

    public int? VoidVoucherNumber { get; set; }
    public DateTime? VoidDate { get; set; }

    [MaxLength(20)]
    public string? RecordUserId { get; set; }

    public DateTime? RecordDate { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }
}
