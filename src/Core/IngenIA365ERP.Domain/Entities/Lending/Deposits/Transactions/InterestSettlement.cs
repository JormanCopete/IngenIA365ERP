using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;

/// <summary>
/// <c>LND_InterestSettlements</c> (feature 014, US5, T130; data-model §4.5; FR-061, FR-063, FR-064, SC-003, SC-016): la liquidación de intereses de una
/// cuenta y un período —lo causado, lo que se abona redondeado y la diferencia de redondeo ajustada en la misma operación, la retención con su umbral y su
/// componente inflacionario, el neto y su destino—. Es <b>idempotente por cuenta y período</b>: el único <c>(MemberAccountId, PeriodTo)</c> sobre las vivas
/// hace que volver a liquidar responda con la existente. Hecho de sólo inserción que escribe sólo <c>RegistroDeCuentas</c>; reversarla es reversar su
/// operación y anotar aquí el reverso (escritura única). La explicación día por día no se guarda: la da la reconstrucción (FR-011). (nuevo)
/// </summary>
public class InterestSettlement : AuditableEntity, IHechoInmutable
{
    /// <summary>Largo del motivo de exención de la retención (<c>nvarchar(30)</c>).</summary>
    public const int LargoDelMotivo = 30;

    public int MemberAccountId { get; init; }

    /// <summary>La operación <c>InterestSettlement</c> (o la de cancelación) que lleva las filas de la liquidación.</summary>
    public long OperationId { get; init; }

    /// <summary>Navegación para enlazar en el mismo guardado con la operación todavía sin Id.</summary>
    public MemberAccountOperation? Operation { get; init; }

    public DateOnly PeriodFrom { get; init; }

    public DateOnly PeriodTo { get; init; }

    /// <summary>El período del plan del CDAT (K9); nulo en las demás clases.</summary>
    public int? SchedulePeriodNumber { get; init; }

    /// <summary>Σ de lo causado del período (6 decimales).</summary>
    public decimal AccruedInterest { get; init; }

    /// <summary>Lo que se abona, redondeado a la unidad de la versión.</summary>
    public decimal GrossInterest { get; init; }

    /// <summary><c>GrossInterest − AccruedInterest</c>, ajustado en la misma operación (D9).</summary>
    public decimal RoundingDifference { get; init; }

    /// <summary>¿Algún día del período alcanzó el umbral diario de la retención? (R06).</summary>
    public bool ThresholdReached { get; init; }

    /// <summary><c>Retencion.UmbralDiarioUvt × UVT</c> del período; nulo si el umbral no alcanza a la clase.</summary>
    public decimal? ThresholdDailyAmount { get; init; }

    public decimal? InflationaryComponentFraction { get; init; }

    public decimal? InflationaryComponentAmount { get; init; }

    public decimal WithholdingBase { get; init; }

    public decimal WithholdingRate { get; init; }

    public decimal WithholdingAmount { get; init; }

    /// <summary><c>NonTaxpayer</c>, <c>SelfWithholder</c>, <c>BelowThreshold</c>, <c>ZeroRate</c>, <c>NoInterest</c> (nulo si se retuvo).</summary>
    public string? WithholdingExemptionReason { get; init; }

    /// <summary><c>GrossInterest − WithholdingAmount</c>.</summary>
    public decimal NetInterest { get; init; }

    public InterestDestination Destination { get; init; } = InterestDestination.Capitalize;

    /// <summary>El perfil tributario leído del titular y la política de retención vigente (JSON).</summary>
    public string TaxProfileSnapshotJson { get; init; } = "{}";

    /// <summary>El reverso de la operación que la anuló; se escribe una vez.</summary>
    [EscrituraUnica]
    public long? ReversedByOperationId { get; set; }

    public string InputsSha256 { get; init; } = string.Empty;
}
