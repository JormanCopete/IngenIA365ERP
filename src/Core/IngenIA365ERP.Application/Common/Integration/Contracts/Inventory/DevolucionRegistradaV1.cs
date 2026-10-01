namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>DevolucionRegistrada</c> v1 (contracts/mensajes.md §6.8): sólo cantidades y costo de una devolución a
/// proveedor (<c>Exit</c> al costo con que entró) o de cliente (<c>Entry</c> al costo con que salió).
/// <see cref="Operation"/> es <c>DevolucionAProveedor</c> o <c>DevolucionDeCliente</c>.
/// </summary>
public sealed record DevolucionRegistradaV1
{
    public const string Type = "DevolucionRegistrada";

    public string Operation { get; init; } = string.Empty;

    public IReadOnlyList<CostLineV1> Lines { get; init; } = [];
}
