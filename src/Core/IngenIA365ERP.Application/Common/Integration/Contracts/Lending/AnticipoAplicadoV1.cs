namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>AnticipoAplicado</c> v1 (§10.10; <c>ANA</c>): origen la parte del ciclo diario, clave <c>Confirmation:{crédito:N}</c>.
/// Lo origina el proceso al vencer la cuota que el anticipo cubre (D6). Admite proceso.
/// </summary>
public sealed record AnticipoAplicadoV1
{
    public const string Type = "AnticipoAplicado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Lo que cubre por concepto.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>La cuota.</summary>
    public int InstallmentNumber { get; init; }

    /// <summary>Los pagos en que se recibió el anticipo.</summary>
    public IReadOnlyList<OriginalFactRefV1> AdvanceSources { get; init; } = [];
}
