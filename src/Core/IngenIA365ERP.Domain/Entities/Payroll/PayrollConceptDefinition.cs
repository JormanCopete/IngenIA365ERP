using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_ConceptDefinitions]. UNA FILA POR VERSIÓN: <see cref="Code"/> es
/// estable entre versiones y <see cref="ValidFrom"/>/<see cref="ValidTo"/> delimitan
/// cada una (FR-029). Modificar un concepto crea una versión nueva y cierra la
/// anterior; las liquidaciones aprobadas referencian la versión con la que se
/// calcularon y no cambian.
///
/// <para>
/// Es la definición nueva de la feature 005; el catálogo heredado
/// (<see cref="PayrollConcept"/>) queda de sólo consulta y no participa en ninguna
/// liquidación (spec, decisión Q2).
/// </para>
/// </summary>
public class PayrollConceptDefinition : AuditableEntity
{
    [MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public ConceptNature Nature { get; set; }

    public CalculationKind CalculationKind { get; set; }

    // --- Parámetros de la forma de cálculo (los que apliquen a CalculationKind) ---

    /// <summary>FixedAmount: valor fijo de la definición…</summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>…o código de parámetro legal que aporta el valor con vigencia (auxilio de transporte). Manda sobre <see cref="FixedAmount"/>.</summary>
    [MaxLength(40)]
    public string? AmountParameterCode { get; set; }

    /// <summary>FixedAmount: el valor mensual se proporciona a los días liquidados (auxilio de transporte sí; una bonificación fija no).</summary>
    public bool ProrateByDays { get; set; }

    /// <summary>PercentOfBase y RangeTable: sobre qué base.</summary>
    public CalculationBase? BaseKind { get; set; }

    /// <summary>PercentOfBase: porcentaje fijo de la definición…</summary>
    public decimal? Percent { get; set; }

    /// <summary>…o código de parámetro legal que lo aporta con vigencia (manda sobre <see cref="Percent"/>).</summary>
    [MaxLength(40)]
    public string? PercentParameterCode { get; set; }

    /// <summary>QuantityTimesUnit.</summary>
    public UnitKind? UnitKind { get; set; }

    /// <summary>QuantityTimesUnit: factor de recargo sobre la unidad (1.25, 1.75, 2.0…).</summary>
    public decimal? UnitFactor { get; set; }

    /// <summary>RangeTable: código del parámetro legal de tipo tabla.</summary>
    [MaxLength(40)]
    public string? TableParameterCode { get; set; }

    /// <summary>CompositeOfConcepts: «+HEX_DIURNA*1;+COMISION*1» (signo, código, peso).</summary>
    [MaxLength(400)]
    public string? ComponentConceptCodes { get; set; }

    // --- Qué bases alimenta ---

    public bool AffectsSalaryBase { get; set; }
    public bool AffectsContributionBase { get; set; }
    public bool AffectsBenefitsBase { get; set; }
    public bool AffectsWithholdingBase { get; set; }

    /// <summary>Prestacional.</summary>
    public bool IsBenefitRelated { get; set; }

    /// <summary>
    /// Feature 010 (R6): entra a la base de vacaciones e indemnización (CST art. 192: salario
    /// ordinario sin auxilio, sin extras, sin trabajo en descanso obligatorio). La semilla lo pone
    /// en su sitio en las versiones sembradas; no altera ningún valor de la nómina ordinaria.
    /// </summary>
    public bool AffectsVacationBase { get; set; }

    /// <summary>
    /// Feature 010 (R10): ruta del concepto en el XML de nómina electrónica (anexo 3.1):
    /// <c>Devengados/Basico</c>, <c>Deducciones/Salud</c>… Nulo = el concepto no va al documento
    /// (provisiones, aportes del empleador, informativos). Un concepto con valor y sin ruta es
    /// inconsistencia bloqueante al generar.
    /// </summary>
    [MaxLength(60)]
    public string? DianElement { get; set; }

    // --- Reglas de la novedad ---

    public bool AllowsRepeatInPeriod { get; set; }
    public decimal? MaxQuantity { get; set; }
    public decimal? MaxAmount { get; set; }

    /// <summary>Máscara de <see cref="EmployeeClass"/> (bit = 1 &lt;&lt; clase). 0 = todas.</summary>
    public int ApplicableClasses { get; set; }

    public bool RequiresDates { get; set; }
    public bool RequiresQuantity { get; set; }
    public bool RequiresAmount { get; set; }

    /// <summary>Lo genera el motor sin novedad (salario, auxilio, aportes, provisiones).</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>
    /// La novedad de este concepto resta días al salario (incapacidad, vacaciones,
    /// licencias). El motor liquida el salario por los días del período menos estos.
    /// </summary>
    public bool ReducesWorkedDays { get; set; }

    // --- Origen y vigencia ---

    public ConceptOrigin Origin { get; set; } = ConceptOrigin.Custom;

    /// <summary>Referencia al concepto heredado del que se tradujo a mano, si aplica.</summary>
    public int? LegacyConceptId { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;

    public bool AppliesTo(EmployeeClass employeeClass) =>
        ApplicableClasses == 0 || (ApplicableClasses & (1 << (int)employeeClass)) != 0;

    public bool IsValidAt(DateTime date) =>
        IsActive && ValidFrom <= date && (ValidTo is null || ValidTo >= date);
}
