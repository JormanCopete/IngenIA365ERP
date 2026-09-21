using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_VacationMovements]. Un movimiento de vacaciones (feature 010, R6,
/// FR-014 a FR-017): disfrute, compensación en dinero, ajuste con signo o el pago al retiro. La
/// causación no se guarda: se deriva (<c>días trabajados × VACACIONES_DIAS_ANIO / 360</c> menos
/// suspensiones), y el saldo tampoco (<c>causado + saldo inicial − Σ movimientos</c>).
///
/// <para>
/// En el disfrute los días hábiles los cuenta <c>DiasHabiles.Contar</c> con la semana laboral
/// vigente y quedan <b>congelados</b> aquí, con los domingos y festivos que saltó: si mañana
/// cambia la política o alguien decreta un puente, lo ya registrado no se mueve. Registrar el
/// disfrute o la compensación crea este movimiento y la corrida <c>Vacation</c> en borrador en la
/// misma acción; aprobarla lo deja <c>Liquidated</c>, reversarla lo devuelve a <c>Registered</c>.
/// </para>
/// </summary>
public class VacationMovement : AuditableEntity
{
    public int EmployeeId { get; set; }

    public VacationMovementKind Kind { get; set; }

    /// <summary>Obligatorias en <c>Enjoyment</c>; en <c>Compensation</c> <see cref="StartDate"/> es la fecha de la compensación.</summary>
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Días hábiles que consume; en <c>Adjustment</c> con signo.</summary>
    public decimal BusinessDays { get; set; }

    public int CalendarDays { get; set; }

    /// <summary>La <c>SemanaLaboral</c> vigente al registrar (<c>LunesASabado</c> / <c>LunesAViernes</c>).</summary>
    [MaxLength(20)]
    public string WeekPolicyUsed { get; set; } = string.Empty;

    /// <summary>JSON <c>[{ fecha, motivo }]</c> de los domingos y festivos saltados al contar.</summary>
    public string SkippedDaysJson { get; set; } = "[]";

    public VacationMovementStatus Status { get; set; } = VacationMovementStatus.Registered;

    /// <summary>
    /// La corrida <c>Vacation</c> (o <c>Settlement</c>) que lo liquidó. Sin navegación a propósito:
    /// <c>PayrollRun.VacationMovementId</c> apunta de vuelta y dos navegaciones cruzadas las
    /// emparejaría la convención de EF como uno a uno.
    /// </summary>
    public int? PayrollRunId { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    [MaxLength(300)]
    public string? CancelReason { get; set; }

    public Employee? Employee { get; set; }

    public bool EstaVivo => Status != VacationMovementStatus.Cancelled;
}
