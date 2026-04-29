using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Web;

/// <summary>Maps to [dbo].[WEB_DataUpdates] (web_actdatos).</summary>
public class WebDataUpdate : AuditableEntityLong
{
    public int? PersonId { get; set; }
    public int? SequenceNumber { get; set; }
    public DateOnly? UpdateDate { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(250)]
    public string? NewAddress { get; set; }

    [MaxLength(30)]
    public string? NewPhone { get; set; }

    [MaxLength(30)]
    public string? NewMobilePhone { get; set; }

    [MaxLength(200)]
    public string? NewEmail { get; set; }

    [MaxLength(100)]
    public string? NewCity { get; set; }

    [MaxLength(100)]
    public string? NewDepartment { get; set; }

    [MaxLength(100)]
    public string? NewEmployerName { get; set; }

    [MaxLength(250)]
    public string? NewEmployerAddress { get; set; }

    [MaxLength(30)]
    public string? NewEmployerPhone { get; set; }

    [MaxLength(100)]
    public string? NewPosition { get; set; }

    public decimal? NewSalary { get; set; }

    [MaxLength(5)]
    public string? NewMaritalStatus { get; set; }

    [MaxLength(5)]
    public string? NewEducationLevel { get; set; }

    [MaxLength(5)]
    public string? NewHousingType { get; set; }

    public DateTime? SystemDate { get; set; }
    public bool? IsProcessed { get; set; }

    [MaxLength(50)]
    public string? ProcessedBy { get; set; }
}
