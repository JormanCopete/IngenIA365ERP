using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PreLiquidations] (nom_preliq).</summary>
public class PreLiquidation : AuditableEntityLong
{
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int Sequence { get; set; }
    public long IdentificationNumber { get; set; }
    public decimal Value { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal Ibc { get; set; }
    public int TotalDays { get; set; }
    public int PreviousDays { get; set; }
    public int EntryDays { get; set; }
    public decimal HealthValue { get; set; }
    public decimal PensionValue { get; set; }
    public decimal WorkRiskValue { get; set; }
    public decimal SolidarityValue { get; set; }
    public int BranchId { get; set; }

    [MaxLength(1)]
    public string ClassCode { get; set; } = string.Empty;

    [MaxLength(1)]
    public string PensionEntry { get; set; } = string.Empty;

    [MaxLength(1)]
    public string HealthEntry { get; set; } = string.Empty;

    [MaxLength(1)]
    public string WorkRiskEntry { get; set; } = string.Empty;

    public int HealthEntityId { get; set; }
    public int PensionEntityId { get; set; }
    public int WorkRiskEntityId { get; set; }
    public decimal MaternityValue { get; set; }
    public decimal GeneralValue { get; set; }

    [MaxLength(1)]
    public string IsNewHire { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsTermination { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsRateChange { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsEntityChange { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsSuspensionPension { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsSuspensionTemp { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsUnpaidLeave { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsGeneralIncapacity { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsMaternityLeave { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsVacation { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsTemporaryTransfer { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsVoluntaryPension { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsWorkRiskIncapacity { get; set; } = string.Empty;

    public decimal WorkRiskRate { get; set; }

    [MaxLength(15)]
    public string UserName { get; set; } = string.Empty;

    public DateTime ProcessDate { get; set; }
    public int SalaryClass { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [MaxLength(40)]
    public string EmployeeName { get; set; } = string.Empty;

    public int RecordNumber { get; set; }
    public int TotalEmployees { get; set; }
    public int GeneralAuth { get; set; }
    public int MaternityAuth { get; set; }
    public int UpcValue { get; set; }

    [MaxLength(1)]
    public string IsVacationCause { get; set; } = string.Empty;

    [MaxLength(1)]
    public string IsVacationEnjoyment { get; set; } = string.Empty;

    public decimal CurrentSalary { get; set; }
    public decimal MinimumWage { get; set; }
    public int IncapacityClass { get; set; }

    [MaxLength(1)]
    public string IsExtension { get; set; } = string.Empty;

    public int AffiliationDays { get; set; }
    public decimal IbcWorkRisk { get; set; }
    public int EntryCode { get; set; }
    public int WorkRiskIncapacityDays { get; set; }

    [MaxLength(6)]
    public string PilaPensionCode { get; set; } = string.Empty;

    [MaxLength(6)]
    public string PilaHealthCode { get; set; } = string.Empty;

    [MaxLength(6)]
    public string PilaWorkRiskCode { get; set; } = string.Empty;

    [MaxLength(6)]
    public string PilaCcfCode { get; set; } = string.Empty;

    public int CcfValue { get; set; }

    [MaxLength(1)]
    public string IsIntegralSalary { get; set; } = string.Empty;

    public int SenaValue { get; set; }
    public int IcbfValue { get; set; }
    public int EsapValue { get; set; }
    public int EducationMinValue { get; set; }
    public int IbcCcf { get; set; }

    [MaxLength(1)]
    public string SenaContrib { get; set; } = string.Empty;

    public int CompensationFundId { get; set; }
    public int Period { get; set; }
    public decimal CurrentSalaryFull { get; set; }
    public int VacationEntryDays { get; set; }
    public int IncapacityEntryDays { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
