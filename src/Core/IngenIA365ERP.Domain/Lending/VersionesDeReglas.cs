using System.Text.RegularExpressions;

namespace IngenIA365ERP.Domain.Lending;

/// <summary>
/// La versión de las reglas de cálculo del motor de Cartera (feature 013, T29, R37, M8; FR-011): <c>LND-AAAA.n</c>. La sella cada
/// versión de condiciones del crédito (<c>LoanTermsVersion.RulesVersion</c>), cada simulación y cada documento emitido. Un cambio de
/// algoritmo que mueva un centavo es una versión nueva y sólo aplica a créditos o novedades nuevos; las anteriores se conservan
/// mientras haya créditos que las usen (precedente del layout versionado de la PILA). (nuevo)
/// </summary>
public static partial class VersionesDeReglas
{
    /// <summary>El prefijo de Cartera.</summary>
    public const string Prefijo = "LND-";

    /// <summary>La versión vigente en esta versión del programa: la primera de K1.</summary>
    public const string Vigente = "LND-2026.1";

    /// <summary>Las versiones que este programa sabe calcular, de la más antigua a la vigente.</summary>
    public static IReadOnlyList<string> Conocidas { get; } = [Vigente];

    /// <summary>¿Tiene la forma <c>LND-AAAA.n</c> (n desde 1)?</summary>
    public static bool TieneFormato(string? version) => version is not null && Formato().IsMatch(version);

    /// <summary>¿La sabe calcular este programa?</summary>
    public static bool EsConocida(string? version) => version is not null && Conocidas.Contains(version, StringComparer.Ordinal);

    [GeneratedRegex(@"^LND-\d{4}\.[1-9]\d*$", RegexOptions.CultureInvariant)]
    private static partial Regex Formato();
}
