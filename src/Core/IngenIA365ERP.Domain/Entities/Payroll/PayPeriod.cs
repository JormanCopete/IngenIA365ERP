using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_PayPeriods] (nom_perpagos).
///
/// <para>
/// <see cref="PlanId"/> es el número de <b>planilla</b> del legado, no un plan de
/// nómina: el plan de la feature 005 va en <see cref="PayrollPlanId"/> (hallazgo H-1
/// de research.md). Dos períodos del mismo plan no se superponen.
/// </para>
/// </summary>
public class PayPeriod : AuditableEntity
{
    /// <summary>Número de planilla (legado). No reinterpretar.</summary>
    public int PlanId { get; set; }

    public int PayrollCompanyId { get; set; }

    /// <summary>Plan de nómina al que pertenece el período (feature 005).</summary>
    public int PayrollPlanId { get; set; }

    public PayrollPlan? PayrollPlan { get; set; }

    [MaxLength(100)]
    public string? Description { get; set; }

    [MaxLength(40)]
    public string? PayDate { get; set; }

    [MaxLength(4)]
    public string? LiquidationCompanyId { get; set; }

    public int? CycleMonth { get; set; }
    public int? CycleHours { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? Periodicity { get; set; }
    public int? AdditionalConcept1 { get; set; }
    public int? AdditionalConcept2 { get; set; }
    public int? AdditionalConcept3 { get; set; }
    public int? AdditionalConcept4 { get; set; }

    [MaxLength(1)]
    public string? OnlyEntries { get; set; }

    [MaxLength(1)]
    public string? NoAutoSalaryLiq { get; set; }

    [MaxLength(1)]
    public string? NoAbsenceLiq { get; set; }

    [MaxLength(1)]
    public string? NoDirectDebitLiq { get; set; }

    /// <summary>
    /// Open → Calculated → Approved → Reversed → Open. Era un <c>int</c> libre; el
    /// valor 1 («liquidado» del cálculo preliminar retirado) se reinterpreta como
    /// «calculado (borrador)» porque ninguna nómina real lo usó.
    /// </summary>
    public PayPeriodStatus Status { get; set; } = PayPeriodStatus.Open;

    [MaxLength(100)]
    public string StatusMessage { get; set; } = string.Empty;

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(100)]
    public string? ApprovedBy { get; set; }

    /// <summary>Corrida vigente (borrador o aprobada) del período, si existe.</summary>
    public Guid? RunPublicId { get; set; }

    public int PeriodId { get; set; }

    [MaxLength(1)]
    public string AdvanceLiquidation { get; set; } = string.Empty;

    [MaxLength(1)]
    public string AdvanceCrossing { get; set; } = string.Empty;

    /// <summary>Largo de la columna legada <c>StatusMessage</c> (SOLIDO): varchar(100).</summary>
    public const int StatusMessageMaxLength = 100;

    /// <summary>
    /// Deja el mensaje de estado en lo que cabe en la columna. Un correo largo como usuario más
    /// la fecha ya se acercan al límite, y PostgreSQL rechaza el UPDATE entero (22001). Lo
    /// que se recorta es texto de cortesía: quién y cuándo también están en las columnas
    /// propias (ApprovedBy, ApprovedAt, la corrida).
    /// </summary>
    public static string Mensaje(string texto) =>
        texto.Length <= StatusMessageMaxLength ? texto : texto[..(StatusMessageMaxLength - 1)] + "…";
}
