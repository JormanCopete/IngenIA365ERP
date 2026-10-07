using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Schedules;

/// <summary>
/// <c>LND_RepaymentInstallments</c> (feature 013; data-model §4.5; FR-023, FR-069, FR-070): una cuota de una versión del plan. Hecho
/// de sólo inserción (<see cref="IHechoInmutable"/>) y <c>bigint</c> (<see cref="AuditableEntityLong"/>). No lleva estado de pago:
/// pagada, vencida, cubierta por anticipo o por vencer se calcula al leer (FR-069).
/// </summary>
public class RepaymentInstallment : AuditableEntityLong, IHechoInmutable
{
    public int ScheduleVersionId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>RepaymentScheduleVersion</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Schedules.RepaymentScheduleVersion? ScheduleVersion { get; init; }

    public int InstallmentNumber { get; init; }

    public DateOnly PeriodStart { get; init; }

    public DateOnly DueDate { get; init; }

    /// <summary>El siguiente hábil si vence en día no hábil (art. 829, <c>CalendarioHabil</c>; FR-070).</summary>
    public DateOnly ExigibleDate { get; init; }

    public decimal OpeningPrincipal { get; init; }

    public decimal Principal { get; init; }

    /// <summary>El interés causado entre vencimientos si se paga a tiempo (T25).</summary>
    public decimal Interest { get; init; }

    public decimal GraceInterest { get; init; }

    public decimal Insurance { get; init; }

    public decimal OtherCharges { get; init; }

    public decimal Total { get; init; }

    public decimal ClosingPrincipal { get; init; }

    public bool IsExtraordinary { get; init; }

    public bool IsGraceInstallment { get; init; }
}
