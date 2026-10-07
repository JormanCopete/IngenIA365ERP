namespace IngenIA365ERP.Domain.Lending.Rates;

/// <summary>Cómo se encontró la tasa interna de retorno.</summary>
public enum MetodoDeTir
{
    Newton = 1,
    Biseccion = 2,
}

/// <summary>La tasa periódica que anula el valor presente de los flujos, con el método que la encontró y sus iteraciones.</summary>
public sealed record ResultadoDeTir(decimal Tasa, MetodoDeTir Metodo, int Iteraciones);

/// <summary>
/// La aritmética exacta del motor de Cartera (feature 013, T26, M5, T0202; FR-010): potencia entera, raíz entera y TIR, sólo con
/// <c>decimal</c>. Ni <c>double</c>, ni <c>float</c>, ni <c>Math.Pow</c>/<c>Exp</c>/<c>Log</c> (<c>ElMotorDeCarteraEsDecimal</c>):
/// la coma flotante difiere entre plataformas y puede mover una cuota o una calificación en el borde de un corte.
///
/// <para>
/// La equivalencia compuesta de tasas (de EA a diaria y a la del período) se resuelve con <see cref="RaizEntera"/> sobre
/// <c>1 + EA</c>: <c>(1 + EA)^(1/B) − 1</c>, con B la base de días (360 o 365) o el número de períodos del año. Pura: sin IO,
/// sin reloj.
/// </para>
/// </summary>
public static class AritmeticaDecimal
{
    /// <summary>Decimales a los que se redondea una raíz (la única aproximación del motor, además de la TIR).</summary>
    public const int DecimalesDeRaiz = 24;

    private const int MaximoDeIteracionesDeRaiz = 200;
    private const int MaximoDeIteracionesDeNewton = 100;
    private const int MaximoDeIteracionesDeBiseccion = 400;

    /// <summary>Tolerancia de las iteraciones: 10^-26, por debajo del último decimal que se devuelve.</summary>
    private static readonly decimal Tolerancia = new(1, 0, 0, false, 26);

    /// <summary>Tolerancia de la TIR sobre la tasa: 10^-24.</summary>
    private static readonly decimal ToleranciaDeTir = new(1, 0, 0, false, 24);

    /// <summary>Semilla de Newton para la TIR cuando no se da: 0,01 (1 % por período).</summary>
    private static readonly decimal SemillaDeTir = new(1, 0, 0, false, 2);

    /// <summary>Lo más cerca de −1 que llega el intervalo de la bisección: −0,999999.</summary>
    private static readonly decimal CotaInferiorDeTir = -1m + new decimal(1, 0, 0, false, 6);

    private const int MaximoDeDuplicacionesDeLaCota = 64;

    /// <summary>
    /// <paramref name="x"/> elevado a <paramref name="n"/> por cuadrados sucesivos: exacta mientras el resultado quepa en
    /// <c>decimal</c> (28 dígitos significativos); con exponente negativo devuelve el inverso de la potencia positiva.
    /// </summary>
    /// <exception cref="DivideByZeroException">Base cero con exponente negativo.</exception>
    public static decimal Potencia(decimal x, int n)
    {
        if (n == 0) return 1m;
        if (n < 0)
        {
            if (x == 0m) throw new DivideByZeroException("Cero no tiene potencia negativa.");
            return 1m / Potencia(x, checked(-n));
        }

        var resultado = 1m;
        var factor = x;
        var exponente = n;
        while (exponente > 0)
        {
            if ((exponente & 1) == 1) resultado *= factor;
            exponente >>= 1;
            if (exponente > 0) factor *= factor;
        }
        return resultado;
    }

    /// <summary>
    /// La raíz <paramref name="n"/>-ésima positiva de <paramref name="x"/> por Newton, redondeada a
    /// <see cref="DecimalesDeRaiz"/> decimales (MidpointRounding.ToEven). <b>Es la única aproximación del motor</b>: la iteración
    /// <c>y ← ((n − 1)·y + x / y^(n−1)) / n</c> arranca en <c>1 + (x − 1)/n</c> (cerca de la raíz para las tasas, donde x ≈ 1) y
    /// para cuando el paso es menor que 10^-26; el error de redondeo de <c>decimal</c> en <c>y^(n−1)</c> queda dos órdenes por
    /// debajo del último decimal devuelto.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Con <paramref name="x"/> negativo o <paramref name="n"/> menor que 1.</exception>
    public static decimal RaizEntera(decimal x, int n)
    {
        if (n < 1) throw new ArgumentOutOfRangeException(nameof(n), n, "El índice de la raíz es 1 o mayor.");
        if (x < 0m) throw new ArgumentOutOfRangeException(nameof(x), x, "No hay raíz real positiva de un número negativo.");
        if (x == 0m || x == 1m || n == 1) return decimal.Round(x, DecimalesDeRaiz, MidpointRounding.ToEven);

        var y = x > 1m && x < 2m ? 1m + (x - 1m) / n : (x > 1m ? x / n : 1m);
        if (y <= 0m) y = 1m;

        for (var i = 0; i < MaximoDeIteracionesDeRaiz; i++)
        {
            var anterior = y;
            var potencia = Potencia(y, n - 1);
            y = ((n - 1) * y + x / potencia) / n;
            if (Math.Abs(y - anterior) < Tolerancia) break;
        }
        return decimal.Round(y, DecimalesDeRaiz, MidpointRounding.ToEven);
    }

    /// <summary>
    /// La tasa periódica r que anula <c>Σ flujo[t] / (1 + r)^t</c> (t desde 0). Primero Newton desde <paramref name="semilla"/>
    /// (0,01 si no se da); si Newton no converge en <c>100</c> pasos, sale del dominio (<c>1 + r ≤ 0</c>) o la derivada se anula,
    /// cae a bisección sobre el primer intervalo que cambie el signo (desde [0, 1], agrandado hacia arriba o acercado hacia −1). Devuelve la tasa redondeada a 24 decimales.
    /// </summary>
    /// <exception cref="ArgumentException">Sin al menos un flujo negativo y uno positivo, o sin cambio de signo en el intervalo.</exception>
    public static ResultadoDeTir Tir(IReadOnlyList<decimal> flujos, decimal? semilla = null)
    {
        ArgumentNullException.ThrowIfNull(flujos);
        if (!flujos.Any(f => f < 0m) || !flujos.Any(f => f > 0m))
            throw new ArgumentException("La TIR necesita al menos un flujo negativo y uno positivo.", nameof(flujos));

        var r = semilla ?? SemillaDeTir;
        for (var i = 1; i <= MaximoDeIteracionesDeNewton; i++)
        {
            if (1m + r <= 0m) break;
            var (valor, derivada) = ValorYDerivada(flujos, r);
            if (derivada == 0m) break;
            var siguiente = r - valor / derivada;
            if (Math.Abs(siguiente - r) < ToleranciaDeTir)
                return new(decimal.Round(siguiente, DecimalesDeRaiz, MidpointRounding.ToEven), MetodoDeTir.Newton, i);
            r = siguiente;
        }

        return Biseccion(flujos);
    }

    private static ResultadoDeTir Biseccion(IReadOnlyList<decimal> flujos)
    {
        // El intervalo arranca en [0, 1]; si no cambia de signo, se agranda hacia arriba duplicando la cota y, si tampoco, se acerca
        // hacia −1 a mitades (−0,5; −0,75…) mientras el valor presente quepa en decimal: cerca de −1 el descuento crece sin tope.
        var bajo = 0m;
        var alto = 1m;
        var valorBajo = ValorPresente(flujos, bajo);
        if (valorBajo == 0m) return new(0m, MetodoDeTir.Biseccion, 0);
        var valorAlto = ValorPresente(flujos, alto);
        for (var i = 0; Math.Sign(valorBajo) == Math.Sign(valorAlto) && i < MaximoDeDuplicacionesDeLaCota; i++)
        {
            bajo = alto;
            valorBajo = valorAlto;
            alto *= 2m;
            valorAlto = ValorPresente(flujos, alto);
        }
        if (Math.Sign(valorBajo) == Math.Sign(valorAlto))
        {
            alto = 0m;
            valorAlto = ValorPresente(flujos, alto);
            bajo = alto;
            valorBajo = valorAlto;
            for (var i = 0; Math.Sign(valorBajo) == Math.Sign(valorAlto) && bajo > CotaInferiorDeTir; i++)
            {
                var siguiente = (bajo - 1m) / 2m;
                decimal valor;
                try { valor = ValorPresente(flujos, siguiente); }
                catch (OverflowException) { break; }
                alto = bajo;
                valorAlto = valorBajo;
                bajo = siguiente;
                valorBajo = valor;
            }
        }
        if (Math.Sign(valorBajo) == Math.Sign(valorAlto))
            throw new ArgumentException("Los flujos no cambian de signo en el intervalo de la TIR.", nameof(flujos));

        var medio = bajo;
        for (var i = 1; i <= MaximoDeIteracionesDeBiseccion; i++)
        {
            medio = (bajo + alto) / 2m;
            var valorMedio = ValorPresente(flujos, medio);
            if (valorMedio == 0m || (alto - bajo) / 2m < ToleranciaDeTir)
                return new(decimal.Round(medio, DecimalesDeRaiz, MidpointRounding.ToEven), MetodoDeTir.Biseccion, i);
            if (Math.Sign(valorMedio) == Math.Sign(valorBajo))
            {
                bajo = medio;
                valorBajo = valorMedio;
            }
            else
            {
                alto = medio;
            }
        }
        return new(decimal.Round(medio, DecimalesDeRaiz, MidpointRounding.ToEven), MetodoDeTir.Biseccion, MaximoDeIteracionesDeBiseccion);
    }

    /// <summary>Σ flujo[t]·v^t con v = 1 / (1 + r), acumulando la potencia de v término a término.</summary>
    private static decimal ValorPresente(IReadOnlyList<decimal> flujos, decimal r)
    {
        var v = 1m / (1m + r);
        var descuento = 1m;
        var suma = 0m;
        foreach (var flujo in flujos)
        {
            suma += flujo * descuento;
            descuento *= v;
        }
        return suma;
    }

    /// <summary>El valor presente y su derivada respecto de r: <c>Σ −t·flujo[t]·v^(t+1)</c>.</summary>
    private static (decimal Valor, decimal Derivada) ValorYDerivada(IReadOnlyList<decimal> flujos, decimal r)
    {
        var v = 1m / (1m + r);
        var descuento = 1m;
        var valor = 0m;
        var derivada = 0m;
        for (var t = 0; t < flujos.Count; t++)
        {
            valor += flujos[t] * descuento;
            descuento *= v;
            derivada -= t * flujos[t] * descuento;
        }
        return (valor, derivada);
    }
}
