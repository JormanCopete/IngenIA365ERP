using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Cómo trata la retención en la fuente al titular (FR-021; mensajes.md §3, <c>WithholdingDetailV1.holderTaxProfile</c>): retenible, no contribuyente
/// (art. 23 ET, incluidos los fondos de empleados), autorretenedor o sin perfil tributario registrado (se retiene, como a un retenible). Se guarda como
/// texto: nunca cambia de nombre. (nuevo)
/// </summary>
public enum PerfilDeRetencion
{
    Standard = 1,
    NonTaxpayer = 2,
    SelfWithholder = 3,
    NoProfile = 4,
}

/// <summary>
/// La política de retención que rige al abonar o pagar intereses (research R06; parámetros <c>Retencion.*</c> con vigencia y los valores nacionales): la
/// tarifa de la clase de la línea (fracción), el umbral diario en UVT y la UVT del año, a qué clases alcanza el umbral, si se descuenta el componente
/// inflacionario y su porcentaje publicado (nulo = no se conoce: no se descuenta) y la unidad de redondeo de la retención (el peso por defecto). (nuevo)
/// </summary>
public sealed record PoliticaDeRetencion(
    decimal Tarifa,
    decimal UmbralDiarioUvt,
    decimal Uvt,
    WithholdingThresholdScope Alcance,
    bool ComponenteInflacionarioAplica,
    decimal? ComponenteInflacionario,
    decimal UnidadDeRedondeo)
{
    /// <summary>El umbral diario en pesos: <c>UmbralDiarioUvt × UVT</c>, a dos decimales (0,055 × 52.374 = 2.880,57).</summary>
    public decimal UmbralEnPesos => Math.Round(UmbralDiarioUvt * Uvt, 2, MidpointRounding.AwayFromZero);

    /// <summary>¿Aplica el umbral a la clase? <c>AllDepositClasses</c>: a todas; <c>ExceptTermDeposit</c>: a todas menos el CDAT; <c>None</c>: a ninguna.</summary>
    public bool UmbralAplicaA(DepositClass clase) => Alcance switch
    {
        WithholdingThresholdScope.AllDepositClasses => true,
        WithholdingThresholdScope.ExceptTermDeposit => clase != DepositClass.TermDeposit,
        _ => false,
    };
}

/// <summary>El interés de un día del período (el causado a seis decimales), para medir el umbral diario. (nuevo)</summary>
public sealed record InteresDiarioDeRetencion(DateOnly Dia, decimal Interes);

/// <summary>
/// Lo que se retiene al abonar o pagar un interés (FR-021, FR-063): el titular con su perfil, si es persona natural no obligada a llevar contabilidad (a
/// quien se descuenta el componente inflacionario, P27), la clase de la línea, el interés bruto que se abona (redondeado) y el interés de cada día del
/// período. (nuevo)
/// </summary>
public sealed record EntradaDeRetencion(
    PerfilDeRetencion Perfil,
    bool PersonaNaturalNoObligada,
    DepositClass Clase,
    decimal InteresBruto,
    IReadOnlyList<InteresDiarioDeRetencion> InteresesDiarios);

/// <summary>
/// La retención calculada (data-model §4.5; mensajes.md §3, <c>WithholdingDetailV1</c>): la base (el interés bruto), el umbral en pesos y si algún día lo
/// alcanzó (con los días), el componente inflacionario descontado, la base gravable, la tarifa, el valor, por qué no se retuvo si no se retuvo
/// (<c>NonTaxpayer</c>, <c>SelfWithholder</c>, <c>BelowThreshold</c>, <c>ZeroRate</c>) y su explicación. (nuevo)
/// </summary>
public sealed record RetencionCalculada(
    decimal Base,
    decimal? UmbralDiario,
    bool UmbralAplica,
    bool UmbralAlcanzado,
    IReadOnlyList<DateOnly> DiasQueAlcanzan,
    decimal? ComponenteInflacionario,
    decimal ComponenteInflacionarioValor,
    decimal BaseGravable,
    decimal Tarifa,
    decimal Valor,
    string? MotivoDeExencion,
    string Explicacion)
{
    /// <summary>¿Se retuvo algo?</summary>
    public bool SeRetiene => Valor > 0m;
}

/// <summary>
/// La retención en la fuente sobre los rendimientos de una cuenta de captación (feature 014, US5, T128; research R06, D10, P2, P27; FR-021, FR-063): pura y
/// decimal. El único que la calcula (<c>LaRetencionYElGmfSoloLosCalculanSusMotores</c>); el registro sólo escribe lo que devuelve, en pareja.
/// <list type="number">
/// <item><b>Perfil</b> (012): no contribuyente o autorretenedor, cero.</item>
/// <item><b>Umbral diario</b> del sector solidario (DUR 1625 de 2016, art. 1.2.4.2.6): si el umbral alcanza a la clase y <b>ningún</b> día del período causó
/// <c>UmbralDiarioUvt × UVT</c> o más (0,055 × 52.374 = 2.880,57 en 2026), cero; si alguno lo alcanzó, la base es el interés <b>total</b> del período.</item>
/// <item><b>Componente inflacionario</b> (ET arts. 38 a 41; DUR 1625 art. 1.2.1.12.6; para el año gravable 2025, 55,43 % por el Decreto 898 de 2026): si la
/// política lo descuenta y el titular es persona natural no obligada a llevar contabilidad, se descuenta del interés su porcentaje publicado.</item>
/// <item><b>Tarifa</b> de la clase de la línea (7 % ahorro, 4 % CDAT por el DUR 1625 art. 1.2.4.2.83 con el Decreto 572 de 2025): sobre la base gravable.</item>
/// </list>
/// El componente y la retención se redondean a la unidad de la política (al peso por defecto, mitad hacia arriba): sobre 488.088 con 55,43 % la base es
/// 217.541 y la retención al 4 %, 8.702 (investigación §3.6). Sin valores legales en el código: todo llega en la política. (nuevo)
/// </summary>
public static class RetencionSobreRendimientos
{
    public const string MotivoNoContribuyente = "NonTaxpayer";
    public const string MotivoAutorretenedor = "SelfWithholder";
    public const string MotivoBajoElUmbral = "BelowThreshold";
    public const string MotivoTarifaCero = "ZeroRate";
    public const string MotivoSinInteres = "NoInterest";

    public static RetencionCalculada Calcular(PoliticaDeRetencion politica, EntradaDeRetencion entrada)
    {
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentNullException.ThrowIfNull(entrada);
        var bruto = entrada.InteresBruto;
        var aplica = politica.UmbralAplicaA(entrada.Clase);
        decimal? umbral = aplica ? politica.UmbralEnPesos : null;
        var alcanzan = aplica
            ? entrada.InteresesDiarios.Where(d => d.Interes >= politica.UmbralEnPesos).Select(d => d.Dia).OrderBy(d => d).ToList()
            : [];

        RetencionCalculada Cero(string motivo, string explicacion) =>
            new(bruto, umbral, aplica, alcanzan.Count > 0, alcanzan, null, 0m, 0m, politica.Tarifa, 0m, motivo, explicacion);

        if (bruto <= 0m) return Cero(MotivoSinInteres, "Sin interés que abonar: no hay retención.");
        if (entrada.Perfil == PerfilDeRetencion.NonTaxpayer)
            return Cero(MotivoNoContribuyente, "El titular es no contribuyente según su perfil tributario: no se le retiene.");
        if (entrada.Perfil == PerfilDeRetencion.SelfWithholder)
            return Cero(MotivoAutorretenedor, "El titular es autorretenedor según su perfil tributario: no se le retiene.");
        if (aplica && alcanzan.Count == 0)
            return Cero(MotivoBajoElUmbral, string.Create(CultureInfo.InvariantCulture,
                $"Ningún día del período causó el umbral diario ({politica.UmbralDiarioUvt:0.####} UVT × {politica.Uvt:0.##} = {politica.UmbralEnPesos:0.##}): no se retiene."));
        if (politica.Tarifa <= 0m) return Cero(MotivoTarifaCero, "La tarifa de retención de la clase es cero: no se retiene.");

        decimal? componente = null;
        var componenteValor = 0m;
        if (politica.ComponenteInflacionarioAplica && entrada.PersonaNaturalNoObligada && politica.ComponenteInflacionario is { } fraccion and > 0m)
        {
            componente = fraccion;
            componenteValor = Redondear(bruto * fraccion, politica.UnidadDeRedondeo);
            if (componenteValor > bruto) componenteValor = bruto;
        }
        var gravable = bruto - componenteValor;
        var valor = Redondear(gravable * politica.Tarifa, politica.UnidadDeRedondeo);
        if (valor > bruto) valor = bruto;

        var porUmbral = aplica
            ? string.Create(CultureInfo.InvariantCulture, $"El {alcanzan[0]:yyyy-MM-dd} el interés diario alcanzó el umbral ({politica.UmbralEnPesos:0.##}): se retiene sobre el total. ")
            : "El umbral diario no alcanza a esta clase: se retiene sobre el total. ";
        var porComponente = componente is { } c
            ? string.Create(CultureInfo.InvariantCulture, $"Se descuenta el componente inflacionario ({c:0.####}): {componenteValor:0.##}. ")
            : string.Empty;
        return new RetencionCalculada(bruto, umbral, aplica, alcanzan.Count > 0, alcanzan, componente, componenteValor, gravable, politica.Tarifa, valor, null,
            porUmbral + porComponente + string.Create(CultureInfo.InvariantCulture,
                $"Base gravable {gravable:0.##} × tarifa {politica.Tarifa:0.######} = {valor:0.##}."));
    }

    /// <summary>A la unidad de la política (el peso o el centavo), mitad hacia arriba.</summary>
    public static decimal Redondear(decimal valor, decimal unidad) =>
        RedondeoDeCartera.AUnidad(valor, unidad <= 0m ? 1m : unidad, RoundingMode.AwayFromZero);
}
