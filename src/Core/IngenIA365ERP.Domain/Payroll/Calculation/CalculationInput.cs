using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Todo lo que el motor necesita para liquidar a UN empleado en UN período. No hay
/// nada más: ni base de datos, ni reloj, ni configuración. Dos entradas iguales dan
/// dos resultados iguales (FR-014), y el hash de la entrada lo demuestra.
/// </summary>
public sealed class CalculationInput
{
    public required PeriodInput Period { get; init; }
    public required EmployeeInput Employee { get; init; }

    /// <summary>Novedades <c>Active</c> del empleado en el período.</summary>
    public IReadOnlyList<NoveltyInput> Novelties { get; init; } = [];

    /// <summary>Versiones de concepto vigentes a <c>Period.EndDate</c> (una por código).</summary>
    public required IReadOnlyList<PayrollConceptDefinition> Concepts { get; init; }

    /// <summary>Parámetros legales vigentes a <c>Period.EndDate</c> (una vigencia por código), con sus rangos.</summary>
    public required IReadOnlyList<PayrollLegalParameter> Parameters { get; init; }

    public CalculationPolicies Policies { get; init; } = new();
}

/// <summary>El período: sus fechas y los días que paga según la periodicidad del plan (FR-008).</summary>
public sealed record PeriodInput(DateTime StartDate, DateTime EndDate, PayrollPeriodicity Periodicity)
{
    public int DaysInPeriod => (int)Periodicity;
}

/// <summary>Decisiones de la cooperativa (D-09), no valores legales.</summary>
public sealed record CalculationPolicies
{
    public PayrollRounding Rounding { get; init; } = PayrollRounding.Peso;

    /// <summary>
    /// Si el empleador goza de la exoneración de salud, SENA e ICBF (Ley 1607 de 2012,
    /// art. 114-1 E.T.) para empleados por debajo del tope parametrizado. Depende del
    /// régimen tributario del empleador; una cooperativa del régimen especial
    /// normalmente NO. Por eso es política, no parámetro legal.
    /// </summary>
    public bool ApplyEmployerExemption { get; init; }

    /// <summary>
    /// Feature 010 (R6, D-01): si el aporte a riesgos laborales se causa también sobre los
    /// días de vacaciones que la liquidación ya pagó (<c>AUSENCIA_VACACIONES</c>). Los
    /// operadores no cotizan ARL durante el descanso (Decreto 1772/1994 art. 19) y Mintrabajo
    /// opina lo contrario, así que lo decide la cooperativa (<c>CotizaArlEnVacaciones</c>,
    /// «no» por defecto). Salud, pensión y parafiscales se causan completos en todo caso.
    /// </summary>
    public bool CotizaArlEnVacaciones { get; init; }
}

public sealed record EmployeeInput
{
    public required Guid PublicId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public EmployeeClass Class { get; init; } = EmployeeClass.Standard;
    public required DateTime JoinDate { get; init; }
    public DateTime? TerminationDate { get; init; }

    /// <summary>
    /// Historial de salario por fecha de efecto. Debe contener el salario vigente al
    /// inicio del período (la fecha de efecto puede ser anterior). El motor arma los
    /// tramos con los cambios que caen dentro del período.
    /// </summary>
    public required IReadOnlyList<SalaryChangeInput> SalaryHistory { get; init; }

    public AffiliationsInput Affiliations { get; init; } = new();

    /// <summary>1 ó 2 (FR-039).</summary>
    public byte WithholdingProcedure { get; init; } = 1;

    /// <summary>Procedimiento 2: porcentaje vigente a la fecha del período; nulo = no hay vigencia.</summary>
    public decimal? WithholdingRatePercent { get; init; }

    public IReadOnlyList<TaxDeductionInput> TaxDeductions { get; init; } = [];
}

public sealed record SalaryChangeInput(DateTime EffectiveDate, decimal MonthlySalary);

/// <summary>Afiliaciones registradas en la ficha. Sin ellas los aportes no tienen destinatario (edge case de la spec).</summary>
public sealed record AffiliationsInput
{
    public bool Health { get; init; }
    public bool Pension { get; init; }
    /// <summary>Clase de riesgo ARL 1..5; nula = sin afiliación registrada.</summary>
    public int? WorkRiskClass { get; init; }
    public bool FamilyCompensation { get; init; }
}

public sealed record TaxDeductionInput(TaxDeductionKind Kind, decimal? MonthlyAmount, decimal? Percent);

/// <summary>Una novedad activa. Los días dentro del período los recalcula el motor a partir de las fechas.</summary>
public sealed record NoveltyInput
{
    public required Guid PublicId { get; init; }
    public required string ConceptCode { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? Amount { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public NoveltyOrigin Origin { get; init; } = NoveltyOrigin.Manual;
    public string? Description { get; init; }
}
