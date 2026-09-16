using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 009 (R9, FR-016): las cuentas contables son parametrización de cada cooperativa, no
/// literales del código. Hasta la 009 el desembolso y el recaudo de cartera acreditaban una
/// <c>"11100501"</c> escrita en el handler, que en una cooperativa con otro plan sencillamente
/// no existía y el asiento se guardaba cojo sin avisar. Un literal de 4 a 12 dígitos en
/// <c>Application</c> es, casi seguro, una cuenta escondida; si alguna vez es otra cosa, va a la
/// lista explícita con su porqué.
/// </summary>
public class LaContabilidadNoTieneCuentasEnCodigo
{
    private static readonly Regex Literal = new(@"""\d{4,12}""", RegexOptions.Compiled);

    /// <summary>Rutas relativas a <c>src/Core/IngenIA365ERP.Application</c> con un literal numérico que NO es una cuenta.</summary>
    private static readonly HashSet<string> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    [Fact]
    public void Ningun_handler_escribe_una_cuenta_contable_como_literal()
    {
        var aplicacion = Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application");
        var infractores = Directory.EnumerateFiles(aplicacion, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(f => (Relativa: Path.GetRelativePath(aplicacion, f), Texto: File.ReadAllText(f)))
            .Where(x => !Permitidos.Contains(x.Relativa))
            .SelectMany(x => Literal.Matches(x.Texto).Select(m => $"{x.Relativa}: {m.Value}"))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Literales de 4 a 12 dígitos en Application (¿una cuenta contable en código?). Las cuentas " +
            "salen de la parametrización y se resuelven con AccountEligibility (feature 009, FR-016):\n  " +
            string.Join("\n  ", infractores));
    }
}
