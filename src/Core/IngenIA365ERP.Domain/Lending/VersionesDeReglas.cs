using System.Text.RegularExpressions;

namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// La versión de las reglas de cálculo del motor de Cartera (feature 013, T29, R37, M8; FR-011): <c>LND-AAAA.n</c>. La sella cada
/// versión de condiciones del crédito (<c>LoanTermsVersion.RulesVersion</c>), cada simulación y cada documento emitido. Un cambio de
/// algoritmo que mueva un centavo es una versión nueva y sólo aplica a créditos o novedades nuevos; las anteriores se conservan
/// mientras haya créditos que las usen (precedente del layout versionado de la PILA). (nuevo)
///
/// <para>
/// US4 (T0484) suma el <b>despacho</b>: la reconstrucción de un crédito vivo pide a <see cref="Para"/> las reglas de SU versión sellada,
/// no las vigentes del programa; una versión que el programa no sabe calcular se rechaza con <see cref="CodigoVersionDesconocida"/> en
/// vez de calcularse con otras reglas.
/// </para>
/// </summary>
public static partial class VersionesDeReglas
{
    /// <summary>El prefijo de Cartera.</summary>
    public const string Prefijo = "LND-";

    /// <summary>La versión vigente en esta versión del programa para todo crédito que no es de vivienda: la primera de K1.</summary>
    public const string Vigente = "LND-2026.1";

    /// <summary>
    /// K6b (T1053; FR-024, D18): la versión de las reglas de <b>vivienda</b> con la Ley 546 de 1999 —la siguiente a la de K1—. La sella todo
    /// crédito de modalidad <c>VIVIENDA</c> (en pesos o en UVR) originado o migrado desde K6b; los créditos de K1 conservan la suya.
    /// </summary>
    public const string Vivienda = "LND-2026.2";

    /// <summary>La versión sellada en un crédito no es una que este programa sepa calcular.</summary>
    public const string CodigoVersionDesconocida = "Lending.Rules.UnknownVersion";

    /// <summary>Las reglas de <c>LND-2026.1</c> (K1).</summary>
    public static ReglasDeCalculo K1 { get; } = new(
        Vigente,
        "Causación por diferencia de acumulados con ventana compuesta o lineal que se reinicia al cambiar la base, la tasa o el "
        + "período (cada vencimiento del plan y, después del último, cada mes); gracia total sin interés sobre interés; mora = factor × "
        + "corriente pactada antes de topar; saldo vencido de la cuota sin su mora para la tolerancia.");

    /// <summary>
    /// Las reglas de <c>LND-2026.2</c> (K6b, vivienda): las de K1 más la Ley 546 de 1999 —tasa fija salvo pacto de rebaja, interés cobrado vencido
    /// y sin capitalizar, sin mora sobre intereses aunque <c>Mora.SobreIntereses</c> esté encendido, el tope de vivienda del día además de la usura
    /// (rige el menor) y prepago sin penalidad—. Un crédito en UVR lleva plan y saldos en UVR con la serie diaria (ver
    /// <c>Housing/ReconstruccionEnUvr</c>).
    /// </summary>
    public static ReglasDeCalculo K6bVivienda { get; } = new(
        Vivienda,
        "Las de LND-2026.1 con la Ley 546 de 1999: tasa fija; interés vencido sin capitalizar; sin mora sobre intereses; tope de vivienda "
        + "del día además de la usura (rige el menor); prepago sin penalidad; en UVR, plan y saldos en UVR convertidos a pesos con la UVR del día.")
    {
        DeVivienda = true,
    };

    /// <summary>Las versiones que este programa sabe calcular, de la más antigua a la más nueva.</summary>
    public static IReadOnlyList<string> Conocidas { get; } = [Vigente, Vivienda];

    private static readonly IReadOnlyDictionary<string, ReglasDeCalculo> PorVersion =
        new Dictionary<string, ReglasDeCalculo>(StringComparer.Ordinal) { [Vigente] = K1, [Vivienda] = K6bVivienda };

    /// <summary>La versión que sella un crédito o una simulación nuevos: la de vivienda para la modalidad de vivienda; si no, la vigente.</summary>
    public static string ParaCreditoNuevo(bool esVivienda) => esVivienda ? Vivienda : Vigente;

    /// <summary>¿La versión es la de vivienda (sus reglas lo dicen)?</summary>
    public static bool EsDeVivienda(string? version) =>
        version is not null && PorVersion.TryGetValue(version, out var reglas) && reglas.DeVivienda;

    /// <summary>¿Tiene la forma <c>LND-AAAA.n</c> (n desde 1)?</summary>
    public static bool TieneFormato(string? version) => version is not null && Formato().IsMatch(version);

    /// <summary>¿La sabe calcular este programa?</summary>
    public static bool EsConocida(string? version) => version is not null && Conocidas.Contains(version, StringComparer.Ordinal);

    /// <summary>
    /// Las reglas con que se calcula un crédito sellado con <paramref name="version"/> (T29). Una versión desconocida no se calcula con
    /// otra: lanza <see cref="ReglaDeCarteraException"/> con <see cref="CodigoVersionDesconocida"/>.
    /// </summary>
    public static ReglasDeCalculo Para(string? version)
    {
        if (version is not null && PorVersion.TryGetValue(version, out var reglas)) return reglas;
        throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoVersionDesconocida,
            $"El crédito está sellado con las reglas «{version}» y este programa sólo sabe calcular {string.Join(", ", Conocidas)}.",
            "rulesVersion", version));
    }

    [GeneratedRegex(@"^LND-\d{4}\.[1-9]\d*$", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}

/// <summary>
/// Las reglas de cálculo de una <c>RulesVersion</c> (T29): su nombre y en palabras lo que fija. Una versión nueva agrega su instancia en
/// <see cref="VersionesDeReglas"/> y las ramas que la distinguen en el motor. (nuevo)
/// </summary>
public sealed record ReglasDeCalculo(string Version, string Descripcion)
{
    /// <summary>
    /// K6b (T1053): las reglas de vivienda de la Ley 546 de 1999. El motor no causa mora sobre intereses aunque la condición sellada lo diga, topa
    /// con el menor entre la usura y el tope de vivienda del día, y el prepago no lleva penalidad.
    /// </summary>
    public bool DeVivienda { get; init; }

    /// <summary>¿Se puede causar mora sobre los intereses vencidos? Nunca en vivienda (FR-063, D14).</summary>
    public bool AdmiteMoraSobreIntereses => !DeVivienda;

    /// <summary>¿El prepago va siempre sin penalidad (Ley 546 de 1999)? Sólo vivienda; los demás, según el tope legal (Ley 1555 de 2012).</summary>
    public bool PrepagoSinPenalidad => DeVivienda;
}
