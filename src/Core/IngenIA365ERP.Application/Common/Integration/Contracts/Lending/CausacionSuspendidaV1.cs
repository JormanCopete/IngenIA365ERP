namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CausacionSuspendida</c> v1 (§10.6; <c>CSU</c>; informativo): origen la operación que cruza el umbral (clave
/// <c>Confirmation:{tipoDeHecho:N}</c>) o la parte del ciclo diario (clave <c>Confirmation:{crédito:N}</c>). Admite proceso. Sin
/// comprobante: Contabilidad deja su recibo.
/// </summary>
public sealed record CausacionSuspendidaV1
{
    public const string Type = "CausacionSuspendida";

    public LoanRefV1 Loan { get; init; } = new();

    public Guid DebtorPersonPublicId { get; init; }

    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    public DateTimeOffset RegisteredAt { get; init; }

    public DateOnly EffectiveDate { get; init; }

    /// <summary><c>ArrearsThreshold</c>, <c>RepeatedRestructuring</c>, <c>Insolvency</c>, <c>WriteOff</c>.</summary>
    public string Cause { get; init; } = string.Empty;

    /// <summary>Altura de mora a la fecha efectiva.</summary>
    public int DaysPastDue { get; init; }

    /// <summary>La operación que lo provocó.</summary>
    public OriginalFactRefV1? CauseFact { get; init; }
}
