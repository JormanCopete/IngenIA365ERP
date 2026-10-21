namespace IngenIA365ERP.Domain.Lending.Scoring;

/// <summary>
/// Lo que aportó una variable al puntaje (feature 013, T0387; data-model §2.5 <c>Explanation</c>): su valor (nulo = sin dato), el rango en que
/// cayó, los puntos, el peso, el aporte (<c>peso × puntos</c> a cuatro decimales) y la frase que lo explica. <see cref="SinDato"/> dice que
/// la solicitud no traía el valor; <see cref="FueraDeRango"/>, que lo traía pero ningún rango lo contiene (cero puntos). (nuevo)
/// </summary>
public sealed record AporteDeVariable(
    string Codigo,
    string Nombre,
    string Fuente,
    decimal? Valor,
    RangoDePuntaje? Rango,
    bool SinDato,
    bool FueraDeRango,
    decimal Puntos,
    decimal Peso,
    decimal Aporte,
    string Explicacion);

/// <summary>
/// El puntaje de una persona con el modelo de una versión de línea (feature 013, T0387; FR-037; data-model §2.5): el puntaje (alias
/// <c>Puntaje</c> 12,4), el mínimo de la versión, si quedó bajo él (no rechaza: sube la solicitud al nivel de excepción), el máximo que el
/// modelo puede dar (su escala), lo que aportó cada variable, la huella de las entradas y la explicación. (nuevo)
/// </summary>
public sealed record ResultadoDePuntaje(
    decimal Puntaje,
    decimal? Minimo,
    bool BajoElMinimo,
    decimal PuntajeMaximo,
    IReadOnlyList<AporteDeVariable> Variables,
    string HuellaDeEntradas,
    ExplicacionDeCartera Explicacion);

/// <summary>
/// El motor puro del puntaje de crédito (feature 013, T0387; FR-037, §2.8). Recibe el modelo de la versión de la línea
/// (<see cref="ModeloDePuntaje"/>: variables con su fuente, peso y rangos con puntos), el mínimo de la versión y los valores de cada
/// fuente, y devuelve:
/// <list type="bullet">
/// <item>por variable, los puntos del rango que contiene su valor (desde incluido, hasta excluido); sin valor, los del rango «sin dato»
/// declarado o cero; con un valor que ningún rango contiene, cero —y la explicación dice cuál de los tres—;</item>
/// <item>el aporte de cada variable, <c>peso × puntos</c> redondeado a cuatro decimales (mitad hacia arriba), y el puntaje como la suma de
/// los aportes: así la explicación suma exactamente el puntaje;</item>
/// <item><see cref="ResultadoDePuntaje.BajoElMinimo"/> si hay mínimo y el puntaje es menor (igual al mínimo no está bajo). Nunca rechaza;</item>
/// <item>la huella SHA-256 de las entradas (<see cref="HuellaDeCartera"/>): el modelo canónico, el mínimo y los valores por fuente en orden
/// ordinal.</item>
/// </list>
/// La escala no se guarda aparte: la dan los puntos de los rangos (0 a 1.000, 0 a 100…), y <see cref="ResultadoDePuntaje.PuntajeMaximo"/>
/// la informa. Publica la lista cerrada de fuentes (<see cref="Fuentes"/>) que acepta <c>SetCreditLineScoringCommand</c>. Sin IO ni reloj;
/// sólo <c>decimal</c>. (nuevo)
/// </summary>
public static class MotorDePuntaje
{
    /// <summary>Las fuentes que el motor sabe leer: la lista cerrada de <see cref="FuentesDePuntaje"/>.</summary>
    public static IReadOnlyList<string> Fuentes => FuentesDePuntaje.Todas;

    /// <summary>Calcula el puntaje. Un modelo inválido (<see cref="ModeloDePuntaje.Validar"/>) es un error de quien llama.</summary>
    public static ResultadoDePuntaje Calcular(ModeloDePuntaje modelo, decimal? minimo, IReadOnlyDictionary<string, decimal?> valores)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(valores);
        if (modelo.Validar() is { } problema)
            throw new ArgumentException($"El modelo de puntaje no es válido: {problema.Mensaje}", nameof(modelo));

        var explicacion = new ExplicacionDeCartera();
        var aportes = new List<AporteDeVariable>(modelo.Variables.Count);
        foreach (var v in modelo.Variables.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            var valor = valores.TryGetValue(v.Source, out var leido) ? leido : null;
            RangoDePuntaje? rango;
            bool sinDato = valor is null, fueraDeRango = false;
            string porQue;
            if (valor is not { } dato)
            {
                rango = v.Ranges.FirstOrDefault(r => r.NoData);
                porQue = rango is null
                    ? "sin dato y sin rango «sin dato» declarado: cero puntos"
                    : $"sin dato: toma los puntos de su rango «sin dato» ({Numero(rango.Points)})";
            }
            else
            {
                rango = v.Ranges.FirstOrDefault(r => r.Contiene(dato));
                fueraDeRango = rango is null;
                porQue = rango is null
                    ? $"valor {Numero(dato)} fuera de todos sus rangos: cero puntos"
                    : $"valor {Numero(dato)} en el rango {Rango(rango)}: {Numero(rango.Points)} puntos";
            }

            var puntos = rango?.Points ?? 0m;
            var aporte = Redondear(v.Weight * puntos);
            var frase = $"{v.Name} ({v.Code}, {v.Source}): {porQue}; peso {Numero(v.Weight)}, aporta {Numero(aporte)}.";
            explicacion.Linea(frase);
            aportes.Add(new AporteDeVariable(v.Code, v.Name, v.Source, valor, rango, sinDato, fueraDeRango, puntos, v.Weight, aporte, frase));
        }

        var puntaje = aportes.Sum(a => a.Aporte);
        var maximo = modelo.Variables.Sum(v => Redondear(v.Weight * v.Ranges.Max(r => r.Points)));
        var bajo = minimo is { } m && puntaje < m;
        explicacion.Linea($"Puntaje {Numero(puntaje)} de un máximo de {Numero(maximo)}" + (minimo is { } min
            ? bajo
                ? $": por debajo del mínimo {Numero(min)}. No rechaza: la solicitud sube al nivel de excepción."
                : $": no está por debajo del mínimo {Numero(min)}."
            : ": la línea no tiene puntaje mínimo."));

        return new ResultadoDePuntaje(puntaje, minimo, bajo, maximo, aportes, Huella(modelo, minimo, valores), explicacion);
    }

    /// <summary>La huella de las entradas: el modelo canónico, el mínimo y el valor de cada fuente que el modelo usa, por orden ordinal.</summary>
    public static string Huella(ModeloDePuntaje modelo, decimal? minimo, IReadOnlyDictionary<string, decimal?> valores)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(valores);
        var fuentes = modelo.Variables.Select(v => v.Source).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new HuellaDeCartera()
            .Texto("modelo", modelo.Escribir())
            .Decimal("minimo", minimo)
            .Lista("valores", fuentes, (h, f) => h.Texto("fuente", f).Decimal("valor", valores.TryGetValue(f, out var x) ? x : null))
            .Calcular();
    }

    /// <summary>A cuatro decimales, mitad hacia arriba (alias <c>Puntaje</c> 12,4).</summary>
    public static decimal Redondear(decimal valor) => decimal.Round(valor, ModeloDePuntaje.DecimalesDelPuntaje, MidpointRounding.AwayFromZero);

    private static string Rango(RangoDePuntaje r) =>
        $"[{(r.From is { } d ? Numero(d) : "−∞")}, {(r.To is { } h ? Numero(h) : "+∞")})";

    private static string Numero(decimal valor) => ExplicacionDeCartera.Numero(valor);
}
