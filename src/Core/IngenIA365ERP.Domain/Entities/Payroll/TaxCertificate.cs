using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_TaxCertificates] (nom_impcert).</summary>
public class TaxCertificate : AuditableEntityLong
{
    [MaxLength(5)]
    public string PeriodCode { get; set; } = string.Empty;

    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public decimal Value34 { get; set; }
    public decimal Value35 { get; set; }
    public decimal Value36 { get; set; }
    public decimal Value37 { get; set; }
    public decimal Value38 { get; set; }
    public decimal Value39 { get; set; }
    public decimal Value40 { get; set; }
    public decimal Value41 { get; set; }
    public decimal Value42 { get; set; }
    public decimal Value43 { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime IssueDate { get; set; }

    [MaxLength(60)]
    public string IssuedAt { get; set; } = string.Empty;

    [MaxLength(60)]
    public string PayerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PayerTaxId { get; set; } = string.Empty;

    public decimal Threshold { get; set; }
    public decimal Rate { get; set; }
    public int CityId { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
