using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T35, §2.18), FR-009: toda consulta de inventario
/// filtra por el alcance de bodega y punto de venta de quien pregunta (<c>IAlcanceDeInventario</c> y
/// <c>FiltroDeAlcance</c>); falla cerrado salvo el permiso de alcance total. Una consulta sin filtro
/// muestra existencias y documentos de bodegas ajenas.
///
/// <para>
/// Esqueleto del Setup: <see cref="ConsultasDeInventario"/> (nombres de tipo de los handlers de
/// consulta) empieza vacía y la prueba afirma la regla sobre cada elemento; con la lista vacía pasa
/// porque no hay nada que violar, no por un <c>return</c> temprano. La llena cada historia dueña de
/// sus consultas (fase 3 en adelante, empezando por US1).
/// </para>
/// </summary>
public class LasConsultasDeInventarioRespetanElAlcance
{
    /// <summary>Nombres de tipo de los handlers de consulta de inventario. Los agrega cada historia.</summary>
    private static readonly string[] ConsultasDeInventario = [];

    [Fact]
    public void Cada_consulta_de_inventario_aplica_el_alcance()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var consulta in ConsultasDeInventario)
        {
            var declaracion = new Regex($@"\b(record|class)\s+{Regex.Escape(consulta)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto));

            if (archivo is null)
                infractores.Add($"{consulta}: no se encontró su declaración (si se renombró, actualizá ConsultasDeInventario)");
            else if (!texto.Contains("IAlcanceDeInventario", StringComparison.Ordinal))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {consulta} no aplica IAlcanceDeInventario");
        }

        Assert.True(infractores.Count == 0,
            "Consultas de inventario sin alcance de bodega o punto (T35):\n  " + string.Join("\n  ", infractores));
    }
}
