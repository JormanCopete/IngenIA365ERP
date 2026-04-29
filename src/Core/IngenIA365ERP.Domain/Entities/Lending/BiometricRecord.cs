using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_BiometricRecords].</summary>
public class BiometricRecord : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public byte[]? SignatureImage { get; set; }
    public decimal FingerprintCode { get; set; }
    public byte[]? FingerprintData { get; set; }
    [MaxLength(500)]
    public string FingerprintString { get; set; } = string.Empty;
    public byte[]? PhotoImage { get; set; }
}
