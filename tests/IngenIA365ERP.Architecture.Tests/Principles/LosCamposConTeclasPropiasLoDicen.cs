using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Xunit;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 022 (T055; FR-001, FR-003): el Enter que avanza de campo (teclado.js) no debe pisar a un campo que maneja sus propias
/// teclas —un buscador que elige con Enter, un lector de códigos, una búsqueda que corre con Enter en keyup—. Todo elemento con
/// <c>@onkeydown</c>, <c>@onkeyup</c> o <c>@onkeypress</c> lo dice con <c>data-enter="propio"</c> (usa Enter) o
/// <c>data-enter="avanza"</c> (tiene teclas propias pero deja avanzar). El JavaScript también mira si el elemento tiene un
/// manejador de Blazor, pero eso es una defensa: la marca es el contrato, y esta prueba impide olvidarla.
/// </summary>
public class LosCamposConTeclasPropiasLoDicen
{
    private static readonly Regex Manejador = new(@"@onkey(down|up|press)=", RegexOptions.Compiled);
    private static readonly Regex InicioDeEtiqueta = new(@"<[A-Za-z]", RegexOptions.Compiled);

    [Fact]
    public void Todo_elemento_con_teclas_propias_declara_data_enter()
    {
        var shared = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared");
        var faltan = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(shared, "*.razor", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") || archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            var fuente = File.ReadAllText(archivo);
            foreach (Match m in Manejador.Matches(fuente))
            {
                // La etiqueta va del último «<letra» antes del manejador hasta el siguiente «<letra» (o 600 caracteres).
                var inicio = InicioDeEtiqueta.Matches(fuente[..m.Index]).LastOrDefault()?.Index ?? 0;
                var siguiente = InicioDeEtiqueta.Match(fuente, m.Index);
                var fin = Math.Min(siguiente.Success ? siguiente.Index : fuente.Length, m.Index + 600);
                if (!fuente[inicio..fin].Contains("data-enter=", StringComparison.Ordinal))
                {
                    var linea = fuente[..m.Index].Count(c => c == '\n') + 1;
                    faltan.Add($"{Path.GetRelativePath(shared, archivo)}:{linea}");
                }
            }
        }

        faltan.Should().BeEmpty("un elemento con @onkeydown/@onkeyup/@onkeypress tiene que decir data-enter=\"propio\" o \"avanza\" (teclado.js)");
    }
}
