using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 (decisiones-transversales T23, §2.18), FR-012: la UVT tiene <b>un solo
/// lector</b>, <c>Application/Common/Taxation/LectorDeUvt</c> (<c>IValorUvt</c>), que lee el código
/// <c>UVT</c> de los parámetros legales a la fecha y falla visible si no hay vigencia. Fuera de
/// nómina, nadie más lee la tabla de parámetros legales: cuando la UVT se promueva a Core, cambia
/// sólo el lector.
///
/// <para>
/// Completada por la sección tributaria de la fase 3 (T117, T163): <see cref="SimbolosDeUvt"/> lleva el
/// <c>DbSet</c> de parámetros legales y el código de la UVT; <see cref="LectoresAutorizados"/> y
/// <see cref="CarpetasAutorizadas"/> dicen quién puede usarlos. <see cref="Hay_un_solo_lector_de_la_UVT"/> fija que
/// el lector existe y es el único que implementa <c>IValorUvt</c>.
/// </para>
/// </summary>
public class LaUvtSeLeeEnUnSoloSitio
{
    /// <summary>
    /// Identificadores restringidos: el <c>DbSet</c> de parámetros legales y el código de la UVT (T117, sección
    /// tributaria de la fase 3, al crear <c>LectorDeUvt</c>).
    /// </summary>
    private static readonly string[] SimbolosDeUvt = ["PayrollLegalParameters", "LegalParameterCodes.Uvt"];

    /// <summary>
    /// Nombres de archivo (sin ruta) autorizados: el lector, y los que declaran la tabla o la siembran (el contexto de
    /// datos y la semilla de nómina).
    /// </summary>
    private static readonly string[] LectoresAutorizados =
    [
        "LectorDeUvt.cs",
        "IApplicationDbContext.cs",
        "ApplicationDbContext.cs",
        "PayrollLegalParametersSeeder.cs",
    ];

    /// <summary>
    /// Carpetas (fragmento de ruta con '/') autorizadas: nómina conserva su lectura propia (su motor, sus casos de uso y
    /// sus rutas de parámetros legales).
    /// </summary>
    private static readonly string[] CarpetasAutorizadas =
    [
        "/IngenIA365ERP.Application/Payroll/",
        "/IngenIA365ERP.Domain/Payroll/",
        "/IngenIA365ERP.API/Endpoints/Payroll/",
    ];

    [Fact]
    public void Solo_el_lector_de_UVT_lee_los_parametros_legales_fuera_de_nomina()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var simbolo in SimbolosDeUvt)
        {
            var uso = new Regex($@"\b{Regex.Escape(simbolo)}\b", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
                if (LectoresAutorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (CarpetasAutorizadas.Any(c => ("/" + relativo).Contains(c, StringComparison.Ordinal))) continue;
                if (uso.IsMatch(texto))
                    infractores.Add($"{relativo}: usa {simbolo} fuera de LectorDeUvt");
            }
        }

        Assert.True(infractores.Count == 0,
            "La UVT se lee en un solo sitio (T23):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Hay_un_solo_lector_de_la_UVT()
    {
        var implementaciones = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IngenIA365ERP.Application.Common.Taxation.IValorUvt).IsAssignableFrom(t))
            .Select(t => t.Name).ToList();
        Assert.Equal(["LectorDeUvt"], implementaciones);
    }
}
