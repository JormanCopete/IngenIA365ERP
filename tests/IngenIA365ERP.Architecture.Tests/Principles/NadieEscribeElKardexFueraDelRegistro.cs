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
/// <see cref="TiposDelKardex"/> son el hecho y sus tres proyecciones (US2, T245); <see cref="EscritoresAutorizados"/> los
/// archivos que pueden crearlos o modificarlos: <c>RegistroDeKardex</c> y la reconstrucción
/// (<c>RebuildInventoryProjectionsCommand</c>). Tres reglas: nadie más los crea (<c>new</c>), nadie más asigna las propiedades
/// propias de una proyección (<c>.Physical =</c>, <c>.Reserved =</c>, <c>.LastMovementDate =</c>, <c>.AverageCost =</c>,
/// <c>.LastUnitCost =</c>) y nadie los borra (<c>Remove</c>, <c>RemoveRange</c>, <c>ExecuteDelete</c> sobre sus conjuntos).
/// </para>
/// </summary>
public class NadieEscribeElKardexFueraDelRegistro
{
    /// <summary>Entidades del kardex y sus proyecciones (US2, T249).</summary>
    private static readonly string[] TiposDelKardex = ["KardexEntry", "StockBalance", "StockDetail", "CostState"];

    /// <summary>Nombres de archivo (sin ruta) que pueden instanciarlas y modificarlas: el registro y la reconstrucción.</summary>
    private static readonly string[] EscritoresAutorizados = ["RegistroDeKardex.cs", "RebuildInventoryProjectionsCommand.cs"];

    /// <summary>Los conjuntos del contexto que las guardan.</summary>
    private static readonly string[] Conjuntos = ["KardexEntries", "StockBalances", "StockDetails", "CostStates"];

    /// <summary>Asignaciones a propiedades que sólo tienen las proyecciones (acceso por miembro: no atrapa inicializadores de DTO).</summary>
    private static readonly Regex AsignacionDeProyeccion =
        new(@"\.\s*(Physical|Reserved|LastMovementDate|AverageCost|LastUnitCost)\s*(\+|-)?=(?!=)", RegexOptions.Compiled);

    [Fact]
    public void Solo_el_registro_y_la_reconstruccion_modifican_las_proyecciones()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => !EscritoresAutorizados.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Application{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || f.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Persistence{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => AsignacionDeProyeccion.IsMatch(File.ReadAllText(f)))
            .Select(f => $"{Path.GetRelativePath(root, f)}: modifica una proyección del kardex")
            .ToList();

        Assert.True(infractores.Count == 0,
            "Proyecciones modificadas fuera de RegistroDeKardex y la reconstrucción (FR-002, T18):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Nadie_borra_el_kardex_ni_sus_proyecciones()
    {
        var root = RepoPath.FindRepoRoot();
        var borrado = new Regex($@"\.({string.Join("|", Conjuntos)})\s*\.\s*(Remove|RemoveRange|ExecuteDelete|ExecuteDeleteAsync)", RegexOptions.Compiled);
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => borrado.IsMatch(File.ReadAllText(f)))
            .Select(f => $"{Path.GetRelativePath(root, f)}: borra filas del kardex o de sus proyecciones")
            .ToList();

        Assert.True(infractores.Count == 0, "El kardex sólo crece y las proyecciones nunca se dan de baja (FR-002):\n  " + string.Join("\n  ", infractores));
    }

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
