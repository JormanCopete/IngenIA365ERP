using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 010, revisión adversarial de N1 (lote «pantallas», 2026-09-21): lo que la pantalla ofrece
/// tiene que ser lo que la API acepta. Cuatro desajustes se colaron en la entrega y ninguna prueba
/// los vio porque el repositorio no tiene pruebas de navegador: un botón «Aprobar» sobre un borrador
/// <c>Stale</c> que el servidor rechaza con <c>NotDraft</c>; «Marcar pagados» sin el gate de
/// <c>Payroll.Payments.Mark</c> que el Operador no tiene; «Registrar disfrute» detrás de un solo permiso
/// mientras la vista previa obligatoria que cruza pedía otro; y tres pantallas sin gate de lectura, que ante
/// el 404 indistinguible (FR-017) decían «no se pudieron cargar» en vez de «sin permiso».
/// Es una prueba sobre el fuente, como <see cref="LasPantallasDicenQueEstanCargando"/>.
/// </summary>
public class LasPantallasDeNominaSiguenAlContrato
{
    private static string Raiz => RepoPath.FindRepoRoot();
    private static string Nomina => Path.Combine(Raiz, "src", "Presentation", "IngenIA365ERP.Shared", "Pages", "Nomina");

    /// <summary>Las pantallas de la feature 010 y el recurso cuyo <c>View</c> exige la API en sus lecturas.</summary>
    public static TheoryData<string, string> PantallasConSuRecurso => new()
    {
        { "Prima.razor", "Payroll.ServiceBonus" },
        { "CesantiasAnuales.razor", "Payroll.Severance" },
        { "Vacaciones.razor", "Payroll.Vacations" },
        { "LiquidacionDefinitiva.razor", "Payroll.Settlements" },
        { "Politicas.razor", "Payroll.CompanyPolicies" },
        { "Festivos.razor", "Payroll.Holidays" },
        { "SaldosIniciales.razor", "Payroll.BenefitBalances" },
    };

    private static string Leer(string pantalla)
    {
        var ruta = Path.Combine(Nomina, pantalla);
        Assert.True(File.Exists(ruta), $"No existe {ruta}: si la pantalla se movió, actualizá la lista.");
        return File.ReadAllText(ruta);
    }

    private static IEnumerable<string> PantallasDeNomina() =>
        Directory.EnumerateFiles(Nomina, "*.razor").OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>Lo que precede a cada aparición de <paramref name="patron"/>, para mirar qué la envuelve.</summary>
    private static IEnumerable<(int Linea, string Antes)> Contextos(string texto, Regex patron, int ventana)
    {
        foreach (Match m in patron.Matches(texto))
        {
            var desde = Math.Max(0, m.Index - ventana);
            var linea = texto[..m.Index].Count(c => c == '\n') + 1;
            yield return (linea, texto[desde..m.Index]);
        }
    }

    [Theory]
    [MemberData(nameof(PantallasConSuRecurso))]
    public void Cada_pantalla_de_la_feature_protege_la_lectura_con_su_View_y_traduce_el_404_a_sin_permiso(string pantalla, string recurso)
    {
        var texto = Leer(pantalla);

        Assert.True(texto.Contains($"<PermissionGate Required=\"{recurso}.View\" MostrarAviso=\"true\">", StringComparison.Ordinal),
            $"{pantalla} no envuelve la página en <PermissionGate Required=\"{recurso}.View\" MostrarAviso=\"true\">: " +
            "sin el gate, quien no tiene el permiso ve la grilla vacía y un «no se pudieron cargar» (la API responde 404 indistinguible, FR-017).");

        Assert.True(texto.Contains("NotificationServiceExtensions.SinPermiso", StringComparison.Ordinal),
            $"{pantalla} no traduce Generic.NotFound a NotificationServiceExtensions.SinPermiso en ninguna carga.");
    }

    [Fact]
    public void Marcar_pagados_va_detras_de_Payments_Mark_en_toda_pantalla()
    {
        // POST /api/payroll/runs/{id}/payments exige Payroll.Payments.Mark (PayrollPaymentsEndpoints) y el
        // Operador no lo tiene (BuiltInRolesSeeder): sin gate ve el botón, llena el diálogo y recibe un 404.
        var patron = new Regex(@">Marcar pagados</SfButton>", RegexOptions.Compiled);
        var sinGate = new List<string>();
        var vistos = 0;
        foreach (var archivo in PantallasDeNomina())
        {
            var texto = File.ReadAllText(archivo);
            foreach (var (linea, antes) in Contextos(texto, patron, 600))
            {
                vistos++;
                if (!antes.Contains("Required=\"Payroll.Payments.Mark\"", StringComparison.Ordinal))
                    sinGate.Add($"{Path.GetFileName(archivo)}:{linea}");
            }
        }

        Assert.True(vistos > 0, "No se encontró ningún botón «Marcar pagados» en Pages/Nomina: ¿cambió el texto?");
        Assert.True(sinGate.Count == 0, "Botones «Marcar pagados» sin <PermissionGate Required=\"Payroll.Payments.Mark\">:\n  " + string.Join("\n  ", sinGate));
    }

    [Fact]
    public void Aprobar_solo_se_ofrece_cuando_la_corrida_se_puede_aprobar()
    {
        // SettlementRunWorkflow.ApproveAsync sólo acepta Draft: un borrador Stale (toda vigencia nueva de
        // política lo deja así, D-19) responde Payroll.Settlement.NotDraft. EsBorrador incluye Stale y
        // sirve para Recalcular y Descartar; el botón Aprobar mira SePuedeAprobar.
        var patron = new Regex(@"OnClick=""@(AbrirAprobacion""|\(\(\)\s*=>\s*AbrirAprobacion\()", RegexOptions.Compiled);
        // La ordinaria (005) lo escribe como `_corrida is { Status: "Draft" }`; las especiales, `SePuedeAprobar`.
        var soloDraft = new Regex(@"SePuedeAprobar|Status\s*(==|:)\s*""Draft""", RegexOptions.Compiled);
        var sueltos = new List<string>();
        var vistos = 0;
        foreach (var archivo in PantallasDeNomina())
        {
            var texto = File.ReadAllText(archivo);
            foreach (var (linea, antes) in Contextos(texto, patron, 700))
            {
                vistos++;
                if (!soloDraft.IsMatch(antes))
                    sueltos.Add($"{Path.GetFileName(archivo)}:{linea}");
            }
        }

        Assert.True(vistos >= 4, $"Se esperaban al menos los botones Aprobar de prima, cesantías, vacaciones y definitiva; se vieron {vistos}.");
        Assert.True(sueltos.Count == 0,
            "Botones «Aprobar» que no dependen de SePuedeAprobar (la API rechaza Stale con NotDraft):\n  " + string.Join("\n  ", sueltos));
    }

    [Fact]
    public void Registrar_vacaciones_exige_en_la_pantalla_los_permisos_de_los_dos_endpoints_que_cruza()
    {
        // D-28: «Registrar disfrute o compensación» cruza dos endpoints con permisos distintos —la vista
        // previa obligatoria de hábiles (FR-015, Register) y el registro que crea la liquidación
        // (Calculate)— y el botón lleva la unión de los dos: un botón que se ve y no se puede terminar
        // es peor que uno que no se ve. Los permisos se leen del endpoint, no se escriben aquí, para que
        // un cambio en la API mueva la exigencia de la pantalla en vez de dejarla desalineada en silencio.
        var endpoints = File.ReadAllText(Path.Combine(Raiz, "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Payroll", "VacationsEndpoints.cs"));
        var tramos = LosEndpointsProtegidosExigenPermiso.Tramos(endpoints);
        var registro = Permisos(tramos.Single(t => t.Contains("Payroll_Settlements_Vacations_Calculate", StringComparison.Ordinal)));
        var vistaPrevia = Permisos(tramos.Single(t => t.Contains("Payroll_Vacations_WorkingDays", StringComparison.Ordinal)));
        var exigidos = registro.Union(vistaPrevia).ToHashSet(StringComparer.Ordinal);

        Assert.True(exigidos.Count > 0, "Ni el registro ni la vista previa de hábiles exigen permiso: ¿cambió el nombre de los endpoints?");

        var pantalla = Leer("Vacaciones.razor");
        var patron = new Regex(@"OnClick=""@\(\(\)\s*=>\s*AbrirRegistro\(", RegexOptions.Compiled);
        var incompletos = new List<string>();
        var vistos = 0;
        foreach (var (linea, antes) in Contextos(pantalla, patron, 700))
        {
            vistos++;
            var faltan = exigidos.Where(p => !antes.Contains($"Required=\"{p}\"", StringComparison.Ordinal)).ToList();
            if (faltan.Count > 0) incompletos.Add($"Vacaciones.razor:{linea} sin {string.Join(", ", faltan)}");
        }

        Assert.True(vistos > 0, "No se encontró el botón «Registrar disfrute o compensación» en Vacaciones.razor.");
        Assert.True(incompletos.Count == 0, "Botones de registro sin todos los gates de los endpoints que cruza (D-28):\n  " + string.Join("\n  ", incompletos));

        static HashSet<string> Permisos(string tramo) =>
            Regex.Matches(tramo, @"\.RequirePermission\(""([^""]+)""\)").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void Toda_accion_de_vacaciones_que_puede_caer_en_un_periodo_aprobado_ofrece_el_ajuste_retroactivo()
    {
        // Registrar, aprobar y recalcular pasan por VacationNoveltyPlanner y las tres responden
        // Payroll.Vacation.PeriodApproved cuando un período del disfrute ya se aprobó; las tres admiten
        // AcceptRetroactive. Hasta el 2026-09-21 «Recalcular» sólo mostraba el mensaje y la única salida
        // era descartar el borrador y registrar de nuevo, perdiendo la versión.
        var pantalla = Leer("Vacaciones.razor");
        var llamadas = new Regex(@"Nomina\.(Registrar|Aprobar|Recalcular)VacacionesAsync\(", RegexOptions.Compiled);
        var sinSalida = new List<string>();
        var vistas = 0;
        foreach (Match m in llamadas.Matches(pantalla))
        {
            vistas++;
            var despues = pantalla[m.Index..Math.Min(pantalla.Length, m.Index + 900)];
            if (!despues.Contains("\"Payroll.Vacation.PeriodApproved\"", StringComparison.Ordinal))
                sinSalida.Add($"Vacaciones.razor:{pantalla[..m.Index].Count(c => c == '\n') + 1} ({m.Groups[1].Value})");
        }

        Assert.True(vistas >= 3, $"Se esperaban las llamadas de registrar, aprobar y recalcular; se vieron {vistas}.");
        Assert.True(sinSalida.Count == 0,
            "Llamadas que pueden responder PeriodApproved sin ofrecer el ajuste retroactivo:\n  " + string.Join("\n  ", sinSalida));

        Assert.True(Regex.IsMatch(pantalla, @"RecalcularVacacionesAsync\([^,\)]+,\s*aceptarRetroactivo\)"),
            "RecalcularVacacionesAsync se llama sin el segundo argumento: AcceptRetroactive viaja siempre en false y el reintento aceptando el ajuste es imposible.");
    }
}
