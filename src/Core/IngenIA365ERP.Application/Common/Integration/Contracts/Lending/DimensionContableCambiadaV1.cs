namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>DimensionContableCambiada</c> v1 (§10.15; <c>DCC</c>): mueve los saldos vivos de una combinación de dimensiones a otra.
/// Origen la operación (clave <c>Confirmation:{tipoDeHecho:N}</c>) o la parte del ciclo diario (<c>Confirmation:{crédito:N}</c>).
/// Admite proceso.
/// </summary>
public sealed record DimensionContableCambiadaV1
{
    public const string Type = "DimensionContableCambiada";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Saldos que se trasladan, positivos; <c>dimensions</c> son las nuevas.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    public LoanFactDimensionsV1 PreviousDimensions { get; init; } = new();

    /// <summary>Nombres de los campos de <c>LoanFactDimensionsV1</c> que cambiaron.</summary>
    public IReadOnlyList<string> ChangedDimensions { get; init; } = [];

    public DateOnly EffectiveDate { get; init; }

    public OriginalFactRefV1? CauseFact { get; init; }
}
