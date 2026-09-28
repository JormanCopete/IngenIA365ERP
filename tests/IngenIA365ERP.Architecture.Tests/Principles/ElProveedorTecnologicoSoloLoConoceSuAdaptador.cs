using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 y T668 (decisiones-transversales T40, §2.18; contracts/dian.md §2), FR-063: el proveedor tecnológico de
/// facturación electrónica vive detrás del puerto <c>ICanalDeEmisionElectronica</c>. Domain y Application no referencian
/// <c>IngenIA365ERP.ElectronicInvoicing</c> ni el SDK de ningún proveedor; la API lo referencia sólo como raíz de composición:
/// únicamente <c>Program.cs</c> llama <c>AddElectronicInvoicing(</c> y nombra el espacio <c>IngenIA365ERP.ElectronicInvoicing.</c>.
/// Cambiar de proveedor es cambiar el adaptador.
///
/// <para>
/// <see cref="SdksDelProveedor"/> está vacía hasta que se contrate el proveedor (A3): el adaptador real (T732) agrega aquí el
/// espacio de nombres de su SDK, si lo trae.
/// </para>
/// </summary>
public class ElProveedorTecnologicoSoloLoConoceSuAdaptador
{
    /// <summary>El ensamblado de los adaptadores (<c>src/Infrastructure/IngenIA365ERP.ElectronicInvoicing</c>).</summary>
    private const string EnsambladoDeAdaptadores = "IngenIA365ERP.ElectronicInvoicing";

    /// <summary>Espacios de nombres (y ensamblados) del SDK del proveedor. Vacía hasta A3.</summary>
    private static readonly string[] SdksDelProveedor = [];

    private static IEnumerable<string> EspaciosDelProveedor => new[] { EnsambladoDeAdaptadores }.Concat(SdksDelProveedor);

    [Fact]
    public void El_ensamblado_de_adaptadores_es_el_que_se_vigila()
    {
        // Si el proyecto se renombra, las reglas de abajo dejarían de mirar nada: esto lo ata al tipo real.
        Assert.Equal(EnsambladoDeAdaptadores, typeof(IngenIA365ERP.ElectronicInvoicing.Channels.Simulado.CanalSimulado).Assembly.GetName().Name);
    }

    [Fact]
    public void Ni_Domain_ni_Application_referencian_el_adaptador_ni_un_SDK_de_proveedor()
    {
        var internas = new[]
        {
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.ElectronicInvoicing.Channels.ICanalDeEmisionElectronica).Assembly,
        };

        var infractores = internas
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => new { Capa = a.GetName().Name, Ref = r.Name ?? string.Empty }))
            .Where(x => EspaciosDelProveedor.Any(e => x.Ref.Equals(e, StringComparison.OrdinalIgnoreCase)
                                                   || x.Ref.StartsWith(e + ".", StringComparison.OrdinalIgnoreCase)))
            .Select(x => $"{x.Capa} referencia {x.Ref}")
            .ToList();

        Assert.True(infractores.Count == 0,
            "El proveedor tecnológico se filtró a una capa interna (FR-063, T40):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_la_raiz_de_composicion_conoce_al_proveedor()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var espacio in EspaciosDelProveedor)
        {
            var uso = new Regex($@"\b(?<!\.){Regex.Escape(espacio)}(\.|;|\b)", RegexOptions.Compiled);
            foreach (var archivo in RepoPath.ProductionCSharpFiles())
            {
                var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
                var esNucleo = relativo.Contains("/IngenIA365ERP.Domain/", StringComparison.Ordinal)
                            || relativo.Contains("/IngenIA365ERP.Application/", StringComparison.Ordinal);
                var esApi = relativo.Contains("/IngenIA365ERP.API/", StringComparison.Ordinal);
                if (!esNucleo && !esApi) continue;
                if (esApi && Path.GetFileName(archivo) == "Program.cs") continue;

                if (uso.IsMatch(FuenteSinComentarios.Leer(archivo)))
                    infractores.Add($"{relativo}: conoce {espacio}");
            }
        }

        Assert.True(infractores.Count == 0,
            "El proveedor tecnológico sólo lo conoce su adaptador (FR-063, T40):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_Program_de_la_API_llama_AddElectronicInvoicing()
    {
        var root = RepoPath.FindRepoRoot();
        var llamada = new Regex(@"\.AddElectronicInvoicing\s*\(", RegexOptions.Compiled);
        var llamadores = RepoPath.ProductionCSharpFiles()
            .Where(f => llamada.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .ToList();

        Assert.True(llamadores.Count == 1 && llamadores[0] == "src/Presentation/IngenIA365ERP.API/Program.cs",
            "AddElectronicInvoicing se llama sólo desde API/Program.cs (T749, T40). Lo llaman:\n  " + string.Join("\n  ", llamadores));
    }
}
