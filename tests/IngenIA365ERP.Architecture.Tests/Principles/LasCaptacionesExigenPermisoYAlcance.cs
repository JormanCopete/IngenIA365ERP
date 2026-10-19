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
        // US9 (T153–T156): la carga de migración es de toda la cooperativa (como la de créditos, la 013 §29); la importación de cada parte y su descarga
        // con datos sí aplican el alcance de quien importa (sus archivos lo nombran).
        ["CreateDepositMigrationLoadCommand"] = "la carga de migración es de toda la cooperativa, sin oficina ni línea (como CreateMigrationLoadCommand)",
        ["ListDepositMigrationLoadsQuery"] = "las cargas de migración son de toda la cooperativa: partes, conteos y totales, sin titulares",
        ["GetDepositMigrationLoadQuery"] = "las cargas de migración son de toda la cooperativa: partes, conteos y totales, sin titulares",
        ["ApplyDepositMigrationLoadCommand"] = "aplicar la carga es de toda la cooperativa: sus filas ya pasaron por el alcance de quien las importó",
        ["GetDepositMigrationBalanceCheckQuery"] = "el cuadre de la carga es por conjunto de cuentas de toda la cooperativa, sin titulares",
        ["GetDepositReferenceFiguresQuery"] = "las cifras de referencia son totales por línea y concepto de toda la cooperativa",
        ["SetDepositReferenceFiguresCommand"] = "las cifras de referencia son totales por línea y concepto de toda la cooperativa",
        ["GetDepositsActivationStatusQuery"] = "la activación es de toda la cooperativa (FR-116): requisitos y conteos",
        ["ConsumeChargeInvoiceRegisteredCommand"] = "consumo de la bandeja (Comercial informa la factura de un cargo): sin usuario, sin ruta ni alcance (I-2)",
        // US16 (T162): crear una línea no toca una línea existente ni una oficina; la línea nueva entra al alcance de cada usuario por su asignación de
        // líneas (LND_UserCreditLineScopes) o por Lending.Scope.AllLines, como las líneas de crédito de la 013.
        ["CreateDepositLineCommand"] = "crear una línea no toca una línea existente ni una oficina; entra al alcance por asignación (como CreateCreditLineCommand)",
        ["CreateContributionLineCommand"] = "crear una línea no toca una línea existente ni una oficina; entra al alcance por asignación (como CreateCreditLineCommand)",
    };

    /// <summary>
    /// US16 (T162): los ayudantes de captaciones que aplican el alcance por dentro; un handler que los usa lo aplica aunque no nombre
    /// <c>IAlcanceDeCartera</c> (la solicitud de aportes lee su cuenta y la del destino de una cesión con <c>SolicitudesDeAportes</c>).
    /// </summary>
    // US12 (T203): DistribucionesDeAsamblea exige alcance total (una decisión de asamblea es de toda la cooperativa) y responde el 404 de lo inexistente.
    private static readonly string[] AyudantesConAlcance = ["SolicitudesDeAportes", "DistribucionesDeAsamblea"];

    private static readonly Regex ConsultaOComando = new(@"public\s+sealed\s+record\s+(\w+(Query|Command))\b", RegexOptions.Compiled);

    private static readonly Regex AplicaElAlcance = new(@"\b(IAlcanceDeCartera|AlcanceDeCarteraDelActor|FiltroDeAlcanceDeCartera)\b", RegexOptions.Compiled);

    /// <summary>
    /// US16 (T162; FR-004, SC-015): la regla anterior mira el archivo; ésta mira el <b>handler</b> de cada consulta y comando —que nombre
    /// <c>IAlcanceDeCartera</c> o un ayudante que lo aplica por dentro—, porque un archivo con varias consultas pasaba con que una sola lo aplicara.
    /// </summary>
    [Fact]
    public void Cada_handler_de_captaciones_aplica_el_alcance_o_su_consulta_esta_exceptuada()
    {
        var raiz = RepoPath.FindRepoRoot();
        var archivos = ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Application/Lending/Deposits"])
            .Select(a => (Archivo: a, Fuente: FuenteSinComentarios.Leer(a))).ToList();
        var todo = string.Join('\n', archivos.Select(a => a.Fuente));
        var infracciones = new List<string>();
        foreach (var (archivo, fuente) in archivos)
        {
            foreach (Match m in ConsultaOComando.Matches(fuente))
            {
                var nombre = m.Groups[1].Value;
                if (Exceptuados.ContainsKey(nombre)) continue;
                var inicio = todo.IndexOf($"class {nombre}Handler", StringComparison.Ordinal);
                if (inicio < 0) { infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {nombre} no tiene su {nombre}Handler en la carpeta"); continue; }
                var siguiente = todo.IndexOf("\npublic ", inicio + 1, StringComparison.Ordinal);
                var cuerpo = siguiente < 0 ? todo[inicio..] : todo[inicio..siguiente];
                if (!AplicaElAlcance.IsMatch(cuerpo) && !AyudantesConAlcance.Any(a => cuerpo.Contains(a, StringComparison.Ordinal)))
                    infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {nombre}Handler no aplica IAlcanceDeCartera ni usa un ayudante que lo aplique");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Handlers de captaciones sin alcance (FR-004, SC-015)");
    }

    /// <summary>
    /// US16 (T162; api.md §1.2): ninguna consulta ni comando de captaciones lee una cuenta por el <c>PublicId</c> que trae el pedido sin pasar por
    /// <c>LectorDeCuentas.CuentaEnAlcanceAsync</c> —tampoco la <b>otra</b> cuenta de una operación: el destino de un traslado o de una cancelación—. Hasta
    /// esta fase el destino se leía directo y un usuario de la oficina norte trasladaba a una cuenta de la oficina sur.
    /// </summary>
    [Fact]
    public void Ninguna_cuenta_se_lee_por_el_pedido_sin_pasar_por_el_alcance()
    {
        var raiz = RepoPath.FindRepoRoot();
        var lecturaDirecta = new Regex(@"MemberAccounts[^;]*?\.PublicId\s*==\s*request\.", RegexOptions.Compiled | RegexOptions.Singleline);
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Application/Lending/Deposits"]))
        {
            var fuente = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in lecturaDirecta.Matches(fuente))
                infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {m.Value.Split('\n')[0].Trim()}");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Una cuenta se lee por el pedido sin el alcance (api.md §1.2)");
    }

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
                if (!AplicaElAlcance.IsMatch(fuente) && !AyudantesConAlcance.Any(a => fuente.Contains(a, StringComparison.Ordinal)))
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
