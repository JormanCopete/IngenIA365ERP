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

    /// <summary>
    /// Carpetas del catálogo tributario y de la UVT (T117, sección tributaria de la fase 3), en el molde de
    /// <see cref="LaNominaNoTieneValoresLegalesFijos"/>: además de los literales decimales, ningún porcentaje, UVT ni base
    /// mínima escrita de otra forma (como <c>double</c>, entero o texto). Las semillas quedan fuera: son datos.
    /// </summary>
    private static readonly string[] CarpetasTributarias =
    [
        "IngenIA365ERP.Domain/Taxes",
        "IngenIA365ERP.Application/Core/Taxes",
        "IngenIA365ERP.Application/Common/Taxation",
    ];

    /// <summary>Tarifas, UVT y bases de la norma vigente, por si vuelven sin sufijo decimal.</summary>
    private static readonly string[] SospechososTributarios =
    [
        "0.19", "0.16", "0.05", "0.025", "0.035", "0.04", "0.06", "0.11", "0.15", "0.08",
        "52374", "52_374", "49799", "49_799", "47065", "47_065", "1414098", "1_414_098",
    ];

    [Fact]
    public void El_catalogo_tributario_no_escribe_tarifas_UVT_ni_bases_minimas()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();
        var carpetasRevisadas = 0;

        foreach (var carpeta in CarpetasTributarias)
        {
            var ruta = Path.Combine(root, "src", "Core", carpeta);
            if (!Directory.Exists(ruta)) continue;
            carpetasRevisadas++;

            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
                if (ArchivosExceptuados.Contains(relativo, StringComparer.OrdinalIgnoreCase)) continue;

                var lineas = File.ReadAllLines(archivo);
                for (var i = 0; i < lineas.Length; i++)
                {
                    var linea = lineas[i].Trim();
                    if (linea.StartsWith("//", StringComparison.Ordinal) || linea.StartsWith("*", StringComparison.Ordinal)) continue;
                    foreach (var sospechoso in SospechososTributarios)
                        if (System.Text.RegularExpressions.Regex.IsMatch(linea, $@"(?<![\w.]){System.Text.RegularExpressions.Regex.Escape(sospechoso)}(?![\w])"))
                            infractores.Add($"{relativo}:{i + 1}: valor legal '{sospechoso}' escrito en el programa");
                }
            }
        }

        Assert.True(carpetasRevisadas == CarpetasTributarias.Length, "Las carpetas del catálogo tributario existen desde la fase 3: si se movieron, actualizá CarpetasTributarias.");
        Assert.True(infractores.Count == 0,
            "Tarifas, UVT o bases mínimas escritas en el catálogo tributario (FR-013, T22, T23):\n  " + string.Join("\n  ", infractores));
    }

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
