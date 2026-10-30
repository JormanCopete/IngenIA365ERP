namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>ResultadoDelGiro</c> v1 (interfaces.md §4.2, mensajes.md §12.4; <c>TES → Lending</c>): lo define esta especificación (D27) y
/// Tesorería lo adopta y lo emite (feature 015; hasta la fase 19 también la tesorería de prueba de la 013). Clave <c>Confirmation:{orden:N}</c>; no admite
/// proceso.
/// </summary>
public sealed record ResultadoDelGiroV1
{
    public const string Type = "ResultadoDelGiro";

    public Guid OrderPublicId { get; init; }

    /// <summary>La <c>OrdenDeGiroEmitida</c> a la que responde (C12).</summary>
    public Guid OrderMessagePublicId { get; init; }

    /// <summary><c>Disbursed</c>, <c>Rejected</c>, <c>CheckNotCashed</c> o <c>Voided</c>.</summary>
    public string Outcome { get; init; } = string.Empty;

    public DateOnly OccurredOn { get; init; }

    public decimal Amount { get; init; }

    public string TreasuryReference { get; init; } = string.Empty;

    public string? CheckNumber { get; init; }

    public string? Reason { get; init; }
}
