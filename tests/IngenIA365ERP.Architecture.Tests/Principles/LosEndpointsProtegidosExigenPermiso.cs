using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 008 (FR-008, SC-004) y feature 009 (FR-042): toda ruta de los maestros de persona y
/// de la contabilidad exige un permiso propio con <c>.RequirePermission(</c>. Hasta el
/// 2026-09-13 bastaba con estar autenticado —<c>RequireAuthorization()</c> en el grupo— y
/// cualquier sesión podía crear, editar o eliminar personas. Es una prueba sobre el fuente porque
/// una ruta nueva sin permiso no rompe ninguna prueba de unidad: sólo abre una puerta.
///
/// <para>
/// El archivo se parte en tramos: de cada <c>.Map(Get|Post|Put|Delete|Patch)(</c> hasta el
/// siguiente. Un tramo es una ruta con toda su cadena (<c>WithName</c>, filtros, permisos), y
/// tiene que contener al menos un <c>.RequirePermission(</c>. Las altas «con persona» llevan
/// dos: crear la persona <b>y</b> el rol.
/// </para>
///
/// <para>
/// Los patrones con <c>*</c> se expanden al correr: una carpeta que todavía no existe no falla
/// (la contabilidad se entrega por etapas), pero en cuanto aparece un archivo, entra.
/// </para>
/// </summary>
public class LosEndpointsProtegidosExigenPermiso
{
    private static readonly string[] Patrones =
    [
        Path.Combine("Endpoints", "Core", "PeopleEndpoints.cs"),
        Path.Combine("Endpoints", "Core", "PeopleDetailEndpoints.cs"),
        Path.Combine("Endpoints", "Core", "AssociatesEndpoints.cs"),
        // Feature 010 (R12): toda la nómina, para que los archivos nuevos —prima, cesantías,
        // vacaciones, definitivas, políticas, festivos, saldos, PILA, DIAN, dispersión— entren
        // solos al aparecer. Hasta entonces la lista sólo tenía EmployeesEndpoints.cs (008).
        Path.Combine("Endpoints", "Payroll", "*.cs"),
        // Feature 009: todo el módulo contable y su centro de informes.
        Path.Combine("Endpoints", "Accounting", "*.cs"),
        Path.Combine("Endpoints", "Reports", "Accounting*.cs"),
    ];

    /// <summary>
    /// Catálogos de nómina heredados de SOLIDO (EPS, ARL, fondos, cajas, causas y parámetros de
    /// retención, conceptos heredados, cuentas por concepto heredadas, autoaportes): sus rutas
    /// llevan sólo <c>RequireAuthorization()</c> del grupo y no un permiso propio. Es deuda
    /// conocida que no abre esta feature; quedan fuera <b>por nombre</b>, para que cualquier
    /// archivo nuevo de la carpeta sí entre. Al ponerles permiso se borran de aquí.
    /// </summary>
    private static readonly HashSet<string> CatalogosHeredadosSinPermiso = new(StringComparer.OrdinalIgnoreCase)
    {
        "AutoContributionParamsEndpoints.cs",
        "ConceptAccountsEndpoints.cs",
        "FamilyCompensationFundsEndpoints.cs",
        "HealthInsuranceProvidersEndpoints.cs",
        "PayrollConceptsEndpoints.cs",
        "PensionProvidersEndpoints.cs",
        "SeveranceProvidersEndpoints.cs",
        "WithholdingCausesEndpoints.cs",
        "WithholdingParametersEndpoints.cs",
        "WorkRiskProvidersEndpoints.cs",
        "WorkRiskRatesEndpoints.cs",
    };

    /// <summary>
    /// Rutas sueltas de archivos que no entran enteros a <see cref="Patrones"/> porque conviven con rutas
    /// abiertas a toda sesión: (archivo, fragmento de la ruta, permiso exigido). Feature 012 (T019):
    /// <c>AuditLogModule.cs</c> también publica <c>POST /api/audit/access</c>, que registra el ingreso a
    /// una opción y es de cualquier usuario autenticado; la verificación de integridad (contracts/api.md
    /// §29) sí exige <c>AuditLog.VerifyIntegrity</c>. Las rutas de <c>Endpoints/Inventory</c> las agrega
    /// la fase 3 como glob.
    /// </summary>
    private static readonly (string Archivo, string Ruta, string Permiso)[] RutasSueltas =
    [
        (Path.Combine("Endpoints", "AuditLogModule.cs"), "/api/audit/integrity/verify", "AuditLog.VerifyIntegrity"),
    ];

    private static readonly Regex InicioDeRuta = new(@"\.Map(Get|Post|Put|Delete|Patch)\(", RegexOptions.Compiled);

    private static string Api => Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API");

    /// <summary>Los archivos concretos: los fijos tienen que existir; los globs aportan lo que haya.</summary>
    internal static IEnumerable<string> Archivos()
    {
        foreach (var patron in Patrones)
        {
            var nombre = Path.GetFileName(patron);
            if (!nombre.Contains('*'))
            {
                yield return patron;
                continue;
            }
            var carpeta = Path.Combine(Api, Path.GetDirectoryName(patron)!);
            if (!Directory.Exists(carpeta)) continue;
            foreach (var f in Directory.EnumerateFiles(carpeta, nombre).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (CatalogosHeredadosSinPermiso.Contains(Path.GetFileName(f))) continue;
                yield return Path.GetRelativePath(Api, f);
            }
        }
    }

    public static TheoryData<string> LosArchivos => [.. Archivos()];

    /// <summary>Cada ruta con toda su cadena: desde su <c>.Map*(</c> hasta el siguiente o el final.</summary>
    internal static IReadOnlyList<string> Tramos(string texto)
    {
        var inicios = InicioDeRuta.Matches(texto).Cast<Match>().Select(m => m.Index).ToList();
        var tramos = new List<string>();
        for (var i = 0; i < inicios.Count; i++)
        {
            var fin = i + 1 < inicios.Count ? inicios[i + 1] : texto.Length;
            tramos.Add(texto[inicios[i]..fin]);
        }
        return tramos;
    }

    [Theory]
    [MemberData(nameof(LosArchivos))]
    public void Cada_ruta_lleva_RequirePermission(string relativo)
    {
        var ruta = Path.Combine(Api, relativo);
        Assert.True(File.Exists(ruta), $"No existe {ruta}: si el archivo se movió, actualizá la lista.");

        var tramos = Tramos(File.ReadAllText(ruta));
        Assert.True(tramos.Count > 0, $"{relativo} no declara ninguna ruta: ¿cambió la forma de mapear?");

        var sinPermiso = tramos
            .Where(t => !t.Contains(".RequirePermission(", StringComparison.Ordinal))
            .Select(t => Regex.Replace(t, @"\s+", " ").Trim())
            .Select(t => t.Length > 90 ? t[..90] + "…" : t)
            .ToList();

        Assert.True(sinPermiso.Count == 0,
            $"Rutas de {relativo} sin .RequirePermission( (features 008 y 009):\n  " + string.Join("\n  ", sinPermiso));
    }

    [Fact]
    public void La_deuda_heredada_de_nomina_sigue_siendo_la_que_dice_la_lista()
    {
        // Si un catálogo heredado recibe permiso, sale de la lista y entra al recorrido; si el
        // archivo desaparece, la lista se limpia. Ninguna de las dos cosas debe pasar en silencio.
        var carpeta = Path.Combine(Api, "Endpoints", "Payroll");
        foreach (var nombre in CatalogosHeredadosSinPermiso)
        {
            var ruta = Path.Combine(carpeta, nombre);
            Assert.True(File.Exists(ruta), $"{nombre} ya no existe: quitálo de CatalogosHeredadosSinPermiso.");
            Assert.DoesNotContain(".RequirePermission(", File.ReadAllText(ruta), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Las_altas_con_persona_exigen_crear_la_persona_y_el_rol()
    {
        // FR-001 (008): el compuesto exige los dos permisos; con uno solo la puerta responde 404.
        var api = Path.Combine(Api, "Endpoints");
        var casos = new[]
        {
            (Archivo: Path.Combine(api, "Payroll", "EmployeesEndpoints.cs"), Rol: "Payroll.Employees.Create", Nombre: "empleados"),
            (Archivo: Path.Combine(api, "Core", "AssociatesEndpoints.cs"), Rol: "Core.Associates.Create", Nombre: "asociados"),
        };

        foreach (var caso in casos)
        {
            var conPersona = Tramos(File.ReadAllText(caso.Archivo))
                .FirstOrDefault(t => t.Contains("with-person", StringComparison.Ordinal));
            if (conPersona is null) continue;

            Assert.True(
                conPersona.Contains($"RequirePermission(\"{caso.Rol}\")", StringComparison.Ordinal)
                && conPersona.Contains("RequirePermission(\"Core.People.Create\")", StringComparison.Ordinal),
                $"La ruta with-person de {caso.Nombre} debe exigir {caso.Rol} y Core.People.Create.");
        }
    }

    [Fact]
    public void Las_rutas_sueltas_exigen_su_permiso()
    {
        foreach (var (archivo, ruta, permiso) in RutasSueltas)
        {
            var camino = Path.Combine(Api, archivo);
            Assert.True(File.Exists(camino), $"No existe {camino}: si el archivo se movió, actualizá RutasSueltas.");

            var tramo = Tramos(File.ReadAllText(camino))
                .FirstOrDefault(t => t.Contains($"\"{ruta}\"", StringComparison.Ordinal));
            Assert.True(tramo is not null, $"{archivo} ya no publica {ruta}: actualizá RutasSueltas.");
            Assert.True(tramo!.Contains($".RequirePermission(\"{permiso}\")", StringComparison.Ordinal),
                $"{ruta} de {archivo} debe exigir .RequirePermission(\"{permiso}\").");
        }
    }
}
