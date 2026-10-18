using System.Globalization;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Domain.Lending.Regulatory;

/// <summary>
/// Un rango de mora de un tipo de cartera: su número (1, 2…), desde qué día y hasta qué día de mora (inclusive; nulo en el último, que queda abierto).
/// (nuevo)
/// </summary>
public sealed record RangoDeMora(int Numero, int Desde, int? Hasta)
{
    public bool Contiene(int dias) => dias >= Desde && (Hasta is null || dias <= Hasta);

    /// <summary>La etiqueta del rango como la lee una persona: <c>1-30</c>, <c>31-60</c>… y el abierto <c>181+</c>.</summary>
    public string Etiqueta => Hasta is { } h
        ? string.Create(CultureInfo.InvariantCulture, $"{Desde}-{h}")
        : string.Create(CultureInfo.InvariantCulture, $"{Desde}+");
}

/// <summary>
/// Los rangos de mora de un tipo de cartera para el reporte a centrales (decisión del dueño del 2026-10-16): los límites superiores de cada rango, en
/// días de mora y en orden creciente, y desde qué rango el dato es <b>negativo</b>. El rango 1 va de 1 al primer límite (cero días es «al día», fuera de
/// todo rango); cada uno de los siguientes, del límite anterior más uno al suyo; y uno más, abierto, desde el último límite más uno. (nuevo)
/// </summary>
public sealed record RangosDeMoraDeTipo(string TipoDeCartera, IReadOnlyList<int> Limites, int NegativoDesde)
{
    /// <summary>Los rangos: uno por límite y el último abierto.</summary>
    public IReadOnlyList<RangoDeMora> Rangos
    {
        get
        {
            var rangos = new List<RangoDeMora>(Limites.Count + 1);
            var desde = 1;
            for (var i = 0; i < Limites.Count; i++)
            {
                rangos.Add(new RangoDeMora(i + 1, desde, Limites[i]));
                desde = Limites[i] + 1;
            }
            rangos.Add(new RangoDeMora(Limites.Count + 1, desde, null));
            return rangos;
        }
    }

    /// <summary>El rango de <paramref name="dias"/> de mora, o nulo si no está en mora (cero días o menos).</summary>
    public RangoDeMora? RangoDe(int dias) => dias <= 0 ? null : Rangos.First(r => r.Contiene(dias));

    /// <summary>¿Con <paramref name="dias"/> de mora el dato es negativo? Sólo desde el rango <see cref="NegativoDesde"/>.</summary>
    public bool EsNegativo(int dias) => RangoDe(dias) is { } r && r.Numero >= NegativoDesde;
}

/// <summary>
/// Los rangos de mora del reporte a centrales por <b>tipo de cartera</b> (la modalidad del catálogo nacional: <c>CONSUMO</c>, <c>COMERCIAL</c>,
/// <c>VIVIENDA</c>, <c>MICROCREDITO</c>…), tal como los configura la cooperativa en el parámetro con vigencia <c>Centrales.RangosDeMora</c> (decisión del
/// dueño del 2026-10-16). Por tipo, el texto lleva <c>TIPO=límite,límite,…|rango</c>, separados por punto y coma; <c>*</c> es el de los tipos que no se
/// nombran:
/// <code>*=30,60,90,120,150,180|1;VIVIENDA=60,120,180|1</code>
/// es: para todo tipo, rangos 1-30, 31-60, 61-90, 91-120, 121-150, 151-180 y 181+, negativo desde el primero; para vivienda, 1-60, 61-120, 121-180 y
/// 181+, negativo desde el primero. Sin valores legales: el <see cref="Defecto"/> reproduce el comportamiento de antes del parámetro —toda mora mayor que
/// cero es dato negativo— y está «pendiente de validar». Un tipo que no está en el texto ni tiene <c>*</c> usa el comodín del defecto. (nuevo)
/// </summary>
public sealed class RangosDeMoraParaCentrales
{
    /// <summary>El tipo de cartera comodín: rige para los tipos que el texto no nombra.</summary>
    public const string Comodin = "*";

    /// <summary>
    /// El defecto: rangos de 30 días hasta 180 y negativo desde el primero, es decir, toda mora mayor que cero es dato negativo, como antes del
    /// parámetro. Pendiente de validar por el dueño y la contadora.
    /// </summary>
    public const string Defecto = "*=30,60,90,120,150,180|1";

    /// <summary>La forma del texto (la regla de negocio —límites crecientes, rango negativo existente, tipos sin repetir— la revisa <see cref="Validar"/>).</summary>
    public const string Patron = @"^(\*|[A-Z0-9_]{1,40})=\d{1,4}(,\d{1,4})*\|\d{1,2}(;(\*|[A-Z0-9_]{1,40})=\d{1,4}(,\d{1,4})*\|\d{1,2})*$";

    private static readonly Regex Forma = new(Patron, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private RangosDeMoraParaCentrales(IReadOnlyDictionary<string, RangosDeMoraDeTipo> porTipo) => PorTipo = porTipo;

    /// <summary>Los rangos por tipo de cartera (con el comodín si el texto lo trae).</summary>
    public IReadOnlyDictionary<string, RangosDeMoraDeTipo> PorTipo { get; }

    /// <summary>Los del <see cref="Defecto"/>.</summary>
    public static RangosDeMoraParaCentrales PorDefecto { get; } = Leer(Defecto)!;

    /// <summary>Los rangos del tipo de cartera <paramref name="tipo"/>: los suyos, los del comodín o los del comodín del defecto.</summary>
    public RangosDeMoraDeTipo Para(string? tipo)
    {
        var clave = tipo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (clave.Length > 0 && PorTipo.TryGetValue(clave, out var propio)) return propio;
        if (PorTipo.TryGetValue(Comodin, out var comodin)) return comodin;
        return PorDefecto.PorTipo[Comodin];
    }

    /// <summary>Lee el texto; vacío o inválido (forma o regla), nulo: quien lee usa <see cref="PorDefecto"/>.</summary>
    public static RangosDeMoraParaCentrales? Leer(string? texto) => Interpretar(texto, out _);

    /// <summary>Nulo si el texto es válido; si no, el motivo en español (para rechazar la vigencia al darla de alta).</summary>
    public static string? Validar(string? texto)
    {
        Interpretar(texto, out var error);
        return error;
    }

    private static RangosDeMoraParaCentrales? Interpretar(string? texto, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(texto) || !Forma.IsMatch(texto.Trim()))
        {
            error = "Escriba los rangos por tipo de cartera como TIPO=límite,límite,…|rango separados por punto y coma; por ejemplo "
                    + "«*=30,60,90,120,150,180|1;VIVIENDA=60,120,180|1».";
            return null;
        }

        var porTipo = new Dictionary<string, RangosDeMoraDeTipo>(StringComparer.Ordinal);
        foreach (var parte in texto.Trim().Split(';'))
        {
            var igual = parte.IndexOf('=');
            var barra = parte.IndexOf('|');
            var tipo = parte[..igual];
            var limites = parte[(igual + 1)..barra].Split(',').Select(n => int.Parse(n, CultureInfo.InvariantCulture)).ToList();
            var negativoDesde = int.Parse(parte[(barra + 1)..], CultureInfo.InvariantCulture);
            if (porTipo.ContainsKey(tipo))
            {
                error = $"El tipo de cartera {tipo} está repetido en los rangos de mora.";
                return null;
            }
            if (limites[0] < 1 || limites.Zip(limites.Skip(1)).Any(p => p.Second <= p.First))
            {
                error = $"Los límites de los rangos de {tipo} van en días de mora, mayores que cero y en orden creciente.";
                return null;
            }
            if (negativoDesde < 1 || negativoDesde > limites.Count + 1)
            {
                error = $"El rango desde el que el dato es negativo para {tipo} va de 1 a {limites.Count + 1} (los {limites.Count} rangos y el último abierto).";
                return null;
            }
            porTipo[tipo] = new RangosDeMoraDeTipo(tipo, limites, negativoDesde);
        }
        return new RangosDeMoraParaCentrales(porTipo);
    }
}
