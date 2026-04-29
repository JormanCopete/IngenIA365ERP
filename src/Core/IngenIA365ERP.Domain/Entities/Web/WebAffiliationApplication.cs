using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Web;

/// <summary>Maps to [dbo].[WEB_AffiliationApplications] (web_solafi).</summary>
public class WebAffiliationApplication : AuditableEntityLong
{
    public int? SequenceNumber { get; set; }
    public DateOnly? ApplicationDate { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(20)]
    public string? IdentificationNumber { get; set; }

    [MaxLength(5)]
    public string? IdentificationType { get; set; }

    [MaxLength(150)]
    public string? FirstName { get; set; }

    [MaxLength(150)]
    public string? LastName { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? EmployerName { get; set; }

    public decimal? Salary { get; set; }

    [MaxLength(150)]
    public string? SpouseFirstName { get; set; }

    [MaxLength(150)]
    public string? SpouseLastName { get; set; }

    [MaxLength(20)]
    public string? SpouseIdentificationNumber { get; set; }

    [MaxLength(5)]
    public string? SpouseIdentificationType { get; set; }

    [MaxLength(30)]
    public string? SpousePhone { get; set; }

    [MaxLength(200)]
    public string? SpouseEmail { get; set; }

    [MaxLength(100)]
    public string? SpouseEmployerName { get; set; }

    public decimal? SpouseSalary { get; set; }
}
