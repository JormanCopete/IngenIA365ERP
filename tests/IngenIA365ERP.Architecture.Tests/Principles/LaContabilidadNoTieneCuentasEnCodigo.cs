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
        // Feature 015 (corrida del 2026-10-26): los ejemplos de las plantillas de puesta en marcha de Recaudo —una base de caja (500000), un documento de
        // persona (1098765432) y una denominación (50000)—, que la plantilla muestra en su hoja de instrucciones. Ninguno es una cuenta.
        Path.Combine("Receipts", "Imports", "PlantillasDePuestaEnMarchaDeRecaudo.cs"),
        // Feature 016: los ejemplos de la hoja de instrucciones de las plantillas de personas y asociados: un documento (1010101010) y el
        // código DANE de una ciudad (76001). Ninguno es una cuenta.
        Path.Combine("Core", "People", "Imports", "PlantillaDePersonas.cs"),
        Path.Combine("Core", "Associates", "Imports", "PlantillaDeAsociados.cs"),
        // Feature 020: los ejemplos de las hojas de instrucciones de las plantillas de Transporte —documentos (1010101010, 2020202020), un
        // número de póliza (123456789), un número interno (0001), un modelo (2022) y un cilindraje (1400)— y el número interno de ejemplo de
        // la simulación de un conector. Ninguno es una cuenta.
        Path.Combine("Transport", "Imports", "PlantillaDeVehiculos.cs"),
        Path.Combine("Transport", "Imports", "PlantillaDeConductores.cs"),
        Path.Combine("Transport", "Imports", "PlantillaDeDocumentosDeVehiculo.cs"),
        Path.Combine("Platform", "Connectors", "PlatformConnectorsFeature.cs"),
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
