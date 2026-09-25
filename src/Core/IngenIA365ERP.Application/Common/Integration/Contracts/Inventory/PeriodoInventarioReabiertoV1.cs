namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>PeriodoInventarioReabierto</c> v1 (contracts/mensajes.md §7.3): informativo. Mismo origen que el cierre;
/// <c>originEventKey</c> <c>Reopen:{closingVersion}</c>.
/// </summary>
public sealed record PeriodoInventarioReabiertoV1
{
    public const string Type = "PeriodoInventarioReabierto";

    public int Year { get; init; }

    public int Month { get; init; }

    /// <summary>Versión del valorizado que queda <c>Superseded</c>.</summary>
    public int ReopenedClosingVersion { get; init; }

    public string Reason { get; init; } = string.Empty;
}
