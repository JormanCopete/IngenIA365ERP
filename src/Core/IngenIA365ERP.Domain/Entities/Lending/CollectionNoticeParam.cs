using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CollectionNoticeParams] (cop_paracircular).</summary>
public class CollectionNoticeParam : AuditableEntity
{
    [MaxLength(3)]
    public string NoticeCode { get; set; } = string.Empty;
    public int DaysFrom { get; set; }
    public int DaysTo { get; set; }
    public string Detail1 { get; set; } = string.Empty;
    public string Detail2 { get; set; } = string.Empty;
    [MaxLength(80)]
    public string CreatorName { get; set; } = string.Empty;
    [MaxLength(60)]
    public string Position { get; set; } = string.Empty;
    public byte[]? SignatureImage { get; set; }
}
