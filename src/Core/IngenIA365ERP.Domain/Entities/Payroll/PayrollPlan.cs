using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_PayrollPlans]. Grupo de empleados que se liquida junto, con su
/// periodicidad y su calendario de períodos (spec, decisión Q1 de la clarificación).
/// Toda cooperativa tiene al menos uno —el plan por defecto que crea la semilla— y
/// cada empleado pertenece a exactamente uno. No confundir con
/// <see cref="PayPeriod.PlanId"/>, que en el legado es el número de planilla.
/// </summary>
public class PayrollPlan : AuditableEntity
{
    public const string DefaultCode = "DEFAULT";

    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public PayrollPeriodicity Periodicity { get; set; } = PayrollPeriodicity.Monthly;

    /// <summary>Exactamente uno por cooperativa. Con un solo plan, las pantallas no preguntan.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Días del período según la periodicidad (FR-008).</summary>
    public int DaysInPeriod => (int)Periodicity;
}
