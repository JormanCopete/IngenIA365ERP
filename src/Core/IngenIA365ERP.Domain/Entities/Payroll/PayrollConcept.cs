using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PayrollConcepts] (nom_cptos).</summary>
public class PayrollConcept : AuditableEntity
{
    public int ConceptCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;

    public int ConceptClass { get; set; }
    public int Nature { get; set; }
    public decimal Value { get; set; }
    public decimal Factor { get; set; }
    public int Base { get; set; }
    public int AffectsSalary { get; set; }

    [MaxLength(2)]
    public string DaysComputed { get; set; } = string.Empty;

    public int TimesExtended { get; set; }
    public int ValueExtended { get; set; }
    public int LiquidationBase { get; set; }
    public decimal TopSalary { get; set; }

    [MaxLength(4)]
    public string CertificateLine { get; set; } = string.Empty;

    [MaxLength(4)]
    public string CertificateColumn { get; set; } = string.Empty;

    public int AffectsBenefits { get; set; }
    public int AffectsWithholding { get; set; }
    public int IsBenefit { get; set; }
    public int MaintainBalance { get; set; }

    [MaxLength(2)]
    public string Priority { get; set; } = string.Empty;

    public decimal ProvisionRate { get; set; }
    public int ProvisionBase { get; set; }

    [MaxLength(14)]
    public string TaxId { get; set; } = string.Empty;

    public int IntegralSalary { get; set; }
    public int SingleUnit { get; set; }
    public int AdminBase { get; set; }
    public int RelatedConceptId { get; set; }
    public int PaymentConceptId { get; set; }
    public decimal VatRate { get; set; }

    [MaxLength(6)]
    public string EquivalentCode { get; set; } = string.Empty;

    public decimal MinorRate { get; set; }
    public decimal MajorRate { get; set; }
    public int AffectsSeverance { get; set; }
    public int AffectsBonus { get; set; }
    public int AffectsVacation { get; set; }
    public int AffectsIndemnity { get; set; }

    [MaxLength(6)]
    public string AdminId { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsAutomatic { get; set; } = string.Empty;

    public int ConceptSubClass { get; set; }
}
