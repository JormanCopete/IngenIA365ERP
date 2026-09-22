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
    /// <summary>Fila de <c>PAY_SeveranceProviders</c>. Era decimal(6,0) heredado del SOLIDO; entero desde el 2026-09-12.</summary>
    public int SeveranceFundId { get; set; }
    public int SenaId { get; set; }
    public int IcbfId { get; set; }
    public int FamilySubsidyId { get; set; }

    [MaxLength(1)]
    public string? PensionFundMember { get; set; }

    // === BANCA NOMINA (decision P5a) ===

    /// <summary>Código del banco en el legado (nvarchar(4)). Se conserva; la dispersión usa <see cref="DisbursementBankId"/>.</summary>
    [MaxLength(4)]
    public string? PayrollBankId { get; set; }

    /// <summary>1 ahorros, 2 corriente. Sin cambio de columna; <see cref="BankAccountType"/> es su nombre en Domain (feature 010).</summary>
    public int PayrollBankAccountType { get; set; }

    /// <summary>Alias de <see cref="PayrollBankAccountType"/> (data-model 010 §1.4). No se mapea: misma columna.</summary>
    public int BankAccountType
    {
        get => PayrollBankAccountType;
        set => PayrollBankAccountType = value;
    }

    /// <summary>«Empleado sin cuenta» = NULL o vacío: queda en pendientes de la dispersión.</summary>
    [MaxLength(25)]
    public string? PayrollBankAccountNumber { get; set; }

    /// <summary>
    /// Feature 010 (R11): banco destino de la dispersión, FK a <c>COR_Banks</c> (código ACH en
    /// <c>Bank.TransferCode</c>). Reemplaza al uso de <see cref="PayrollBankId"/>; la migración lo
    /// rellena por dato con <c>COR_Banks.LegacyCode = PayrollBankId</c>.
    /// </summary>
    public int? DisbursementBankId { get; set; }

    // === PILA Y NOMINA ELECTRONICA (feature 010, data-model §1.4) ===
    // Sólo lo laboral (Principio V): la identificación y el domicilio siguen en Person.

    /// <summary>
    /// Tipo de cotizante PILA (campo 5) y <c>TipoTrabajador</c> DIAN (D-05: la DIAN adoptó los
    /// mismos códigos). Vacío = lo deriva la regla paramétrica desde <see cref="EmployeeClass"/> y
    /// <see cref="ApprenticeStage"/>.
    /// </summary>
    [MaxLength(2)]
    public string? PilaContributorType { get; set; }

    /// <summary>Subtipo de cotizante PILA (campo 6) y <c>SubTipoTrabajador</c> DIAN.</summary>
    [MaxLength(2)]
    public string? PilaContributorSubType { get; set; }

    /// <summary>PILA campo 79; DIAN <c>AltoRiesgoPension</c>.</summary>
    public bool HighRiskPension { get; set; }

    /// <summary>DIAN <c>TipoContrato</c>. El <see cref="ContractType"/> heredado es otro código y no se reinterpreta.</summary>
    public Enums.Payroll.DianContractType? DianContractType { get; set; }

    /// <summary>
    /// DIAN <c>Pago/Metodo</c> (tabla 5.3.3.2). Vacío = se deriva de <see cref="PaymentMethod"/>
    /// por la política <c>DianMedioPagoMapa</c>; si viene, manda.
    /// </summary>
    [MaxLength(3)]
    public string? DianPaymentMethodCode { get; set; }

    /// <summary>Etapa del aprendiz (Ley 2466 de 2025). Obligatoria si la clase es aprendiz o pasante.</summary>
    public Enums.Payroll.ApprenticeStage? ApprenticeStage { get; set; }

    /// <summary>Régimen de transición de la Ley 2381 de 2024. <c>Unknown</c> es alerta PILA desde abril de 2027.</summary>
    public Enums.Payroll.PensionTransitionRegime PensionTransitionRegime { get; set; } = Enums.Payroll.PensionTransitionRegime.Unknown;

    /// <summary>PILA campo 8: extranjero no obligado a cotizar a pensión.</summary>
    public bool ForeignNotRequiredToContributePension { get; set; }

    /// <summary>PILA campo 9: colombiano en el exterior.</summary>
    public bool ColombianAbroad { get; set; }

    /// <summary>
    /// Municipio DANE del lugar de trabajo (departamento = 2 primeros dígitos, municipio = 3
    /// últimos): PILA campos 9-10 y DIAN <c>LugarTrabajo</c>. Vacío = el de la empresa.
    /// </summary>
    [MaxLength(5)]
    public string? WorkMunicipalityDaneCode { get; set; }

    /// <summary>DIAN <c>LugarTrabajoDireccion</c>. Vacío = dirección de la empresa.</summary>
    [MaxLength(120)]
    public string? WorkAddress { get; set; }

    /// <summary>PILA campo 98 (Decreto 768 de 2022). Vacío = el de la empresa.</summary>
    [MaxLength(7)]
    public string? EconomicActivityCode { get; set; }

    /// <summary>PILA campo 62.</summary>
    [MaxLength(9)]
    public string? WorkCenterCode { get; set; }

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
