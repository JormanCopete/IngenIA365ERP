using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Web;

/// <summary>Maps to [dbo].[WEB_Services] (web_maeser).</summary>
public class WebService : AuditableEntity
{
    public int? ServiceNumber { get; set; }

    [MaxLength(2)]
    public string? ServiceType { get; set; }

    [MaxLength(20)]
    public string? IdentificationNumber { get; set; }

    public DateOnly? CallDate { get; set; }
    public DateOnly? AppointmentDate { get; set; }
    public string? Observations { get; set; }
    public bool? IsReviewed { get; set; }

    [MaxLength(50)]
    public string? ReviewedBy { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }
}
