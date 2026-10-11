using System.Globalization;

namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// Un tramo de la explicación (feature 013, M11, §2.8): de <see cref="Desde"/> (excluida) a <see cref="Hasta"/> (incluida),
/// <see cref="Dias"/> días de la convención, la tasa diaria aplicada, la base sobre la que se aplicó y lo que dio, con su concepto
/// y la cuota a la que va. (nuevo)
/// </summary>
public sealed record TramoDeCartera(
    DateOnly Desde,
    DateOnly Hasta,
    int Dias,
    decimal TasaDiaria,
    decimal Base,
    decimal Valor,
    string Concepto,
    int? Cuota = null);

/// <summary>
/// La explicación de un cálculo de Cartera (feature 013, M11, §2.8; FR-010, SC-023): los tramos con sus días, tasa diaria y base,
/// y las líneas que dicen en palabras lo que hizo el motor —la tasa diaria con su única aproximación, el residuo que absorbió la
/// última cuota, la usura que se usó—, para que la contadora pueda rehacer el número a mano. La arman los motores puros
/// (<c>GeneradorDePlan</c>, <c>ControlDeUsura</c>, <c>TasaDelDia</c>); la reconstrucción de US4 le suma sus tramos de causación. Los
/// números van en cultura invariante (punto decimal), como en la huella. (nuevo)
/// </summary>
public sealed class ExplicacionDeCartera
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly List<TramoDeCartera> _tramos = [];
    private readonly List<string> _lineas = [];

    public IReadOnlyList<TramoDeCartera> Tramos => _tramos;

    public IReadOnlyList<string> Lineas => _lineas;

    /// <summary>Agrega una línea en palabras.</summary>
    public ExplicacionDeCartera Linea(string texto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        _lineas.Add(texto);
        return this;
    }

    /// <summary>Agrega un tramo.</summary>
    public ExplicacionDeCartera Tramo(TramoDeCartera tramo)
    {
        ArgumentNullException.ThrowIfNull(tramo);
        _tramos.Add(tramo);
        return this;
    }

    /// <summary>Suma a ésta los tramos y las líneas de otra explicación, en su orden.</summary>
    public ExplicacionDeCartera Agregar(ExplicacionDeCartera otra)
    {
        ArgumentNullException.ThrowIfNull(otra);
        _tramos.AddRange(otra._tramos);
        _lineas.AddRange(otra._lineas);
        return this;
    }

    /// <summary>Todo en texto: las líneas y después un renglón por tramo.</summary>
    public string Texto => string.Join("\n", _lineas.Concat(_tramos.Select(TextoDelTramo)));

    public override string ToString() => Texto;

    /// <summary>Un número en cultura invariante, sin ceros sobrantes.</summary>
    public static string Numero(decimal valor) => valor.ToString("0.############################", Inv);

    /// <summary>Una fecha <c>yyyy-MM-dd</c>.</summary>
    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", Inv);

    private static string TextoDelTramo(TramoDeCartera t) =>
        $"{Fecha(t.Desde)}..{Fecha(t.Hasta)}: {t.Dias.ToString(Inv)} días × tasa diaria {Numero(t.TasaDiaria)} sobre {Numero(t.Base)} → "
        + $"{t.Concepto} {Numero(t.Valor)}" + (t.Cuota is { } c ? $" (cuota {c.ToString(Inv)})" : string.Empty);
}
