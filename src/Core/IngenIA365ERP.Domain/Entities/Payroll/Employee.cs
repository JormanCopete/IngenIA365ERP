using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_Employees] (nom_empleados). Hija de <see cref="Person"/>.
///
/// <para>
/// Contiene SOLO datos del rol "empleado interno de la cooperativa" (planilla).
/// Los datos personales (nombre, documento, contacto, demografia) viven en
/// <see cref="Person"/> y se acceden via la relacion <see cref="Person"/>.
/// </para>
///
/// <para>
/// Si la persona es tambien asociado de la cooperativa, tendra ademas una fila
/// en <see cref="Associate"/> con su salario externo, fecha de afiliacion, etc.
/// (decision P2). Los salarios <see cref="Salary"/> aqui (interno) y
/// <c>Associate.ExternalSalary</c> (externo) son INDEPENDIENTES (decision P3).
/// </para>
/// </summary>
public class Employee : AuditableEntity
{
    public int PersonId { get; set; }   // NOT NULL — siempre apunta a una Person

    public int PayrollCompanyId { get; set; }

    // === NOMINA (feature 005) ===

    /// <summary>Plan de nómina al que pertenece; exactamente uno (FR-037).</summary>
    public int PayrollPlanId { get; set; }

    /// <summary>Fecha de efecto del último cambio de plan; rige desde el primer período posterior.</summary>
    public DateTime? PayrollPlanEffectiveFrom { get; set; }

    /// <summary>Procedimiento de retención en la fuente: 1 (tabla cada período) o 2 (porcentaje fijo semestral).</summary>
    public byte WithholdingProcedure { get; set; } = 1;

    /// <summary>Decide qué conceptos aplican por parametrización, nunca por casos en el motor.</summary>
    public Enums.Payroll.EmployeeClass EmployeeClass { get; set; } = Enums.Payroll.EmployeeClass.Standard;

    public PayrollPlan? PayrollPlan { get; set; }

    [MaxLength(8)]
    public string CostCenterId { get; set; } = string.Empty;

    // === DATOS LABORALES INTERNOS ===

    public int PositionId { get; set; }

    [MaxLength(4)]
    public string? AreaCode { get; set; }

    [MaxLength(4)]
    public string? SectionId { get; set; }

    public decimal Salary { get; set; }
    public int SalaryType { get; set; }
    public DateTime? EffectiveDate { get; set; }

    public int ContractType { get; set; }
    public DateTime ContractEndDate { get; set; }

    public DateTime JoinDate { get; set; }   // Fecha de ingreso a la cooperativa
    public DateTime TerminationDate { get; set; }

    [MaxLength(4)]
    public string? TerminationCause { get; set; }

    public DateTime RehireDate { get; set; }
    public int Status { get; set; }
    public int EmployeeType { get; set; }

    // === DEDUCCIONES Y RETENCION ===

    public decimal? WithholdingTaxRate { get; set; }
    public int WithholdingCycle { get; set; }
    public int WithholdingAvgType { get; set; }
    public decimal DeductibleWithholding { get; set; }

    public int FirstPayCycle { get; set; }
    public int ContributionCycle { get; set; }
    public int TransportSubsidyClass { get; set; }
    public int PaymentMethod { get; set; }
    public int PayrollClass { get; set; }

    // === SEGURIDAD SOCIAL ===

    public int HealthInsuranceId { get; set; }
    public int PensionFundId { get; set; }
    public int WorkRiskId { get; set; }
    public int WorkRiskRateId { get; set; }
    public decimal SeveranceFundId { get; set; }
    public int SenaId { get; set; }
    public int IcbfId { get; set; }
    public int FamilySubsidyId { get; set; }

    [MaxLength(1)]
    public string? PensionFundMember { get; set; }

    // === BANCA NOMINA (decision P5a) ===

    [MaxLength(4)]
    public string? PayrollBankId { get; set; }

    public int PayrollBankAccountType { get; set; }

    [MaxLength(25)]
    public string? PayrollBankAccountNumber { get; set; }

    // === BONIFICACIONES Y PROVISIONES ===

    public decimal RepresentationExpense { get; set; }
    public decimal TechnicalBonus { get; set; }
    public decimal OtherBonus { get; set; }

    public DateTime SeveranceCauseDate { get; set; }
    public decimal BonusDays { get; set; }
    public decimal VacationDays { get; set; }
    public decimal IndemnityDays { get; set; }
    public decimal SeveranceAvgDays { get; set; }
    public decimal BonusAvgDays { get; set; }
    public decimal IndemnityAvgDays { get; set; }
    public int HolidayDays { get; set; }
    public DateTime LicenseExpiryDate { get; set; }
    public decimal VacationAvgDays { get; set; }
    public DateTime VacationCauseDate { get; set; }
    public DateTime BonusCauseDate { get; set; }

    public decimal SeveranceDaysCalc { get; set; }
    public decimal IndemnityDaysCalc { get; set; }

    [MaxLength(1)]
    public string? IsLiquidated { get; set; }

    public DateTime? LiquidationDate { get; set; }

    [MaxLength(1)]
    public string? SpecialRegime { get; set; }

    [MaxLength(1)]
    public string? ExtraBonusFlag { get; set; }

    // === LEGACY ===

    public int? LegacyIdNomina { get; set; }
    public long? LegacyIdEmpleado { get; set; }

    // === Navigation ===

    public Person Person { get; set; } = null!;
}
