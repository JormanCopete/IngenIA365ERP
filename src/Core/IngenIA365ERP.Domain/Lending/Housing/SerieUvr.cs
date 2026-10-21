namespace IngenIA365ERP.Domain.Lending.Housing;

/// <summary>El valor publicado de una UVR un día (valor nacional <c>UVR</c>, en pesos por UVR, con cuatro decimales o más).</summary>
public sealed record ValorDeUvr(DateOnly Fecha, decimal Valor);

/// <summary>
/// La UVR de un día tal como la usa el motor (feature 013, K6b, T1054): su valor, si es el publicado o una <b>proyección</b> (sólo para mostrar una
/// liquidación a una fecha que todavía no tiene UVR publicada) y la fecha de la última UVR publicada de donde sale la proyección. (nuevo)
/// </summary>
public sealed record UvrDelDia(DateOnly Fecha, decimal Valor, bool Proyectada, DateOnly UltimaPublicada);

/// <summary>
/// La serie diaria de la UVR (feature 013, K6b, T1054; FR-024, D18): los valores publicados por el Banco de la República que llegan como valores
/// nacionales <c>UVR</c> replicados (<c>IValoresNacionales.LeerSerieAsync</c>), un valor por día. El motor no conoce ninguno: la serie llega por
/// parámetro.
///
/// <list type="bullet">
/// <item><see cref="Publicada"/>: el valor publicado del día, o nulo. La causación de un día exige el publicado (si falta,
/// <c>NationalValues.Missing</c> sólo para ese crédito).</item>
/// <item><see cref="Del"/>: el publicado o, si no hay, la <b>proyección</b> desde el último publicado anterior con el factor diario de sus dos
/// últimos días publicados consecutivos (la UVR crece cada día con un factor constante dentro de su período de cálculo), marcada
/// <see cref="UvrDelDia.Proyectada"/> y llevada a la diezmilésima (como se publica). Una proyección nunca se causa ni se contabiliza: sólo se muestra (FR-024).</item>
/// </list>
/// Pura. (nuevo)
/// </summary>
public sealed class SerieUvr
{
    /// <summary>El código con que se responde un día sin UVR publicada.</summary>
    public const string CodigoFaltante = "NationalValues.Missing";

    private readonly SortedDictionary<DateOnly, decimal> _valores = [];

    public SerieUvr(IEnumerable<ValorDeUvr> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        foreach (var v in valores)
        {
            if (v.Valor <= 0m) throw new ArgumentOutOfRangeException(nameof(valores), v.Valor, "La UVR de un día es mayor que cero.");
            if (!_valores.TryAdd(v.Fecha, v.Valor))
                throw new ArgumentException($"La UVR del {ExplicacionDeCartera.Fecha(v.Fecha)} está repetida.", nameof(valores));
        }
    }

    /// <summary>Los valores publicados, en orden de fecha.</summary>
    public IReadOnlyList<ValorDeUvr> Valores => _valores.Select(kv => new ValorDeUvr(kv.Key, kv.Value)).ToList();

    /// <summary>La última fecha con UVR publicada; nula si la serie está vacía.</summary>
    public DateOnly? UltimaPublicada => _valores.Count == 0 ? null : _valores.Keys.Last();

    /// <summary>El valor publicado del día, o nulo.</summary>
    public decimal? Publicada(DateOnly fecha) => _valores.TryGetValue(fecha, out var v) ? v : null;

    /// <summary>El valor publicado del día; sin él, lanza <see cref="ReglaDeCarteraException"/> con <see cref="CodigoFaltante"/>.</summary>
    public decimal Requerida(DateOnly fecha) => Publicada(fecha)
        ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoFaltante,
            $"No hay UVR publicada para el {ExplicacionDeCartera.Fecha(fecha)}: el crédito en UVR no se calcula ese día hasta que se replique.", "date", fecha));

    /// <summary>
    /// La UVR del día: la publicada o, si no hay, la proyectada desde la última publicada anterior (ver la clase). Sin ninguna publicada antes del
    /// día, lanza <see cref="ReglaDeCarteraException"/> con <see cref="CodigoFaltante"/>.
    /// </summary>
    public UvrDelDia Del(DateOnly fecha)
    {
        if (_valores.TryGetValue(fecha, out var publicada)) return new UvrDelDia(fecha, publicada, false, fecha);
        var anteriores = _valores.Where(kv => kv.Key < fecha).ToList();
        if (anteriores.Count == 0) Requerida(fecha);
        var (ultima, valor) = (anteriores[^1].Key, anteriores[^1].Value);
        var factor = 1m;
        if (anteriores.Count > 1 && anteriores[^2].Key == ultima.AddDays(-1)) factor = valor / anteriores[^2].Value;
        var dias = fecha.DayNumber - ultima.DayNumber;
        var proyectada = RedondeoDeCartera.AUnidad(Rates.AritmeticaDecimal.Potencia(factor, dias) * valor, RedondeoDeCartera.DiezMilesima,
            Enums.Lending.RoundingMode.AwayFromZero);
        return new UvrDelDia(fecha, proyectada, true, ultima);
    }
}
