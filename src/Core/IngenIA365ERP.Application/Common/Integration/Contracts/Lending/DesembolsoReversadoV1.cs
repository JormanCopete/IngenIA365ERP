namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>DesembolsoReversado</c> v1 (§10.2; <c>DRV</c>): el reverso del desembolso desde su origen. Origen <c>DisbursementReversal</c>,
/// clave <c>Confirmation</c>, <c>related</c> = el desembolso; hereda el modo del desembolso (C08). Lo origina el consumo de
/// <c>ResultadoDelGiro</c> (usuario de Tesorería como usuario de origen) o la persona que anula.
/// </summary>
public sealed record DesembolsoReversadoV1
{
    public const string Type = "DesembolsoReversado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>Positivos: el capital del tramo y lo causado que se anula, con el mismo <c>reasonCode</c> del desembolso.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary><c>Rejected</c>, <c>CheckNotCashed</c>, <c>Voided</c> o <c>Manual</c>.</summary>
    public string Cause { get; init; } = string.Empty;

    /// <summary>El <c>ResultadoDelGiro</c> que lo provocó.</summary>
    public Guid? TreasuryResultMessageId { get; init; }

    public string? Reason { get; init; }

    /// <summary>La del desembolso reversado (C22).</summary>
    public string DisbursementForm { get; init; } = string.Empty;

    /// <summary>El beneficiario del desembolso reversado (C22).</summary>
    public Guid? PayeePersonPublicId { get; init; }

    /// <summary>El del desembolso en especie (C22).</summary>
    public InKindDisbursementV1? InKind { get; init; }

    /// <summary>Valor de <c>LoanStatus</c>: <c>PendingDisbursement</c> o <c>Voided</c>.</summary>
    public string LoanStatusAfter { get; init; } = string.Empty;

    /// <summary>Los <c>PagoReversado</c> de la recogida nacidos en este guardado porque el crédito se anuló (C11).</summary>
    public IReadOnlyList<OriginalFactRefV1> PayoffReversals { get; init; } = [];
}
