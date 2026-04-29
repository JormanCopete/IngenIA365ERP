using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_AutoContributionParams] (nom_parautapo).</summary>
public class AutoContributionParam : AuditableEntity
{
    public int Code { get; set; }
    public int IdType { get; set; }
    public int IdNumber { get; set; }
    public int CheckDigit { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Fax { get; set; } = string.Empty;

    public int CityId { get; set; }

    [MaxLength(50)]
    public string CityName { get; set; } = string.Empty;

    public int DepartmentId { get; set; }

    [MaxLength(50)]
    public string DepartmentName { get; set; } = string.Empty;

    public decimal HealthRate { get; set; }
    public decimal PensionRate { get; set; }
    public decimal WorkRiskRate { get; set; }
    public decimal CcfRate { get; set; }
    public decimal SenaRate { get; set; }
    public decimal IcbfRate { get; set; }
    public decimal SolidarityFundRate { get; set; }
    public decimal EsapRate { get; set; }
    public decimal EducationMinRate { get; set; }

    [MaxLength(6)]
    public string CcfAdminCode { get; set; } = string.Empty;

    [MaxLength(6)]
    public string WorkRiskAdminCode { get; set; } = string.Empty;

    public decimal LatePaymentRate { get; set; }
    public int LinkType { get; set; }
    public int ContributionType { get; set; }
    public int HealthCoverage { get; set; }
    public int ContributionBase { get; set; }

    [MaxLength(15)]
    public string EmployerNumber { get; set; } = string.Empty;

    public decimal MinimumWage { get; set; }
    public int ProvisionRegime { get; set; }

    [MaxLength(15)]
    public string FormNumber { get; set; } = string.Empty;

    public DateTime CorrectionDate { get; set; }
    public int ContributionClass { get; set; }
    public int LegalNature { get; set; }
    public int EconomicActivityId { get; set; }

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(15)]
    public string RepresentativeId { get; set; } = string.Empty;

    [MaxLength(1)]
    public string RepresentativeCheckDigit { get; set; } = string.Empty;

    [MaxLength(30)]
    public string RepresentativeLastName1 { get; set; } = string.Empty;

    [MaxLength(30)]
    public string RepresentativeLastName2 { get; set; } = string.Empty;

    [MaxLength(30)]
    public string RepresentativeFirstName1 { get; set; } = string.Empty;

    [MaxLength(30)]
    public string RepresentativeFirstName2 { get; set; } = string.Empty;

    public int PresentationMethod { get; set; }
}
