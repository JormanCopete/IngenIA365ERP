namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>SaldoInicialCargado</c> v1 (contracts/mensajes.md §7.1): informativo, sin comprobante (su valor ya está en
/// los libros). Sirve a la conciliación y a la activación de la bodega.
/// </summary>
public sealed record SaldoInicialCargadoV1
{
    public const string Type = "SaldoInicialCargado";

    /// <summary>Fecha de corte de la bodega (la víspera de su activación).</summary>
    public DateOnly CutoffDate { get; init; }

    /// <summary><c>Entry</c>, por grupo y bodega.</summary>
    public IReadOnlyList<CostLineV1> Lines { get; init; } = [];
}
