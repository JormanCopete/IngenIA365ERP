using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Domain.Payroll.Settlements;

/// <summary>
/// Qué liquidación especial se calcula (feature 010, FR-001). Coincide en valores con el
/// <c>PayrollRunKind</c> de la corrida (1..4; el 0 es la ordinaria, que no pasa por aquí).
/// </summary>
public enum SettlementKind
{
    /// <summary>Prima de servicios del semestre (CST art. 306).</summary>
    ServiceBonus = 1,

    /// <summary>Cesantías e intereses del año, a 31 de diciembre (CST art. 249; Ley 50/1990 art. 99).</summary>
    Severance = 2,

    /// <summary>Un disfrute o una compensación de vacaciones (CST arts. 186-189).</summary>
    Vacation = 3,

    /// <summary>Liquidación definitiva por retiro (CST arts. 64, 65, 249, 253, 306; Ley 52/1975).</summary>
    Settlement = 4,
}

// Etapa de aprendiz, tipo de contrato DIAN y clase de movimiento de vacaciones son los enums
// comunes de Domain/Enums/Payroll (ApprenticeStage, DianContractType, VacationMovementKind): la
// misma numeración que las columnas de la ficha y de PAY_VacationMovements.

/// <summary>
/// Todo lo que el motor de liquidaciones necesita para liquidar a UN empleado en UN corte.
/// No hay nada más: ni base de datos, ni reloj, ni configuración (research R1). El
/// cargador de Application arma esto desde la ficha, las corridas aprobadas, los saldos
/// iniciales, los movimientos de vacaciones, la terminación y Cartera; el motor sólo
/// calcula y explica. Dos entradas iguales dan dos resultados iguales y el hash lo demuestra.
/// </summary>
public sealed class SettlementInput
{
    public required SettlementKind Kind { get; init; }

    /// <summary>
    /// Fecha de corte: 30-06 ó 31-12 para la prima, 31-12 para las cesantías, el fin del
    /// disfrute para las vacaciones, la fecha de retiro para la definitiva. Los parámetros y
    /// conceptos se resuelven a esta fecha y el comprobante contable la lleva (D-04).
    /// </summary>
    public required DateTime CutoffDate { get; init; }

    /// <summary>
    /// Inicio del período que se liquida: el semestre en la prima, el año en las cesantías. En
    /// vacaciones y definitiva no aplica (el período es desde el ingreso, o desde el saldo inicial).
    /// </summary>
    public DateTime? PeriodStart { get; init; }

    public required SettlementEmployeeInput Employee { get; init; }

    /// <summary>Ausencias y suspensiones del contrato con fechas; el motor recorta las que caen en cada período.</summary>
    public IReadOnlyList<AbsenceInput> Absences { get; init; } = [];

    /// <summary>Saldo inicial de prestaciones a la fecha de arranque (FR-007); nulo = no hay.</summary>
    public OpeningBalanceInput? OpeningBalance { get; init; }

    /// <summary>Movimientos de vacaciones ya registrados (disfrutes, compensaciones, ajustes), sin el que se está liquidando.</summary>
    public IReadOnlyList<VacationMovementInput> VacationMovements { get; init; } = [];

    /// <summary>El disfrute o la compensación que esta liquidación paga (sólo <see cref="SettlementKind.Vacation"/>).</summary>
    public VacationMovementInput? MovementToSettle { get; init; }

    /// <summary>
    /// Devengos variables por mes tomados de las corridas aprobadas (comisiones, extras,
    /// recargos: todo lo prestacional distinto de salario y auxilio), para promediar la base
    /// cuando el salario es variable (CST arts. 192 y 253) y para el ingreso mensual que las
    /// reglas de retención comparan con los topes en UVT.
    /// </summary>
    public IReadOnlyList<MonthlyBaseInput> MonthlyBases { get; init; } = [];

    /// <summary>Provisión acumulada por concepto de provisión (<c>PROV_PRIMA</c>, <c>PROV_CESANTIAS</c>…) para este empleado, menos lo ya consumido.</summary>
    public IReadOnlyList<ProvisionBalanceInput> Provisions { get; init; } = [];

    /// <summary>La terminación del contrato (sólo <see cref="SettlementKind.Settlement"/>).</summary>
    public TerminationInput? Termination { get; init; }

    /// <summary>Descuentos propuestos desde Cartera y libranzas, ya validados por la responsable (FR-018a).</summary>
    public IReadOnlyList<ProposedDeductionInput> ProposedDeductions { get; init; } = [];

    /// <summary>Prima ya pagada al empleado en una definitiva aprobada del mismo semestre (FR-009).</summary>
    public IReadOnlyList<PaidServiceBonusInput> ServiceBonusPaidInSettlements { get; init; } = [];

    /// <summary>Días del último período ordinario que la definitiva paga como salario (el período abierto donde cae el retiro).</summary>
    public PendingSalaryInput? PendingSalary { get; init; }

    /// <summary>Lo consumido en el año de los cupos anuales de retención, cuando la política es «acumulado».</summary>
    public AcumuladoAnualDeRetencion? WithholdingYearToDate { get; init; }

    public SettlementPolicies Policies { get; init; } = new();

    /// <summary>Parámetros legales vigentes al corte (una vigencia por código), con sus rangos.</summary>
    public required IReadOnlyList<PayrollLegalParameter> Parameters { get; init; }

    /// <summary>Versiones de concepto vigentes al corte (una por código).</summary>
    public required IReadOnlyList<PayrollConceptDefinition> Concepts { get; init; }
}

/// <summary>El empleado: lo laboral de la ficha que las prestaciones necesitan.</summary>
public sealed record SettlementEmployeeInput
{
    public required Guid PublicId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public EmployeeClass Class { get; init; } = EmployeeClass.Standard;
    public required DateTime JoinDate { get; init; }

    /// <summary>Fecha de retiro cuando ya está registrada (en la definitiva es el corte).</summary>
    public DateTime? TerminationDate { get; init; }

    /// <summary>Historial de salario por fecha de efecto; debe contener el salario vigente al inicio del período.</summary>
    public required IReadOnlyList<SalaryChangeInput> SalaryHistory { get; init; }

    /// <summary>Etapa del aprendiz y desde cuándo está en ella (Ley 2466/2025). Sólo <see cref="EmployeeClass.Apprentice"/>.</summary>
    public ApprenticeStage? ApprenticeStage { get; init; }
    public DateTime? ApprenticeStageFrom { get; init; }

    /// <summary>
    /// Si la ficha dice que devenga auxilio de transporte (un trabajador remoto puede no
    /// devengarlo). El tope de salario en SMMLV se comprueba aparte, por tramo, con el parámetro.
    /// </summary>
    public bool TransportAllowanceEntitled { get; init; } = true;

    public AffiliationsInput Affiliations { get; init; } = new();

    /// <summary>1 ó 2 (FR-039).</summary>
    public byte WithholdingProcedure { get; init; } = 1;

    /// <summary>Procedimiento 2: porcentaje vigente al corte; nulo = no hay vigencia.</summary>
    public decimal? WithholdingRatePercent { get; init; }

    public IReadOnlyList<TaxDeductionInput> TaxDeductions { get; init; } = [];

    public DianContractType ContractType { get; init; } = DianContractType.Indefinite;

    /// <summary>Término fijo u obra: hasta cuándo iba el contrato («el tiempo que faltare», CST art. 64).</summary>
    public DateTime? ContractEndDate { get; init; }
}

/// <summary>Una ausencia con fechas. Las suspensiones del contrato (CST art. 51) descuentan vacaciones y cesantías (art. 53); las demás sólo el salario.</summary>
public sealed record AbsenceInput(DateTime From, DateTime To, bool IsSuspension, string? Description = null);

/// <summary>Saldo inicial de prestaciones a la fecha de arranque (R3, FR-007), digitado y auditado.</summary>
public sealed record OpeningBalanceInput
{
    public required DateTime AsOfDate { get; init; }
    public decimal PendingVacationDays { get; init; }
    public decimal AccruedSeverance { get; init; }
    public decimal AccruedSeveranceInterest { get; init; }
    public decimal AccruedServiceBonus { get; init; }

    /// <summary>Días ya contados en el saldo, para que la proporción no los duplique. Nulos = se cuentan desde el inicio del período hasta <see cref="AsOfDate"/>.</summary>
    public int? ServiceBonusDaysAccrued { get; init; }
    public int? SeveranceDaysAccrued { get; init; }

    public string EnteredBy { get; init; } = string.Empty;
    public DateTime? EnteredAt { get; init; }
}

/// <summary>Un movimiento de vacaciones. <see cref="BusinessDays"/> con signo en los ajustes.</summary>
public sealed record VacationMovementInput
{
    public Guid? PublicId { get; init; }
    public required VacationMovementKind Kind { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal BusinessDays { get; init; }
    public int CalendarDays { get; init; }
    public bool IsCancelled { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Un mes de corridas aprobadas: los devengos variables prestacionales y los de la base de
/// vacaciones (sin salario ni auxilio), y el ingreso laboral del mes para los topes de
/// retención. Los ceros valen: un mes sin variables cuenta en el promedio.
/// </summary>
public sealed record MonthlyBaseInput(int Year, int Month, decimal VariableBenefitsEarnings, decimal VariableVacationEarnings, decimal LaborIncome);

/// <summary>Provisión acumulada de un concepto de provisión para el empleado (sumada de las corridas aprobadas, menos lo consumido).</summary>
public sealed record ProvisionBalanceInput(string ProvisionConceptCode, decimal Accrued);

/// <summary>La terminación registrada (R7).</summary>
public sealed record TerminationInput
{
    public required string ReasonCode { get; init; }
    public string ReasonName { get; init; } = string.Empty;

    /// <summary>La marca del catálogo: el motivo genera indemnización (despido sin justa causa, CST art. 64).</summary>
    public bool GeneratesSeverancePay { get; init; }

    /// <summary>Bonificación por retiro voluntario pactada, si la hay (mismo tratamiento tributario que la indemnización).</summary>
    public decimal? VoluntaryRetirementBonus { get; init; }
}

/// <summary>Un descuento propuesto y validado (préstamo de la cooperativa, libranza, otro), con el valor que la responsable aprobó.</summary>
public sealed record ProposedDeductionInput
{
    public Guid? PublicId { get; init; }
    public required string ConceptCode { get; init; }
    public required string Description { get; init; }
    public decimal ProposedAmount { get; init; }
    public decimal AppliedAmount { get; init; }

    /// <summary>Verdadero cuando otro módulo ya contabiliza el recaudo (Cartera, D-08): la línea no genera asiento.</summary>
    public bool AccountedByOtherModule { get; init; }
}

/// <summary>Prima pagada en una definitiva aprobada del mismo semestre (FR-009).</summary>
public sealed record PaidServiceBonusInput(Guid RunPublicId, DateTime PaidThrough, decimal Amount, int Days);

/// <summary>El período ordinario abierto donde cae el retiro: la definitiva paga los días desde su inicio hasta el retiro.</summary>
public sealed record PendingSalaryInput(DateTime PeriodStart, DateTime PeriodEnd);

/// <summary>
/// Decisiones de la cooperativa que el motor recibe ya leídas (políticas por empresa con
/// vigencia, research R4): ninguna es un valor legal. El cargador las arma desde
/// <c>PAY_CompanyPolicies</c> a la fecha de corte.
/// </summary>
public sealed record SettlementPolicies
{
    public PayrollRounding Rounding { get; init; } = PayrollRounding.Peso;
    public SemanaLaboral SemanaLaboral { get; init; } = SemanaLaboral.LunesASabado;

    /// <summary>D-01: la liquidación de vacaciones paga los días del disfrute; si es falso los paga la nómina ordinaria.</summary>
    public bool VacacionesPagoAnticipado { get; init; } = true;

    public ModoDeTopesAnuales RetefteTopesAnualesModo { get; init; } = ModoDeTopesAnuales.Mensualizado;

    /// <summary>Fecha del primer período de nómina de la cooperativa (<c>ArranqueNominaFecha</c>): quien ingresó antes necesita saldo inicial.</summary>
    public DateTime? PayrollStartDate { get; init; }
}
