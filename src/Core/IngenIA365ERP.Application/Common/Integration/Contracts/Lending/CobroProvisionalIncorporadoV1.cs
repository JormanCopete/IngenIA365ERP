namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>CobroProvisionalIncorporado</c> v1 (§10.16; <c>CPI</c>): un cobro de la plantilla de cobros provisionales, aplicado al nacer la
/// obligación de su venta (C62). Origen <c>ProvisionalCollection</c>, clave <c>Confirmation</c>.
/// </summary>
public sealed record CobroProvisionalIncorporadoV1
{
    public const string Type = "CobroProvisionalIncorporado";

    public LoanRefV1 Loan { get; init; } = new();

    /// <summary>Deudor (el tercero del sobre puede ser otro: el pagador).</summary>
    public Guid DebtorPersonPublicId { get; init; }

    /// <summary>Fecha valor (FR-012).</summary>
    public DateOnly ValueDate { get; init; }

    /// <summary>Fecha de registro: el instante R de la operación.</summary>
    public DateTimeOffset RegisteredAt { get; init; }

    /// <summary>Dimensiones vigentes a la fecha contable (T55).</summary>
    public LoanFactDimensionsV1 Dimensions { get; init; } = new();

    /// <summary>La distribución del cobro por concepto.</summary>
    public IReadOnlyList<ConceptAmountV1> Amounts { get; init; } = [];

    /// <summary>El hecho del que depende (FR-126); nulo si no depende de ninguno.</summary>
    public OriginalFactRefV1? Original { get; init; }

    /// <summary><c>RulesVersion</c> del crédito (<c>LND-AAAA.n</c>, T29).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>La venta cobrada.</summary>
    public ExternalDocumentRefV1 Invoice { get; init; } = new();

    /// <summary>El sello de la venta (C22).</summary>
    public string AccountsReceivableRecordedBy { get; init; } = string.Empty;

    /// <summary>Referencia del registro contable o de Tesorería que redujo la provisional.</summary>
    public string AccountingReference { get; init; } = string.Empty;

    public Guid ActivationPublicId { get; init; }

    /// <summary>Fila de la plantilla.</summary>
    public int TemplateRow { get; init; }
}
