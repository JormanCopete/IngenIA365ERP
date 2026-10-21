namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CondonacionAprobada</c> v1 (§10.21; <c>CON</c>; K3, declarado en K1): origen <c>Waiver</c>, clave <c>Confirmation</c>.
/// </summary>
public sealed record CondonacionAprobadaV1
{
    public const string Type = "CondonacionAprobada";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Lo condonado por concepto, con <c>reasonCode</c> = motivo.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    public Guid WaiverPublicId { get; init; }

    public Guid? SessionItemPublicId { get; init; }

    /// <summary>Cuánto de lo condonado sale contra el deterioro individual (C22).</summary>
    public IReadOnlyList<ConceptAmountV1> ImpairmentApplied { get; init; } = [];

    /// <summary>Lo que va al gasto.</summary>
    public decimal ImpairmentShortfall { get; init; }
}
