using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014, T038 (FR-004, FR-016; api.md §1, §2; research R29): las captaciones y aportes exigen permiso y alcance como el resto de la Cartera.
/// <list type="bullet">
/// <item>toda ruta de <c>API/Endpoints/Lending/Deposits/**</c> (también las subcarpetas) declara <c>RequirePermission</c> con un permiso
/// <c>Lending.*</c> y arma su grupo con <c>LendingGroup.Grupo</c> (la auditoría del intento denegado); <see cref="LosEndpointsProtegidosExigenPermiso"/>
/// ya recorre la carpeta: aquí se fija que lo haga y se amplía a las subcarpetas;</item>
/// <item>toda consulta (<c>…Query</c>) y todo comando (<c>…Command</c>) de <c>Application/Lending/Deposits</c> aplica <c>IAlcanceDeCartera</c> en su
/// archivo (filtra por oficina y línea, o responde el 404 de lo que está fuera) o está en <see cref="Exceptuados"/> con su motivo. Los del proceso
/// (el ciclo diario, el cierre, la activación) se exceptúan con el suyo cuando nazcan.</item>
/// </list>
/// Las rutas y los comandos llegan con cada historia; hoy la regla no tiene nada que violar y queda fijada.
/// </summary>
public class LasCaptacionesExigenPermisoYAlcance
{
    private static string Api => Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API");

    private static readonly Regex Ruta = new(@"\.Map(Get|Post|Put|Delete|Patch)\(", RegexOptions.Compiled);

    private static readonly Regex Permiso = new(@"\.RequirePermission\(", RegexOptions.Compiled);

    [Fact]
    public void Toda_ruta_de_captaciones_exige_un_permiso_y_usa_el_grupo_de_Cartera()
    {
        var carpeta = Path.Combine(Api, "Endpoints", "Lending", "Deposits");
        Assert.True(Directory.Exists(carpeta), "Endpoints/Lending/Deposits es la carpeta de las rutas de captaciones (research §0.1).");
        var infracciones = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories))
        {
            var relativo = Path.GetRelativePath(Api, archivo);
            var lineas = FuenteSinComentarios.Leer(archivo).Split('\n');
            var rutas = 0;
            for (var i = 0; i < lineas.Length; i++)
            {
                if (!Ruta.IsMatch(lineas[i])) continue;
                rutas++;
                // La declaración de la ruta termina en el primer «;» después del Map; el permiso va en esa cadena.
                var declaracion = string.Join(' ', lineas.Skip(i).TakeWhile((_, k) => k == 0 || !lineas[i + k - 1].TrimEnd().EndsWith(';')));
                if (!Permiso.IsMatch(declaracion)) infracciones.Add($"{relativo}:{i + 1}: ruta sin RequirePermission");
            }
            var fuente = FuenteSinComentarios.Leer(archivo);
            if (rutas > 0 && !fuente.Contains("LendingGroup.Grupo(", StringComparison.Ordinal)) infracciones.Add($"{relativo}: no usa LendingGroup.Grupo");
            foreach (Match m in Regex.Matches(fuente, @"RequirePermission\(\s*""([^""]+)"""))
                if (!m.Groups[1].Value.StartsWith("Lending.", StringComparison.Ordinal))
                    infracciones.Add($"{relativo}: el permiso {m.Groups[1].Value} no es del catálogo Lending.*");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Una ruta de captaciones no exige permiso (FR-016, api.md §2)");
    }

    [Fact]
    public void La_regla_general_de_permisos_recorre_las_captaciones()
    {
        var regla = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "tests", "IngenIA365ERP.Architecture.Tests", "Principles",
            "LosEndpointsProtegidosExigenPermiso.cs"));
        Assert.Contains(@"Path.Combine(""Endpoints"", ""Lending"", ""Deposits"", ""*.cs"")", regla, StringComparison.Ordinal);
    }

    /// <summary>Consultas y comandos de captaciones que no aplican el alcance, con su motivo. Nace vacía: quien agrega uno decide por qué.</summary>
    private static readonly Dictionary<string, string> Exceptuados = new(StringComparer.Ordinal)
    {
        // T055: la activación es de toda la cooperativa (como ActivateLendingCommand de la 013): no hay oficina ni línea que filtrar.
        ["ActivateDepositsCommand"] = "la activación de captaciones es de toda la cooperativa, sin oficina ni línea (FR-116)",
        ["ActivateContributionsCommand"] = "la activación de aportes es de toda la cooperativa, sin oficina ni línea (FR-116)",
        // US4 (T117): lo manda la bandeja con el actor «Proceso de integración», sin usuario ni alcance (I-2); sólo guarda el estado de la factura.
        ["ConsumeChargeInvoiceRegisteredCommand"] = "consumo de la bandeja (Comercial informa la factura de un cargo): sin usuario, sin ruta ni alcance (I-2)",
    };

    private static readonly Regex ConsultaOComando = new(@"public\s+sealed\s+record\s+(\w+(Query|Command))\b", RegexOptions.Compiled);

    private static readonly Regex AplicaElAlcance = new(@"\b(IAlcanceDeCartera|AlcanceDeCarteraDelActor|FiltroDeAlcanceDeCartera)\b", RegexOptions.Compiled);

    [Fact]
    public void Toda_consulta_y_todo_comando_de_captaciones_aplica_el_alcance_o_esta_exceptuado()
    {
        var raiz = RepoPath.FindRepoRoot();
        var encontrados = new HashSet<string>(StringComparer.Ordinal);
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Application/Lending/Deposits"]))
        {
            var fuente = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in ConsultaOComando.Matches(fuente))
            {
                var nombre = m.Groups[1].Value;
                encontrados.Add(nombre);
                if (Exceptuados.ContainsKey(nombre)) continue;
                if (!AplicaElAlcance.IsMatch(fuente))
                    infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {nombre} no aplica IAlcanceDeCartera ni está exceptuado con motivo");
            }
        }
        foreach (var (nombre, motivo) in Exceptuados)
        {
            if (string.IsNullOrWhiteSpace(motivo)) infracciones.Add($"{nombre}: exceptuado sin motivo");
            if (!encontrados.Contains(nombre)) infracciones.Add($"{nombre}: exceptuado pero ya no existe en Application/Lending/Deposits");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Consultas o comandos de captaciones sin alcance (FR-004, SC-015)");
    }
}
