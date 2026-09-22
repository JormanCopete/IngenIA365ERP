using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_EmployeeBenefitOpeningBalances]. Saldo inicial de prestaciones de un
/// empleado a una fecha de corte (feature 010, R3, FR-007): lo que la cooperativa traía causado
/// antes de que la nómina corriera aquí (COOFLOPAL arranca el 01-12-2026 y la prima de
/// diciembre y las cesantías de 2026 saldrían cortas sin esto). Digitación auditada, no
/// migración: la provisión por empleado no existe en el libro.
///
/// <para>
/// Se edita sólo mientras ninguna liquidación aprobada lo usa; después, corregir es una fila
/// <see cref="OpeningBalanceKind.Adjustment"/> con motivo que apunta a ésta. <see cref="ConsumedByRunId"/>
/// señala a la liquidación aprobada más antigua que lo usa; al reversarla pasa a la siguiente que
/// también lo usó (toda liquidación especial aprobada del empleado con corte igual o posterior al
/// saldo lo lee), y sólo vuelve a nulo cuando no queda ninguna. El motor lo recibe como
/// «tramo inicial» y lo explica como paso propio («Saldo inicial al … digitado por … el …»).
/// </para>
/// </summary>
public class EmployeeBenefitOpeningBalance : AuditableEntity
{
    public int EmployeeId { get; set; }

    /// <summary>Fecha de corte del saldo (30-11-2026 en COOFLOPAL).</summary>
    public DateOnly AsOfDate { get; set; }

    public OpeningBalanceKind Kind { get; set; } = OpeningBalanceKind.Opening;

    /// <summary>Días hábiles de vacaciones pendientes.</summary>
    public decimal PendingVacationDays { get; set; }

    /// <summary>Cesantías causadas del año a <see cref="AsOfDate"/>.</summary>
    public decimal AccruedSeverance { get; set; }

    public decimal AccruedSeveranceInterest { get; set; }

    /// <summary>Prima causada del semestre a <see cref="AsOfDate"/>.</summary>
    public decimal AccruedServiceBonus { get; set; }

    /// <summary>Días ya contados en la prima, para que la proporción no los duplique.</summary>
    public int? ServiceBonusDaysAccrued { get; set; }

    /// <summary>Días ya contados en las cesantías.</summary>
    public int? SeveranceDaysAccrued { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Sólo <c>Adjustment</c>: la fila que corrige.</summary>
    public int? AdjustsBalanceId { get; set; }

    /// <summary>Obligatorio en <c>Adjustment</c>.</summary>
    [MaxLength(300)]
    public string? AdjustmentReason { get; set; }

    /// <summary>
    /// La liquidación <b>aprobada</b> más antigua que lo usa; lo escribe la aprobación y la reversión lo pasa a la
    /// siguiente que también lo usó, o lo limpia si no queda ninguna (revisión N1 de la feature 010: hasta entonces
    /// sólo la primera dejaba rastro, y reversarla liberaba el saldo aunque otra liquidación aprobada lo hubiera leído).
    /// </summary>
    public int? ConsumedByRunId { get; set; }

    public Employee? Employee { get; set; }
    public EmployeeBenefitOpeningBalance? AdjustsBalance { get; set; }
    public PayrollRun? ConsumedByRun { get; set; }

    public bool EsEditable => ConsumedByRunId is null;
}
