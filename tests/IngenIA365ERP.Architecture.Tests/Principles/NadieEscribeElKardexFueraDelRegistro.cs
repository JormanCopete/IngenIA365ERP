using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T18, §2.18), FR-002: el kardex es sólo de adición y
/// tiene <b>un solo escritor</b>, <c>RegistroDeKardex</c>, que inserta la línea y actualiza en la
/// misma transacción las proyecciones (existencias, detalle, estado de costo, capas). Otro escritor
/// descuadraría las sumas del kardex contra sus proyecciones, que es lo que vigila la verificación
/// de integridad.
///
/// <para>
/// Esqueleto del Setup: <see cref="TiposDelKardex"/> (entidades que sólo el registro crea) empieza
/// vacía y la prueba afirma la regla sobre cada elemento; con la lista vacía pasa porque no hay nada
/// que violar, no por un <c>return</c> temprano. La llena la fase 3 (base de inventario) al crear
/// <c>KardexEntry</c> y las proyecciones; <see cref="EscritoresAutorizados"/> nombra los archivos
/// que sí pueden crearlas.
/// </para>
/// </summary>
public class NadieEscribeElKardexFueraDelRegistro
{
    /// <summary>Entidades del kardex y sus proyecciones. Las agrega la base de inventario.</summary>
    private static readonly string[] TiposDelKardex = [];

    /// <summary>Nombres de archivo (sin ruta) que pueden instanciarlas: el registro y la reconstrucción.</summary>
    private static readonly string[] EscritoresAutorizados = ["RegistroDeKardex.cs"];

    [Fact]
    public void Solo_el_registro_crea_lineas_del_kardex()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var tipo in TiposDelKardex)
        {
            var creacion = new Regex($@"\bnew\s+{Regex.Escape(tipo)}\s*[({{]", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                if (EscritoresAutorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (creacion.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: crea {tipo} fuera de RegistroDeKardex");
            }
        }

        Assert.True(infractores.Count == 0,
            "Escritores del kardex fuera del registro (FR-002, T18):\n  " + string.Join("\n  ", infractores));
    }
}
