using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_CollectionNotices] (cop_maecircobro).</summary>
public class CollectionNotice : AuditableEntityLong
{
    [MaxLength(3)]
    public string NoticeNumber { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int Period { get; set; }
    [MaxLength(2)]
    public string ConceptClass { get; set; } = string.Empty;
    public DateTime NoticeDate { get; set; }
    [MaxLength(80)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(60)]
    public string? Phone { get; set; }
    public int CityCode { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Detail1 { get; set; }
    public string? Detail2 { get; set; }
    public int? DaysFrom { get; set; }
    public int? DaysTo { get; set; }
    [MaxLength(2)]
    public string SendToCodeudor { get; set; } = string.Empty;
    [MaxLength(2)]
    public string TrailingLegend { get; set; } = string.Empty;
}
