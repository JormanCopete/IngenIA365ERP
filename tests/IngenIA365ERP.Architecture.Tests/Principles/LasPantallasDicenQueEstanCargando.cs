using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Toda pantalla que espera datos lo dice: la grilla o el formulario que está cargando o
/// guardando va dentro de <c>&lt;IndicadorDeCarga&gt;</c> (docs/manual/indicador-de-carga.md).
/// Hasta el 2026-09-13 una grilla vacía y una grilla cargando se veían igual y «Guardar»
/// admitía el doble clic; se corrigió en Core y Nómina y esta prueba impide que una pantalla
/// nueva o modificada de esos módulos vuelva atrás. Cuando otro módulo se migre, se agrega
/// su carpeta a <see cref="ModulosMigrados"/> y la prueba lo cubre desde ese momento.
///
/// <para>
/// Es una prueba sobre el fuente porque el repositorio no tiene pruebas de navegador: un
/// spinner que falta no rompe ninguna prueba de unidad, sólo la experiencia de la persona
/// que mira una tabla en blanco sin saber si es que no hay datos o es que todavía no
/// llegaron.
/// </para>
/// </summary>
public class LasPantallasDicenQueEstanCargando
{
    /// <summary>Carpetas bajo <c>Shared/Pages</c> ya migradas al patrón. Se amplía módulo a módulo.</summary>
    private static readonly string[] ModulosMigrados = ["Maestros", "Nomina", "Asociados"];

    private static IEnumerable<string> PantallasMigradas()
    {
        var pages = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.Shared", "Pages");
        foreach (var modulo in ModulosMigrados)
        {
            var carpeta = Path.Combine(pages, modulo);
            Assert.True(Directory.Exists(carpeta), $"No existe {carpeta}: si el módulo se movió, actualizá ModulosMigrados.");
            foreach (var f in Directory.EnumerateFiles(carpeta, "*.razor", SearchOption.AllDirectories))
                yield return f;
        }
    }

    [Fact]
    public void Toda_pantalla_con_grilla_o_dialogo_de_edicion_lleva_IndicadorDeCarga()
    {
        var sinIndicador = new List<string>();
        var revisadas = 0;
        foreach (var archivo in PantallasMigradas())
        {
            var texto = File.ReadAllText(archivo);
            var esperaDatos = texto.Contains("<SfGrid", StringComparison.Ordinal)
                           || texto.Contains("<DialogTemplates>", StringComparison.Ordinal);
            if (!esperaDatos) continue;
            revisadas++;
            if (!texto.Contains("<IndicadorDeCarga", StringComparison.Ordinal))
                sinIndicador.Add(Path.GetRelativePath(RepoPath.FindRepoRoot(), archivo));
        }

        Assert.True(revisadas > 0, "No se encontró ninguna pantalla con grilla o diálogo en los módulos migrados: revisar ModulosMigrados.");
        Assert.True(sinIndicador.Count == 0,
            "Pantallas con grilla o diálogo de edición sin <IndicadorDeCarga> (ver docs/manual/indicador-de-carga.md):\n  " +
            string.Join("\n  ", sinIndicador));
    }

    [Fact]
    public void Ninguna_pantalla_migrada_deja_la_lista_vacia_en_silencio()
    {
        // El compañero del indicador: cuando la carga falla, la zona se destapa Y aparece el
        // aviso. Un `catch { _items = []; }` deja una tabla vacía sin explicación (Principio IX).
        var silenciosas = PantallasMigradas()
            .Where(f => File.ReadAllText(f).Contains("catch { _items = ", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(RepoPath.FindRepoRoot(), f))
            .ToList();

        Assert.True(silenciosas.Count == 0,
            "Pantallas que vacían la lista sin avisar cuando la carga falla:\n  " + string.Join("\n  ", silenciosas));
    }
}
