using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Beneficiaries] (cop_benef + nom_bene).
/// Merged: cop_benef for associate beneficiaries, nom_bene for employee beneficiaries.
/// </summary>
public class Beneficiary : AuditableEntity
{
    public int PersonId { get; set; }

    [MaxLength(20)]
    public string BeneficiaryIdNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string BeneficiaryName { get; set; } = string.Empty;

    [MaxLength(4)]
    public string? DocumentType { get; set; }

    public int? RelationshipId { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(2)]
    public string? EducationLevel { get; set; }

    public bool HasDisability { get; set; }
    public bool IsEmployed { get; set; }
    public decimal Percentage { get; set; }

    [MaxLength(2)]
    public string? Gender { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(120)]
    public string? Address { get; set; }

    public int? CityId { get; set; }

    [MaxLength(2)]
    public string BeneficiaryType { get; set; } = "A";

    [MaxLength(2)]
    public string? Status { get; set; }

    [MaxLength(20)]
    public string? LegacyBenefCode { get; set; }

    [MaxLength(20)]
    public string? LegacyNewId { get; set; }

    // Navigation
    public Person Person { get; set; } = null!;
    public Relationship? Relationship { get; set; }
    public City? City { get; set; }
}
