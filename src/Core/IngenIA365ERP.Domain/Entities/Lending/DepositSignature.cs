using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_DepositSignatures].</summary>
public class DepositSignature : AuditableEntity
{
    public long AccountNumber { get; set; }
    public int SignatureNumber { get; set; }
    public byte[]? SignatureImage { get; set; }
    [MaxLength(2)]
    public string IsRequired { get; set; } = string.Empty;
}
