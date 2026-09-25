using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T21, §2.18), FR-012: ninguna tarifa, tope ni valor
/// legal del comercio (IVA, INC, retenciones, ICA, UVT, plazos DIAN) vive en el programa. Todo es
/// un parámetro con vigencia (<c>LectorDeParametros</c>) o sale del catálogo (<c>CatalogoDian</c>).
/// Es el molde de <see cref="LaNominaNoTieneValoresLegalesFijos"/>: en las carpetas de
/// <see cref="Carpetas"/> sólo se admiten los literales decimales de proporción y calendario.
///
/// <para>
/// La lista de carpetas es la de §2.18 desde el Setup; una carpeta que todavía no existe cuenta como
/// vacía, sin fallar. <see cref="ArchivosExceptuados"/> empieza vacía y la llena, con su motivo, el
/// bloque que necesite una excepción (una semilla, por ejemplo); la fase 15 (US8, T670) amplía
/// además los literales sospechosos con los de la DIAN.
/// </para>
/// </summary>
public class ElComercioNoTieneValoresLegalesFijos
{
    /// <summary>Carpetas relativas a <c>src/Core/</c> del comercio (§2.18).</summary>
    private static readonly string[] Carpetas =
    [
        "IngenIA365ERP.Domain/Inventory",
        "IngenIA365ERP.Application/Inventory",
        "IngenIA365ERP.Domain/Taxes",
        "IngenIA365ERP.Application/Core/Taxes",
        "IngenIA365ERP.Domain/ElectronicInvoicing",
        "IngenIA365ERP.Application/ElectronicInvoicing",
    ];

    /// <summary>Archivos (relativos a la raíz, con '/') exceptuados, cada uno con su motivo. Empieza vacía.</summary>
    private static readonly string[] ArchivosExceptuados =
    [
        // T068: los tres catálogos cerrados de claves de parámetro. Sus valores son DEFECTOS CONFIGURABLES con
        // vigencia (COR_ParameterVersions, LectorDeParametros), no valores legales fijos: la cooperativa los
        // cambia sin tocar el programa, y los que dependen de una norma exigen su fuente legal.
        "src/Core/IngenIA365ERP.Domain/Inventory/Parameters/ParametrosDeInventario.cs",
        "src/Core/IngenIA365ERP.Domain/Taxes/ParametrosTributarios.cs",
        "src/Core/IngenIA365ERP.Domain/ElectronicInvoicing/ParametrosDeFacturacionElectronica.cs",
    ];

    private static readonly Regex LiteralDecimal = new(@"(?<![\w.])\d+(?:\.\d+)?[mM]\b", RegexOptions.Compiled);

    private static readonly HashSet<string> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "0m", "1m", "2m", "0.5m", "12m", "15m", "30m", "100m", "360m", "0.0m", "1.0m",
    };

    [Fact]
    public void Domain_y_Application_del_comercio_no_llevan_tarifas_ni_topes_fijos()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var carpeta in Carpetas)
        {
            var ruta = Path.Combine(root, "src", "Core", carpeta);
            if (!Directory.Exists(ruta)) continue; // carpeta aún no creada: no tiene nada que violar

            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
                if (ArchivosExceptuados.Contains(relativo, StringComparer.OrdinalIgnoreCase)) continue;

                var lineas = File.ReadAllLines(archivo);
                for (var i = 0; i < lineas.Length; i++)
                {
                    var linea = lineas[i].Trim();
                    if (linea.StartsWith("//", StringComparison.Ordinal) || linea.StartsWith("*", StringComparison.Ordinal)) continue;

                    foreach (Match m in LiteralDecimal.Matches(linea))
                    {
                        if (!Permitidos.Contains(m.Value))
                            infractores.Add($"{relativo}:{i + 1}: literal decimal '{m.Value}'");
                    }
                }
            }
        }

        Assert.True(infractores.Count == 0,
            "Valores legales o tarifas fijas en el código del comercio (FR-012). " +
            "Cada uno debe ser un parámetro con vigencia o un dato del catálogo:\n  " + string.Join("\n  ", infractores));
    }
}
