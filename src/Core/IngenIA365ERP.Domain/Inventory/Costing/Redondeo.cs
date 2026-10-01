namespace IngenIA365ERP.Domain.Inventory.Costing;

/// <summary>A qué se redondean los montos en pesos (<c>Redondeo.Montos</c>). No se guarda: se lee del parámetro. (nuevo)</summary>
public enum RedondeoDeMontos { Centavo = 1, Peso = 2 }

/// <summary>Dónde queda el residuo de un reparto (<c>Redondeo.Residuo</c>). No se guarda. (nuevo)</summary>
public enum ResiduoDeRedondeo { MayorValor = 1, UltimaLinea = 2 }

/// <summary>
/// El redondeo del inventario (feature 012, T19, T281; FR-017; research R11): decimal exacto, siempre
/// <see cref="MidpointRounding.AwayFromZero"/>. Los montos van al centavo o al peso según <c>Redondeo.Montos</c>; los
/// costos unitarios a <see cref="DecimalesDeCostoUnitario"/> (<c>PrecisionDeInventario.CostoUnitario</c>). Cuando un
/// total se reparte en partes (el valor del ámbito por bodega, T18) el residuo no se pierde: va a la parte de mayor
/// valor o a la última según <c>Redondeo.Residuo</c>, y queda visible. El residuo que deja una salida cuando la
/// cantidad llega a cero lo pone el motor como línea <c>RoundingResidue</c>.
/// </summary>
public static class Redondeo
{
    /// <summary>Decimales de un costo unitario (18,6).</summary>
    public const int DecimalesDeCostoUnitario = 6;

    private const int DecimalesAlCentavo = 2;

    public static int Decimales(RedondeoDeMontos montos) => montos == RedondeoDeMontos.Peso ? 0 : DecimalesAlCentavo;

    public static decimal Monto(decimal valor, RedondeoDeMontos montos) =>
        Math.Round(valor, Decimales(montos), MidpointRounding.AwayFromZero);

    public static decimal CostoUnitario(decimal valor) =>
        Math.Round(valor, DecimalesDeCostoUnitario, MidpointRounding.AwayFromZero);

    /// <summary>El valor del parámetro <c>Redondeo.Montos</c> («Centavo», «Peso»); cualquier otro es un error visible.</summary>
    public static RedondeoDeMontos MontosDesde(string valor) =>
        Enum.TryParse<RedondeoDeMontos>(valor, ignoreCase: true, out var r) && Enum.IsDefined(r)
            ? r
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Redondeo.Montos admite «Centavo» o «Peso».");

    /// <summary>El valor del parámetro <c>Redondeo.Residuo</c> («MayorValor», «UltimaLinea»).</summary>
    public static ResiduoDeRedondeo ResiduoDesde(string valor) =>
        Enum.TryParse<ResiduoDeRedondeo>(valor, ignoreCase: true, out var r) && Enum.IsDefined(r)
            ? r
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, "Redondeo.Residuo admite «MayorValor» o «UltimaLinea».");

    /// <summary>
    /// Reparte <paramref name="total"/> (ya redondeado) en partes proporcionales a <paramref name="partesSinRedondear"/>,
    /// cada una redondeada, y deja el residuo en la parte de mayor valor absoluto (la primera, si empatan) o en la
    /// última. La suma de lo devuelto es exactamente <paramref name="total"/>.
    /// </summary>
    public static IReadOnlyList<decimal> Repartir(
        decimal total, IReadOnlyList<decimal> partesSinRedondear, RedondeoDeMontos montos, ResiduoDeRedondeo residuo)
    {
        ArgumentNullException.ThrowIfNull(partesSinRedondear);
        if (partesSinRedondear.Count == 0) return [];

        var partes = partesSinRedondear.Select(p => Monto(p, montos)).ToArray();
        var diferencia = total - partes.Sum();
        if (diferencia == 0m) return partes;

        var indice = partes.Length - 1;
        if (residuo == ResiduoDeRedondeo.MayorValor)
        {
            indice = 0;
            for (var i = 1; i < partes.Length; i++)
                if (Math.Abs(partes[i]) > Math.Abs(partes[indice])) indice = i;
        }

        partes[indice] += diferencia;
        return partes;
    }

    /// <summary>
    /// El valor por bodega de un ámbito (T18; data-model §3.4): cantidad de cada bodega × promedio del ámbito, con el
    /// residuo según <paramref name="residuo"/>, de modo que la suma es el valor del ámbito. Nunca Σ <c>TotalCost</c>
    /// por bodega.
    /// </summary>
    public static IReadOnlyList<decimal> ValorPorBodega(
        decimal valorDelAmbito, decimal promedio, IReadOnlyList<decimal> cantidades, RedondeoDeMontos montos, ResiduoDeRedondeo residuo)
    {
        ArgumentNullException.ThrowIfNull(cantidades);
        return Repartir(valorDelAmbito, cantidades.Select(c => c * promedio).ToList(), montos, residuo);
    }
}
