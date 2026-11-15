namespace IngenIA365ERP.Shared.Services.Compras;

/// <summary>
/// Los descuentos que una pantalla de compras trae de un documento origen (2026-11-13). Un documento que toma <b>parte</b> de la cantidad de
/// su origen —la factura o el documento soporte con lo pendiente de una recepción, la recepción con lo pendiente de una orden— lleva sólo la
/// parte del descuento de esa cantidad: antes la factura iba sin descuento (el neto superaba al de la recepción y nacía una diferencia de
/// precio falsa hacia arriba) y la recepción parcial se llevaba el descuento entero de la orden. Puro: no llama al servidor.
/// </summary>
public static class DescuentosDeCompra
{
    /// <summary>
    /// El descuento de la línea origen en proporción a una parte de su cantidad, las dos en unidad base: descuento × parte ÷ total, al centavo y
    /// con el medio hacia arriba, como los montos del servidor. Toda la cantidad (o más) lleva el descuento entero; sin descuento o sin
    /// cantidad, cero.
    /// </summary>
    public static decimal Proporcional(decimal? descuento, decimal parteEnBase, decimal totalEnBase)
    {
        if (descuento is not { } valor || valor <= 0m || totalEnBase <= 0m || parteEnBase <= 0m) return 0m;
        if (parteEnBase >= totalEnBase) return valor;
        return Math.Round(valor * parteEnBase / totalEnBase, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// La fracción de descuento de una línea de la orden: su descuento sobre su bruto (neto + descuento). Es la que el servidor aplica a la
    /// línea de la recepción contra la orden cuando la pantalla no manda un descuento propio.
    /// </summary>
    public static decimal TasaDeLaOrden(decimal? descuento, decimal? neto)
    {
        if (descuento is not { } valor || valor <= 0m) return 0m;
        var bruto = (neto ?? 0m) + valor;
        return bruto <= 0m ? 0m : valor / bruto;
    }

    /// <summary>
    /// Lo que el servidor descontará a la línea de la recepción con la fracción de la orden: el bruto (cantidad × precio, al centavo) por la
    /// tasa, al centavo. Sólo para mostrarlo antes de guardar: el valor que vale es el que devuelve el servidor.
    /// </summary>
    public static decimal EstimadoConLaOrden(decimal cantidad, decimal precio, decimal tasa)
    {
        if (tasa <= 0m) return 0m;
        var bruto = Math.Round(cantidad * precio, 2, MidpointRounding.AwayFromZero);
        return Math.Round(bruto * tasa, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// ¿El descuento guardado en una línea de la recepción es el que el servidor derivó de la orden? Con menos de un peso de diferencia (el
    /// redondeo de la cooperativa puede ir al peso), sí: la pantalla deja que el servidor lo vuelva a derivar si cambia la cantidad o el precio.
    /// Si no, lo escribió la persona y se manda tal cual.
    /// </summary>
    public static bool EsElDeLaOrden(decimal descuentoGuardado, decimal brutoGuardado, decimal tasa) =>
        Math.Abs(descuentoGuardado - brutoGuardado * tasa) < 1m;
}
