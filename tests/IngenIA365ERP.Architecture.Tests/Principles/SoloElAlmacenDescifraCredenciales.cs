using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 020 (T150; R16): sólo <c>AlmacenDeCredencialesDeConector</c> crea el protector de las credenciales de los conectores (el propósito
/// <c>IngenIA365ERP.Connectors.Credential.v1</c>) y llama a <c>Unprotect</c> en el código de los conectores. Cualquier otro que las
/// descifrara podría dejarlas en un registro o en una respuesta.
/// </summary>
public class SoloElAlmacenDescifraCredenciales
{
    private const string Almacen = "Core/IngenIA365ERP.Application/Core/Connectors/AlmacenDeCredencialesDeConector.cs";

    [Fact]
    public void Nadie_mas_usa_el_proposito_de_las_credenciales_de_conectores()
    {
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        var infractores = RepoPath.ProductionCSharpFiles()
            .Select(f => (Archivo: f, Relativo: Path.GetRelativePath(src, f).Replace('\\', '/')))
            .Where(x => x.Relativo != Almacen)
            .Where(x =>
            {
                var fuente = FuenteSinComentarios.Leer(x.Archivo);
                return fuente.Contains("Connectors.Credential.v1", StringComparison.Ordinal)
                       || fuente.Contains("AlmacenDeCredencialesDeConector.Proposito", StringComparison.Ordinal);
            })
            .Select(x => x.Relativo)
            .ToList();

        Assert.True(infractores.Count == 0, "Sólo el almacén crea el protector de las credenciales de conectores:\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void En_el_codigo_de_conectores_solo_el_almacen_descifra()
    {
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        string[] carpetas =
        [
            Path.Combine(src, "Core", "IngenIA365ERP.Application", "Core", "Connectors"),
            Path.Combine(src, "Core", "IngenIA365ERP.Application", "Platform", "Connectors"),
            Path.Combine(src, "Core", "IngenIA365ERP.Application", "Transport"),
            Path.Combine(src, "Infrastructure", "IngenIA365ERP.ExternalChannels", "Connectors"),
        ];
        var infractores = carpetas.Where(Directory.Exists)
            .SelectMany(c => Directory.EnumerateFiles(c, "*.cs", SearchOption.AllDirectories))
            .Select(f => (Archivo: f, Relativo: Path.GetRelativePath(src, f).Replace('\\', '/')))
            .Where(x => x.Relativo != Almacen && FuenteSinComentarios.Leer(x.Archivo).Contains(".Unprotect(", StringComparison.Ordinal))
            .Select(x => x.Relativo)
            .ToList();

        Assert.True(infractores.Count == 0, "Descifran credenciales fuera del almacén:\n  " + string.Join("\n  ", infractores));
    }
}
