using System.Globalization;
using System.Text.Json;

namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Quién recogió una obligación que ya no recibe pagos y qué se hace con lo que su pagaduría gire después (feature 028, E2, T069; R7, FR-022, HU4-5), como lo
/// lee el recaudador en el <c>data</c> del rechazo <c>Lending.Payment.ObligationNotPayable</c>: el crédito nuevo (<see cref="Obligacion"/>, su número y desde
/// cuándo recibe pagos: la fecha valor de su desembolso) y la regla de <c>Recogida.GiroPosteriorDeCreditoRecogido</c> (<see cref="Regla"/>). (nuevo)
/// </summary>
public sealed record SucesorDelRechazo(Guid Obligacion, string? Numero, DateOnly? Desde, string Regla)
{
    /// <summary>Lo girado se aplica al crédito nuevo.</summary>
    public bool AlCreditoNuevo => Regla == GiroPosteriorDeCreditoRecogido.AlCreditoNuevo;

    /// <summary>Lo girado queda como saldo a favor del asociado.</summary>
    public bool AFavorDelAsociado => Regla == GiroPosteriorDeCreditoRecogido.AFavorDelAsociado;
}

/// <summary>
/// El acuerdo del contrato de obligaciones sobre lo que una pagaduría gira por un crédito <b>ya recogido</b> por el desembolso de otro (feature 028, E2, T069;
/// R7, FR-022, HU4-5): Cartera rechaza el pago con <c>Lending.Payment.ObligationNotPayable</c> y agrega a <c>data</c> el crédito nuevo y la regla de
/// <c>Recogida.GiroPosteriorDeCreditoRecogido</c>; Recaudo —que no lee parámetros de Cartera— la aplica: <see cref="SaldoDeLaPagaduria"/> (lo de siempre),
/// <see cref="AlCreditoNuevo"/> o <see cref="AFavorDelAsociado"/>. Campos nuevos y opcionales: no cambian la versión del contrato. (nuevo)
/// </summary>
public static class GiroPosteriorDeCreditoRecogido
{
    /// <summary>El código del rechazo que puede traer la regla.</summary>
    public const string CodigoNoPagable = "Lending.Payment.ObligationNotPayable";

    public const string ClaveSucesor = "successorObligationPublicId";
    public const string ClaveNumeroDelSucesor = "successorObligationNumber";
    public const string ClaveDesde = "successorFrom";
    public const string ClaveRegla = "afterPayoff";

    /// <summary>Los valores de la regla (los de <c>Recogida.GiroPosteriorDeCreditoRecogido</c>).</summary>
    public const string SaldoDeLaPagaduria = "SaldoDeLaPagaduria";

    /// <inheritdoc cref="SaldoDeLaPagaduria"/>
    public const string AlCreditoNuevo = "AlCreditoNuevo";

    /// <inheritdoc cref="SaldoDeLaPagaduria"/>
    public const string AFavorDelAsociado = "AFavorDelAsociado";

    /// <summary>
    /// Lee el sucesor y la regla del rechazo; nulo si el rechazo no es <see cref="CodigoNoPagable"/> o no trae sucesor. Admite los valores como vienen de un
    /// objeto (Guid, DateOnly, texto) o de JSON. Una regla que no conoce se lee como <see cref="SaldoDeLaPagaduria"/>.
    /// </summary>
    public static SucesorDelRechazo? Leer(string? codigo, IReadOnlyDictionary<string, object?>? datos)
    {
        if (!string.Equals(codigo, CodigoNoPagable, StringComparison.Ordinal) || datos is null) return null;
        if (!Guid.TryParse(Texto(datos.GetValueOrDefault(ClaveSucesor)), out var sucesor) || sucesor == Guid.Empty) return null;
        var regla = Texto(datos.GetValueOrDefault(ClaveRegla)) switch
        {
            AlCreditoNuevo => AlCreditoNuevo,
            AFavorDelAsociado => AFavorDelAsociado,
            _ => SaldoDeLaPagaduria,
        };
        DateOnly? desde = DateOnly.TryParse(Texto(datos.GetValueOrDefault(ClaveDesde)), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        return new SucesorDelRechazo(sucesor, Texto(datos.GetValueOrDefault(ClaveNumeroDelSucesor)), desde, regla);
    }

    /// <summary>Los datos que agrega Cartera al rechazo (con los del rechazo de siempre: el número y el estado de la obligación).</summary>
    public static IReadOnlyDictionary<string, object?> Datos(string numero, string estado, Guid sucesor, string numeroDelSucesor, DateOnly desde, string regla) =>
        new Dictionary<string, object?>
        {
            ["obligationNumber"] = numero,
            ["status"] = estado,
            [ClaveSucesor] = sucesor,
            [ClaveNumeroDelSucesor] = numeroDelSucesor,
            [ClaveDesde] = desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [ClaveRegla] = regla,
        };

    private static string? Texto(object? valor) => valor switch
    {
        null => null,
        string s => s,
        JsonElement e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString(),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString(),
    };
}

/// <summary>
/// Los valores de <c>Pagos.CuotaEnviadaAlCancelar</c> como los lee la caja en el listado del contrato (feature 028, E2, T071; FR-023, D4): <see cref="Incluir"/>
/// (de fábrica: la cancelación cobra la cuota enviada, con el aviso), <see cref="Excluir"/> (propone cancelar todo menos ella) y <see cref="Preguntar"/> (el
/// cajero elige). (nuevo)
/// </summary>
public static class CuotaEnviadaAlCancelar
{
    public const string Incluir = "Incluir";

    /// <inheritdoc cref="Incluir"/>
    public const string Excluir = "Excluir";

    /// <inheritdoc cref="Incluir"/>
    public const string Preguntar = "Preguntar";

    /// <summary>¿Con ese valor la caja ofrece dejar la cuota enviada? «Excluir» y «Preguntar».</summary>
    public static bool OfreceDejarla(string? valor) => valor is Excluir or Preguntar;
}
