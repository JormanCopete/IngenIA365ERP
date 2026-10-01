using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T21, §2.18), FR-012: los parámetros con vigencia de
/// plataforma tienen un solo lector, <c>LectorDeParametros</c> —que cae ámbito → general → defecto
/// seguro y rechaza un valor guardado fuera de lo admitido—, y un solo escritor,
/// <c>AddParameterVersionCommandHandler</c>. Quien lee la tabla por su cuenta se salta la caída y
/// la validación.
///
/// <para>
/// Llenada por la fase 2 (plataforma, T018): <see cref="SimbolosDeParametros"/> son el <c>DbSet</c>
/// <c>ParameterVersions</c> y el acceso genérico <c>Set&lt;ParameterVersion&gt;</c>, buscados en el código
/// sin comentarios. La configuración EF declara la tabla (<c>EntityTypeBuilder&lt;ParameterVersion&gt;</c>)
/// sin leerla, así que no los nombra.
/// </para>
/// </summary>
public class LosParametrosSeLeenEnUnSoloSitio
{
    /// <summary>Accesos restringidos a la tabla de parámetros con vigencia.</summary>
    private static readonly string[] SimbolosDeParametros =
    [
        "ParameterVersions",
        "Set<ParameterVersion>",
    ];

    /// <summary>Nombres de archivo (sin ruta) autorizados; el contexto y su interfaz declaran el DbSet.</summary>
    private static readonly string[] Autorizados =
    [
        "LectorDeParametros.cs",
        "AddParameterVersionCommandHandler.cs",
        "ApplicationDbContext.cs",
        "IApplicationDbContext.cs",
    ];

    /// <summary>
    /// I5, T825 (FR-045; T42g): la puerta de los retroactivos (<c>Costeo.RetroactivosPermitidos</c> y <c>Costeo.RetroactivosDiasMaximos</c>)
    /// se lee en un solo sitio, <c>RegistroDeKardex</c> (paso 5 de la confirmación, dentro del cerrojo, por el <c>LectorDeParametros</c>).
    /// El impacto en costos (<c>GetDocumentCostImpactQuery</c>) la hereda porque corre el mismo registro en modo simulación; si otro archivo
    /// la leyera, la vista previa y la confirmación podrían decir cosas distintas. Además del catálogo de claves
    /// (<c>ParametrosDeInventario</c>), ninguna otra fuente nombra las constantes ni la clave como texto.
    /// </summary>
    [Fact]
    public void Los_retroactivos_se_leen_solo_en_el_registro_del_kardex()
    {
        var root = RepoPath.FindRepoRoot();
        var uso = new Regex(@"\bCosteoRetroactivos(Permitidos|DiasMaximos)\b|""Costeo\.Retroactivos(Permitidos|DiasMaximos)""", RegexOptions.Compiled);
        string[] autorizados = ["ParametrosDeInventario.cs", "RegistroDeKardex.cs"];
        var lectores = new List<string>();
        var infractores = new List<string>();

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            if (!uso.IsMatch(FuenteSinComentarios.Leer(archivo))) continue;
            var nombre = Path.GetFileName(archivo);
            if (autorizados.Contains(nombre, StringComparer.Ordinal)) lectores.Add(nombre);
            else infractores.Add($"{Path.GetRelativePath(root, archivo)}: lee la puerta de los retroactivos fuera de RegistroDeKardex");
        }

        Assert.Contains("RegistroDeKardex.cs", lectores);
        Assert.True(infractores.Count == 0,
            "Costeo.RetroactivosPermitidos y Costeo.RetroactivosDiasMaximos se leen en un solo sitio (FR-045, T825):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_lector_y_el_comando_tocan_los_parametros()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: FuenteSinComentarios.Leer(f))).ToList();
        var infractores = new List<string>();

        foreach (var simbolo in SimbolosDeParametros)
        {
            // Bordes por lookaround y no por \b: «Set<ParameterVersion>» termina en un símbolo.
            var uso = new Regex($@"(?<!\w){Regex.Escape(simbolo)}(?!\w)", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                if (Autorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (uso.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: usa {simbolo} fuera de LectorDeParametros");
            }
        }

        Assert.True(infractores.Count == 0,
            "Los parámetros con vigencia se leen en un solo sitio (FR-012, T21):\n  " + string.Join("\n  ", infractores));
    }
}
