using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_OnlineQueries] (lin_consulta).</summary>
public class OnlineQuery : AuditableEntityLong
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(20)]
    public string IdentificationNumber { get; set; } = string.Empty;
    public int ItemCode { get; set; }
    [MaxLength(60)]
    public string? Description { get; set; }
    public int CreditLineId { get; set; }
    [MaxLength(5)]
    public string VoucherType { get; set; } = string.Empty;
    public long VoucherNumber { get; set; }
    public DateTime? QueryDate { get; set; }
    public decimal QueryAmount { get; set; }
    [MaxLength(50)]
    public string UserFullName { get; set; } = string.Empty;
    public DateTime UpdateDate { get; set; }
    public int? LegacyConsecutivo { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
