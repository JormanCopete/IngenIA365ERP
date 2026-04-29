using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Web;

/// <summary>Maps to [dbo].[WEB_AuxiliaryApplications] (web_solaux).</summary>
public class WebAuxiliaryApplication : AuditableEntityLong
{
    public int? PersonId { get; set; }
    public int? SequenceNumber { get; set; }
    public DateOnly? ApplicationDate { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(5)]
    public string? SubsidyType { get; set; }

    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
}
