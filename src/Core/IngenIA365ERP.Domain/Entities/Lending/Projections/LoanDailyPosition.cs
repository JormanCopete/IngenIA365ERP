using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Projections;

/// <summary>
/// <c>LND_LoanDailyPositions</c> (feature 013; data-model §6.1; FR-069, FR-133, I-6, D13): instantánea versionada por crédito y día.
/// Alimenta el tablero, los informes «a la fecha» y los listados; <b>nunca es la autoridad</b>. Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>, <c>bigint</c>): una operación con fecha valor anterior agrega una <see cref="Version"/> nueva por
/// día cambiado; «a la fecha con información hasta R» toma la versión mayor con <see cref="RegisteredAt"/> ≤ R. Sin diferencias de
/// auditoría (T67).
/// </summary>
[SinDiffDeAuditoria]
public class LoanDailyPosition : AuditableEntityLong, IHechoInmutable
{
    public int LoanId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCartera</c> enlace en el mismo guardado una fila nueva con su <c>Loan</c> todavía sin Id.</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.Loan? Loan { get; init; }

    public DateOnly PositionDate { get; init; }

    public int Version { get; init; }

    /// <summary>R de la instantánea.</summary>
    public DateTime RegisteredAt { get; init; }

    // ------------------------------------------------------------------------------ dimensiones para el alcance --

    public LoanStatus Status { get; init; }

    public int BranchId { get; init; }

    public int CreditLineId { get; init; }

    public string ModalityCode { get; init; } = string.Empty;

    public string? SubsegmentCode { get; init; }

    // -------------------------------------------------------------------------------- saldos por concepto --

    public decimal Principal { get; init; }

    public decimal PrincipalPastDue { get; init; }

    public decimal CurrentInterest { get; init; }

    public decimal DefaultInterest { get; init; }

    public decimal CurrentInterestInSuspense { get; init; }

    public decimal DefaultInterestInSuspense { get; init; }

    public decimal Insurance { get; init; }

    public decimal CollectionFee { get; init; }

    public decimal OtherCharge { get; init; }

    public decimal Advance { get; init; }

    public decimal CreditBalance { get; init; }

    /// <summary>Total para cancelar a esa fecha.</summary>
    public decimal PayoffAmount { get; init; }

    // ----------------------------------------------------------------------------------------------- mora --

    /// <summary>Con <c>Mora.ToleranciaDeSaldoVencido</c> y sin las cuotas cubiertas por anticipo (T30).</summary>
    public int DaysPastDue { get; init; }

    public int? OldestUnpaidInstallment { get; init; }

    public int InstallmentsPastDue { get; init; }

    public bool IsSuspended { get; init; }

    public bool UsuryCapped { get; init; }

    /// <summary>Tasa del día, topada.</summary>
    public decimal? AppliedAnnualRate { get; init; }

    /// <summary>Calificación <b>informativa</b>; la contable es la del último cierre (T46, T48).</summary>
    public RatingCategory? DailyRating { get; init; }

    public RatingSubcategory? DailyRatingSubcategory { get; init; }

    /// <summary>Huella de la reconstrucción.</summary>
    public string InputsSha256 { get; init; } = string.Empty;

    /// <summary>
    /// K6b (T1048, T1059; data-model §6.1): en un crédito en UVR, la UVR del día con que se valoró la posición, el capital y el valor para cancelar en
    /// UVR (alias <c>ValorUvr</c>); nulas en pesos.
    /// </summary>
    public decimal? UvrValue { get; init; }

    public decimal? PrincipalUvr { get; init; }

    public decimal? PayoffAmountUvr { get; init; }
}
