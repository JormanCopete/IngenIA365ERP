namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// 2026-11-19 (pedido del dueño): qué opción del menú usa cada permiso, de qué módulo es y qué tanto pesa. Las opciones salen
/// de <c>MapaDelMenu.g.cs</c>, que genera <c>tools/scripts/generar-mapa-del-menu.py</c> leyendo el menú y los
/// <c>&lt;PermissionGate&gt;</c> de cada pantalla; aquí sólo se consulta. Un permiso que no aparece en ninguna opción se usa
/// en la API (un proceso, una exportación, un paso de otra pantalla): la pantalla de Roles lo dice así, sin inventar opción.
/// </summary>
public static partial class MapaDelMenu
{
    /// <summary>Una opción del menú.</summary>
    /// <param name="Modulo">El módulo del menú donde está (el título del grupo).</param>
    /// <param name="Subgrupo">El título dentro del módulo, si lo tiene.</param>
    /// <param name="Nombre">El texto de la opción.</param>
    /// <param name="Ruta">La dirección a la que lleva.</param>
    /// <param name="PermisosDelMenu">Sin alguno de éstos la opción ni se muestra en el menú.</param>
    /// <param name="PermisosDeAcceso">Los que abren la pantalla (sin ellos dice «No tenés permiso para esta sección»).</param>
    /// <param name="PermisosDeAcciones">Los que habilitan botones o secciones dentro de la pantalla.</param>
    public sealed record Opcion(
        string Modulo, string? Subgrupo, string Nombre, string Ruta,
        IReadOnlyList<string> PermisosDelMenu, IReadOnlyList<string> PermisosDeAcceso, IReadOnlyList<string> PermisosDeAcciones)
    {
        /// <summary>«Módulo › Subgrupo › Opción».</summary>
        public string Camino => Subgrupo is null ? $"{Modulo} › {Nombre}" : $"{Modulo} › {Subgrupo} › {Nombre}";

        /// <summary>La ruta sin la consulta, en minúsculas.</summary>
        public string RutaSinConsulta => Ruta.Split('?')[0].ToLowerInvariant();

        /// <summary>Todo lo que la opción necesita para verse y abrirse.</summary>
        public IEnumerable<string> PermisosParaEntrar => PermisosDelMenu.Concat(PermisosDeAcceso).Distinct(StringComparer.Ordinal);
    }

    /// <summary>Cómo usa una opción un permiso.</summary>
    public enum Uso
    {
        /// <summary>Sin él la opción no aparece en el menú o la pantalla no abre.</summary>
        Abre,
        /// <summary>Habilita botones o secciones dentro de la pantalla.</summary>
        Acciones,
    }

    /// <summary>Una opción donde se usa un permiso, y cómo.</summary>
    public sealed record UsoEnElMenu(Opcion Opcion, Uso Uso);

    private static readonly Lazy<Dictionary<string, List<UsoEnElMenu>>> PorPermiso = new(() =>
    {
        var mapa = new Dictionary<string, List<UsoEnElMenu>>(StringComparer.OrdinalIgnoreCase);
        void Agregar(string codigo, Opcion o, Uso uso)
        {
            if (!mapa.TryGetValue(codigo, out var lista)) mapa[codigo] = lista = [];
            if (lista.Any(x => x.Opcion == o)) return;
            lista.Add(new UsoEnElMenu(o, uso));
        }
        foreach (var o in Opciones)
        {
            foreach (var c in o.PermisosParaEntrar) Agregar(c, o, Uso.Abre);
        }
        foreach (var o in Opciones)
        {
            foreach (var c in o.PermisosDeAcciones) Agregar(c, o, Uso.Acciones);
        }
        return mapa;
    });

    /// <summary>Las opciones del menú donde se usa el permiso; vacía si sólo lo pide la API.</summary>
    public static IReadOnlyList<UsoEnElMenu> DondeSeUsa(string codigo) =>
        PorPermiso.Value.TryGetValue(codigo, out var lista) ? lista : [];

    /// <summary>
    /// Si la dirección actual es una opción del módulo del menú. La usa el grupo del menú para saber, sin preguntarle al
    /// navegador, si está abierto por contener la pantalla actual y así cerrarlo con un solo clic.
    /// </summary>
    public static bool ElModuloContiene(string modulo, string rutaActual)
    {
        var ruta = rutaActual.Split('?', '#')[0].TrimEnd('/').ToLowerInvariant();
        if (ruta.Length == 0) ruta = "/";
        return Opciones.Any(o => string.Equals(o.Modulo, modulo, StringComparison.OrdinalIgnoreCase) && Coincide(o.RutaSinConsulta, ruta));
    }

    /// <summary>
    /// 2026-11-23: si quien tiene estos permisos ve la opción en el menú. Necesita los del menú (los <c>PermissionGate</c>
    /// anidados que la envuelven) y, si la pantalla pide alguno para abrir, al menos uno de ellos: con varios, cada sección de
    /// la pantalla pide el suyo y basta uno para que algo se vea. Una opción que no pide nada se ve siempre.
    /// </summary>
    public static bool PuedeVer(Opcion opcion, IReadOnlySet<string> codigos) =>
        opcion.PermisosDelMenu.All(codigos.Contains)
        && (opcion.PermisosDeAcceso.Count == 0 || opcion.PermisosDeAcceso.Any(codigos.Contains));

    /// <summary>La opción del menú con esa dirección exacta (con su consulta), o nula.</summary>
    public static Opcion? Buscar(string ruta) =>
        Opciones.FirstOrDefault(o => string.Equals(o.Ruta, ruta, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 2026-11-23: si el módulo del menú tiene alguna opción que quien tiene estos permisos pueda ver. Un módulo que el mapa no
    /// conoce (sin opciones) se deja ver: no hay con qué decidir.
    /// </summary>
    public static bool ElModuloTieneAlgoVisible(string modulo, IReadOnlySet<string> codigos)
    {
        var delModulo = Opciones.Where(o => string.Equals(o.Modulo, modulo, StringComparison.OrdinalIgnoreCase)).ToList();
        return delModulo.Count == 0 || delModulo.Any(o => PuedeVer(o, codigos));
    }

    private static bool Coincide(string opcion, string ruta)
    {
        if (opcion == "/") return ruta == "/";
        var o = opcion.TrimEnd('/');
        return ruta == o || ruta.StartsWith(o + "/", StringComparison.Ordinal);
    }

    /// <summary>El nombre del módulo de un permiso (la primera parte del código) como lo lee una persona.</summary>
    public static string NombreDelModulo(string modulo) => modulo switch
    {
        "Accounting" => "Contabilidad",
        "Admin" => "Administración",
        "Channel" => "Canal digital",
        "Compliance" => "Cumplimiento",
        "Core" => "Maestros y personas",
        "ElectronicInvoicing" => "Facturación electrónica",
        "Implantacion" => "Implantación",
        "Inventory" => "Inventario, compras y ventas",
        "Lending" => "Cartera y captaciones",
        "Payroll" => "Nómina",
        "Platform" => "Plataforma",
        "Receipts" => "Recaudo",
        "Saas" => "Consola SaaS",
        "Security" => "Seguridad",
        "Transport" => "Transporte",
        "Treasury" => "Tesorería",
        "Attachments" => "Adjuntos",
        "Audit" => "Auditoría",
        "Reports" => "Reportes",
        _ => modulo,
    };

    /// <summary>Qué tanto pesa un permiso, para avisar antes de concederlo.</summary>
    public enum Peso
    {
        /// <summary>Sólo deja ver o imprimir.</summary>
        Lectura,
        /// <summary>Deja registrar u operar.</summary>
        Operacion,
        /// <summary>Aprueba, anula, reversa, reabre, borra, administra la seguridad o salta un control.</summary>
        Sensible,
    }

    private static readonly HashSet<string> AccionesDeLectura = new(StringComparer.OrdinalIgnoreCase)
    {
        "View", "Read", "ViewHistory", "ViewExpected", "Print", "Reprint", "Verify",
    };

    private static readonly HashSet<string> AccionesSensibles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Approve", "Void", "Reverse", "Reopen", "Delete", "Override", "Waive", "Release", "Unlock", "PolicyChange",
        "ExportPersonalData", "Terminate", "All", "Supervisor", "Management", "Disburse", "WriteOff", "Transfer",
        "Withdraw", "Rebuild", "ViewAll", "CloseYear", "AcceptActivationDifference", "DisableFiscalPosting",
    };

    private static readonly string[] ModulosDeSeguridad = ["Security.", "Admin.", "Platform.", "Saas."];

    /// <summary>El peso de un permiso por su código <c>Recurso.Acción</c>.</summary>
    public static Peso PesoDe(string codigo)
    {
        var punto = codigo.LastIndexOf('.');
        var accion = punto >= 0 ? codigo[(punto + 1)..] : codigo;
        if (AccionesDeLectura.Contains(accion)) return Peso.Lectura;
        if (AccionesSensibles.Contains(accion)) return Peso.Sensible;
        if (ModulosDeSeguridad.Any(m => codigo.StartsWith(m, StringComparison.OrdinalIgnoreCase))) return Peso.Sensible;
        if (accion.StartsWith("Accept", StringComparison.OrdinalIgnoreCase)) return Peso.Sensible;
        return Peso.Operacion;
    }

    /// <summary>Cómo se dice el peso en pantalla.</summary>
    public static string TextoDelPeso(Peso peso) => peso switch
    {
        Peso.Lectura => "Consulta",
        Peso.Operacion => "Opera",
        _ => "Sensible",
    };
}
