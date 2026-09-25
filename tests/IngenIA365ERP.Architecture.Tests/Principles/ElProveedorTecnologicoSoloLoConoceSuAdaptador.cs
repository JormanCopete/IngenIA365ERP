using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T40, §2.18), FR-063: el proveedor tecnológico de
/// facturación electrónica vive detrás del puerto <c>ICanalDeEmisionElectronica</c>. Domain y
/// Application no referencian <c>IngenIA365ERP.ElectronicInvoicing</c> ni el SDK de ningún
/// proveedor; la API lo referencia sólo como raíz de composición: únicamente <c>Program.cs</c>
/// llama <c>AddElectronicInvoicing()</c>. Cambiar de proveedor es cambiar el adaptador.
///
/// <para>
/// Esqueleto del Setup: <see cref="EspaciosDelProveedor"/> empieza vacía y la prueba afirma la regla
/// sobre cada elemento; con la lista vacía pasa porque no hay nada que violar, no por un
/// <c>return</c> temprano. La llena la fase 15 (US8, entrega I4) al crear el proyecto
/// <c>IngenIA365ERP.ElectronicInvoicing</c> y cuando se contrate el proveedor.
/// </para>
/// </summary>
public class ElProveedorTecnologicoSoloLoConoceSuAdaptador
{
    /// <summary>Espacios de nombres del adaptador y del SDK del proveedor. Los agrega la entrega I4.</summary>
    private static readonly string[] EspaciosDelProveedor = [];

    [Fact]
    public void Solo_la_raiz_de_composicion_conoce_al_proveedor()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var espacio in EspaciosDelProveedor)
        {
            var uso = new Regex($@"\b{Regex.Escape(espacio)}(\.|;|\b)", RegexOptions.Compiled);
            foreach (var archivo in RepoPath.ProductionCSharpFiles())
            {
                var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
                var esNucleo = relativo.Contains("/IngenIA365ERP.Domain/", StringComparison.Ordinal)
                            || relativo.Contains("/IngenIA365ERP.Application/", StringComparison.Ordinal);
                var esApi = relativo.Contains("/IngenIA365ERP.API/", StringComparison.Ordinal);
                if (!esNucleo && !esApi) continue;
                if (esApi && Path.GetFileName(archivo) == "Program.cs") continue;

                if (uso.IsMatch(File.ReadAllText(archivo)))
                    infractores.Add($"{relativo}: conoce {espacio}");
            }
        }

        Assert.True(infractores.Count == 0,
            "El proveedor tecnológico sólo lo conoce su adaptador (FR-063, T40):\n  " + string.Join("\n  ", infractores));
    }
}
