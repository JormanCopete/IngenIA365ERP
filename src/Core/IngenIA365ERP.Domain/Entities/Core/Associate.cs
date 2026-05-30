using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_Associates]. Hija de <see cref="Person"/> (1:1).
///
/// <para>
/// Contiene TODO lo que es del rol "asociado" de la cooperativa:
///   - Datos de afiliacion (JoinDate, ContributionRate, etc.)
///   - Datos del empleo del asociado en su EMPRESA EXTERNA
///   - Datos bancarios para deposito de excedentes/aportes
///   - Scoring/riesgo del asociado
///   - Estados/banderas del rol asociado
///   - Datos del CONYUGE referidos al empleo (los datos personales del
///     conyuge viven en <see cref="Spouse"/>).
/// </para>
///
/// <para>
/// Una persona puede ser simultaneamente asociado y empleado interno
/// (decision P2): tendra fila en <see cref="Associate"/> y otra en
/// <see cref="Payroll.Employee"/>, ambas apuntando al mismo PersonId.
/// Los salarios de cada rol son independientes (decision P3).
/// </para>
/// </summary>
public class Associate : AuditableEntity
{
    public int PersonId { get; set; }

    // === DATOS DE AFILIACION ===

    public DateOnly? JoinDate { get; set; }
    public decimal ContributionRate { get; set; }
    public int? BranchId { get; set; }
    public int? CostCenterId { get; set; }
    public int? SectionId { get; set; }
    public short CutoffDay { get; set; }

    [MaxLength(2)]
    public string? Status { get; set; }

    public DateOnly? WithdrawalDate { get; set; }
    public int? WithdrawalReasonId { get; set; }
    public DateOnly? RejoinDate { get; set; }

    [MaxLength(2)]
    public string? CategoryRating { get; set; }

    public int? AdvisorId { get; set; }

    [MaxLength(2)]
    public string? DeductionPeriod { get; set; }

    [MaxLength(2)]
    public string? DeductionType { get; set; }

    [MaxLength(4)]
    public string? Rank { get; set; }

    [MaxLength(20)]
    public string? ReferredBy { get; set; }

    public bool IsInLegalCollection { get; set; }
    public short ContractNumber { get; set; }
    public DateOnly? ContractExpiryDate { get; set; }
    public int? CommitteeId { get; set; }

    [MaxLength(20)]
    public string? ZoneCode { get; set; }

    public decimal ContributionPledged { get; set; }

    [MaxLength(4)]
    public string? AssociateClass { get; set; }

    [MaxLength(4)]
    public string? PaymentType { get; set; }

    [MaxLength(10)]
    public string? SectorCode { get; set; }

    public DateOnly? LastTransferDate { get; set; }
    public short MailingOption { get; set; }

    [MaxLength(2)]
    public string? ManualRating { get; set; }

    [MaxLength(2)]
    public string? PreviousClass { get; set; }

    // === EMPLEO DEL ASOCIADO EN SU EMPRESA EXTERNA ===
    // Vienen del legacy sys_maenit. NO se confunden con PAY_Employees:
    // PAY_Employees es la planilla INTERNA de la cooperativa.

    public int? EmployerCompanyId { get; set; }   // FK a COR_EmployerCompanies (legacy)

    [MaxLength(80)]
    public string? ExternalEmployerName { get; set; }

    public DateOnly? ExternalEmploymentStartDate { get; set; }

    public decimal ExternalSalary { get; set; }

    [MaxLength(2)]
    public string? ExternalSalaryType { get; set; }

    public decimal ExternalSeverance { get; set; }

    [MaxLength(100)]
    public string? ExternalSeveranceFund { get; set; }

    public int? ExternalProfessionId { get; set; }
    public int? ExternalPositionId { get; set; }

    [MaxLength(120)]
    public string? ExternalOtherIncomeDescription { get; set; }

    // === BANCA DEL ASOCIADO (deposito de excedentes/aportes — decision P5b) ===

    public int? DepositBankId { get; set; }

    [MaxLength(30)]
    public string? DepositBankAccountNumber { get; set; }

    [MaxLength(2)]
    public string? DepositBankAccountType { get; set; }

    public int? DepositBankAccountCityId { get; set; }

    // === SCORING / RIESGO DEL ROL ASOCIADO ===

    public bool AuthCentralRisk { get; set; }

    [MaxLength(2)]
    public string? PosCardClass { get; set; }

    public decimal PosCardLimit { get; set; }
    public decimal InsuranceRiskRate { get; set; }
    public int AssociateZoneTypeId { get; set; }
    public int AssociateZoneId { get; set; }

    public bool IsSiplaExempt { get; set; }
    public DateTime? SiplaExemptDate { get; set; }

    [MaxLength(20)]
    public string? SiplaUser { get; set; }

    public bool SinglePromissoryNote { get; set; }
    public bool PledgesContributions { get; set; }
    public bool InManagement { get; set; }

    public decimal CreditLimit { get; set; }

    // === CENTROS CONTABLES DEL ROL ASOCIADO ===

    public bool CpAdmin { get; set; }
    public bool CpContributions { get; set; }
    public bool CpLocal { get; set; }
    public bool CpCommission { get; set; }

    [MaxLength(20)]
    public string? ProfitCenter { get; set; }

    public bool CapacityPayPct { get; set; }

    // === ESTADOS/BANDERAS DEL ROL ===

    public bool IsFromGovernment { get; set; }
    public bool IsPublicResourceAdmin { get; set; }
    public bool IsPensioner { get; set; }
    public bool IsInsubordinate { get; set; }
    public bool IsOnVacation { get; set; }
    public bool IsOnUnpaidLeave { get; set; }

    [MaxLength(10)]
    public string? AssociatePensionType { get; set; }

    [MaxLength(10)]
    public string? AssociateSeveranceType { get; set; }

    // === DATOS DEL EMPLEO DEL CONYUGE (decision P10) ===

    [MaxLength(80)]
    public string? SpouseEmployer { get; set; }

    [MaxLength(80)]
    public string? SpouseEmployerAddress { get; set; }

    public DateOnly? SpouseEmployerStart { get; set; }

    public decimal SpouseSalary { get; set; }

    [MaxLength(2)]
    public string? SpouseSalaryType { get; set; }

    [MaxLength(60)]
    public string? SpousePosition { get; set; }

    [MaxLength(10)]
    public string? SpouseProfession { get; set; }

    [MaxLength(2)]
    public string? SpouseEducationLevel { get; set; }

    public decimal? SpouseSeverance { get; set; }
    public decimal? SpouseOtherIncome { get; set; }

    [MaxLength(120)]
    public string? SpouseOtherIncomeDesc { get; set; }

    [MaxLength(10)]
    public string? SpouseCompanyCode { get; set; }

    [MaxLength(10)]
    public string? SpouseBranchCode { get; set; }

    [MaxLength(10)]
    public string? SpouseSectionCode { get; set; }

    // === Navigation properties ===

    public Person Person { get; set; } = null!;
    public EmployerCompany? EmployerCompany { get; set; }
    public Branch? Branch { get; set; }
    public CostCenter? CostCenter { get; set; }
    public Section? Section { get; set; }
    public WithdrawalReason? WithdrawalReason { get; set; }
    public Advisor? Advisor { get; set; }
    public Committee? Committee { get; set; }
}
