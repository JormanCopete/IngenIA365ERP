namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>DocumentoAnulado</c> v1 (contracts/mensajes.md §6.9): el contenido del original con <b>todos</b> los importes
/// y cantidades con signo contrario. Su <c>Kind</c> es el del original (informativo si anula un saldo inicial). Un
/// documento fiscal validado por la DIAN nunca lo emite (FR-066).
/// </summary>
public sealed record DocumentoAnuladoV1
{
    public const string Type = "DocumentoAnulado";

    public string Reason { get; init; } = string.Empty;

    /// <summary><c>DianRejectionReplaced</c> (caso b), <c>DianRejectionCancelled</c> (caso c) o nulo.</summary>
    public string? FiscalCase { get; init; }

    /// <summary>Igual a <c>related</c> del sobre.</summary>
    public DocumentRefV1 VoidedDocument { get; init; } = new();

    /// <summary>Tipo del anulado: con él Contabilidad elige el tipo de comprobante del original (T28).</summary>
    public string VoidedDocumentTypeCode { get; init; } = string.Empty;

    /// <summary>Uno por cada mensaje a Contabilidad del evento <c>Confirmation</c> del anulado.</summary>
    public IReadOnlyList<VoidedContentV1> VoidedContents { get; init; } = [];
}

/// <summary>
/// Un contenido anulado (§6.9). <see cref="OperationDate"/> es la del original (fecha de las reglas, T29);
/// <see cref="Content"/> es el contenido original con los signos invertidos, del tipo que dice <see cref="Type"/>.
/// </summary>
public sealed record VoidedContentV1
{
    public Guid MessageId { get; init; }

    public string Type { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public DateOnly OperationDate { get; init; }

    public object? Content { get; init; }
}
