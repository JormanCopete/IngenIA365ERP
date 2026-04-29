using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Debit;

/// <summary>Maps to [dbo].[DEB_PosTerminals] (deb_pardatafonos).</summary>
public class PosTerminal : AuditableEntity
{
    [MaxLength(20)]
    public string TerminalCode { get; set; } = string.Empty;

    public int InternalCode { get; set; }

    [MaxLength(5)]
    public string? VoucherCode { get; set; }

    [MaxLength(100)]
    public string? MerchantName { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }
}
