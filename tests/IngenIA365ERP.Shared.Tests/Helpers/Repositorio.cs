namespace IngenIA365ERP.Shared.Tests.Helpers;

/// <summary>
/// Dónde está el código fuente de Shared, para las pruebas que leen archivos <c>.razor</c> en vez
/// de IL (qué rutas tienen <c>@page</c>, cómo pinta un componente su pie). Sube desde el directorio
/// del runner (<c>bin/Debug/net10.0/</c>) hasta encontrar <c>IngenIA365ERP.slnx</c>, igual que
/// <c>RepoPath</c> en Architecture.Tests.
/// </summary>
internal static class Repositorio
{
    public static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IngenIA365ERP.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("No se encontró IngenIA365ERP.slnx al subir desde " + AppContext.BaseDirectory);
    }

    public static string Shared() => Path.Combine(Raiz(), "src", "Presentation", "IngenIA365ERP.Shared");

    /// <summary>Todas las rutas con <c>@page</c> de <c>Shared/Pages</c>, partidas en segmentos.</summary>
    public static List<string[]> RutasConPagina()
    {
        var pagina = new System.Text.RegularExpressions.Regex(@"@page\s+""([^""]+)""");
        return Directory.EnumerateFiles(Path.Combine(Shared(), "Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(a => pagina.Matches(File.ReadAllText(a)).Select(m => m.Groups[1].Value))
            .Select(r => r.TrimEnd('/').Split('/'))
            .ToList();
    }

    /// <summary>La ruta (sin query string) coincide con alguna página, segmento a segmento; un segmento <c>{…}</c> empareja con cualquiera.</summary>
    public static bool TienePagina(List<string[]> rutas, string href)
    {
        var partes = href.Split('?')[0].TrimEnd('/').Split('/');
        if (partes.Length == 1 && partes[0].Length == 0) partes = [""];
        return rutas.Any(r => r.Length == partes.Length && r.Zip(partes).All(par => par.First == par.Second || par.First.StartsWith('{') || par.Second.StartsWith('{')));
    }
}
