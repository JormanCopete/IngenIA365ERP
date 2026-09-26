namespace IngenIA365ERP.Domain.Inventory.Replenishment;

/// <summary>
/// Lo que entra al cálculo de reposición de un producto en una bodega (feature 012, T952; FR-035). Las tres primeras las lee
/// <c>PosicionDeReposicion</c> (el único lector); las tres últimas son la política de <c>INV_ReorderPolicies</c>. Todo en la
/// unidad base del producto. (nuevo)
/// </summary>
/// <param name="Disponible">Físico − reservado (puede ser negativo con el stock negativo permitido).</param>
/// <param name="EnTransito">Despachado hacia la bodega y todavía sin resolver.</param>
/// <param name="PorRecibir">Pedido a proveedores sin recibir: 0 hasta I5.</param>
public sealed record EntradaDeReposicion(
    decimal Disponible,
    decimal EnTransito,
    decimal PorRecibir,
    decimal Minimo,
    decimal Maximo,
    decimal PuntoDeReorden);

/// <summary>El resultado del cálculo de reposición (feature 012, T952). (nuevo)</summary>
/// <param name="Posicion">Disponible + en tránsito + por recibir.</param>
/// <param name="RequiereReorden">La posición es igual o menor que el punto de reorden.</param>
/// <param name="Sugerido">Máximo − posición cuando pide reorden; 0 si no.</param>
/// <param name="Quiebre">El disponible es estrictamente menor que el mínimo.</param>
public sealed record ResultadoDeReposicion(decimal Posicion, bool RequiereReorden, decimal Sugerido, bool Quiebre)
{
    /// <summary>La fila merece aviso: pide reorden o está en quiebre (la vista <c>reorder-alerts</c> sólo muestra éstas).</summary>
    public bool Alerta => RequiereReorden || Quiebre;
}

/// <summary>
/// El cálculo puro de reposición (feature 012, US17, T952; FR-035; contracts/api.md §4.4, §27): posición = disponible + en
/// tránsito hacia la bodega + por recibir; <b>pide reorden</b> cuando la posición es igual o menor que el punto de reorden, y
/// entonces el sugerido es máximo − posición; <b>quiebre</b> cuando el disponible queda por debajo del mínimo. Sin IO ni valores
/// fijos: la política la pone la cooperativa. Cantidades a <see cref="DecimalesDeCantidad"/> decimales, como
/// <c>PrecisionDeInventario.Cantidad</c>. Lo usan <c>AvisoDeReposicionAlConfirmar</c>, <c>RevisionDeReorden</c> y la vista
/// <c>reorder-alerts</c> (y en I6 el sugerido de compras): ninguna decide el reorden por su cuenta. (nuevo)
/// </summary>
public static class CalculoDeReposicion
{
    /// <summary>Los decimales de una cantidad de inventario (<c>decimal(18,4)</c>).</summary>
    public const int DecimalesDeCantidad = 4;

    public static ResultadoDeReposicion Calcular(EntradaDeReposicion e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var posicion = Cantidad(e.Disponible + e.EnTransito + e.PorRecibir);
        var requiere = posicion <= e.PuntoDeReorden;
        var sugerido = requiere ? Math.Max(0m, Cantidad(e.Maximo - posicion)) : 0m;
        var quiebre = e.Disponible < e.Minimo;
        return new ResultadoDeReposicion(posicion, requiere, sugerido, quiebre);
    }

    private static decimal Cantidad(decimal valor) => Math.Round(valor, DecimalesDeCantidad, MidpointRounding.AwayFromZero);
}
