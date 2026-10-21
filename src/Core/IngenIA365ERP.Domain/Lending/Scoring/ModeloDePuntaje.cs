using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Domain.Lending.Scoring;

/// <summary>
/// Las fuentes de las variables de puntaje que el motor sabe leer de una solicitud (feature 013, FR-037; api.md §3.3). Es la
/// <b>lista cerrada</b> que publica <c>MotorDePuntaje</c> (US2, T0387) y contra la que <c>SetCreditLineScoringCommand</c> (US1, T0354)
/// valida el bloque de puntaje de una línea: una variable con otra fuente no se puede calcular y responde
/// <c>Lending.Scoring.VariableUnknown</c>. US1 la adelanta aquí para que la línea no guarde un modelo que la originación no sepa
/// evaluar; US2 la lee sin cambiarla (si suma una fuente, la suma aquí). (nuevo)
/// </summary>
public static class FuentesDePuntaje
{
    public const string EdadDelSolicitante = "Applicant.Age";
    public const string AntiguedadComoAsociado = "Applicant.AssociateSeniorityMonths";
    public const string AntiguedadLaboral = "Applicant.EmploymentSeniorityMonths";
    public const string IngresoMensual = "Applicant.MonthlyIncome";
    public const string SaldoDeAportes = "Applicant.ContributionsBalance";
    public const string CapacidadDePago = "Application.PaymentCapacityRatio";
    public const string MontoSolicitado = "Application.Amount";
    public const string PlazoEnMeses = "Application.TermMonths";
    public const string CoberturaDeGarantias = "Application.CollateralCoverage";
    public const string PuntajeDeCentrales = "Bureau.Score";
    public const string MoraMaximaEnCentrales = "Bureau.MaxDaysPastDue12M";
    public const string MoraMaximaInterna = "Internal.MaxDaysPastDue12M";
    public const string CreditosPagados = "Internal.PaidOffLoans";

    /// <summary>Las fuentes admitidas, en el orden en que la pantalla las ofrece.</summary>
    public static IReadOnlyList<string> Todas { get; } =
    [
        EdadDelSolicitante, AntiguedadComoAsociado, AntiguedadLaboral, IngresoMensual, SaldoDeAportes, CapacidadDePago, MontoSolicitado,
        PlazoEnMeses, CoberturaDeGarantias, PuntajeDeCentrales, MoraMaximaEnCentrales, MoraMaximaInterna, CreditosPagados,
    ];

    public static bool Existe(string? fuente) => fuente is not null && Todas.Contains(fuente, StringComparer.Ordinal);
}

/// <summary>
/// Un rango de una variable: desde (incluido) y hasta (excluido) —nulo = sin límite de ese lado— y los puntos que da (US2, T0387: un
/// valor igual al «hasta» cae en el rango siguiente). <see cref="NoData"/> marca el rango «sin dato», sin límites: los puntos que toma la
/// variable cuando la solicitud no trae su valor; sin ese rango, cero. No se escribe en el JSON cuando es falso, así los modelos que no lo
/// usan conservan su forma. (nuevo)
/// </summary>
public sealed record RangoDePuntaje(
    decimal? From,
    decimal? To,
    decimal Points,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool NoData = false)
{
    /// <summary>¿El valor cae en este rango? (desde incluido, hasta excluido; el rango «sin dato» no recibe valores.)</summary>
    public bool Contiene(decimal valor) => !NoData && (From is not { } desde || valor >= desde) && (To is not { } hasta || valor < hasta);
}

/// <summary>Una variable del modelo: código propio de la línea, nombre, fuente de <see cref="FuentesDePuntaje"/>, peso (fracción) y rangos. (nuevo)</summary>
public sealed record VariableDePuntaje(string Code, string Name, string Source, decimal Weight, IReadOnlyList<RangoDePuntaje> Ranges);

/// <summary>
/// El bloque de puntaje de una versión de línea (feature 013, FR-037; data-model §1.3 <c>ScoringModel</c> y <c>MinimumScore</c>;
/// api.md §3.3): las variables con su fuente, peso y rangos con puntos, y el código del nivel de excepción de la matriz de
/// atribuciones al que sube una solicitud bajo el mínimo (un puntaje bajo nunca rechaza). Se guarda como JSON canónico en
/// <c>ScoringModel</c>; el mínimo va en su columna. Puro, sin IO. (nuevo)
/// </summary>
public sealed record ModeloDePuntaje(string? ExceptionLevelCode, IReadOnlyList<VariableDePuntaje> Variables)
{
    public const string CodigoPesosInvalidos = "Lending.Scoring.WeightsInvalid";
    public const string CodigoVariableDesconocida = "Lending.Scoring.VariableUnknown";
    public const string CodigoInvalido = "Validation.Invalid";

    /// <summary>Decimales de un peso (alias <c>Fraccion</c>, 9,6).</summary>
    public const int DecimalesDelPeso = 6;

    /// <summary>Decimales de los puntos y del mínimo (alias <c>Puntaje</c>, 12,4).</summary>
    public const int DecimalesDelPuntaje = 4;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    /// <summary>
    /// Lo que está mal en el modelo, o nulo (api.md §3.3; T0336): al menos una variable; códigos no vacíos y sin repetir; cada fuente en
    /// <see cref="FuentesDePuntaje"/> (<see cref="CodigoVariableDesconocida"/>); cada peso entre 0 y 1 con hasta seis decimales y la suma
    /// exactamente 1 (<see cref="CodigoPesosInvalidos"/> con <c>reason</c>); rangos con el desde no mayor que el hasta y puntos con hasta
    /// cuatro decimales.
    /// </summary>
    public ProblemaDeCartera? Validar()
    {
        if (Variables is null || Variables.Count == 0)
            return ProblemaDeCartera.Con(CodigoInvalido, "El puntaje lleva al menos una variable.", "field", "variables");
        if (Variables.Any(v => string.IsNullOrWhiteSpace(v.Code) || string.IsNullOrWhiteSpace(v.Name)))
            return ProblemaDeCartera.Con(CodigoInvalido, "Cada variable lleva código y nombre.", "field", "variables");
        if (Variables.Select(v => v.Code.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count() != Variables.Count)
            return ProblemaDeCartera.Con(CodigoInvalido, "El código de una variable no se repite.", "field", "variables");

        var desconocida = Variables.FirstOrDefault(v => !FuentesDePuntaje.Existe(v.Source));
        if (desconocida is not null)
            return new ProblemaDeCartera(CodigoVariableDesconocida,
                $"La variable «{desconocida.Code}» usa la fuente «{desconocida.Source}», que el puntaje no sabe leer. Elija una de las fuentes de la lista.",
                new Dictionary<string, object?> { ["variable"] = desconocida.Code, ["source"] = desconocida.Source, ["allowed"] = FuentesDePuntaje.Todas });

        var fuera = Variables.FirstOrDefault(v => v.Weight <= 0m || v.Weight > 1m || decimal.Round(v.Weight, DecimalesDelPeso) != v.Weight);
        if (fuera is not null)
            return ProblemaDeCartera.Con(CodigoPesosInvalidos,
                $"El peso de «{fuera.Code}» ({fuera.Weight.ToString(CultureInfo.InvariantCulture)}) tiene que ser mayor que 0 y no pasar de 1, con hasta seis decimales.",
                "reason", "WeightOutOfRange");
        var suma = Variables.Sum(v => v.Weight);
        if (suma != 1m)
            return ProblemaDeCartera.Con(CodigoPesosInvalidos,
                $"Los pesos suman {suma.ToString(CultureInfo.InvariantCulture)}: tienen que sumar exactamente 1.", "reason", "WeightsDoNotSumOne");

        foreach (var v in Variables)
        {
            if (v.Ranges is null || v.Ranges.Count == 0)
                return ProblemaDeCartera.Con(CodigoInvalido, $"La variable «{v.Code}» lleva al menos un rango con sus puntos.", "field", "variables");
            if (v.Ranges.Any(r => r.From is { } d && r.To is { } h && d > h))
                return ProblemaDeCartera.Con(CodigoInvalido, $"Un rango de «{v.Code}» empieza después de terminar.", "field", "variables");
            if (v.Ranges.Any(r => decimal.Round(r.Points, DecimalesDelPuntaje) != r.Points))
                return ProblemaDeCartera.Con(CodigoInvalido, $"Los puntos de «{v.Code}» admiten hasta cuatro decimales.", "field", "variables");
            if (v.Ranges.Any(r => r.NoData && (r.From is not null || r.To is not null)))
                return ProblemaDeCartera.Con(CodigoInvalido, $"El rango «sin dato» de «{v.Code}» no lleva desde ni hasta.", "field", "variables");
            if (v.Ranges.Count(r => r.NoData) > 1)
                return ProblemaDeCartera.Con(CodigoInvalido, $"«{v.Code}» lleva a lo sumo un rango «sin dato».", "field", "variables");
        }
        return null;
    }

    /// <summary>El JSON canónico que guarda <c>ScoringModel</c>: variables por código, rangos en el orden dado.</summary>
    public string Escribir()
    {
        var canonico = new ModeloDePuntaje(
            string.IsNullOrWhiteSpace(ExceptionLevelCode) ? null : ExceptionLevelCode.Trim().ToUpperInvariant(),
            Variables.Select(v => new VariableDePuntaje(v.Code.Trim().ToUpperInvariant(), v.Name.Trim(), v.Source, v.Weight, v.Ranges.ToList()))
                .OrderBy(v => v.Code, StringComparer.Ordinal).ToList());
        return JsonSerializer.Serialize(canonico, Json);
    }

    /// <summary>Lee lo que escribió <see cref="Escribir"/>; nulo si la versión no tiene puntaje.</summary>
    public static ModeloDePuntaje? Leer(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ModeloDePuntaje>(json, Json);
}
