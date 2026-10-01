namespace IngenIA365ERP.Domain.Inventory.Units;

/// <summary>
/// Lo que se convierte: la cantidad digitada en la unidad de la línea (<see cref="UnitCode"/>, con los decimales que
/// admite) y el factor a la unidad base (<see cref="BaseUnitCode"/>, con los suyos). <see cref="LineNumber"/> sólo sirve
/// para nombrar el rechazo. (nuevo)
/// </summary>
public sealed record PedidoDeConversion(
    int LineNumber,
    string UnitCode,
    decimal Quantity,
    decimal Factor,
    int UnitAllowedDecimals,
    string BaseUnitCode,
    int BaseAllowedDecimals);

/// <summary>
/// Por qué no se admite la conversión: la línea, la unidad que no admite los decimales (la de la línea o la base), los
/// decimales que admite y la cantidad en unidad base que resultaría. Es lo que la aplicación publica como
/// <c>Inventory.Unit.DecimalsNotAllowed</c>. (nuevo)
/// </summary>
public sealed record RechazoDeConversion(int LineNumber, string UnitCode, int AllowedDecimals, decimal QuantityBase);

/// <summary>
/// El resultado: la cantidad en unidad base que va al kardex (<see cref="QuantityBase"/>) y la diferencia que la
/// conversión no da exacta (<see cref="RoundingQuantity"/>, en la misma línea y visible), o el rechazo. Siempre
/// <c>Quantity × Factor = QuantityBase + RoundingQuantity</c>. (nuevo)
/// </summary>
public sealed record ResultadoDeConversion(decimal QuantityBase, decimal RoundingQuantity, RechazoDeConversion? Rechazo)
{
    public bool Admitida => Rechazo is null;
}

/// <summary>
/// El motor puro de la conversión de unidades (feature 012, T205; FR-017, FR-025; decisiones-transversales T19): de una
/// cantidad en una unidad de empaque a la unidad base, en decimal exacto, sin IO ni EF. Lo usan el borrador del documento
/// genérico, el POS y los conteos, así ninguno redondea por su cuenta.
///
/// <list type="bullet">
/// <item>La cantidad no puede traer más decimales de los que admite su unidad.</item>
/// <item>La base se redondea (lejos del cero) a los decimales que admite la unidad base, hasta
/// <see cref="DecimalesDeCantidad"/>. La diferencia es la cantidad de redondeo de la línea.</item>
/// <item>Una conversión con un factor de seis decimales que no da exacta deja la diferencia visible; pero si la
/// diferencia llega a <see cref="ToleranciaDeRedondeo"/> —la cantidad más chica que se guarda— es que la base no admite
/// los decimales necesarios, y se rechaza (FR-017). Una cantidad que en la base se vuelve cero también.</item>
/// </list>
/// </summary>
public static class ConversionDeUnidades
{
    /// <summary>Las cantidades se guardan con 4 decimales (<c>PrecisionDeInventario.Cantidad</c>).</summary>
    public const int DecimalesDeCantidad = 4;

    /// <summary>La cantidad más chica que se guarda: una diferencia de este tamaño ya no es redondeo del factor.</summary>
    public static readonly decimal ToleranciaDeRedondeo = new(1, 0, 0, false, DecimalesDeCantidad);

    public static ResultadoDeConversion Convertir(PedidoDeConversion pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var bruta = pedido.Quantity * pedido.Factor;

        if (Decimales(pedido.Quantity) > pedido.UnitAllowedDecimals)
            return Rechazo(pedido, pedido.UnitCode, pedido.UnitAllowedDecimals, bruta);

        var decimalesDeLaBase = Math.Clamp(pedido.BaseAllowedDecimals, 0, DecimalesDeCantidad);
        var enBase = Math.Round(bruta, decimalesDeLaBase, MidpointRounding.AwayFromZero);
        var redondeo = bruta - enBase;

        if (Math.Abs(redondeo) >= ToleranciaDeRedondeo)
            return Rechazo(pedido, pedido.BaseUnitCode, pedido.BaseAllowedDecimals, bruta);
        if (pedido.Quantity > 0 && enBase <= 0)
            return Rechazo(pedido, pedido.BaseUnitCode, pedido.BaseAllowedDecimals, enBase);

        return new ResultadoDeConversion(enBase, redondeo, null);
    }

    /// <summary>Cuántos decimales significativos trae un valor (1,50 tiene uno).</summary>
    public static int Decimales(decimal valor)
    {
        valor = Math.Abs(valor);
        var decimales = 0;
        while (valor != Math.Truncate(valor))
        {
            valor *= 10;
            decimales++;
        }
        return decimales;
    }

    private static ResultadoDeConversion Rechazo(PedidoDeConversion pedido, string unidad, int decimales, decimal enBase) =>
        new(0m, 0m, new RechazoDeConversion(pedido.LineNumber, unidad, decimales, enBase));
}
