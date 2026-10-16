namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>
/// Lo que el motor mira de una etapa de cobranza vigente (<c>LND_CollectionStages</c>, data-model §26.4): su código, su orden, el rango de
/// días de mora (ambos extremos incluidos; el máximo nulo = sin tope) y si es la jurídica. La preventiva admite días negativos: los días
/// que faltan para el vencimiento, con signo menos. (nuevo)
/// </summary>
public sealed record DefinicionDeEtapa(string Codigo, int Orden, int MinimoDias, int? MaximoDias, bool EsJuridica)
{
    public bool Contiene(int dias) => dias >= MinimoDias && (MaximoDias is not { } max || dias <= max);
}

/// <summary>
/// Motor puro de las etapas de cobranza (feature 013, K4, T0937; FR-117, T77): valida que las etapas vigentes cubran un rango continuo de
/// días de mora —sin cruces ni huecos, en el orden de la etapa, sólo la última sin tope— y dice qué etapa le corresponde a unos días de mora.
/// No sabe de vigencias: recibe las versiones vigentes el día que se mide. (nuevo)
/// </summary>
public static class EtapasDeCobranza
{
    /// <summary>Los rangos de las etapas vigentes se cruzan, dejan huecos o no siguen el orden (api.md §14.5).</summary>
    public const string CodigoRangosInvalidos = "Lending.CollectionStage.RangesInvalid";

    /// <summary>
    /// Nulo si las etapas forman un rango continuo; si no, el primer problema con <c>data: { kind, stage, previous }</c>: <c>Empty</c>,
    /// <c>DuplicateCode</c>, <c>Inverted</c> (máximo menor que el mínimo), <c>Overlap</c>, <c>Gap</c>, <c>OpenNotLast</c> (una etapa sin tope
    /// que no es la última) u <c>OrderMismatch</c> (el orden no sigue a los días).
    /// </summary>
    public static ProblemaDeCartera? Validar(IReadOnlyCollection<DefinicionDeEtapa> etapas)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        if (etapas.Count == 0)
            return Problema("Empty", null, null, "No hay etapas de cobranza vigentes: debe haber al menos una.");

        var repetido = etapas.GroupBy(e => e.Codigo, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            return Problema("DuplicateCode", repetido.Key, null, $"La etapa «{repetido.Key}» está dos veces entre las vigentes.");

        var invertida = etapas.FirstOrDefault(e => e.MaximoDias is { } max && max < e.MinimoDias);
        if (invertida is not null)
            return Problema("Inverted", invertida.Codigo, null,
                $"La etapa «{invertida.Codigo}» va de {invertida.MinimoDias} a {invertida.MaximoDias} días: el máximo no puede ser menor que el mínimo.");

        var ordenadas = etapas.OrderBy(e => e.MinimoDias).ThenBy(e => e.Codigo, StringComparer.Ordinal).ToList();
        for (var i = 1; i < ordenadas.Count; i++)
        {
            var anterior = ordenadas[i - 1];
            var actual = ordenadas[i];
            if (anterior.MaximoDias is not { } maxAnterior)
                return Problema("OpenNotLast", anterior.Codigo, null,
                    $"La etapa «{anterior.Codigo}» no tiene tope de días y no es la última: «{actual.Codigo}» empieza después.");
            if (actual.MinimoDias <= maxAnterior)
                return Problema("Overlap", actual.Codigo, anterior.Codigo,
                    $"La etapa «{actual.Codigo}» empieza en {actual.MinimoDias} días y «{anterior.Codigo}» llega hasta {maxAnterior}: se cruzan.");
            if (actual.MinimoDias > maxAnterior + 1)
                return Problema("Gap", actual.Codigo, anterior.Codigo,
                    $"Entre «{anterior.Codigo}» (hasta {maxAnterior} días) y «{actual.Codigo}» (desde {actual.MinimoDias}) quedan días sin etapa.");
            if (actual.Orden <= anterior.Orden)
                return Problema("OrderMismatch", actual.Codigo, anterior.Codigo,
                    $"La etapa «{actual.Codigo}» va después de «{anterior.Codigo}» en días de mora y su orden no es mayor.");
        }
        return null;
    }

    /// <summary>
    /// La etapa que contiene los <paramref name="diasDeMora"/>, o nula si caen por debajo de la primera o por encima de la última con tope.
    /// Las etapas se suponen válidas (<see cref="Validar"/>); si alguna se cruzara, gana la de menor orden.
    /// </summary>
    public static DefinicionDeEtapa? EtapaPara(IReadOnlyCollection<DefinicionDeEtapa> etapas, int diasDeMora)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        return etapas.Where(e => e.Contiene(diasDeMora)).OrderBy(e => e.Orden).FirstOrDefault();
    }

    private static ProblemaDeCartera Problema(string clase, string? etapa, string? anterior, string mensaje) =>
        new(CodigoRangosInvalidos, mensaje, new Dictionary<string, object?> { ["kind"] = clase, ["stage"] = etapa, ["previous"] = anterior });
}
