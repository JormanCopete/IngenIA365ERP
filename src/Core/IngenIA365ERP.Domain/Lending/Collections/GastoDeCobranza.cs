using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>
/// La regla del gasto de cobranza de una etapa (<c>LND_CollectionStages.CollectionFeeRuleJson</c>, data-model §26.4; FR-121, T77; api.md §14.5
/// <c>feeRule { base, rate, cap, moment }</c>): sobre qué se calcula (<see cref="Base"/>), la tarifa como fracción (<see cref="Rate"/>), el tope en
/// pesos (<see cref="Cap"/>, opcional) y cuándo se propone (<see cref="Moment"/>). Ningún número de la ley está aquí: base, tarifa, tope y momento
/// los pone la cooperativa por etapa. (nuevo)
/// </summary>
public sealed record ReglaDeGastoDeCobranza
{
    /// <summary>El gasto se calcula sobre todo lo vencido (capital, intereses y cargos exigibles).</summary>
    public const string BaseValorVencido = "PastDueAmount";

    /// <summary>El gasto se calcula sobre el capital vencido.</summary>
    public const string BaseCapitalVencido = "PastDuePrincipal";

    /// <summary>Se propone una vez, el día en que el crédito entra a la etapa.</summary>
    public const string MomentoAlEntrar = "OnStageEntry";

    public static IReadOnlyList<string> Bases { get; } = [BaseValorVencido, BaseCapitalVencido];

    public static IReadOnlyList<string> Momentos { get; } = [MomentoAlEntrar];

    /// <summary>El gasto no es una regla válida (api.md §14.5): base o momento desconocidos, tarifa fuera de (0, 1], tope no positivo.</summary>
    public const string CodigoReglaInvalida = "Lending.CollectionStage.FeeRuleInvalid";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public string Base { get; init; } = BaseValorVencido;

    public decimal Rate { get; init; }

    public decimal? Cap { get; init; }

    public string Moment { get; init; } = MomentoAlEntrar;

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>La regla guardada, o nula si no hay (o el texto no es una regla).</summary>
    public static ReglaDeGastoDeCobranza? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<ReglaDeGastoDeCobranza>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Nulo si la regla sirve; si no, el problema con <c>data.field</c>.</summary>
    public ProblemaDeCartera? Validar(string etapa)
    {
        if (!Bases.Contains(Base, StringComparer.Ordinal))
            return ProblemaDeCartera.Con(CodigoReglaInvalida, $"El gasto de la etapa «{etapa}» se calcula sobre lo vencido o sobre el capital vencido.", "base", Base);
        if (Rate <= 0m || Rate > 1m)
            return ProblemaDeCartera.Con(CodigoReglaInvalida, $"La tarifa del gasto de la etapa «{etapa}» es una fracción mayor que cero y hasta uno.", "rate", Rate);
        if (Cap is { } tope && tope <= 0m)
            return ProblemaDeCartera.Con(CodigoReglaInvalida, $"El tope del gasto de la etapa «{etapa}» es mayor que cero.", "cap", tope);
        if (!Momentos.Contains(Moment, StringComparer.Ordinal))
            return ProblemaDeCartera.Con(CodigoReglaInvalida, $"El gasto de la etapa «{etapa}» se propone al entrar a la etapa.", "moment", Moment);
        return null;
    }

    /// <summary>
    /// El gasto para una base en pesos: base × tarifa, al centavo (hacia arriba sólo desde la mitad), sin pasar del tope. Cero si la base no es
    /// positiva.
    /// </summary>
    public decimal Calcular(decimal valorVencido, decimal capitalVencido)
    {
        var baseDelGasto = Base == BaseCapitalVencido ? capitalVencido : valorVencido;
        if (baseDelGasto <= 0m) return 0m;
        var gasto = decimal.Round(baseDelGasto * Rate, 2, MidpointRounding.AwayFromZero);
        return Cap is { } tope && gasto > tope ? tope : gasto;
    }
}
