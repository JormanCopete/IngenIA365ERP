using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T18, §2.18), FR-002 y FR-008: las entidades que son
/// <b>hechos</b> (<c>IHechoInmutable</c>: línea de kardex, consumo de capa, mensaje, dependencia,
/// intento, decisión de aprobación, <c>InventoryPosting</c>, instantánea de tercero, línea de
/// impuesto, versión y transmisión electrónica, ancla) no cambian después de insertarse. En el
/// fuente eso se ve en que no tienen <c>set</c> público: sólo <c>init</c> o <c>private set</c>. En
/// ejecución lo hace cumplir <c>ApplicationDbContext.SaveChangesAsync</c>.
///
/// <para>
/// Esqueleto del Setup: <see cref="Hechos"/> (nombres de tipo) empieza vacía y la prueba afirma la
/// regla sobre cada elemento; con la lista vacía pasa porque no hay nada que violar, no por un
/// <c>return</c> temprano. La llena el bloque que crea cada entidad (plataforma, fase 2; base de
/// inventario, fase 3; y las historias que agregan hechos).
/// </para>
/// </summary>
public class LosHechosInmutablesNoSeModifican
{
    /// <summary>Nombres de tipo de las entidades que son hechos inmutables. Los agrega el bloque que las crea.</summary>
    private static readonly string[] Hechos = [];

    private static readonly Regex SetPublico = new(@"public\s+[^;{=]+\{\s*get;\s*set;", RegexOptions.Compiled);

    [Fact]
    public void Ningun_hecho_expone_un_set_publico()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var hecho in Hechos)
        {
            var declaracion = new Regex($@"\bclass\s+{Regex.Escape(hecho)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto));

            if (archivo is null)
                infractores.Add($"{hecho}: no se encontró su declaración (si se renombró, actualizá Hechos)");
            else if (SetPublico.IsMatch(texto))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {hecho} tiene una propiedad con set público");
        }

        Assert.True(infractores.Count == 0,
            "Hechos inmutables que se pueden modificar (FR-002, Principio XI):\n  " + string.Join("\n  ", infractores));
    }
}
