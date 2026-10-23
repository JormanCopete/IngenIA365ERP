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
        // Feature 012 (T118, decisiones-transversales §2.18): ampliación canónica por glob; cubre las rutas de todas
        // las historias del comercio (US1–US17). Las carpetas o archivos que todavía no existen no fallan.
        Path.Combine("Endpoints", "Inventory", "*.cs"),
        Path.Combine("Endpoints", "Core", "Taxes*.cs"),
        Path.Combine("Endpoints", "Core", "PaymentMeans*.cs"),
        Path.Combine("Endpoints", "ElectronicInvoicing", "*.cs"),
        Path.Combine("Endpoints", "Reports", "Inventory*.cs"),
        // Feature 012 (T159): las rutas reutilizables de las plantillas (RutasDePlantilla.MapPlantilla).
        Path.Combine("Endpoints", "Common", "*.cs"),
        // Feature 013, fase 2 (T0038; contracts/api.md §21, §22): la plataforma que pide Cartera —valores nacionales de la
        // cooperativa, perfil regulatorio, canales externos, registro de participantes— y el cumplimiento (listas restrictivas,
        // solicitudes del titular, autorizaciones por finalidad). Las rutas /api/saas/* del maestro van aparte (RequireMasterAdminAttribute).
        Path.Combine("Endpoints", "Core", "NationalValuesEndpoints.cs"),
        Path.Combine("Endpoints", "Core", "RegulatoryProfileEndpoints.cs"),
        Path.Combine("Endpoints", "Core", "ExternalChannelsEndpoints.cs"),
        Path.Combine("Endpoints", "Core", "IntegrationParticipantsEndpoints.cs"),
        Path.Combine("Endpoints", "Compliance", "*.cs"),
        Path.Combine("Endpoints", "HabeasDataModule.cs"),
        // Feature 013, fase 3 (T0126, R9.1): toda ruta de Cartera, sin excepciones (hoy la carpeta no existe: las 63 rutas del
        // crédito heredado se retiraron con T0192 y las de la Cartera nueva entran solas al aparecer). Feature 014 (T009, R28): lo
        // conservado de ahorros y aportes (/api/savings, /api/reports/savings) se retiró y con ello su exención por nombre; las
        // captaciones viven en Endpoints/Lending/Deposits y entran por este mismo glob, sin excepciones.
        Path.Combine("Endpoints", "Lending", "*.cs"),
        Path.Combine("Endpoints", "Lending", "Deposits", "*.cs"),
        Path.Combine("Endpoints", "Reports", "Lending*.cs"),
        // Feature 015 (T0021; R-37; FR-011, SC-018): toda ruta de Recaudo y de la Tesorería nueva, por glob, y las dos piezas de plataforma
        // (cuentas bancarias de la persona y go/no-go). Las tres rutas heredadas de Tesorería quedan fuera por nombre hasta su retiro.
        Path.Combine("Endpoints", "Receipts", "*.cs"),
        Path.Combine("Endpoints", "Treasury", "*.cs"),
        Path.Combine("Endpoints", "Reports", "Receipts*.cs"),
        Path.Combine("Endpoints", "Reports", "Treasury*.cs"),
        Path.Combine("Endpoints", "Core", "PersonBankAccountsEndpoints.cs"),
        Path.Combine("Endpoints", "Core", "GoLiveEndpoints.cs"),
    ];

    /// <summary>
    /// Catálogos de nómina heredados del sistema anterior (EPS, ARL, fondos, cajas, causas y parámetros de
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
    /// Feature 015 (T0021; R-04): la Tesorería heredada (<c>/api/treasury/{checks, concepts, invoices, cash-flow}</c>) sigue sin permiso propio
    /// hasta su retiro después de T2; queda fuera por ruta (relativa a la API) y las rutas nuevas de <c>Endpoints/Treasury</c> sí entran.
    /// </summary>
    private static readonly HashSet<string> TesoreriaHeredadaSinPermiso = new(StringComparer.OrdinalIgnoreCase)
    {
        "Endpoints/Treasury/ChecksEndpoints.cs",
        "Endpoints/Treasury/TreasuryConceptsEndpoints.cs",
        "Endpoints/Treasury/TreasuryTransactionsEndpoints.cs",
    };

    [Fact]
    public void La_deuda_heredada_de_tesoreria_sigue_siendo_la_que_dice_la_lista()
    {
        foreach (var relativo in TesoreriaHeredadaSinPermiso)
        {
            var ruta = Path.Combine(Api, relativo.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(ruta), $"{relativo} ya no existe: quitálo de TesoreriaHeredadaSinPermiso.");
            Assert.DoesNotContain(".RequirePermission(", File.ReadAllText(ruta), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Rutas sueltas de archivos que no entran enteros a <see cref="Patrones"/> porque conviven con rutas
    /// abiertas a toda sesión: (archivo, fragmento de la ruta, permiso exigido). Feature 012 (T019):
    /// <c>AuditLogModule.cs</c> también publica <c>POST /api/audit/access</c>, que registra el ingreso a
    /// una opción y es de cualquier usuario autenticado; la verificación de integridad (contracts/api.md
    /// §29) sí exige <c>AuditLog.VerifyIntegrity</c>. Las rutas de <c>Endpoints/Inventory</c> entran por glob en
    /// <see cref="Patrones"/> (fase 3, T118).
    /// </summary>
    private static readonly (string Archivo, string Ruta, string Permiso)[] RutasSueltas =
    [
        (Path.Combine("Endpoints", "AuditLogModule.cs"), "/api/audit/integrity/verify", "AuditLog.VerifyIntegrity"),
        // Feature 012 (T128, contracts/api.md §1.5): los perfiles sugeridos son parte de crear un rol.
        (Path.Combine("Endpoints", "RolesModule.cs"), "/templates", "Security.Roles.Create"),
        (Path.Combine("Endpoints", "RolesModule.cs"), "/from-template", "Security.Roles.Create"),
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
                if (TesoreriaHeredadaSinPermiso.Contains(Path.GetRelativePath(Api, f).Replace(Path.DirectorySeparatorChar, '/'))) continue;
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

    /// <summary>
    /// Feature 013, US7 (T0610): las rutas de la integración contable de Cartera entran al recorrido por los globs de <c>Endpoints/Accounting</c>
    /// y <c>Endpoints/Lending</c>; aquí se fija que estén y que no se las exceptúe por nombre.
    /// </summary>
    [Fact]
    public void Las_rutas_de_la_integracion_contable_de_Cartera_estan_en_el_recorrido()
    {
        var archivos = Archivos().ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("Endpoints", "Accounting", "LendingIntegrationEndpoints.cs"), archivos);
        Assert.Contains(Path.Combine("Endpoints", "Lending", "AccountingIntegrationEndpoints.cs"), archivos);
        Assert.Contains(Path.Combine("Endpoints", "Lending", "LendingMessagesEndpoints.cs"), archivos);
        // US13 (bloque 14B, T0768): los documentos al deudor y su verificación (Lending.Documents.*), sin ruta anónima (D21).
        Assert.Contains(Path.Combine("Endpoints", "Lending", "DocumentsEndpoints.cs"), archivos);
    }

    /// <summary>
    /// Feature 013, K5 (US16, T0994; api.md §18, DT §2.25): las nueve rutas de <c>Endpoints/Lending/RegulatoryEndpoints.cs</c> entran al recorrido por el
    /// glob de <c>Endpoints/Lending</c> y cada una exige uno de los cuatro permisos del contrato —<c>RegulatoryFiles.Read</c>,
    /// <c>RegulatoryReports.Generate</c>, <c>CreditBureauFiles.Generate</c>, <c>RegulatoryFiles.MarkUploaded</c>—, nunca un <c>View</c>.
    /// </summary>
    [Fact]
    public void Las_rutas_regulatorias_de_Cartera_exigen_los_permisos_del_contrato()
    {
        var relativo = Path.Combine("Endpoints", "Lending", "RegulatoryEndpoints.cs");
        Assert.Contains(relativo, Archivos().ToHashSet(StringComparer.OrdinalIgnoreCase));

        var fuente = File.ReadAllText(Path.Combine(Api, relativo));
        var tramos = Tramos(fuente);
        Assert.Equal(9, tramos.Count);
        string[] permitidos = ["Leer", "GenerarSicses", "GenerarCentral", "MarcarCargado"];
        var infracciones = tramos
            .Where(t => !permitidos.Any(p => t.Contains($".RequirePermission({p})", StringComparison.Ordinal)))
            .Select(t => t[..Math.Min(90, t.Length)])
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Una ruta regulatoria de Cartera sin uno de los permisos de api.md §18");
        Assert.Contains("Leer = \"Lending.RegulatoryFiles.Read\"", fuente, StringComparison.Ordinal);
        Assert.Contains("GenerarSicses = \"Lending.RegulatoryReports.Generate\"", fuente, StringComparison.Ordinal);
        Assert.Contains("GenerarCentral = \"Lending.CreditBureauFiles.Generate\"", fuente, StringComparison.Ordinal);
        Assert.Contains("MarcarCargado = \"Lending.RegulatoryFiles.MarkUploaded\"", fuente, StringComparison.Ordinal);
        Assert.DoesNotContain(".View\"", fuente, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(LosArchivos))]
    public void Cada_ruta_lleva_RequirePermission(string relativo)
    {
        var ruta = Path.Combine(Api, relativo);
        Assert.True(File.Exists(ruta), $"No existe {ruta}: si el archivo se movió, actualizá la lista.");

        var texto = File.ReadAllText(ruta);
        var tramos = Tramos(texto);
        // Feature 012, T238: un archivo que en esta entrega sólo publica la descarga de su plantilla lo hace por
        // RutasDePlantilla.MapPlantilla, que exige su permiso adentro (Endpoints/Common entra al recorrido). Feature 012, T262:
        // lo mismo un grupo de documentos que publica sólo el ciclo común por CicloDeDocumentoRutas.MapCicloDeDocumento
        // (ajustes), que pone el permiso de cada ruta adentro (CicloDeDocumentoRutas.cs también entra al recorrido).
        // Feature 013, T0312: LendingGroup.cs sólo arma el grupo base de Cartera; no publica rutas (lo fija Cartera_usa_el_grupo_base).
        Assert.True(tramos.Count > 0 || texto.Contains(".MapPlantilla(", StringComparison.Ordinal)
                || texto.Contains(".MapCicloDeDocumento(", StringComparison.Ordinal)
                || GruposBase.Contains(Path.GetFileName(relativo), StringComparer.Ordinal),
            $"{relativo} no declara ninguna ruta: ¿cambió la forma de mapear?");

        var sinPermiso = tramos
            .Where(t => !t.Contains(".RequirePermission(", StringComparison.Ordinal))
            .Select(t => Regex.Replace(t, @"\s+", " ").Trim())
            .Select(t => t.Length > 90 ? t[..90] + "…" : t)
            .ToList();

        Assert.True(sinPermiso.Count == 0,
            $"Rutas de {relativo} sin .RequirePermission( (features 008 y 009):\n  " + string.Join("\n  ", sinPermiso));
    }

    /// <summary>El archivo del grupo base de Cartera (feature 013, T0312).</summary>
    private const string GrupoBaseDeCartera = "LendingGroup.cs";

    /// <summary>Los archivos de grupo base que no publican rutas: Cartera (T0312), Recaudo y Tesorería (feature 015, T0046).</summary>
    private static readonly string[] GruposBase = [GrupoBaseDeCartera, "ReceiptsGroup.cs", "TreasuryGroup.cs"];

    [Fact]
    public void Recaudo_y_Tesoreria_tienen_su_grupo_base_con_la_auditoria_del_intento_denegado()
    {
        // Feature 015 (T0021, T0046): como el de Cartera, sin publicar rutas.
        foreach (var (carpeta, archivo, modulo, prefijo) in new[]
                 { ("Receipts", "ReceiptsGroup.cs", "Receipts", "\"/api/receipts\""), ("Treasury", "TreasuryGroup.cs", "Treasury", "\"/api/treasury\"") })
        {
            var grupo = FuenteSinComentarios.Leer(Path.Combine(Api, "Endpoints", carpeta, archivo));
            Assert.Contains(".RequireAuthorization()", grupo, StringComparison.Ordinal);
            Assert.Contains($".AuditarIntentoDenegado(ModuloDeAuditoria.{modulo})", grupo, StringComparison.Ordinal);
            Assert.Contains("AddEndpointFilter<ErrorEnvelopeFilter>()", grupo, StringComparison.Ordinal);
            Assert.Contains(prefijo, grupo, StringComparison.Ordinal);
            Assert.Empty(Tramos(grupo));
        }
    }

    [Fact]
    public void Cartera_usa_el_grupo_base_con_la_auditoria_del_intento_denegado()
    {
        // Feature 013, US17 (T0264, T0312; api.md §1.1, T67): el grupo base lleva RequireAuthorization, el sobre de error y el metadato
        // .AuditarIntentoDenegado(ModuloDeAuditoria.Lending) —un 404 por falta de permiso deja Lending.Access.Denied y un 400 del validador
        // Lending.Request.Rejected—, y todo archivo de Endpoints/Lending arma sus grupos con LendingGroup.Grupo, nunca con un MapGroup propio
        // (que se saltaría la auditoría). El permiso de cada ruta lo sigue exigiendo Cada_ruta_lleva_RequirePermission.
        var carpeta = Path.Combine(Api, "Endpoints", "Lending");
        var grupo = FuenteSinComentarios.Leer(Path.Combine(carpeta, GrupoBaseDeCartera));
        Assert.Contains(".RequireAuthorization()", grupo, StringComparison.Ordinal);
        Assert.Contains(".AuditarIntentoDenegado(ModuloDeAuditoria.Lending)", grupo, StringComparison.Ordinal);
        Assert.Contains("AddEndpointFilter<ErrorEnvelopeFilter>()", grupo, StringComparison.Ordinal);
        Assert.Contains("\"/api/lending\"", grupo, StringComparison.Ordinal);
        Assert.Empty(Tramos(grupo));

        var infractores = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs").Where(a => Path.GetFileName(a) != GrupoBaseDeCartera))
        {
            var fuente = FuenteSinComentarios.Leer(archivo);
            if (Regex.IsMatch(fuente, @"\.MapGroup\(")) infractores.Add($"{Path.GetFileName(archivo)}: arma un grupo propio con MapGroup");
            if (!fuente.Contains("LendingGroup.Grupo(", StringComparison.Ordinal)) infractores.Add($"{Path.GetFileName(archivo)}: no usa LendingGroup.Grupo");
            if (Regex.IsMatch(fuente, @"\bapp\.Map(Get|Post|Put|Delete|Patch)\(")) infractores.Add($"{Path.GetFileName(archivo)}: publica rutas fuera del grupo");
        }
        Assert.True(infractores.Count == 0, "Rutas de Cartera fuera del grupo base (T67):\n  " + string.Join("\n  ", infractores));
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
    public void Lo_conservado_de_ahorros_se_retiro_y_no_vuelve()
    {
        // Feature 014 (T009, R28): las 12 familias de /api/savings y los informes de /api/reports/savings se retiraron sin alias, y
        // con ellas su exención por nombre. Que no vuelvan las carpetas: lo de captaciones vive en Endpoints/Lending/Deposits.
        Assert.False(Directory.Exists(Path.Combine(Api, "Endpoints", "Savings")), "Endpoints/Savings volvió: las captaciones viven en Endpoints/Lending/Deposits.");
        Assert.False(Directory.Exists(Path.Combine(Api, "Endpoints", "CDT")), "Endpoints/CDT volvió: el CDAT vive en Endpoints/Lending/Deposits.");
        Assert.False(File.Exists(Path.Combine(Api, "Endpoints", "Reports", "SavingsReportsEndpoints.cs")), "Volvió SavingsReportsEndpoints.cs.");
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
    public void Las_vistas_de_inventario_exigen_ver_y_exportar()
    {
        // Feature 012 (T182; contracts/api.md §27): toda vista de /api/reports/inventory se publica con
        // MapVistaDeInventario, que pone Inventory.Reports.View y, al exportar, Inventory.Reports.Export. Una ruta
        // publicada a mano en el archivo del centro de informes se saltaría la auditoría de la exportación y el permiso
        // de datos personales: además del registro de vistas, sólo esa extensión puede llamar a MapGet.
        var archivo = Path.Combine(Api, "Endpoints", "Reports", "InventoryReportsEndpoints.cs");
        Assert.True(File.Exists(archivo), $"No existe {archivo}.");

        var tramos = Tramos(File.ReadAllText(archivo));
        Assert.True(tramos.Count == 2,
            $"InventoryReportsEndpoints.cs publica {tramos.Count} rutas con .Map*(: sólo el registro de vistas (GET /) y la de " +
            "MapVistaDeInventario. Registrá cada vista con group.MapVistaDeInventario(…).");
        Assert.All(tramos, t => Assert.Contains(".RequirePermission(PermisoDeVer)", t.Replace("\"Inventory.Reports.View\"", "PermisoDeVer"), StringComparison.Ordinal));
        Assert.Contains(".RequirePermissionWhenExporting(PermisoDeExportar)", tramos[1], StringComparison.Ordinal);
        Assert.Contains("EmitirExportacionAsync", tramos[1], StringComparison.Ordinal);
        Assert.Contains("PermisoDeDatosPersonales", tramos[1], StringComparison.Ordinal);

        // T416 (US12; SC-014, FR-087, §2.18): los dos permisos son los del catálogo, al pie de la letra. Cada vista pasa
        // por esa única ruta, así que cada una lleva RequirePermission(View) y RequirePermissionWhenExporting(Export).
        var fuente = FuenteSinComentarios.Leer(archivo);
        Assert.Contains("PermisoDeVer = \"Inventory.Reports.View\"", fuente, StringComparison.Ordinal);
        Assert.Contains("PermisoDeExportar = \"Inventory.Reports.Export\"", fuente, StringComparison.Ordinal);
        Assert.DoesNotContain(".RequirePermissionWhenExporting(\"", fuente.Replace(".RequirePermissionWhenExporting(PermisoDeExportar)", string.Empty),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Las_vistas_de_cartera_exigen_ver_y_exportar()
    {
        // Feature 013, US15 (T0773; contracts/api.md §17, DT §2.25): toda vista de /api/reports/lending se publica con MapVistaDeCartera —una
        // ruta por vista—, que pone Lending.Reports.View y, al exportar, Lending.Reports.Export, más Lending.Loans.Read para consultar y
        // Lending.Reports.ExportPersonalData para exportar en las vistas con datos del deudor (C84), y audita Lending.Report.Exported. Además del
        // registro de vistas, sólo esa extensión puede llamar a MapGet; las rutas heredadas (aging/pdf, loan-statement) no vuelven.
        var archivo = Path.Combine(Api, "Endpoints", "Reports", "LendingReportsEndpoints.cs");
        Assert.True(File.Exists(archivo), $"No existe {archivo}.");

        var tramos = Tramos(File.ReadAllText(archivo));
        Assert.True(tramos.Count == 2,
            $"LendingReportsEndpoints.cs publica {tramos.Count} rutas con .Map*(: sólo el registro de vistas (GET /) y la de " +
            "MapVistaDeCartera. Registrá cada vista con group.MapVistaDeCartera(…).");
        Assert.All(tramos, t => Assert.Contains(".RequirePermission(", t, StringComparison.Ordinal));
        Assert.Contains(".RequirePermission(PermisoDeVer)", tramos[1], StringComparison.Ordinal);
        Assert.Contains(".RequirePermissionWhenExporting(PermisoDeExportar)", tramos[1], StringComparison.Ordinal);
        Assert.Contains("EmitirExportacionAsync", tramos[1], StringComparison.Ordinal);
        Assert.Contains("PermisoDeDatosPersonales", tramos[1], StringComparison.Ordinal);
        Assert.Contains("RequiredPermission", tramos[1], StringComparison.Ordinal);

        var fuente = FuenteSinComentarios.Leer(archivo);
        Assert.Contains("PermisoDeVer = \"Lending.Reports.View\"", fuente, StringComparison.Ordinal);
        Assert.Contains("PermisoDeExportar = \"Lending.Reports.Export\"", fuente, StringComparison.Ordinal);
        Assert.Contains("PermisoDeDatosPersonales = \"Lending.Reports.ExportPersonalData\"", fuente, StringComparison.Ordinal);
        Assert.DoesNotContain("aging/pdf", fuente, StringComparison.Ordinal);
        Assert.DoesNotContain("loan-statement", fuente, StringComparison.Ordinal);

        // Una ruta por vista: cada MapVistaDeCartera del registro nombra una vista distinta (las siete de K1, como mínimo).
        var vistas = Regex.Matches(fuente, @"\.MapVistaDeCartera\((\w+)\.Vista").Select(m => m.Groups[1].Value).ToList();
        Assert.True(vistas.Count >= 7, $"Se esperaban al menos las siete vistas de K1 y hay {vistas.Count}.");
        Assert.Equal(vistas.Count, vistas.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Las_rutas_de_la_integracion_contable_de_inventario_entran_al_recorrido()
    {
        // Feature 012, T447 (US7, I2): el lado contable (/api/accounting/inventory) y la bandeja de Inventario
        // (/api/inventory/messages) entran por los globs de Accounting e Inventory; esta prueba fija que existan y que el
        // recorrido los vea, para que un traslado de carpeta no los deje fuera en silencio. Las otras tres guardas de
        // contracts/contabilidad.md §12.8 —NingunModuloEscribeMovimientosFueraDelContrato, PrincipioXI_ContableImmutable y
        // LaContabilidadNoTieneCuentasEnCodigo— siguen verdes sin tocarlas.
        var archivos = Archivos().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var esperado in new[]
                 {
                     Path.Combine("Endpoints", "Accounting", "InventoryIntegrationEndpoints.cs"),
                     Path.Combine("Endpoints", "Inventory", "MessagesEndpoints.cs"),
                 })
        {
            Assert.True(File.Exists(Path.Combine(Api, esperado)), $"No existe {esperado} (T528, T529).");
            Assert.Contains(esperado, archivos);
        }
    }

    [Fact]
    public void Las_rutas_del_registro_central_exigen_al_maestro()
    {
        // Feature 013, fase 2 (T0038; B5, contracts/api.md §21.5): /api/saas/national-values y /api/saas/regulatory-documents no llevan
        // código de permiso —corren sin cooperativa—: el grupo entero pasa por RequireMasterAdminAttribute.Check (maestro con segundo
        // factor). Se fija que cada grupo del archivo se arme con esa guarda y que ninguna ruta se publique fuera de un grupo.
        var archivo = Path.Combine(Api, "Endpoints", "Saas", "NationalValuesEndpoints.cs");
        Assert.True(File.Exists(archivo), $"No existe {archivo}.");
        var fuente = FuenteSinComentarios.Leer(archivo);
        Assert.Contains("RequireMasterAdminAttribute.Check(", fuente, StringComparison.Ordinal);
        Assert.Contains("\"/api/saas/national-values\"", fuente, StringComparison.Ordinal);
        Assert.Contains("\"/api/saas/regulatory-documents\"", fuente, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(fuente, @"\.MapGroup\(").Count);
        Assert.DoesNotMatch(@"\bapp\.Map(Get|Post|Put|Delete|Patch)\(", fuente);
        Assert.True(Tramos(fuente).Count >= 10, "El registro central publica al menos diez rutas (§21.5).");
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
