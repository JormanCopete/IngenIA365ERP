using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Subsidies].</summary>
public class Subsidy : AuditableEntity
{
    [MaxLength(5)]
    public string SubsidyCode { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(25)]
    public string ShortName { get; set; } = string.Empty;
    [MaxLength(15)]
    public string AccountCode { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public int Amount { get; set; }
    public int SubsidyClass { get; set; }
    [MaxLength(5)]
    public string CommitteeCode { get; set; } = string.Empty;
    public decimal TaxRate { get; set; }
    [MaxLength(15)]
    public string TaxAccount { get; set; } = string.Empty;
    [MaxLength(15)]
    public string TaxExpenseAccount { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ControlsCeiling { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    public int CurrentCeiling { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    [MaxLength(2)]
    public string ControlsAssociateCeiling { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AssociatePeriodicity { get; set; } = string.Empty;
    public int AssociateCeiling { get; set; }
    public DateOnly AssociateStartDate { get; set; }
    public DateOnly AssociateEndDate { get; set; }
    [MaxLength(2)]
    public string InInstallments { get; set; } = string.Empty;
    public int InstallmentCount { get; set; }
    [MaxLength(2)]
    public string ControlsSeniority { get; set; } = string.Empty;
    public int SeniorityYearsStart { get; set; }
    public int SeniorityYearsEnd { get; set; }
}
