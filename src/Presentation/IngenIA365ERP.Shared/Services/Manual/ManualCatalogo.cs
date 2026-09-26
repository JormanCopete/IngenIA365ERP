namespace IngenIA365ERP.Shared.Services.Manual;

/// <summary>
/// El manual del sistema: un tema por opción del menú, en un catálogo estático.
///
/// <para>
/// <b>Por qué estático y no en base.</b> El manual tiene que abrir cuando lo que
/// se quiere saber es «por qué esta pantalla no carga»; si dependiera de la API,
/// fallaría junto con ella. Y viaja con la versión: cada despliegue lleva el manual
/// que describe exactamente esas pantallas.
/// </para>
///
/// <para>
/// <b>Dos clases de tema.</b> Los procesos (registrar una cooperativa, invitar a
/// alguien, liquidar la nómina) tienen pasos escritos a mano. Los maestros y los
/// reportes comparten una guía general: todos se usan igual —«Nuevo», el lápiz para
/// corregir, la papelera para retirar, «Guardar»/«Cancelar»— y escribir ciento
/// veinte veces lo mismo sólo garantiza que algunas copias envejezcan mal. Lo
/// específico de cada uno (qué campos pide, qué valida) lo muestra la propia
/// pantalla.
/// </para>
///
/// <para>
/// <b>Capturas.</b> Cada paso puede llevar una imagen en
/// <c>wwwroot/img/manual/{slug}/paso-{n}.png</c>. Si no existe, no se muestra nada:
/// un tema sin capturas sigue completo. Cómo tomarlas: <c>docs/manual/README.md</c>.
/// </para>
/// </summary>
public static class ManualCatalogo
{
    public static class Modulos
    {
        public const string Inicio = "Inicio";
        public const string Cuenta = "Mi cuenta y acceso";
        public const string Saas = "Consola SaaS";
        public const string Administracion = "Administración";
        public const string Contabilidad = "Contabilidad";
        public const string Cartera = "Cartera Financiera";
        public const string Inventario = "Inventario";
        public const string Nomina = "Nómina";
        public const string Cdt = "CDT";
        public const string Debito = "Tarjeta Débito";
        public const string Tesoreria = "Tesorería";
        public const string Asociados = "Asociados";
        public const string Maestros = "Maestros";
        public const string Cumplimiento = "Cumplimiento";
        public const string Reportes = "Reportes";
        public const string Notificaciones = "Notificaciones";
    }

    /// <summary>Orden en que el índice presenta los módulos: primero lo que todo el mundo usa.</summary>
    public static readonly IReadOnlyList<string> OrdenDeModulos =
    [
        Modulos.Inicio, Modulos.Cuenta, Modulos.Administracion, Modulos.Saas,
        Modulos.Asociados, Modulos.Maestros, Modulos.Contabilidad, Modulos.Cartera,
        Modulos.Cdt, Modulos.Inventario, Modulos.Nomina, Modulos.Tesoreria,
        Modulos.Debito, Modulos.Cumplimiento, Modulos.Reportes, Modulos.Notificaciones,
    ];

    /// <summary>Lo que conviene leer el primer día, en orden.</summary>
    public static readonly IReadOnlyList<string> PrimerosPasos =
    [
        "iniciar-sesion", "activar-segundo-factor", "elegir-cooperativa", "usuarios",
        "invitar-persona", "roles-y-permisos", "personas", "registro-de-asociados",
        "comprobante-contable", "centro-de-reportes",
    ];

    public static IReadOnlyList<TemaDeManual> Temas { get; } = Construir();

    private static readonly Dictionary<string, TemaDeManual> Indice =
        Temas.ToDictionary(t => t.Slug, StringComparer.OrdinalIgnoreCase);

    public static TemaDeManual? PorSlug(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) && Indice.TryGetValue(slug.Trim(), out var t) ? t : null;

    public static IEnumerable<TemaDeManual> DelModulo(string modulo) =>
        Temas.Where(t => t.Modulo == modulo).OrderBy(t => t.Tipo).ThenBy(t => t.Titulo);

    /// <summary>
    /// El tema que explica la pantalla en la que está la persona. Primero la ruta
    /// exacta (o una plantilla con parámetros que la cubra), después el prefijo más
    /// largo: <c>/security/users/{id}/roles</c> cae en «Asignar roles», y
    /// <c>/cartera/creditos/{id}</c> en «Cartera de créditos».
    /// </summary>
    public static TemaDeManual? ParaRuta(string? rutaRelativa)
    {
        var ruta = NormalizarRuta(rutaRelativa);
        if (ruta.Length == 0) ruta = "/";

        foreach (var tema in Temas)
        {
            if (Coincide(tema.Ruta, ruta)) return tema;
            if (tema.RutasCubiertas.Any(r => Coincide(r, ruta))) return tema;
        }

        TemaDeManual? mejor = null;
        var mejorLargo = -1;
        foreach (var tema in Temas)
        {
            foreach (var candidata in tema.RutasCubiertas.Prepend(tema.Ruta))
            {
                var prefijo = SinParametros(candidata);
                if (prefijo.Length <= 1 || prefijo.Length <= mejorLargo) continue;
                if (ruta.StartsWith(prefijo + "/", StringComparison.Ordinal))
                {
                    mejor = tema;
                    mejorLargo = prefijo.Length;
                }
            }
        }
        return mejor;
    }

    private static string NormalizarRuta(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return "/";
        var r = ruta.Trim();
        var corte = r.IndexOfAny(['?', '#']);
        if (corte >= 0) r = r[..corte];
        if (!r.StartsWith('/')) r = "/" + r;
        return r.Length > 1 ? r.TrimEnd('/').ToLowerInvariant() : r;
    }

    private static string SinParametros(string plantilla)
    {
        var i = plantilla.IndexOf('{');
        var p = i < 0 ? plantilla : plantilla[..i];
        return p.Length > 1 ? p.TrimEnd('/').ToLowerInvariant() : p;
    }

    private static bool Coincide(string plantilla, string ruta)
    {
        var a = plantilla.ToLowerInvariant().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var b = ruta.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].StartsWith('{')) continue;
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    // ===================================================================== //
    //  Contenido                                                             //
    // ===================================================================== //

    private static List<TemaDeManual> Construir()
    {
        var t = new List<TemaDeManual>();

        // ------------------------------------------------------------ Inicio --
        t.Add(Proceso("dashboard", "Panel de inicio", Modulos.Inicio, "/",
            "Lo primero que se ve al entrar: indicadores de la cooperativa activa y accesos rápidos. No se registra nada desde aquí.",
            [
                P("Leer los indicadores", "Cada tarjeta resume un módulo. Los valores son de la cooperativa que figura arriba a la derecha; si cambiás de cooperativa, cambian."),
                P("Ir a una pantalla", "El menú lateral agrupa las opciones por módulo. El botón de las tres rayas lo pliega para ganar espacio; plegado, cada grupo se abre al pasar el puntero."),
                P("Pedir ayuda", "En la barra superior, «¿Cómo se hace?» abre la guía de la pantalla en la que estés. Este manual completo está en SISTEMA → Manual del sistema.", "/manual", "Abrir el manual"),
            ],
            ["inicio", "dashboard", "tablero", "indicadores", "menu", "empezar"],
            [], ["iniciar-sesion", "elegir-cooperativa"], ["/home"], TipoDeTema.Consulta));

        // ------------------------------------------------- Mi cuenta y acceso --
        t.Add(Proceso("iniciar-sesion", "Iniciar sesión", Modulos.Cuenta, "/login",
            "Entrar al sistema con correo y contraseña y, si la cuenta lo tiene, con el segundo factor. El administrador maestro siempre pasa por el segundo factor.",
            [
                P("Correo y contraseña", "Escribí el correo con el que te invitaron y tu contraseña. Cinco intentos fallidos seguidos bloquean la cuenta unos minutos; el mensaje lo dice."),
                P("Segundo factor", "Si tenés un autenticador, el sistema pide el código de seis dígitos de la aplicación (Google Authenticator, Microsoft Authenticator o similar) o te ofrece usar tu llave de acceso (huella, rostro o llave física). Con varios autenticadores, sirve cualquiera."),
                P("Si no tenés el teléfono", "En la misma pantalla, «usar un código de respaldo». Son los diez códigos que se mostraron una sola vez al activar el segundo factor; cada uno sirve una vez. Si tampoco los tenés, ver «Recuperar el acceso»."),
                P("Elegir cooperativa", "Con una sola cooperativa entrás directo. Con varias, el sistema pide cuál; podés fijar una por defecto en Mi Cuenta → Empresa por defecto.", "/security/select-tenant", "Ver la selección de cooperativa"),
                P("Si te pide inscribir un segundo factor", "Tu cooperativa lo exige y aún no tenés uno aceptado. La pantalla te lleva a inscribirlo antes de entrar; no es un error."),
            ],
            ["login", "entrar", "ingresar", "contraseña", "clave", "codigo", "bloqueado", "acceso"],
            ["Una invitación aceptada o una cuenta creada por el administrador maestro."],
            ["activar-segundo-factor", "recuperar-acceso", "olvide-mi-contrasena", "elegir-cooperativa"],
            ["/security/mfa-challenge", "/security/no-membership"], TipoDeTema.Proceso));

        t.Add(Proceso("activar-segundo-factor", "Activar el segundo factor (aplicación autenticadora)", Modulos.Cuenta, "/profile/mfa",
            "Vincular una aplicación de códigos al ingreso. Después de la contraseña, el sistema pedirá el código de seis dígitos que la aplicación cambia cada treinta segundos.",
            [
                P("Instalar una aplicación autenticadora", "Google Authenticator, Microsoft Authenticator, Authy o cualquiera compatible con TOTP. En el teléfono que siempre llevás."),
                P("Abrir Mi Cuenta → Autenticación MFA", "Se muestra un código QR y, debajo, la clave en texto por si el QR no se puede escanear.", "/profile/mfa", "Abrir Autenticación MFA"),
                P("Escanear el QR", "En la aplicación, «agregar cuenta» → escanear. Aparece «IngenIA365ERP» con tu correo y un código de seis dígitos."),
                P("Confirmar con el primer código", "Escribí el código que muestra la aplicación y confirmá. Si dice que no coincide, revisá la hora del teléfono: TOTP depende del reloj."),
                P("Guardar los códigos de respaldo", "Se muestran diez códigos una sola vez. Guardalos fuera del teléfono (impresos o en un gestor de contraseñas). Son tu salida si perdés el teléfono."),
                P("Volver a entrar", "Tras inscribir, el sistema no te deja dentro: hay que iniciar sesión de nuevo y esta vez pedirá el código. Es esperado, no un fallo."),
            ],
            ["mfa", "totp", "autenticador", "google authenticator", "microsoft authenticator", "qr", "segundo factor", "dos pasos", "codigos de respaldo"],
            ["Sesión iniciada.", "Un teléfono con una aplicación autenticadora."],
            ["agregar-llave-de-acceso", "mis-autenticadores", "recuperar-acceso"],
            ["/auth/enroll-mfa-forced"], TipoDeTema.Proceso));

        t.Add(Proceso("agregar-llave-de-acceso", "Agregar una llave de acceso (passkey)", Modulos.Cuenta, "/profile/seguridad/credenciales",
            "Entrar con huella, rostro o una llave física en vez de teclear un código. Es un segundo factor igual que la aplicación; se pueden tener los dos.",
            [
                P("Abrir Mi Cuenta → Mis Autenticadores", "Lista tus autenticadores actuales y ofrece agregar una llave de acceso.", "/profile/seguridad/credenciales", "Abrir Mis Autenticadores"),
                P("«Agregar llave de acceso»", "El navegador toma el control: pide la huella, el rostro, el PIN del equipo o que toques la llave física. Si cancelás, no pasa nada; si dice «esa llave ya está inscrita», es que ya la tenías."),
                P("Ponerle nombre", "«Portátil del trabajo», «YubiKey azul». Es para reconocerla después, cuando haya que retirar una."),
                P("Probarla", "Salí y volvé a entrar: después de la contraseña, elegí «usar llave de acceso». Si el navegador no la ofrece, es otro equipo u otro navegador: las llaves de plataforma viven donde se crearon."),
            ],
            ["passkey", "llave de acceso", "webauthn", "huella", "biometria", "yubikey", "fido", "sin codigo"],
            ["Sesión iniciada.", "Un navegador y equipo con soporte de llaves de acceso (Windows Hello, Touch ID, Android o una llave física)."],
            ["activar-segundo-factor", "mis-autenticadores"], [], TipoDeTema.Proceso));

        t.Add(Proceso("mis-autenticadores", "Administrar mis autenticadores", Modulos.Cuenta, "/profile/seguridad/credenciales",
            "Ver, renombrar y retirar los autenticadores (aplicaciones y llaves de acceso) de tu cuenta.",
            [
                P("Ver la lista", "Cada fila es un autenticador: tipo, nombre, cuándo se inscribió y cuándo se usó por última vez.", "/profile/seguridad/credenciales", "Abrir Mis Autenticadores"),
                P("Renombrar", "El lápiz de la fila. Sólo cambia la etiqueta; el autenticador sigue igual."),
                P("Retirar", "La papelera de la fila. El sistema no deja retirar el último si alguna de tus cooperativas exige segundo factor: primero inscribí otro."),
                P("Regenerar códigos de respaldo", "Invalida los diez anteriores y muestra diez nuevos, una sola vez. Hacelo si sospechás que alguien los vio."),
            ],
            ["autenticadores", "credenciales", "renombrar", "revocar", "retirar", "codigos de respaldo", "regenerar"],
            ["Sesión iniciada."], ["activar-segundo-factor", "agregar-llave-de-acceso"], [], TipoDeTema.Proceso));

        t.Add(Proceso("recuperar-acceso", "Recuperar el acceso sin el segundo factor", Modulos.Cuenta, "/security/mfa-challenge",
            "Qué hacer cuando perdiste el teléfono o la llave y el sistema pide un código que no podés dar. Tres caminos, del más rápido al último recurso.",
            [
                P("1. Códigos de respaldo", "En la pantalla del código, «usar un código de respaldo». Entrás con uno de los diez que guardaste y, ya dentro, inscribís un autenticador nuevo. No destruye nada."),
                P("2. Doble aprobación", "Si no tenés los códigos, pedile a tu administrador un restablecimiento. Dos administradores de la cooperativa deben aprobarlo en Mi Cooperativa → Aprobaciones MFA. Al aprobarse, tu próximo ingreso pedirá inscribir un segundo factor nuevo.", "/security/mfa-reset-approvals", "Ver Aprobaciones MFA"),
                P("3. Recuperación por correo", "Sólo si tu cooperativa la habilitó. Se pide desde la pantalla del código, después de acertar la contraseña; llega un aviso con un enlace para cancelar y la recuperación se ejecuta pasadas las horas que fije la cooperativa (24 por defecto). Completarla no te deja dentro: te permite inscribir de nuevo."),
                P("El administrador maestro", "No tiene a quién pedirle: se rescata a sí mismo. Ver el runbook de operaciones «rescate del administrador maestro»."),
            ],
            ["perdi el telefono", "sin codigo", "recuperar", "reset mfa", "restablecer", "doble aprobacion", "correo de recuperacion", "bloqueado"],
            [], ["iniciar-sesion", "aprobaciones-reset-mfa", "mis-autenticadores"],
            ["/security/mfa-recovery/confirmar"], TipoDeTema.Proceso));

        t.Add(Proceso("cambiar-contrasena", "Cambiar mi contraseña", Modulos.Cuenta, "/profile/password",
            "Reemplazar la contraseña actual estando dentro del sistema.",
            [
                P("Abrir Mi Cuenta → Cambiar Contraseña", null, "/profile/password", "Abrir Cambiar Contraseña"),
                P("Contraseña actual y nueva", "La nueva se escribe dos veces. La política de la cooperativa fija el largo y la variedad mínimos; el formulario lo indica al vuelo."),
                P("Guardar", "Las otras sesiones abiertas se cierran: tendrás que entrar de nuevo en los demás equipos."),
            ],
            ["contraseña", "clave", "password", "cambiar"], ["Sesión iniciada."], ["olvide-mi-contrasena"], [], TipoDeTema.Proceso));

        t.Add(Proceso("olvide-mi-contrasena", "Olvidé mi contraseña", Modulos.Cuenta, "/auth/forgot-password",
            "Pedir un enlace de restablecimiento por correo, desde la pantalla de entrada.",
            [
                P("En la pantalla de entrada, «¿Olvidaste tu contraseña?»", null, "/auth/forgot-password", "Abrir Recuperar contraseña"),
                P("Escribí tu correo", "El sistema responde igual exista o no la cuenta: no revela quién está registrado. Si existe, llega un correo con un enlace que vence."),
                P("Abrir el enlace y elegir la nueva contraseña", "Dos veces, cumpliendo la política. Después, entrar con la nueva.", "/auth/reset-password", "Ver la pantalla de restablecer"),
                P("Si no llega el correo", "Revisá correo no deseado. Si la cooperativa no tiene correo saliente configurado, el administrador puede verlo en Auditoría y ayudarte por otro medio."),
            ],
            ["olvide", "contraseña", "restablecer", "reset password", "correo", "enlace"], [], ["cambiar-contrasena", "iniciar-sesion"],
            ["/auth/reset-password"], TipoDeTema.Proceso));

        t.Add(Proceso("elegir-cooperativa", "Elegir o cambiar de cooperativa", Modulos.Cuenta, "/security/select-tenant",
            "Con acceso a varias cooperativas, cuál está activa decide qué datos ves y dónde se guarda lo que registrás.",
            [
                P("Al entrar", "Si tenés más de una, el sistema pide elegir. Cada cooperativa puede exigir un segundo factor distinto: si al elegir te pide inscribir uno, es su política, no un error.", "/security/select-tenant", "Abrir la selección"),
                P("Cambiar sin salir", "Arriba a la derecha, el nombre de la cooperativa es un selector. Al cambiar, el menú y los datos cambian con ella."),
                P("Fijar una por defecto", "Mi Cuenta → Empresa por Defecto. Entrarás directo a esa y podrás cambiar después.", "/profile/default-tenant", "Abrir Empresa por Defecto"),
                P("«Sin acceso»", "Si ves esa pantalla, tu cuenta existe pero ninguna cooperativa te tiene como miembro activo: pedile a un administrador que te invite o te reactive."),
            ],
            ["cooperativa", "empresa", "cambiar", "tenant", "por defecto", "seleccionar", "sin acceso"],
            ["Sesión iniciada."], ["iniciar-sesion", "invitar-persona"],
            ["/profile/default-tenant", "/security/no-membership"], TipoDeTema.Proceso));

        t.Add(Proceso("preferencias-visuales", "Preferencias visuales", Modulos.Cuenta, "/perfil/preferencias",
            "Tema claro u oscuro, densidad, tamaño de letra y contraste. Se guardan en tu cuenta y se aplican en cualquier equipo.",
            [
                P("Abrir el menú de la cuenta → Preferencias", null, "/perfil/preferencias", "Abrir Preferencias"),
                P("Elegir y ver el efecto al instante", "Cada cambio se aplica en vivo. «Alto contraste» y «letra grande» están pensados para leer mucho tiempo la pantalla."),
                P("Guardar", "Queda en el servidor: al entrar desde otro equipo, se aplica igual."),
            ],
            ["tema", "oscuro", "claro", "letra", "contraste", "densidad", "preferencias", "accesibilidad"], ["Sesión iniciada."], [], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Consola SaaS --
        t.Add(Proceso("registrar-cooperativa", "Registrar una cooperativa nueva", Modulos.Saas, "/saas/register-tenant",
            "Crear una cooperativa en la plataforma: su base de datos propia, su cupo de caché, su rastro de auditoría, y la invitación a su primer administrador. Sólo el administrador maestro.",
            [
                P("Consola SaaS → Registrar Cooperativa", null, "/saas/register-tenant", "Abrir Registrar Cooperativa"),
                P("Razón social, nombre corto y NIT", "La razón social es la legal; el nombre corto es el que se ve en el selector de cooperativas."),
                P("Nombre de la base de datos", "Sólo letras sin tilde, dígitos y guion bajo, sin empezar por dígito: por ejemplo «coop_prueba». Es el nombre físico de la base; no se cambia después."),
                P("Correo de contacto y correo del primer administrador", "Al segundo le llega la invitación. Usá un buzón real: en QA y producción el correo sale de verdad."),
                P("Registrar", "Tarda unos segundos: crea y migra la base, reserva la ranura de caché y la base de auditoría. Al terminar dice si el correo salió. Si dice que no, trae el motivo textual del servidor de correo, y la invitación queda creada para reenviarla desde Cooperativas → Invitaciones."),
                P("Comprobar", "Consola SaaS → Cooperativas debe mostrarla activa. El primer administrador acepta la invitación desde el correo y entra como «Administrador de Cooperativa»."),
            ],
            ["cooperativa", "nueva", "registrar", "alta", "tenant", "base de datos", "primer administrador", "saas", "maestro"],
            ["Ser administrador maestro.", "Correo saliente configurado en el ambiente (si no, la invitación se reenvía después)."],
            ["cooperativas", "invitar-persona", "aceptar-invitacion"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cooperativas", "Cooperativas (consola)", Modulos.Saas, "/admin/tenants",
            "Listar las cooperativas de la plataforma, suspenderlas o reactivarlas y llegar a sus invitaciones. Sólo el administrador maestro.",
            [
                P("Consola SaaS → Cooperativas", "Búsqueda por nombre; la casilla «incluir suspendidas» muestra también las inactivas.", "/admin/tenants", "Abrir Cooperativas"),
                P("Suspender", "Corta el acceso a todos sus usuarios de inmediato. Pide un motivo libre y obligatorio: quien lo lea después merece saber por qué."),
                P("Reactivar", "Devuelve el acceso. Las sesiones que estaban abiertas siguen cerradas hasta que la gente vuelva a entrar."),
                P("Invitaciones", "Desde la fila, «Invitaciones» abre la lista de esa cooperativa, donde también se invita.", null, null),
            ],
            ["cooperativas", "suspender", "reactivar", "listar", "consola", "maestro"], ["Ser administrador maestro."],
            ["registrar-cooperativa", "invitar-persona"], ["/admin/tenants/new"], TipoDeTema.Proceso));

        t.Add(Proceso("reset-mfa-maestro", "Restablecer el segundo factor de un usuario (maestro)", Modulos.Saas, "/saas/force-mfa-reset",
            "El administrador maestro borra todos los autenticadores de una persona para que inscriba de nuevo. Es el atajo cuando la doble aprobación no es posible.",
            [
                P("Consola SaaS → Reset MFA Usuarios", null, "/saas/force-mfa-reset", "Abrir Reset MFA"),
                P("Buscar a la persona por correo", "Verificá que es quien dice ser por un canal distinto al correo: quien pueda hacerse pasar por ella con un correo, con esto entra."),
                P("Restablecer", "Se retiran sus autenticadores y se cierran sus sesiones. En el próximo ingreso el sistema le pide inscribir uno nuevo. Queda en auditoría con tu usuario."),
            ],
            ["reset", "mfa", "forzar", "maestro", "usuario bloqueado", "autenticador perdido"], ["Ser administrador maestro."],
            ["recuperar-acceso", "aprobaciones-reset-mfa"], [], TipoDeTema.Proceso));

        t.Add(Proceso("contenido-promocional", "Contenido promocional del ingreso", Modulos.Saas, "/administracion/promociones",
            "Las piezas (imagen y texto) que se muestran en la pantalla de entrada, antes de iniciar sesión. Las gestiona el maestro; también las ve Administración.",
            [
                P("Abrir Contenido promocional", null, "/administracion/promociones", "Abrir Contenido promocional"),
                P("Nueva pieza", "Título, texto, imagen y vigencia. Fuera de la vigencia la pieza deja de mostrarse sola."),
                P("Ordenar y activar", "Sólo las activas y vigentes salen en el ingreso. Comprobalo abriendo la pantalla de entrada en una ventana privada."),
            ],
            ["promociones", "login", "banner", "pieza", "imagen", "pantalla de entrada"], ["Ser administrador maestro o de la cooperativa."], [], [], TipoDeTema.Proceso));

        // ----------------------------------------------------- Administración --
        t.Add(Proceso("usuarios", "Usuarios", Modulos.Administracion, "/security/users",
            "Quién puede entrar a la cooperativa y con qué roles. Las personas no se crean aquí: se invitan, y la invitación crea la cuenta.",
            [
                P("Administración → Usuarios", "Lista con estado, si tiene segundo factor, si está bloqueado, roles y último ingreso. «Buscar» filtra por usuario o correo; «incluir deshabilitados» muestra los inactivos.", "/security/users", "Abrir Usuarios"),
                P("Invitar persona", "Lleva a Invitaciones, donde se escribe el correo y se envía. Ver «Invitar a una persona».", null, null),
                P("Editar", "Datos del usuario y su estado."),
                P("Roles", "Qué puede hacer. Ver «Asignar roles a un usuario»."),
                P("Desbloquear", "Tras varios intentos fallidos la cuenta se bloquea unos minutos; esto lo levanta antes."),
                P("Deshabilitar", "Le quita el acceso sin borrar nada: el historial y la auditoría siguen apuntando a la persona."),
            ],
            ["usuarios", "invitar", "roles", "bloqueado", "desbloquear", "deshabilitar", "acceso", "personas del sistema"],
            ["Ser administrador de la cooperativa.", "Tener la cooperativa activa en la sesión."],
            ["invitar-persona", "asignar-roles", "roles-y-permisos"], [], TipoDeTema.Proceso));

        t.Add(Proceso("invitar-persona", "Invitar a una persona", Modulos.Administracion, "/admin/tenants/{TenantPublicId}/invitaciones",
            "Dar acceso a alguien nuevo. Recibe un correo con un enlace; con él crea su contraseña —o usa la que ya tiene si ya está en otra cooperativa— y queda como miembro.",
            [
                P("Llegar a Invitaciones", "Desde Administración → Usuarios → «Invitar persona». El administrador maestro también llega desde Consola SaaS → Cooperativas → Invitaciones.", "/security/users", "Ir a Usuarios"),
                P("Escribir el correo y enviar", "«Enviar invitación». El maestro además puede marcar «como administradora de la cooperativa»; un administrador de cooperativa sólo invita miembros regulares, y los roles se asignan después en Usuarios."),
                P("Leer el resultado", "Dice hasta cuándo vale el enlace. Si el correo no salió, la invitación quedó creada igual y aparece abajo como pendiente: «Reenviar» genera un enlace nuevo."),
                P("Seguirla", "Pendiente, aceptada, cancelada, vencida o reemplazada. «Incluir aceptadas y canceladas» muestra el historial. «Cancelar» invalida el enlace."),
                P("Reenviar", "Genera un enlace nuevo y anula el anterior: si la persona encuentra el correo viejo, ese ya no sirve."),
            ],
            ["invitar", "invitacion", "nuevo usuario", "alta de usuario", "correo", "enlace", "reenviar", "cancelar invitacion", "administradora"],
            ["Ser administrador de la cooperativa (o maestro).", "Correo saliente configurado en el ambiente."],
            ["usuarios", "aceptar-invitacion", "asignar-roles"], [], TipoDeTema.Proceso));

        t.Add(Proceso("aceptar-invitacion", "Aceptar una invitación", Modulos.Cuenta, "/auth/accept-invitation",
            "Lo que hace la persona invitada al abrir el enlace del correo. Tres casos según si ya tiene cuenta.",
            [
                P("Abrir el enlace del correo", "Muestra qué cooperativa te invita y quién. Si dice vencida o cancelada, pedí que te reenvíen.", "/auth/accept-invitation", "Ver la pantalla"),
                P("Soy nuevo", "«Crear cuenta y entrar»: elegís contraseña y quedás dentro."),
                P("Ya tengo cuenta en otra cooperativa", "Entrá con tu correo y contraseña actuales: la cooperativa nueva se suma a las tuyas, sin segunda contraseña."),
                P("Ya estoy con la sesión abierta", "«Usar la sesión activa» acepta con la cuenta con la que estás."),
                P("Después", "Si la cooperativa exige segundo factor, el sistema te pide inscribirlo antes de mostrarte nada."),
            ],
            ["aceptar", "invitacion", "crear cuenta", "enlace", "primer ingreso"], ["Un enlace de invitación vigente."],
            ["invitar-persona", "iniciar-sesion", "activar-segundo-factor"], [], TipoDeTema.Proceso));

        t.Add(Proceso("roles-y-permisos", "Roles y permisos", Modulos.Administracion, "/security/roles",
            "Qué puede hacer cada tipo de usuario. Cuatro roles vienen con el sistema y no se borran; se pueden crear otros combinando permisos.",
            [
                P("Administración → Roles y Permisos", "Lista los roles y cuántas personas tienen cada uno.", "/security/roles", "Abrir Roles"),
                P("Los roles integrados", "Administrador de Cooperativa, Operador, Auditor y Solo Lectura. Se pueden consultar y asignar, no modificar ni eliminar: son la garantía de que siempre haya alguien que pueda administrar."),
                P("Crear un rol", "«Nuevo»: nombre, descripción y la lista de permisos por módulo. Un permiso es una acción concreta (ver, crear, aprobar, exportar); marcá sólo lo que el puesto necesita."),
                P("Editar o retirar", "El lápiz y la papelera de la fila. Un rol con personas asignadas no se retira: primero reasignalas."),
            ],
            ["roles", "permisos", "perfil", "administrador", "operador", "auditor", "solo lectura", "que puede hacer"],
            ["Ser administrador de la cooperativa."], ["asignar-roles", "usuarios"], ["/security/roles/new", "/admin/roles"], TipoDeTema.Proceso));

        t.Add(Proceso("asignar-roles", "Asignar roles a un usuario", Modulos.Administracion, "/security/users/{PublicId}/roles",
            "Darle o quitarle roles a una persona ya invitada. El efecto es inmediato en su próximo ingreso o cambio de cooperativa.",
            [
                P("Administración → Usuarios → «Roles» en la fila", null, "/security/users", "Ir a Usuarios"),
                P("Marcar los roles", "Puede tener varios; los permisos se suman. Quien necesite todo, «Administrador de Cooperativa»; quien sólo consulte, «Solo Lectura»."),
                P("Guardar", "Si la persona está dentro, verá los cambios al volver a entrar o al cambiar de cooperativa."),
            ],
            ["asignar roles", "permisos de usuario", "administrador", "quitar rol"], ["Ser administrador de la cooperativa."],
            ["usuarios", "roles-y-permisos"], ["/security/users/{PublicId}", "/admin/usuarios"], TipoDeTema.Proceso));

        t.Add(Proceso("auditoria", "Registro de auditoría", Modulos.Administracion, "/admin/auditoria",
            "Quién hizo qué y cuándo, en la cooperativa activa. No se puede editar ni borrar: es la evidencia regulatoria, conservada cinco años.",
            [
                P("Administración → Auditoría", "Se cargan los últimos eventos. Cada fila: fecha y hora, usuario, acción, entidad afectada y módulo.", "/admin/auditoria", "Abrir Auditoría"),
                P("Filtrar", "Por usuario, acción, tipo de entidad, módulo y rango de fechas. Combiná filtros para acotar; «Buscar» aplica."),
                P("Ver el detalle", "Al abrir una fila se ven los valores antes y después del cambio, la IP y el punto de entrada."),
                P("Exportar", "«Exportar CSV» para hojas de cálculo. «Exportar PDF firmado» genera un documento con firma verificable: cualquier byte cambiado después hace que la verificación diga «no válido»."),
                P("Qué no aparece aquí", "Los ingresos, el segundo factor y las invitaciones ocurren antes de elegir cooperativa y se guardan en el rastro global, que consulta el administrador maestro."),
                P("Canal, actor, motivo y rechazos", "Las columnas dicen por dónde entró la operación (web, app, punto de venta o proceso), si la hizo una persona o un proceso automático, el motivo declarado y, en un rechazo, su código de error. «Sólo módulos encadenados» deja inventario, aprobaciones, alertas, parámetros y navegación; «sólo rechazos», lo que el sistema negó."),
                P("Pestaña Integridad", "Con el permiso AuditLog.VerifyIntegrity: elegí el rango y «Verificar». Recalcula la cadena de sellos y dice, por posición, si un evento fue alterado, eliminado, intercalado, si un ancla no corresponde o si venció su plazo de diez años. La verificación misma queda en la auditoría."),
            ],
            ["auditoria", "integridad", "cadena", "rechazos", "canal", "quien hizo", "trazabilidad", "historial", "cambios", "exportar", "pdf firmado", "csv", "sarlaft", "evidencia"],
            ["Permiso de auditoría (roles Administrador de Cooperativa o Auditor)."], ["usuarios", "roles-y-permisos"], [], TipoDeTema.Consulta));

        t.Add(Proceso("politica-segundo-factor", "Política de segundo factor de la cooperativa", Modulos.Administracion, "/admin/tenant/{TenantPublicId}/mfa-policy",
            "Decidir si la cooperativa exige segundo factor para entrar, cuáles métodos acepta y si permite la recuperación por correo.",
            [
                P("Mi Cooperativa → Política MFA", "Sólo la ve el administrador de la cooperativa.", null, null),
                P("Exigir segundo factor", "Al activarlo, quien no tenga uno aceptado deberá inscribirlo en su próximo ingreso o cambio de cooperativa. Nadie queda fuera: siempre se le indica qué inscribir."),
                P("Métodos aceptados", "Aplicación autenticadora, llave de acceso, o ambos. La lista sólo restringe si se exige segundo factor. Al quitar un método, la pantalla dice cuántas personas quedan sin uno válido: hacelo con eso a la vista."),
                P("Recuperación por correo", "Apagada por defecto. Si la habilitás, fijá la demora (mínimo una hora, 24 por defecto): es el tiempo que la persona tiene para cancelar un intento que no fue suyo."),
                P("Guardar", "Rige de inmediato para los próximos ingresos. Las sesiones ya abiertas no se expulsan."),
            ],
            ["politica", "mfa", "exigir", "obligatorio", "metodos", "passkey", "recuperacion por correo", "demora"],
            ["Ser administrador de la cooperativa."], ["activar-segundo-factor", "recuperar-acceso", "miembros"], [], TipoDeTema.Proceso));

        t.Add(Proceso("miembros", "Miembros de la cooperativa", Modulos.Administracion, "/admin/tenant/{TenantPublicId}/members",
            "Las personas con acceso a la cooperativa, su estado de membresía y quién es administrador. Es la vista de identidad; los roles de trabajo se ven en Usuarios.",
            [
                P("Mi Cooperativa → Miembros", null, null, null),
                P("Suspender o reactivar una membresía", "Suspender corta el acceso a esta cooperativa sin tocar las demás que la persona tenga."),
                P("Nombrar o quitar administrador", "Un administrador puede invitar, aprobar restablecimientos y cambiar la política. Conviene que haya al menos dos: la doble aprobación los necesita."),
            ],
            ["miembros", "membresia", "suspender", "administrador de cooperativa", "acceso"], ["Ser administrador de la cooperativa."],
            ["usuarios", "invitar-persona", "aprobaciones-reset-mfa"], [], TipoDeTema.Proceso));

        t.Add(Proceso("aprobaciones-reset-mfa", "Aprobaciones de restablecimiento de MFA", Modulos.Administracion, "/security/mfa-reset-approvals",
            "Cuando alguien pierde su segundo factor y no tiene códigos de respaldo, dos administradores aprueban que lo inscriba de nuevo. Ninguno puede solo.",
            [
                P("Registrar la solicitud", "Un administrador la crea a nombre de la persona, tras verificar su identidad por un canal distinto al correo.", "/security/mfa-reset-approvals", "Abrir Aprobaciones MFA"),
                P("Primera aprobación", "El mismo u otro administrador aprueba. La pantalla muestra quién y cuándo."),
                P("Segunda aprobación", "Tiene que ser otra persona. Al completarse, se retiran los autenticadores de la cuenta y se le avisa por correo."),
                P("Qué pasa después", "En su próximo ingreso el sistema le pide inscribir un autenticador nuevo antes de entrar."),
            ],
            ["aprobaciones", "reset", "mfa", "doble aprobacion", "dos administradores", "perdio el telefono"],
            ["Ser administrador de la cooperativa.", "Dos administradores distintos."], ["recuperar-acceso", "reset-mfa-maestro"], [], TipoDeTema.Proceso));

        t.Add(Proceso("sucursales", "Sucursales", Modulos.Administracion, "/admin/branches",
            "Las agencias u oficinas de la cooperativa. Los movimientos y los usuarios se asocian a una sucursal.",
            [
                P("Administración → Sucursales", null, "/admin/branches", "Abrir Sucursales"),
                P("Nueva sucursal", "Código, nombre, ciudad y datos de contacto. El código no se cambia después: lo usan los comprobantes."),
                P("Editar o desactivar", "Una sucursal con movimientos no se elimina: se desactiva y deja de ofrecerse."),
            ],
            ["sucursales", "agencias", "oficinas", "sedes"], ["Ser administrador de la cooperativa."], ["parametros-del-sistema"], ["/admin/branches/new"], TipoDeTema.Maestro));

        t.Add(Proceso("parametros-del-sistema", "Parámetros del sistema", Modulos.Administracion, "/admin/parametros",
            "Valores que gobiernan el comportamiento de la cooperativa: moneda, redondeos, límites, textos legales y opciones por módulo.",
            [
                P("Administración → Parámetros del Sistema", "Agrupados por módulo. «Buscar» encuentra por nombre o clave.", "/admin/parametros", "Abrir Parámetros"),
                P("Cambiar un valor", "Cada parámetro dice qué controla y su tipo. Los cambios quedan en auditoría con el valor anterior."),
                P("Cuándo aplica", "La mayoría al instante; algunos, en el próximo proceso que los use (una liquidación, un cierre). La descripción del parámetro lo indica."),
            ],
            ["parametros", "configuracion", "ajustes", "moneda", "redondeo"], ["Ser administrador de la cooperativa."], [], [], TipoDeTema.Maestro));

        // ----------------------------------------------------------- Asociados --
        t.Add(Proceso("personas", "Personas", Modulos.Maestros, "/maestros/personas",
            "El registro único de personas naturales y jurídicas: asociados, empleados, terceros y proveedores parten de aquí. Una persona, una sola ficha, aunque tenga varios papeles.",
            [
                P("Maestros → Maestros Core → Personas", "«Buscar» por nombre, documento o correo. Buscá siempre antes de crear: los duplicados cuestan caro después.", "/maestros/personas", "Abrir Personas"),
                P("Nuevo", "Tipo y número de documento, nombres, contacto, dirección y ciudad. Marcá qué es (asociado, empleado, tercero, proveedor); se puede ampliar después."),
                P("Habeas data", "Antes de tratar sus datos, la persona debe haber aceptado la política vigente. Desde su ficha se registra el consentimiento y se ve el historial.", "/compliance/habeas-data/policies", "Ver la política de habeas data"),
                P("Editar", "El lápiz de la fila. El documento no se cambia a la ligera: queda en auditoría."),
            ],
            ["personas", "terceros", "documento", "cedula", "nit", "registro unico", "cliente", "proveedor"],
            ["Cooperativa activa.", "Permiso de maestros."], ["registro-de-asociados", "habeas-data", "empleados"], [], TipoDeTema.Proceso));

        t.Add(Proceso("registro-de-asociados", "Registrar un asociado", Modulos.Asociados, "/asociados/registro",
            "Vincular a una persona como asociada de la cooperativa: fecha de ingreso, agencia, aportes y beneficiarios.",
            [
                P("Asociados → Registro", null, "/asociados/registro", "Abrir Registro de asociados"),
                P("Buscar la persona", "«Buscar Persona» abre el buscador del registro único. Si no existe, se crea desde ahí mismo."),
                P("Datos de vinculación", "Fecha de ingreso, agencia, tipo de asociado y la información socioeconómica que pida la cooperativa."),
                // Sin botón: la pantalla de beneficiarios no existe todavía (tampoco tiene enlace en el menú).
                P("Beneficiarios", "Quiénes reciben en caso de fallecimiento, con parentesco y porcentaje; deben sumar cien."),
                P("Guardar", "El asociado queda activo y puede abrir productos (ahorros, créditos, aportes)."),
            ],
            ["asociado", "afiliar", "vincular", "ingreso", "beneficiarios", "socio"], ["Cooperativa activa.", "La persona en el registro único."],
            ["personas", "aportes", "retiro-de-asociado"], [], TipoDeTema.Proceso));

        // Beneficiarios: la ruta /asociados/beneficiarios no tiene página (el menú tampoco la enlaza, ver
        // TodoEnlaceDelMenuTieneSuPagina). El tema se destapa cuando exista la pantalla:
        // t.Add(Maestro("/asociados/beneficiarios", "Beneficiarios", Modulos.Asociados, "un beneficiario", "Se registran por asociado; los porcentajes deben sumar cien.", "beneficiarios", "herederos", "parentesco"));

        // ---------------------------------------------------------- Contabilidad --
        // Feature 009: las rutas del capítulo son las de la contabilidad NIIF (E1/E2). Hasta el 2026-09-20 el manual
        // seguía anunciando las pantallas heredadas (/contabilidad/plan-cuentas, /movimientos, /saldos, /conciliacion,
        // /cierre-periodo, /tipos-comprobante, grupos, subgrupos, impuestos, DIAN…), retiradas con la migración
        // ContabilidadNiif: cada botón «Abrir …» llevaba a «Página no encontrada». Lo vigila ManualCatalogoTests.
        t.Add(Proceso("plan-de-cuentas", "Plan de cuentas", Modulos.Contabilidad, "/contabilidad/plan-de-cuentas",
            "El catálogo contable (PUC) de la cooperativa: clases, grupos, cuentas y subcuentas con su naturaleza y nivel.",
            [
                P("Contabilidad → Plan de Cuentas", "Se ve como árbol o lista. «Buscar» por código o nombre.", "/contabilidad/plan-de-cuentas", "Abrir Plan de Cuentas"),
                P("Cargar muchas de una vez", "«Plantilla Excel» baja la hoja con las columnas (código, nombre, a qué módulos aplica y qué exige cada línea); «Importar cuentas» la sube. Si una fila falla, la pantalla dice fila, columna y problema, y no se guarda nada. Una cuenta que ya existe se actualiza: con movimientos sólo cambia de nombre.", "/contabilidad/plan-de-cuentas", "Abrir Plan de Cuentas"),
                P("Crear una cuenta", "«Nuevo»: código (el nivel lo da la longitud), nombre, naturaleza (débito o crédito) y si admite movimientos. Sólo las de último nivel reciben movimientos."),
                P("Editar", "El nombre y las marcas se pueden cambiar; el código de una cuenta con movimientos no."),
                P("Cuentas de los módulos", "Cartera, inventario y nómina necesitan saber contra qué cuentas contabilizan: eso se define en las pantallas «Cuentas …» de cada módulo, no aquí."),
            ],
            ["plan de cuentas", "puc", "cuenta contable", "codigo", "naturaleza", "catalogo contable", "importar cuentas", "carga masiva", "auxiliares"], ["Cooperativa activa.", "Permiso de contabilidad."],
            ["comprobante-contable", "contabilidad-libro-auxiliar"], [], TipoDeTema.Proceso));

        t.Add(Proceso("comprobante-contable", "Comprobante contable", Modulos.Contabilidad, "/contabilidad/comprobantes",
            "Registrar un asiento manual: fecha, tipo de comprobante, terceros y las líneas débito/crédito, que deben cuadrar.",
            [
                P("Contabilidad → Comprobantes Contables", "La lista muestra los del período. Filtrá por fecha, tipo o número.", "/contabilidad/comprobantes", "Abrir Comprobantes"),
                P("Nuevo Comprobante", "Tipo (el numerador es por tipo), fecha dentro de un período abierto, y descripción.", "/contabilidad/comprobantes/nuevo", "Abrir Nuevo Comprobante"),
                P("Agregar líneas", "«Agregar Línea»: cuenta de último nivel, tercero si la cuenta lo exige, centro de costo si aplica, y el valor en débito o en crédito."),
                P("Cuadrar", "Débitos y créditos deben ser iguales al centavo. El pie del comprobante muestra la diferencia hasta que sea cero."),
                P("Guardar", "Queda en borrador o contabilizado según la política de la cooperativa. Contabilizado no se edita: se anula con un comprobante de reversión."),
            ],
            ["comprobante", "asiento", "contabilizar", "debito", "credito", "cuadrar", "nota contable"], ["Cooperativa activa.", "Un período contable abierto."],
            ["plan-de-cuentas", "contabilidad-libro-auxiliar", "cierre-de-periodo"], ["/contabilidad/comprobantes/nuevo", "/contabilidad/comprobantes/{Id}", "/contabilidad/borradores"], TipoDeTema.Proceso));

        // «Movimientos contables» y «Saldos por cuenta» eran consultas heredadas; hoy son el libro auxiliar
        // (/contabilidad/libro-auxiliar) y los informes (/contabilidad/informes), que tienen su tema más abajo.
        // t.Add(Consulta("/contabilidad/movimientos", "Movimientos contables", Modulos.Contabilidad,
        //     "Todas las líneas contabilizadas, por cuenta, tercero, fecha o comprobante. Es la vista de detalle detrás de cualquier saldo.",
        //     "Cuenta, rango de fechas, tercero y tipo de comprobante.", "movimientos", "auxiliar", "detalle contable"));
        // t.Add(Consulta("/contabilidad/saldos", "Saldos por cuenta", Modulos.Contabilidad,
        //     "Saldo inicial, débitos, créditos y saldo final de cada cuenta a una fecha.",
        //     "Fecha de corte y, si se quiere, un rango de cuentas.", "saldos", "balance", "cuenta"));

        // Conciliación bancaria: llega con E4 (feature 009). Sin pantalla no se ofrece el tema; destapar al crearla:
        // t.Add(Proceso("conciliacion-bancaria", "Conciliación bancaria", Modulos.Contabilidad, "/contabilidad/conciliacion",
        //     "Cruzar el extracto del banco con los movimientos de la cuenta contable del banco y dejar explicadas las diferencias.",
        //     [
        //         P("Contabilidad → Conciliación Bancaria", "Elegí la cuenta bancaria y el mes.", "/contabilidad/conciliacion", "Abrir Conciliación"),
        //         P("Cargar el extracto", "Importá el archivo del banco o registrá sus movimientos. El sistema propone coincidencias por valor y fecha."),
        //         P("Marcar conciliados", "Cada movimiento tiene una casilla. Lo que coincide se marca; lo que no, queda como partida pendiente con su explicación (cheque no cobrado, consignación no identificada)."),
        //         P("Cerrar la conciliación", "Cuando el saldo del extracto y el contable, ajustados por las partidas pendientes, coinciden. Queda el informe para la revisoría."),
        //     ],
        //     ["conciliacion", "banco", "extracto", "partidas pendientes", "cheques no cobrados"], ["Cooperativa activa.", "La cuenta bancaria creada en Maestros → Bancos."],
        //     ["comprobante-contable", "cheques"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cierre-de-periodo", "Cierre de período", Modulos.Contabilidad, "/contabilidad/periodos",
            "Cerrar un mes (o el año) para que nadie contabilice en él, con las verificaciones previas. El cierre anual además traslada resultados.",
            [
                P("Antes", "Todos los módulos deben haber contabilizado el mes: causación de intereses, liquidación de nómina, movimientos de inventario. Revisá el balance de prueba: debe cuadrar.", "/contabilidad/informes?vista=trial-balance", "Abrir Balance de Prueba"),
                P("Contabilidad → Períodos", "Cada mes tiene su botón «Cerrar». La pantalla cuenta los comprobantes en borrador fechados en el mes; no cierra con pendientes.", "/contabilidad/periodos", "Abrir Períodos contables"),
                P("Cerrar", "El período pasa a cerrado. Un comprobante con fecha en un período cerrado es rechazado. Reabrir requiere permiso y queda en auditoría."),
                P("Cierre anual", "Con los doce meses cerrados, el anterior cerrado y la cuenta de resultado definida en Configuración inicial, «Cerrar el ejercicio» genera el comprobante CI del 31 de diciembre: cancela ingresos, costos y gastos contra la cuenta de resultado, sucursal por sucursal. Los informes lo dejan fuera salvo que pidás «incluir cierre»; el balance del año siguiente arranca sin resultados."),
                P("Reabrir el ejercicio", "Si faltó algo, «Reabrir el ejercicio» pide motivo, reversa el CI en su misma fecha y deja el año abierto con los meses todavía cerrados: reabrí el mes que necesités corregir, corregí, y volvé a cerrar mes y año."),
            ],
            ["cierre", "periodo", "mes", "año", "reabrir", "cierre anual", "resultados", "excedente", "comprobante de cierre"], ["Cooperativa activa.", "Permiso de cierre."],
            ["contabilidad-periodos", "comprobante-contable", "contabilidad-informes", "saldos-de-apertura"], [], TipoDeTema.Proceso));

        // Feature 009 E2 (US13): la carga única de saldos con que la cooperativa arranca en el ERP.
        t.Add(Proceso("saldos-de-apertura", "Saldos de apertura", Modulos.Contabilidad, "/contabilidad/apertura",
            "Cargar una sola vez los saldos con que la cooperativa arranca en el ERP: se importan desde una plantilla, se revisan como borrador y se contabilizan como cualquier comprobante.",
            [
                P("Antes", "Contabilidad iniciada, ejercicio abierto y las auxiliares de movimiento creadas con sus reglas (tercero, documento cruce, centro de costo). Las personas de cartera y proveedores tienen que existir en Personas.", "/contabilidad/plan-de-cuentas", "Abrir Plan de cuentas"),
                P("Contabilidad → Saldos de apertura", "«Plantilla Excel» descarga la hoja con los encabezados; el contador la llena desde SOLIDO con una fila por auxiliar (y por tercero y documento donde la cuenta lo exige): importes sin miles y con hasta dos decimales.", "/contabilidad/apertura", "Abrir Saldos de apertura"),
                P("Elegí la fecha", "Es el corte de tus saldos anteriores. Se propone la víspera del primer período y podés moverla hasta el fin del primer ejercicio; lo único que no se admite es un mes ya cerrado. Cualquiera sea, la apertura es saldo inicial y nunca movimiento del mes."),
                P("Importar", "Cada fila se valida con las reglas de su cuenta. Si una falla, la pantalla muestra fila, columna y problema, y no se guarda nada: corregí el archivo y volvé a importar. Sin errores queda un borrador; no tiene que cuadrar para importarse."),
                P("Corregir mientras sea borrador", "«Editar» abre el comprobante y ahí agregás, cambiás o quitás cuentas línea a línea; «Cambiar la fecha» la mueve sin tocar las líneas; «Descartar» lo elimina; y volver a importar reemplaza todas las líneas por las del archivo nuevo."),
                P("Contabilizar", "Revisá el borrador y contabilizalo (cuatro ojos si la empresa lo exige). Queda como la única apertura vigente: para cargar otra, reversá ésta primero. Desde ahí ya no se edita. En el balance de prueba la apertura es saldo inicial, no movimiento del mes."),
            ],
            ["apertura", "saldos iniciales", "saldos de apertura", "migración", "solido", "plantilla", "importar saldos", "fecha de corte"], ["Cooperativa activa.", "Permiso Accounting.Opening.Manage."],
            ["comprobante-contable", "plan-de-cuentas", "cierre-de-periodo", "contabilidad-informes"], [], TipoDeTema.Proceso));

        // Feature 009 E2 (US9): la ruta es /contabilidad/presupuesto (singular, la de T140); hasta el
        // 2026-09-20 el manual anunciaba /contabilidad/presupuestos, que nunca existió.
        t.Add(Proceso("presupuesto", "Presupuesto y ejecución presupuestal", Modulos.Contabilidad, "/contabilidad/presupuesto",
            "Registrar el presupuesto anual por cuenta de movimiento —y si se quiere por sucursal y centro de costo— con doce cuotas, aprobarlo, versionarlo con motivo y seguir la ejecución contra lo contabilizado.",
            [
                P("Contabilidad → Presupuesto", "Elegí el año. Se ve la versión vigente y su estado (borrador, aprobado).", "/contabilidad/presupuesto", "Abrir Presupuesto"),
                P("Cargar las cuentas", "«Agregar cuenta de movimiento» (sólo cuentas de último nivel; opcionalmente sucursal y centro) y escribí las doce cuotas. «Distribuir» reparte un total por igual, por porcentajes o con valores a mano. «Copiar del año anterior» trae el vigente del año pasado ajustado en un porcentaje."),
                P("Guardar y aprobar", "El borrador se corrige en su sitio. «Aprobar» lo fija; desde entonces cada cambio pide un motivo y crea la versión siguiente, y la anterior queda como reemplazada. «Versiones» muestra el historial y permite ver cualquiera."),
                P("Seguir la ejecución", "Pestaña «Ejecución»: mes, nivel y filtros. Presupuestado, ejecutado, variación y porcentaje del mes y acumulado por cuenta, comparado con la versión vigente y con la inicial. Un clic en la cuenta abre su libro auxiliar del mes.", "/contabilidad/presupuesto?pestana=ejecucion", "Abrir Ejecución"),
            ],
            ["presupuesto", "ejecucion", "variacion", "anual", "version", "aprobar", "distribuir", "copiar"], ["Cooperativa activa.", "Contabilidad iniciada y el ejercicio abierto en Períodos.", "Permiso Budget.View (consultar) o Budget.Manage (guardar, aprobar)."],
            // El slug del auxiliar es el de su ruta («contabilidad-libro-auxiliar»); «libro-auxiliar» a secas
            // no existía y Tema.razor descartaba el enlace en silencio hasta el 2026-09-20.
            ["plan-de-cuentas", "contabilidad-periodos", "contabilidad-libro-auxiliar"], ["/contabilidad/presupuesto"], TipoDeTema.Proceso));

        // Feature 009 E2 (US5): las consultas e informes contables, en su módulo. El Centro de Reportes sólo enlaza.
        t.Add(Consulta("/contabilidad/libro-auxiliar", "Libro auxiliar", Modulos.Contabilidad,
            "La consulta dinámica del libro: se baja de la clase al grupo, la cuenta, la subcuenta y el auxiliar; de ahí al tercero, al documento cruce, al comprobante y a sus líneas, con saldo inicial, débitos, créditos y saldo final en cada nivel. Cada nivel se exporta a Excel, PDF o Word tal como se ve.",
            "Rango de fechas y, plegados bajo «Filtros», rama del plan o rango de cuentas, tercero, documento cruce, sucursal, centro de costo, tipo de comprobante, origen, usuario e «incluir cierre». Todos se combinan y valen en cada nivel.",
            "libro auxiliar", "auxiliar", "profundizar", "tercero", "documento cruce", "comprobante", "movimientos", "saldos"));
        t.Add(Reporte("/contabilidad/informes", "Informes contables", Modulos.Contabilidad,
            "Balance de prueba, libro diario, libro mayor y balances, relación de comprobantes, documentos cruce con saldo pendiente y saldo diario promedio, con los mismos filtros combinables y exportación a Excel, PDF y Word. En el balance de prueba y el libro mayor, un clic en la cuenta abre su libro auxiliar.",
            "El informe, el rango de fechas, el nivel de detalle (balance y mayor), «con terceros» (balance) y los filtros comunes; el saldo diario promedio exige una cuenta.",
            "balance de prueba", "sumas y saldos", "libro diario", "libro mayor", "relacion de comprobantes", "documentos pendientes", "saldo promedio", "cuadrar"));
        t.Add(Reporte("/contabilidad/estados-financieros", "Estados financieros", Modulos.Contabilidad,
            "Los cuatro estados por rubro NIIF: situación financiera a una fecha (con comparativo al mismo día del año anterior), resultados del período (con el mismo período un año antes), cambios en el patrimonio y flujo de efectivo por el método indirecto. Salen siempre de los movimientos contabilizados; el cierre entra sólo si se pide.",
            "El estado, la fecha de corte o el rango, sucursal, centro de costo e «incluir cierre».",
            "estados financieros", "situacion financiera", "balance general", "estado de resultados", "pyg", "excedente", "patrimonio", "flujo de efectivo", "niif", "rubro"));
        t.Add(Consulta("/contabilidad/terceros", "Estado de cuenta del tercero", Modulos.Contabilidad,
            "Todo lo de un tercero en un rango, en una sola pantalla: débitos, créditos y neto; saldos por cuenta; documentos cruce con saldo pendiente; y los movimientos con saldo corrido. Desde cada tabla se baja al libro auxiliar de esa cuenta filtrado por el tercero, o al comprobante. Cada tabla se exporta aparte.",
            "El tercero (por documento o nombre), el rango de fechas e «incluir cierre».",
            "tercero", "estado de cuenta", "extracto", "saldo por tercero", "documentos cruce", "pendientes", "cartera del tercero"));

        t.Add(Maestro("/contabilidad/tipos-de-comprobante", "Tipos de comprobante", Modulos.Contabilidad, "un tipo de comprobante", "Cada tipo lleva su propio numerador.", "tipo", "numerador", "consecutivo"));
        t.Add(Maestro("/contabilidad/periodos", "Períodos contables", Modulos.Contabilidad, "un período", "Se crean por año y se abren o cierran por mes; ver «Cierre de período».", "periodos", "meses", "abierto", "cerrado"));
        // Grupos, subgrupos y categorías de riesgo eran maestros heredados retirados con la 009 (los rubros NIIF
        // van en el plan de cuentas); las líneas de impuestos, los formatos DIAN y los códigos de impuestos llegan
        // con E4. Sin pantalla no se ofrecen; se destapan al crear cada una:
        // t.Add(Maestro("/contabilidad/grupos-cuenta", "Grupos de cuenta", Modulos.Contabilidad, "un grupo de cuenta", null, "grupos", "agrupacion", "estados financieros"));
        // t.Add(Maestro("/contabilidad/subgrupos-cuenta", "Subgrupos de cuenta", Modulos.Contabilidad, "un subgrupo", null, "subgrupos"));
        // t.Add(Maestro("/contabilidad/categorias-riesgo", "Categorías de riesgo", Modulos.Contabilidad, "una categoría de riesgo", "Las usa la calificación de cartera para provisionar.", "riesgo", "calificacion", "provision", "a b c d e"));
        // t.Add(Maestro("/contabilidad/impuestos/iva", "Líneas de IVA", Modulos.Contabilidad, "una línea de IVA", "Tarifa, cuenta y base mínima.", "iva", "impuesto", "tarifa"));
        // t.Add(Maestro("/contabilidad/impuestos/ica", "Líneas de ICA", Modulos.Contabilidad, "una línea de ICA", null, "ica", "industria y comercio", "tarifa por mil"));
        // t.Add(Maestro("/contabilidad/impuestos/gmf", "Líneas de GMF", Modulos.Contabilidad, "una línea de GMF", null, "gmf", "cuatro por mil"));
        // t.Add(Maestro("/contabilidad/impuestos/renta", "Líneas de renta", Modulos.Contabilidad, "una línea de renta", null, "renta", "retencion"));
        // t.Add(Maestro("/contabilidad/impuestos/retefuente", "Líneas de retefuente", Modulos.Contabilidad, "una línea de retención en la fuente", "Concepto, tarifa, base y cuenta; alimentan los certificados.", "retefuente", "retencion en la fuente", "certificado"));
        // t.Add(Maestro("/contabilidad/formatos-dian", "Formatos DIAN", Modulos.Contabilidad, "un formato", "Medios magnéticos: qué conceptos y cuentas alimentan cada formato.", "dian", "medios magneticos", "exogena"));
        // t.Add(Maestro("/contabilidad/codigos-impuestos", "Códigos de impuestos", Modulos.Contabilidad, "un código de impuesto", null, "impuestos", "codigos"));

        // El balance de prueba es una vista de «Informes contables» (/contabilidad/informes?vista=trial-balance).
        // Certificados de retención: la pantalla llega con E4 (feature 009, impuestos y exógena). Hasta entonces el
        // tema no se ofrece —igual que se retiró su tarjeta del Centro de Reportes—, porque «Abrir Certificados de
        // retención» llevaba a «Página no encontrada». Al crear /contabilidad/certificados-retencion, destapar:
        // t.Add(Reporte("/contabilidad/certificados-retencion", "Certificados de retención", Modulos.Contabilidad,
        //     "Certificados de retención en la fuente, ICA e IVA por tercero y año.", "Tercero (o todos), año gravable y tipo de retención.", "certificado", "retencion", "tercero", "año gravable"));

        // ---------------------------------------------------- Cartera Financiera --
        t.Add(Proceso("solicitud-de-credito", "Solicitud de crédito", Modulos.Cartera, "/cartera/solicitudes",
            "Registrar la solicitud de un asociado, evaluarla (scoring, capacidad de pago, garantías) y aprobarla o rechazarla. Aprobada, se desembolsa desde Cartera de Créditos.",
            [
                P("Cartera Financiera → Solicitudes Crédito", "Lista por estado: radicada, en estudio, aprobada, rechazada.", "/cartera/solicitudes", "Abrir Solicitudes"),
                P("Crear solicitud", "«Buscar Asociado», línea de crédito, monto, plazo y periodicidad. La tasa la trae la línea; el plan de pagos se calcula al vuelo.", "/cartera/solicitudes/nueva", "Abrir Nueva Solicitud"),
                P("Evaluar", "«Calcular» corre el scoring con los parámetros de la cooperativa. Registrá garantías y codeudores si la línea los exige."),
                P("Aprobar o rechazar", "Según la atribución de quien aprueba (montos máximos por rol). Rechazar pide motivo. Todo queda en auditoría."),
                P("Desembolsar", "La solicitud aprobada aparece en Cartera de Créditos → «Desembolsar»: elige la forma (abono a ahorros, cheque, transferencia) y contabiliza.", "/cartera/creditos", "Ir a Cartera de Créditos"),
            ],
            ["solicitud", "credito", "prestamo", "scoring", "aprobar", "rechazar", "plan de pagos", "codeudor", "garantia"],
            ["Cooperativa activa.", "El asociado registrado y activo.", "Líneas de crédito y tasas configuradas."],
            ["cartera-de-creditos", "cartera-lineas-credito", "cartera-scoring"], ["/cartera/solicitudes/nueva"], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-de-creditos", "Cartera de créditos", Modulos.Cartera, "/cartera/creditos",
            "Los créditos vigentes y su estado: saldo, cuotas, mora, calificación. Desde aquí se desembolsa, se consulta el plan de pagos y se ve el detalle de cada crédito.",
            [
                P("Cartera Financiera → Cartera Créditos", "Filtrá por asociado, línea, estado o días de mora.", "/cartera/creditos", "Abrir Cartera de Créditos"),
                P("Desembolsar", "Para créditos aprobados y aún no entregados. Elegí la forma de desembolso; el comprobante contable se genera solo.", "/cartera/desembolsos", "Abrir Desembolsos"),
                P("Ver el detalle", "Plan de pagos, cuotas pagadas y pendientes, intereses causados, historial de recaudos, garantías."),
                P("Extracto", "Desde el detalle, «Extracto de crédito» genera el estado de cuenta para el asociado.", "/reportes/extracto-credito", "Abrir Extracto de Crédito"),
            ],
            ["cartera", "creditos", "saldo", "cuotas", "mora", "desembolsar", "plan de pagos", "estado de cuenta"], ["Cooperativa activa."],
            ["solicitud-de-credito", "recaudos-y-pagos", "mora-y-cobro", "causacion-de-intereses"], ["/cartera/creditos/{PortfolioId}", "/cartera/desembolsos"], TipoDeTema.Proceso));

        t.Add(Proceso("recaudos-y-pagos", "Recaudos y pagos", Modulos.Cartera, "/cartera/recaudos",
            "Aplicar el pago de un asociado a sus obligaciones: cuotas de crédito, aportes, ahorros programados. El sistema distribuye según la prelación configurada.",
            [
                P("Cartera Financiera → Recaudos/Pagos", null, "/cartera/recaudos", "Abrir Recaudos"),
                P("Buscar al asociado", "Se listan sus obligaciones con lo vencido y lo por vencer."),
                P("Registrar el pago", "Valor, forma de pago (efectivo, consignación, descuento de nómina) y fecha. El sistema aplica primero mora, luego intereses, luego capital, salvo que la cooperativa configure otro orden."),
                P("Confirmar", "Se genera el recibo y el comprobante contable. Un recaudo confirmado se reversa con «Devolver», no se edita."),
            ],
            ["recaudo", "pago", "abono", "cuota", "recibo", "aplicar pago", "devolver"], ["Cooperativa activa.", "Caja o cuenta bancaria definida para la forma de pago."],
            ["cartera-de-creditos", "descuento-de-nomina", "aportes"], [], TipoDeTema.Proceso));

        t.Add(Proceso("mora-y-cobro", "Mora y cobro", Modulos.Cartera, "/cartera/mora",
            "Los créditos con cuotas vencidas, por días de mora y calificación, y el registro de las gestiones de cobro.",
            [
                P("Cartera Financiera → Mora y Cobro", "Ordenado por días de mora. Filtrá por rango, línea o zona.", "/cartera/mora", "Abrir Mora y Cobro"),
                P("Registrar una gestión", "Llamada, visita, acuerdo de pago: fecha, resultado y próxima acción. Queda en el historial del crédito."),
                P("Cartera por edades", "El reporte agrupa la cartera por tramos de mora; es el insumo de la provisión.", "/reportes/cartera-edades", "Abrir Cartera por Edades"),
            ],
            ["mora", "cobro", "vencido", "gestion de cobro", "acuerdo de pago", "edades"], ["Cooperativa activa."],
            ["cartera-de-creditos", "calificacion-de-cartera"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cuentas-de-ahorro", "Cuentas de ahorro", Modulos.Cartera, "/cartera/ahorros",
            "Abrir cuentas de ahorro a la vista o programado, consignar, retirar y liquidar intereses.",
            [
                P("Cartera Financiera → Cuentas de Ahorro", "Lista por asociado y tipo. «Nuevo» abre una cuenta: asociado, tipo de ahorro (sus parámetros traen tasa y condiciones) y monto de apertura.", "/cartera/ahorros", "Abrir Cuentas de Ahorro"),
                P("Depositar", "Cartera → Depósitos: cuenta, valor y forma de pago. Genera el comprobante.", "/cartera/depositos", "Abrir Depósitos"),
                P("Retirar", "Cartera → Retiros: valida saldo disponible y, si el ahorro es programado, las condiciones de retiro anticipado.", "/cartera/retiros", "Abrir Retiros"),
                P("Liquidar intereses", "Proceso periódico (mensual, normalmente) que causa y abona intereses a todas las cuentas.", "/cartera/ahorros/liquidacion", "Abrir Liquidación de Intereses"),
                P("Detalle y extracto", "Desde la fila: movimientos, saldos y el extracto para el asociado.", "/cartera/extractos", "Abrir Extractos"),
            ],
            ["ahorro", "cuenta de ahorro", "consignar", "depositar", "retirar", "intereses", "a la vista", "programado"], ["Cooperativa activa.", "Parámetros de ahorro configurados."],
            ["cartera-parametros-ahorro", "liquidacion-intereses-ahorros"], ["/cartera/ahorros/{AccountId}", "/cartera/depositos", "/cartera/retiros"], TipoDeTema.Proceso));

        t.Add(Proceso("cdt", "CDT: certificados de depósito a término", Modulos.Cartera, "/cartera/cdt",
            "Constituir un CDT, seguirlo hasta el vencimiento, renovarlo o cancelarlo, y liquidar sus intereses.",
            [
                P("Cartera Financiera → CDT Certificados", "Lista por estado: vigente, vencido, renovado, cancelado.", "/cartera/cdt", "Abrir CDT"),
                P("Nuevo CDT", "Asociado, monto, plazo en días y periodicidad de pago de intereses. La tasa la trae CDT → Tasas por Plazo.", "/cartera/cdt/nuevo", "Abrir Nuevo CDT"),
                P("Emitir", "Genera el certificado con su número y el comprobante contable."),
                P("Al vencimiento", "«Renovar CDT» constituye uno nuevo con el capital (y los intereses, si se capitalizan). «Cancelar» paga capital e intereses; una cancelación anticipada aplica la penalidad configurada."),
                P("Liquidar intereses", "Proceso periódico para los CDT con pago de intereses periódico.", "/cartera/cdt/liquidacion", "Abrir Liquidación de Intereses CDT"),
            ],
            ["cdt", "certificado", "deposito a termino", "renovar", "cancelar", "plazo", "tasa"], ["Cooperativa activa.", "Parámetros y tasas de CDT configurados."],
            ["cdt-parametros", "cdt-tasas-plazo"], ["/cartera/cdt/nuevo", "/cartera/cdt/{CertificateId}"], TipoDeTema.Proceso));

        t.Add(Proceso("aportes", "Aportes sociales", Modulos.Cartera, "/cartera/aportes",
            "Registrar los aportes de los asociados, obligatorios o extraordinarios, y consultar su acumulado.",
            [
                P("Cartera Financiera → Aportes", "Por asociado: acumulado, últimos aportes, cuota mensual pactada.", "/cartera/aportes", "Abrir Aportes"),
                P("Registrar Aporte", "Asociado, valor, fecha y forma de pago. Los aportes por nómina llegan solos desde Descuento Nómina."),
                P("Devolución", "Al retirarse el asociado, los aportes se devuelven desde Retiro de Asociado, cruzados con lo que deba.", "/cartera/retiros-asociado", "Abrir Retiro de Asociado"),
            ],
            ["aportes", "aporte social", "cuota", "acumulado", "devolucion de aportes"], ["Cooperativa activa."], ["registro-de-asociados", "retiro-de-asociado", "descuento-de-nomina"], [], TipoDeTema.Proceso));

        t.Add(Proceso("retiro-de-asociado", "Retiro de asociado", Modulos.Cartera, "/cartera/retiros-asociado",
            "Desvincular a un asociado: cruzar sus aportes y ahorros contra sus deudas, devolver el saldo y dejarlo inactivo.",
            [
                P("Cartera Financiera → Retiro Asociado", null, "/cartera/retiros-asociado", "Abrir Retiro de Asociado"),
                P("Buscar al asociado y elegir el motivo", "Los motivos se administran en Maestros → Motivos de Retiro. La pantalla muestra aportes, ahorros y deudas."),
                P("Cruce", "El sistema propone aplicar aportes y ahorros a las deudas. Si queda saldo a favor, se devuelve; si queda deuda, no se puede retirar hasta cubrirla."),
                P("Confirmar Retiro", "Genera los comprobantes y deja al asociado inactivo. El estado del trámite se sigue en Estados de Retiro."),
            ],
            ["retiro", "desvincular", "asociado", "devolucion", "cruce", "paz y salvo"], ["Cooperativa activa.", "Permiso de retiro."],
            ["aportes", "registro-de-asociados", "cartera-estados-retiro"], [], TipoDeTema.Proceso));

        t.Add(Proceso("causacion-de-intereses", "Causación de intereses", Modulos.Cartera, "/cartera/causacion",
            "Proceso de cierre de mes que reconoce contablemente los intereses devengados y no cobrados de toda la cartera.",
            [
                P("Cartera Financiera → Causación Intereses", "Elegí el período. La pantalla muestra si ya se causó.", "/cartera/causacion", "Abrir Causación"),
                P("Simular", "Antes de contabilizar, revisá el total por línea. Diferencias grandes contra el mes anterior merecen una mirada."),
                P("Contabilizar", "Genera el comprobante. Se corre una vez por período; correrlo de nuevo no duplica: reemplaza."),
            ],
            ["causacion", "intereses", "devengado", "cierre de mes", "contabilizar cartera"], ["Cooperativa activa.", "Cuentas de cartera configuradas."],
            ["cierre-de-periodo", "calificacion-de-cartera", "cartera-cuentas-cartera"], [], TipoDeTema.Proceso));

        t.Add(Proceso("calificacion-de-cartera", "Calificación de cartera y provisión", Modulos.Cartera, "/cartera/calificacion",
            "Asignar a cada crédito su categoría de riesgo según días de mora y otros criterios, y calcular la provisión.",
            [
                P("Cartera Financiera → Calificación Cartera", "Elegí la fecha de corte.", "/cartera/calificacion", "Abrir Calificación"),
                P("Calcular", "Aplica las categorías (A a E) con los parámetros de provisión. Revisá los créditos que cambian de categoría."),
                P("Contabilizar la provisión", "Genera el comprobante de provisión o su reversión. Es insumo del cierre y de los reportes a la Supersolidaria."),
            ],
            ["calificacion", "provision", "categoria de riesgo", "supersolidaria", "mora"], ["Cooperativa activa.", "Categorías y parámetros de provisión configurados."],
            ["mora-y-cobro", "causacion-de-intereses", "cartera-parametros-provision"], [], TipoDeTema.Proceso));

        t.Add(Proceso("descuento-de-nomina", "Descuento por nómina", Modulos.Cartera, "/cartera/descuento-nomina",
            "Generar el archivo de descuentos para las empresas pagadoras y aplicar lo que devuelven pagado.",
            [
                P("Cartera Financiera → Descuento Nómina", "Elegí la empresa (convenio) y el período.", "/cartera/descuento-nomina", "Abrir Descuento Nómina"),
                P("Generar", "Lista lo que cada asociado de esa empresa debe este período: cuotas, aportes, ahorro programado. Exportá el archivo en el formato del convenio."),
                P("Aplicar el pago", "Cuando la empresa gira, cargá el archivo de respuesta o marcá lo pagado: se aplican los recaudos uno a uno y quedan las novedades (no descontado, descontado parcial)."),
            ],
            ["descuento de nomina", "libranza", "empresa", "convenio", "archivo", "pagaduria"], ["Cooperativa activa.", "Empresas y convenios en Maestros."],
            ["recaudos-y-pagos", "maestros-convenios"], [], TipoDeTema.Proceso));

        t.Add(Consulta("/cartera/extractos", "Extractos", Modulos.Cartera,
            "Estados de cuenta de ahorros y créditos por asociado y período, para entregar o enviar.",
            "Asociado, producto y período.", "extracto", "estado de cuenta", "movimientos del asociado"));
        t.Add(Proceso("liquidacion-intereses-ahorros", "Liquidación de intereses de ahorros", Modulos.Cartera, "/cartera/ahorros/liquidacion",
            "Causar y abonar los intereses de todas las cuentas de ahorro del período.",
            [
                P("Cartera Financiera → Liq. Int. Ahorros", "Elegí el período y el tipo de ahorro (o todos).", "/cartera/ahorros/liquidacion", "Abrir la liquidación"),
                P("Simular", "Revisá el total y los casos extremos antes de abonar."),
                P("Liquidar", "Abona a cada cuenta y genera el comprobante. Retención en la fuente sobre intereses según la línea de retefuente."),
            ],
            ["liquidacion", "intereses", "ahorros", "abonar"], ["Cooperativa activa."], ["cuentas-de-ahorro"], [], TipoDeTema.Proceso));
        t.Add(Proceso("liquidacion-intereses-cdt", "Liquidación de intereses de CDT", Modulos.Cartera, "/cartera/cdt/liquidacion",
            "Pagar o capitalizar los intereses de los CDT con vencimiento de intereses en el período.",
            [
                P("Cartera Financiera → Liq. Int. CDTs", null, "/cartera/cdt/liquidacion", "Abrir la liquidación"),
                P("Revisar la lista", "Qué certificados vencen intereses y cómo se pagan (abono a ahorros, cheque, capitalización)."),
                P("Liquidar", "Genera los pagos y el comprobante."),
            ],
            ["liquidacion", "intereses", "cdt"], ["Cooperativa activa."], ["cdt"], [], TipoDeTema.Proceso));

        t.Add(Maestro("/cartera/lineas-credito", "Líneas de crédito", Modulos.Cartera, "una línea de crédito", "Tasa, plazo máximo, garantías exigidas y cuentas contables.", "lineas", "credito", "tasa", "plazo"));
        t.Add(Maestro("/cartera/codigos-movimiento", "Códigos de movimiento", Modulos.Cartera, "un código de movimiento", null, "codigos", "movimiento", "transaccion"));
        t.Add(Maestro("/cartera/parametros-ahorro", "Parámetros de ahorro", Modulos.Cartera, "un tipo de ahorro", "Tasa, monto mínimo, condiciones de retiro y cuentas.", "ahorro", "tipo", "tasa", "parametros"));
        t.Add(Maestro("/cartera/tasas-interes", "Tasas de interés", Modulos.Cartera, "una tasa", "Vigencias: al cambiar una tasa se crea una vigencia nueva, no se pisa la anterior.", "tasas", "interes", "vigencia"));
        t.Add(Maestro("/cartera/parametros-provision", "Parámetros de provisión", Modulos.Cartera, "un parámetro de provisión", "Porcentaje por categoría de riesgo y tipo de cartera.", "provision", "porcentaje", "categoria"));
        t.Add(Maestro("/cartera/zonas", "Zonas", Modulos.Cartera, "una zona", null, "zonas", "geografia", "cobro"));
        t.Add(Maestro("/cartera/tipos-zona", "Tipos de zona", Modulos.Cartera, "un tipo de zona", null, "tipos de zona"));
        t.Add(Maestro("/cartera/scoring", "Parámetros de scoring", Modulos.Cartera, "un criterio de scoring", "Variables, pesos y puntaje mínimo de aprobación.", "scoring", "puntaje", "riesgo", "aprobacion"));
        t.Add(Maestro("/cartera/conceptos-descuento", "Conceptos de descuento", Modulos.Cartera, "un concepto de descuento", null, "conceptos", "descuento", "nomina"));
        t.Add(Maestro("/cartera/estados-retiro", "Estados de retiro", Modulos.Cartera, "un estado de retiro", "El flujo del trámite de retiro de asociado.", "estados", "retiro", "tramite"));
        t.Add(Maestro("/cartera/parametros-vivienda", "Parámetros de vivienda", Modulos.Cartera, "un parámetro de crédito de vivienda", null, "vivienda", "credito hipotecario", "uvr"));
        t.Add(Maestro("/cartera/parametros-sipla", "Parámetros SIPLA", Modulos.Cartera, "un parámetro SIPLA", "Umbrales de operaciones inusuales y sospechosas para el reporte a la UIAF.", "sipla", "sarlaft", "uiaf", "lavado de activos", "umbral"));
        t.Add(Maestro("/cartera/parametros-periodicidad", "Parámetros de periodicidad", Modulos.Cartera, "una periodicidad", "Mensual, quincenal, semanal: cuántas cuotas por año.", "periodicidad", "cuotas", "frecuencia"));
        t.Add(Maestro("/cartera/tasas-plazo", "Tasas por plazo", Modulos.Cartera, "una tasa por plazo", null, "tasas", "plazo"));
        t.Add(Maestro("/cartera/cuentas-cartera", "Cuentas de cartera", Modulos.Cartera, "una cuenta de cartera", "Contra qué cuentas contabiliza cada línea: capital, intereses, mora, provisión.", "cuentas", "contabilizacion", "cartera"));

        // ------------------------------------------------------------ CDT (params) --
        t.Add(Maestro("/cdt/parametros", "Parámetros CDT", Modulos.Cdt, "un parámetro de CDT", "Monto mínimo, plazos permitidos, penalidad por cancelación anticipada, cuentas.", "cdt", "parametros", "penalidad"));
        t.Add(Maestro("/cdt/tasas-plazo", "Tasas por plazo CDT", Modulos.Cdt, "una tasa por plazo", "Tasa según rango de días y monto; con vigencias.", "cdt", "tasas", "plazo", "vigencia"));

        // --------------------------------------------------------------- Inventario --
        // Feature 012, US12 (T437): el rol vendedor pasó a Ventas; seguridad, aprobaciones, parámetros y alertas.
        t.Add(Maestro("/ventas/vendedores", "Vendedores", Modulos.Inventario, "un vendedor",
            "La persona se busca en el maestro (o se crea en su diálogo); dar el rol a quien lo tuvo lo restaura con el mismo identificador y retirarlo pide motivo. Plantilla 9 para cargar en bloque.",
            "vendedores", "comision", "restaurar", "plantilla 9"));
        t.Add(Proceso("inventario-parametros", "Parámetros de inventario", Modulos.Inventario, "/inventario/parametros",
            "Los parámetros del módulo con su valor vigente, las excepciones por bodega o tipo de documento y lo programado. Un cambio es una vigencia nueva desde una fecha, con motivo: los documentos de hoy siguen con el valor de hoy y el historial conserva todos.",
            [
                P("Inventario → Parámetros", "Cada clave dice su valor vigente, si sale de una vigencia o del defecto y qué hay programado.", "/inventario/parametros", "Abrir Parámetros"),
                P("Historial", "Todas las vigencias de la clave, con autor, fecha y motivo."),
                P("Nueva vigencia", "Valor, ámbito, fecha desde y motivo (Inventory.Parameters.Manage). El costeo exige empezar un período y su permiso; dejar sin paso a contabilidad un tipo fiscal pide confirmarlo con la lista de tipos."),
            ],
            ["parametros", "vigencia", "stock negativo", "modo de paso", "costeo", "historial"],
            ["Permiso Inventory.Parameters.View; para registrar, Inventory.Parameters.Manage."], ["inventario-politicas-de-aprobacion"], [], TipoDeTema.Proceso));
        t.Add(Proceso("inventario-aprobaciones", "Aprobaciones de inventario", Modulos.Inventario, "/inventario/aprobaciones",
            "La bandeja de lo que la persona puede aprobar ahora y el seguimiento de lo de su alcance. Quien crea no aprueba y quien aprobó un nivel no aprueba otro; al aprobar el último nivel el documento se confirma y se numera.",
            [
                P("Inventario → Aprobaciones", "«Por decidir» trae lo pendiente para usted; «Seguimiento», todo lo de su alcance.", "/inventario/aprobaciones", "Abrir Aprobaciones"),
                P("Ver", "Los niveles, quién decidió cada uno y, si no puede decidir, por qué."),
                P("Aprobar o rechazar", "Rechazar pide motivo y devuelve el documento a borrador. Si el documento cambió después de abrirlo, hay que volver a mirarlo."),
                P("Retirar", "Quien pidió la aprobación la puede retirar con motivo desde «Seguimiento»."),
            ],
            ["aprobaciones", "aprobar", "rechazar", "niveles", "segregacion", "bandeja"],
            ["Permiso Inventory.Approvals.View y el permiso del nivel."], ["inventario-politicas-de-aprobacion"], [], TipoDeTema.Proceso));
        t.Add(Proceso("inventario-politicas-de-aprobacion", "Políticas de aprobación y montos máximos", Modulos.Inventario, "/inventario/politicas-de-aprobacion",
            "Qué documentos piden aprobación, en cuántos niveles y de quién, y hasta qué monto puede confirmar cada rol sin aprobación.",
            [
                P("Inventario → Políticas de aprobación", "Pestaña Políticas: por sujeto y tipo de documento, versiones con sus niveles.", "/inventario/politicas-de-aprobacion", "Abrir Políticas"),
                P("Nueva versión", "Niveles en orden con su umbral y el permiso de quien aprueba, fecha desde y motivo."),
                P("Montos máximos", "Por rol y permiso (compras, ajustes, notas de venta, crédito). Vacío es sin límite. Si un documento supera el monto de quien confirma, exige al menos el nivel 1; sin política, se rechaza."),
            ],
            ["politicas", "niveles", "umbral", "monto maximo", "limite", "rol"],
            ["Permiso Inventory.ApprovalPolicies.View; para cambiar, Inventory.ApprovalPolicies.Manage."], ["inventario-aprobaciones"], [], TipoDeTema.Proceso));
        t.Add(Proceso("inventario-alcances", "Alcance comercial por bodega", Modulos.Inventario, "/inventario/alcances",
            "Qué bodegas ve y opera cada usuario. Fuera de su alcance una bodega no existe para él: listas, detalle, kardex y altas responden igual que a lo inexistente.",
            [
                P("Inventario → Alcance comercial", "Elegí el usuario (también desde Seguridad › Usuarios, «Alcance comercial»).", "/inventario/alcances", "Abrir Alcances"),
                P("Asignar", "Marcá las bodegas, operativas o de tránsito, y a lo sumo una por defecto; «Guardar alcance»."),
                P("Alcance total", "Quien tiene el permiso Inventory.Scope.AllWarehouses ve todas sin asignación."),
            ],
            ["alcance", "bodegas", "usuario", "seguridad", "por defecto"],
            ["Permiso Inventory.Scopes.Manage."], ["usuarios"], [], TipoDeTema.Proceso));
        t.Add(Proceso("inventario-alertas", "Alertas de inventario", Modulos.Inventario, "/inventario/alertas",
            "Las alertas que le llegan a la persona —quiebre, reorden, integridad, aprobaciones pendientes— y a quién le llega cada tipo.",
            [
                P("Inventario → Alertas", "Bandeja por estado, tipo, severidad y fecha. «Sin destinatario» es una alerta que se envió al administrador porque nadie activo tenía el permiso.", "/inventario/alertas", "Abrir Alertas"),
                P("Atender", "Con una nota de lo que se hizo; queda para todos quién y cuándo."),
                P("Tipos", "Permisos destinatarios, canales (la aplicación siempre, el correo opcional), umbrales y vigencias; una vigencia nueva pide motivo."),
            ],
            ["alertas", "quiebre", "reorden", "atender", "destinatarios", "notificaciones"],
            ["Permiso Inventory.Alerts.View; atender, Inventory.Alerts.Attend; tipos, Inventory.Alerts.Manage."], [], [], TipoDeTema.Proceso));

        // -------------------------------------------------------------------- Nómina --
        t.Add(Proceso("empleados", "Empleados", Modulos.Nomina, "/nomina/empleados",
            "Las personas vinculadas laboralmente: contrato, cargo, salario, afiliaciones (EPS, ARL, pensión, cesantías) y cuenta de pago.",
            [
                P("Nómina → Empleados", null, "/nomina/empleados", "Abrir Empleados"),
                P("Nuevo Empleado", "Parte de una persona del registro único. Tipo de contrato, fecha de ingreso, cargo, salario base, periodicidad y afiliaciones."),
                P("Detalle", "Historial de contratos, novedades y liquidaciones. «Terminar Contrato» registra la fecha y el motivo y habilita la liquidación definitiva."),
            ],
            ["empleados", "contrato", "salario", "cargo", "afiliaciones", "eps", "arl", "pension", "terminar contrato"], ["Cooperativa activa.", "La persona en el registro único.", "EPS, ARL, fondos y cargos creados."],
            ["personas", "liquidacion-de-nomina", "novedades-de-nomina"], ["/nomina/empleados/{EmployeeId}"], TipoDeTema.Proceso));

        t.Add(Proceso("novedades-de-nomina", "Novedades de nómina", Modulos.Nomina, "/nomina/novedades",
            "Lo que cambia en un período respecto del salario fijo: horas extra y recargos, comisiones y bonificaciones, incapacidades, licencias, vacaciones, descuentos autorizados y cambios de salario con fecha de efecto. Cada novedad dice a quién, con qué concepto, cuánto y desde y hasta cuándo, y muestra el valor que aportará a la liquidación.",
            [
                P("Nómina → Novedades", "Elegí el período (si hay un solo plan de nómina no se pregunta por él). La pastilla de arriba dice si está abierto, calculado o aprobado; en uno aprobado la pantalla es de sólo consulta.", "/nomina/novedades", "Abrir Novedades"),
                P("Nuevo", "Buscá al empleado, elegí el concepto (la lista sólo trae los vigentes que aplican a su clase) y completá lo que el concepto pida: cantidad (horas o días), valor o fechas. Guardá: la grilla muestra el valor previsto con el salario vigente."),
                P("Fechas que cruzan el período", "Una incapacidad o licencia que termina después del fin del período se liquida por los días que caen dentro; el resto queda registrado y aparece solo en el período siguiente como «Traslado»."),
                P("Corregir", "El lápiz de la fila pide el cambio y un motivo. No se sobreescribe nada: queda una versión nueva y la anterior se ve en «Historial» con quién y cuándo."),
                P("Anular", "La papelera pide motivo y marca la novedad como anulada; sigue visible en texto tenue."),
                P("Cambio de salario", "Botón «Cambio de salario»: empleado, salario nuevo, fecha de efecto y motivo. El historial de salarios del empleado se conserva y la liquidación paga cada tramo de días con el salario que regía en él."),
                P("Período aprobado", "No se registra ni se corrige nada en un período aprobado: el sistema ofrece registrar un ajuste retroactivo en el período abierto siguiente, con referencia al original."),
                P("Borrador desactualizado", "Si el período ya estaba calculado, cualquier novedad nueva, corregida o anulada deja el borrador «desactualizado» y hay que recalcular antes de aprobar."),
                P("Importar desde archivo", "Botón «Importar»: descargá la plantilla (CSV separado por punto y coma: documento, concepto, cantidad, valor, desde, hasta, observación), completala y subila. Cada fila pasa las mismas reglas que el registro manual. Si una sola falla, no entra ninguna y la pantalla muestra la lista de errores con fila y columna; corregí el archivo y volvé a subirlo. El límite es 5 MB por archivo."),
                P("Recurrentes", "Botón «Recurrentes»: una novedad que se repite cada período (un descuento por cuotas, un auxilio fijo) se registra una vez con empleado, concepto, cantidad o valor, fecha desde y, si aplica, hasta o número de cuotas. En cada cálculo aparece como novedad «Recurrente» con su número de cuota; al aprobar se cuenta la cuota emitida, y al llegar a la última deja de generarse sola. Desactivarla pide motivo y anula las que estén en períodos aún no aprobados."),
            ],
            ["novedades", "horas extra", "recargo", "incapacidad", "vacaciones", "licencia", "descuento", "prestamo", "libranza", "cambio de salario", "traslado", "corregir", "anular", "retroactivo", "importar", "csv", "plantilla", "recurrente", "cuotas"],
            ["Cooperativa activa.", "Un período de pago abierto del plan del empleado.", "Permiso Payroll.Novelties.Create para registrar; Update para corregir; Cancel para anular; Import para importar archivos."],
            ["liquidacion-de-nomina", "conceptos-de-nomina", "nomina-periodos-pago", "empleados"], [], TipoDeTema.Proceso));

        t.Add(Proceso("liquidacion-de-nomina", "Liquidación de nómina", Modulos.Nomina, "/nomina/liquidacion",
            "Calcular el período en borrador para todos los empleados del plan —salario por los días vinculados, auxilio de transporte, novedades, deducciones de ley, aportes del empleador y provisiones—, revisarlo empleado por empleado con la explicación de cada valor, recalcular las veces que haga falta y aprobar: el período se cierra y el comprobante contable NM se genera en la misma operación.",
            [
                P("Antes", "Todas las novedades registradas y los parámetros legales del año con vigencia. Si falta uno, el cálculo se niega y lo nombra.", "/nomina/novedades", "Revisar Novedades"),
                P("Nómina → Liquidación", "Elegí el período. La tarjeta de estado dice si está sin cálculo, en borrador, desactualizado o aprobado.", "/nomina/liquidacion", "Abrir Liquidación"),
                P("Calcular", "Produce el borrador v1: totales, tabla por concepto y bloqueos (neto negativo, deducciones sobre el máximo, afiliación faltante, procedimiento 2 sin porcentaje). Cada recálculo crea una versión nueva y señala qué empleados cambiaron."),
                P("Revisar", "Pestaña Empleados: días, devengado, deducido, neto y banderas; clic abre el detalle con los tramos de salario, las bases del período y cada línea con su explicación (forma, pasos, parámetro y vigencia, novedad de origen)."),
                P("Comparar y cuadrar", "Pestaña Comparativo: neto anterior, actual y variación por empleado contra el último período aprobado del plan, con las variaciones sobre el umbral resaltadas y los nuevos y retirados marcados. Pestaña Cuadre: devengos − deducciones = neto, aportes y provisiones fuera del neto, y el comprobante cuadrado cuando ya existe."),
                P("Exportar", "Botón «Exportar»: un CSV con cada línea de cada empleado, su base, factor, parámetro y vigencia, novedad de origen y la explicación en texto, para revisarlo en una hoja de cálculo o entregarlo al revisor fiscal. La exportación queda auditada."),
                P("Desactualizado", "Si alguien registra, corrige o anula una novedad, cambia un salario, un concepto o un parámetro, el borrador queda desactualizado y hay que recalcular antes de aprobar."),
                P("Aprobar", "Botón «Aprobar»: resumen, bloqueos con «Autorizar excepción» y motivo (sólo con el permiso), confirmación explícita. Quien registró novedades o calculó no aprueba, salvo que la cooperativa lo permita con segunda confirmación. Al aprobar, el período queda cerrado, las novedades y la liquidación inmutables, y sale el comprobante NM cuadrado."),
                P("Relación de pago", "Con el período aprobado aparece la pestaña «Relación de pago»: cada empleado con su neto, banco, tipo y número de cuenta, y si ya está pagado. El pago se ejecuta fuera del sistema (banco, cheque, efectivo); aquí sólo se deja constancia."),
                P("Marcar pagados", "«Marcar pagados» pide fecha, medio (transferencia, cheque, efectivo) y una referencia opcional, y marca a todos los que faltan. La flecha de una fila retira la marca con motivo, para volver a marcar con el dato correcto. Cada marca y cada retiro quedan auditados."),
                P("Comprobantes", "El ícono de documento de cada fila descarga el comprobante de pago del empleado en PDF: devengos y deducciones con la explicación de cada valor, salario, días, banco y estado de pago. «Comprobantes PDF» baja todos en un solo archivo; también está en el detalle del empleado."),
                P("Enviar por correo", "«Enviar por correo» manda a cada empleado su comprobante al correo registrado en su ficha. Nunca es automático. El resultado dice cuántos salieron, cuántos fallaron y quiénes no tienen correo; cada intento queda en la lista de envíos con fecha, quién lo pidió y el error si lo hubo. Si el ambiente no tiene correo saliente configurado, el sistema lo dice antes de intentar."),
                P("Después", "Un período aprobado no se recalcula ni admite novedades: los ajustes van al siguiente período como retroactivos, o se reversa el período con motivo (Nómina › Liquidación › Reversar)."),
                P("Reversar", "Botón «Reversar», sólo sobre una liquidación aprobada y con permiso Payroll.Runs.Reverse. Pide motivo y explica el efecto: el comprobante NM se reversa con un asiento espejo que lo referencia, la corrida queda marcada como reversada con quién, cuándo y por qué, y el período vuelve a abrirse con sus novedades intactas para corregir y recalcular. Nada se borra y todo queda en la pestaña «Historial»."),
                P("Cuándo no se puede reversar", "Si algún empleado tiene marca de pago vigente, el sistema lo dice y lista a quiénes: retirá las marcas (con motivo) en «Relación de pago» antes. Si el período contable del día está cerrado, la reversión tampoco se contabiliza; hay que reabrirlo en Contabilidad. Reversar no es la vía para un ajuste pequeño: eso es un retroactivo en el período siguiente."),
            ],
            ["liquidacion", "nomina", "calcular", "recalcular", "borrador", "aprobar", "comprobante", "asiento", "devengado", "deduccion", "aportes", "provisiones", "neto", "bloqueo", "excepcion", "explicacion", "relacion de pago", "pagado", "banco", "cuenta", "comprobante de pago", "desprendible", "correo"],
            ["Cooperativa activa.", "Conceptos con cuentas contables, parámetros legales vigentes y período contable abierto.", "Permiso Payroll.Runs.Calculate para calcular y Payroll.Runs.Approve para aprobar; Payroll.Payments.Mark para marcar pagos; Payroll.Payslips.View y Payroll.Payslips.Send para comprobantes y su envío."],
            ["novedades-de-nomina", "parametros-legales", "conceptos-de-nomina", "nomina-periodos-pago"], [], TipoDeTema.Proceso));

        t.Add(Proceso("conceptos-de-nomina", "Conceptos de nómina", Modulos.Nomina, "/nomina/conceptos",
            "Cada concepto dice qué es (devengo, deducción, aporte del empleador, provisión o informativo), cómo se calcula con una de cinco formas predefinidas (valor fijo, porcentaje sobre base, cantidad × unidad, tabla por rangos, suma de conceptos), qué bases alimenta y a qué clases de empleado aplica. Cambiarlo crea una versión con fecha: las liquidaciones aprobadas siguen mostrando la versión con la que se calcularon. No hay fórmulas libres: si ninguna forma cubre el caso, es una forma nueva del programa, no una expresión escrita a mano.",
            [
                P("Nómina → Conceptos", "Pestaña «Definiciones»: los vigentes, con su forma, bases, origen (semilla, propio, traducido) y si tienen cuentas contables. Sin cuentas, la aprobación de la nómina se bloquea.", "/nomina/conceptos", "Abrir Conceptos"),
                P("Nuevo concepto", "Código, nombre, tipo, forma de cálculo —el formulario cambia los campos según la forma—, bases que afecta, si es automático o lo trae la novedad (cantidad, valor, fechas), topes, clases aplicables y vigencia desde. Los porcentajes y topes legales se referencian por código de parámetro, nunca se escriben como número."),
                P("Cuentas contables", "Ícono de tabla en la fila: una fila por defecto y, si hace falta, una por centro de costo, con cuenta débito y crédito buscadas en el plan de cuentas."),
                P("Probar en seco", "Ícono de reproducir: elegí un empleado y un período y el sistema calcula la línea con su explicación, sin guardar. Es la forma de comprobar una definición antes de que entre en una nómina."),
                P("Nueva versión", "El lápiz abre la definición vigente para guardarla como versión nueva desde una fecha; la anterior se cierra el día antes. Si hay un borrador calculado, queda desactualizado."),
                P("Desactivar", "Cierra la vigencia en una fecha. Nada se borra; el salario básico no se desactiva."),
                P("Catálogo heredado", "Los conceptos del sistema anterior, de sólo lectura. «Traducir a definición» prellena lo deducible y deja vacío lo que no se sabe."),
                P("Reaplicar semilla", "Vuelve a insertar los conceptos y parámetros estándar que falten (tras una actualización), sin tocar lo que la cooperativa ya ajustó."),
            ],
            ["conceptos", "devengo", "deduccion", "aporte", "provision", "forma de calculo", "porcentaje", "tabla", "version", "vigencia", "cuentas", "probar en seco", "heredado", "semilla"],
            ["Cooperativa activa.", "Permiso Payroll.Concepts.Manage para crear, revisar, desactivar y configurar cuentas."],
            ["parametros-legales", "liquidacion-de-nomina", "novedades-de-nomina", "plan-de-cuentas"], [], TipoDeTema.Proceso));
        t.Add(Maestro("/nomina/periodos-pago", "Períodos de pago", Modulos.Nomina, "un período de pago", "Cada período pertenece a un plan de nómina (mensual o quincenal) y no puede superponerse con otro del mismo plan. Nace abierto; lo cierran calcular y aprobar, nunca la edición. Con un solo plan la pantalla no pregunta por él.", "periodos", "quincena", "mes", "pago", "plan"));
        t.Add(Maestro("/nomina/planes", "Planes de nómina", Modulos.Nomina, "un plan de nómina", "Un plan es un grupo de empleados que se paga con la misma periodicidad (mensual de 30 días o quincenal de 15). Toda cooperativa nace con el plan por defecto y casi ninguna necesita otro; el plan por defecto no se desactiva y un plan con empleados vigentes tampoco. El cambio de plan de un empleado se hace desde su ficha, con fecha de efecto posterior al período abierto de su plan actual.", "planes", "plan de nomina", "periodicidad", "mensual", "quincenal"));
        t.Add(Proceso("parametros-legales", "Parámetros legales de nómina", Modulos.Nomina, "/nomina/parametros-legales",
            "Los valores de ley con los que liquida la nómina —salario mínimo, auxilio de transporte, UVT, porcentajes de salud, pensión, ARL, parafiscales y provisiones, tablas de retención y de fondo de solidaridad— con la fecha desde la que rigen. El programa no trae ninguno fijo: si falta uno para la fecha del período, el cálculo se niega y lo nombra.",
            [
                P("Nómina → Parámetros legales", "La grilla muestra cada código con su vigencia actual y las anteriores. Arriba avisa si algún código requerido no tiene vigencia para el año en curso o el siguiente.", "/nomina/parametros-legales", "Abrir Parámetros legales"),
                P("Nueva vigencia", "En la fila del código, «Nueva vigencia»: fecha desde, el valor (o la tabla de tramos: desde, hasta, tarifa, fijo) y la fuente normativa (decreto o resolución). La vigencia anterior se cierra el día antes."),
                P("Cuándo hacerlo", "Al empezar el año, cuando el Gobierno fija salario mínimo, auxilio y UVT, y cuando cambie una tarifa. El primer período del año no se puede calcular hasta registrarlos."),
                P("Comprobar", "Calculá el período en borrador: cada línea de la liquidación muestra el parámetro y la vigencia que usó."),
            ],
            ["parametros legales", "salario minimo", "smmlv", "auxilio de transporte", "uvt", "retencion", "tabla", "fondo de solidaridad", "vigencia", "porcentaje"],
            ["Cooperativa activa.", "Permiso Payroll.LegalParameters.Manage para registrar vigencias."],
            ["liquidacion-de-nomina", "conceptos-de-nomina"], [], TipoDeTema.Proceso));
        t.Add(Maestro("/nomina/eps", "EPS", Modulos.Nomina, "una EPS", null, "eps", "salud", "afiliacion"));
        t.Add(Maestro("/nomina/arl", "ARL", Modulos.Nomina, "una ARL", null, "arl", "riesgos laborales"));
        t.Add(Maestro("/nomina/arl-tarifas", "Tarifas ARL", Modulos.Nomina, "una tarifa ARL", "Por clase de riesgo.", "arl", "tarifa", "clase de riesgo"));
        t.Add(Maestro("/nomina/pensiones", "Fondos de pensiones", Modulos.Nomina, "un fondo de pensiones", null, "pensiones", "fondo", "afiliacion"));
        t.Add(Maestro("/nomina/cesantias", "Fondos de cesantías", Modulos.Nomina, "un fondo de cesantías", null, "cesantias", "fondo"));
        t.Add(Maestro("/nomina/parametros-retencion", "Parámetros de retención", Modulos.Nomina, "un tramo de la tabla de retención en la fuente propia de un plan de nómina", "Tramos en UVT y tarifa marginal por plan; si el plan tiene tramos, la liquidación los usa en lugar de la tabla legal RETEFTE_TABLA_UVT.", "retencion", "salarios", "uvt", "tabla", "plan"));
        t.Add(Maestro("/nomina/causas-retencion", "Causas de retención", Modulos.Nomina, "una causa de retención", null, "causas", "retencion"));
        t.Add(Maestro("/nomina/cuentas-concepto", "Cuentas por concepto", Modulos.Nomina, "una cuenta por concepto", "Contra qué cuentas contabiliza cada concepto de nómina.", "cuentas", "concepto", "contabilizacion"));
        t.Add(Maestro("/nomina/parametros-autoaportes", "Parámetros de autoliquidación de aportes", Modulos.Nomina, "un parámetro de autoliquidación", "Porcentajes de salud, pensión, ARL y parafiscales (PILA).", "pila", "autoliquidacion", "aportes", "parafiscales"));

        // Feature 010: parametrización de prestaciones (políticas, festivos, saldos iniciales).
        t.Add(Proceso("politicas-de-nomina", "Políticas de nómina de la empresa", Modulos.Nomina, "/nomina/politicas",
            "Las decisiones de la cooperativa con fecha desde la que rigen: qué días cuentan como hábiles (lunes a sábado o a viernes), si goza de la exoneración del art. 114-1, si las vacaciones se pagan por anticipado, cómo se controlan los topes de retención, qué propone la definitiva como descuento de Cartera y la fecha de arranque de la nómina. No son valores legales —esos van en Parámetros legales—: son lo que la contadora decidió, con motivo e historial.",
            [
                P("Nómina → Políticas de la empresa", "Cada clave con su valor vigente a la fecha elegida, qué decide y de dónde sale el valor (vigencia registrada o defecto del catálogo).", "/nomina/politicas", "Abrir Políticas"),
                P("Nueva vigencia", "En la fila, «Nueva vigencia»: el valor nuevo (entre los admitidos), desde cuándo rige, hasta cuándo si se sabe, y el motivo. La vigencia anterior se cierra el día antes si se cruza."),
                P("Cuidado con la retroactividad", "Una vigencia anterior a una nómina ya aprobada se registra con aviso: esas nóminas no se recalculan. La exoneración 114-1 se bloquea si hay corridas aprobadas desde esa fecha, porque cambia aportes contabilizados: reversalas primero."),
                P("Comprobar", "El historial de la clave muestra cada vigencia con su motivo, quién la registró y cuándo. Los borradores de liquidación quedan desactualizados para que se recalculen con el valor nuevo."),
            ],
            ["politicas", "semana laboral", "exoneracion", "114-1", "vacaciones anticipadas", "arranque", "topes", "descuento al retiro", "vigencia", "motivo"],
            ["Cooperativa activa.", "Permiso Payroll.CompanyPolicies.Manage para registrar vigencias; View para consultar."],
            ["parametros-legales", "festivos", "saldos-iniciales-de-prestaciones"], [], TipoDeTema.Proceso));
        t.Add(Proceso("festivos", "Calendario de festivos", Modulos.Nomina, "/nomina/festivos",
            "Los festivos que descuenta el contador de días hábiles de las vacaciones. Los de la Ley 51 de 1983 (fijos, trasladados al lunes y los que dependen de Pascua) vienen sembrados para 2026–2028 y no se retiran; un puente decretado o un día propio de la cooperativa se agrega aquí y queda quién lo hizo.",
            [
                P("Nómina → Festivos", "Elegí el año: la lista muestra cada festivo con su origen (Ley 51, decretado, manual).", "/nomina/festivos", "Abrir Festivos"),
                P("Agregar uno", "«Nuevo festivo»: fecha, nombre y origen (decretado por el Gobierno o manual). Una fecha que ya es festivo se rechaza."),
                P("Retirar uno", "Sólo los decretados y manuales tienen papelera; uno de la Ley 51 responde que viene de la semilla."),
                P("Comprobar", "Al registrar unas vacaciones que cubran la fecha, la vista previa de días hábiles lo muestra entre los saltados con su nombre."),
            ],
            ["festivos", "ley 51", "puente", "dias habiles", "calendario", "vacaciones"],
            ["Cooperativa activa.", "Permiso Payroll.Holidays.Manage para agregar y retirar; View para consultar."],
            ["politicas-de-nomina", "empleados"], [], TipoDeTema.Proceso));
        t.Add(Proceso("saldos-iniciales-de-prestaciones", "Saldos iniciales de prestaciones", Modulos.Nomina, "/nomina/saldos-iniciales",
            "Lo que cada empleado traía causado antes de que la nómina corriera en esta plataforma: días hábiles de vacaciones pendientes, cesantías e intereses causados del año y prima causada del semestre, a la fecha de arranque. Sin esto, la primera prima y las cesantías del primer año salen cortas para quien ingresó antes. Es digitación auditada, no migración.",
            [
                P("Nómina → Saldos iniciales", "Un renglón por empleado vivo. El filtro «Sólo faltantes» deja a quien ingresó antes del arranque y no tiene saldo: son los que hay que digitar antes de la primera liquidación.", "/nomina/saldos-iniciales", "Abrir Saldos iniciales"),
                P("Digitar o editar", "El lápiz abre el diálogo: fecha de corte (la del arranque, 30-11-2026 en COOFLOPAL), los cuatro valores y, si se sabe, los días ya contados para que la proporción no los duplique. Se reemplaza libremente mientras ninguna liquidación aprobada lo haya consumido."),
                P("Ajustar uno consumido", "Cuando una prima o unas cesantías aprobadas ya lo usaron, el saldo no se reemplaza: «Ajustar con motivo» crea una fila nueva con el saldo completo corregido y la razón; la liquidación siguiente toma esa. Reversar la liquidación también libera el saldo."),
                P("Comprobar", "En la ficha del empleado aparece el saldo vigente, y la liquidación lo explica como primer tramo: «Saldo inicial al … digitado por … el …»."),
            ],
            ["saldos iniciales", "prestaciones", "vacaciones pendientes", "cesantias causadas", "prima causada", "arranque", "ajuste", "consumido"],
            ["Cooperativa activa.", "Permiso Payroll.BenefitBalances.Manage para digitar y ajustar; View para consultar."],
            ["politicas-de-nomina", "empleados", "liquidacion-de-nomina"], [], TipoDeTema.Proceso));

        // Feature 010 N1: las cuatro liquidaciones especiales. Cada una es una corrida más (Kind) y
        // comparte con la ordinaria el detalle por empleado con explicación, la relación de pago, los
        // comprobantes y la exportación; el manual cuenta el ciclo una vez por pantalla y lo propio de cada una.
        t.Add(Proceso("prima-de-servicios", "Prima de servicios", Modulos.Nomina, "/nomina/prima",
            "La prima del semestre para toda la empresa en una sola liquidación: quince días de salario por semestre trabajado, proporcional a los días vinculados, sobre el promedio del salario y el auxilio de transporte. Se calcula en borrador, se revisa empleado por empleado con la explicación de cada valor, se aprueba contra la provisión acumulada con el corte del semestre como fecha del comprobante y se paga con su propia relación de pago. Quien queda fuera (salario integral, aprendiz en etapa lectiva, pasante, prima ya pagada en la definitiva) aparece aparte con la razón.",
            [
                P("Nómina → Liquidaciones → Prima de servicios", "Elegí el año: la lista muestra cada semestre con sus versiones y su estado (borrador, desactualizado, aprobado, reversado, descartado). Clic en una fila carga la corrida abajo.", "/nomina/prima", "Abrir Prima de servicios"),
                P("Nueva liquidación", "Año y semestre (enero–junio corta al 30-06; julio–diciembre al 31-12). Hay una por empresa y semestre: si ya existe un borrador, recalculalo; si está aprobada, reversala antes de liquidar de nuevo."),
                P("Revisar", "Pestaña Empleados: días del semestre, base, prima y neto por persona; clic abre el detalle con cada línea y su explicación (tramos de salario, saldo inicial si lo hubo, parámetro y vigencia). Los bloqueos (neto negativo, afiliación faltante, concepto sin cuentas) se listan arriba: corregí la causa y recalculá."),
                P("Excluidos", "Pestaña Excluidos: quién no entró y por qué. No es un error: es la regla de cada clase de empleado. Si alguien falta por un dato de la ficha (clase, fecha de ingreso), corregilo y recalculá."),
                P("Aprobar", "Botón «Aprobar», sólo con permiso Payroll.ServiceBonus.Approve y sin bloqueos. La fecha del comprobante es por defecto el corte del semestre —para causar contra la provisión en el mes correcto— y sólo puede estar entre el corte y hoy. Quien calculó no aprueba, salvo que la política de la empresa lo permita con segunda confirmación. Sale el comprobante NM: cancela la provisión acumulada y lleva la diferencia al gasto."),
                P("Relación de pago", "Con la prima aprobada: cada empleado con su neto, banco y cuenta. «Marcar pagados» deja constancia con fecha propia, medio y referencia; la marca bloquea la reversión mientras esté vigente. Los comprobantes PDF por empleado llevan la etiqueta «Prima de servicios 2026-II»."),
                P("Exportar", "El detalle línea a línea sale a Excel, PDF o Word desde el botón «Exportar» y desde Reportes de nómina (vistas «Liquidación especial: resumen» y «detalle»); cada exportación queda en auditoría."),
                P("Reversar", "Sobre una prima aprobada y con Payroll.ServiceBonus.Reverse: pide motivo, genera el asiento espejo fechado hoy y deja la corrida marcada como reversada. El saldo inicial que consumió vuelve a ser editable y el semestre se puede liquidar de nuevo. Nada se borra."),
                P("Descartar", "Un borrador que no sirve se descarta con motivo: queda en el historial como descartado y el semestre vuelve a estar libre."),
            ],
            ["prima", "prima de servicios", "semestre", "provision", "liquidacion especial", "excluidos", "salario integral", "aprendiz", "pasante", "relacion de pago", "reversar", "descartar"],
            ["Cooperativa activa.", "Conceptos de prima con cuentas contables, parámetros legales del año y período contable del corte abierto.", "Saldos iniciales digitados para quien ingresó antes del arranque.", "Permiso Payroll.ServiceBonus.Calculate para calcular y descartar; Approve para aprobar; Reverse para reversar; View para consultar; Payroll.Payments.Mark para marcar pagos."],
            ["cesantias-e-intereses", "vacaciones", "liquidacion-definitiva", "saldos-iniciales-de-prestaciones", "liquidacion-de-nomina"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cesantias-e-intereses", "Cesantías e intereses del año", Modulos.Nomina, "/nomina/cesantias-anuales",
            "Las cesantías del año —un mes de salario por año trabajado, proporcional— y sus intereses (12 % anual sobre lo causado) para toda la empresa, a 31 de diciembre o a otro corte del año. Las cesantías no se pagan al empleado: quedan como cuenta por pagar a cada fondo y se consignan antes del 14 de febrero, fondo por fondo; los intereses sí se pagan al empleado antes del 31 de enero con su propia relación de pago. La pantalla lleva las dos cosas: la relación de consignación por fondo, con quién ya consignó y con qué referencia, y la relación de pago de los intereses.",
            [
                P("Nómina → Liquidaciones → Cesantías e intereses", "Elegí el año: la lista trae cada versión con su estado, el total de cesantías, el de intereses y cuántos fondos van consignados. Clic en una fila la carga.", "/nomina/cesantias-anuales", "Abrir Cesantías e intereses"),
                P("Nueva liquidación", "Año y fecha de corte (31-12 por defecto). Una por empresa y año. El cálculo deja los excluidos con su razón (salario integral, aprendiz en etapa lectiva, pasante, retirado con definitiva aprobada, sin días en el año) y los avisos, por ejemplo quién ingresó antes del arranque y no tiene saldo inicial."),
                P("Revisar", "Pestaña Empleados: días del año, base, cesantías e intereses por persona, con el detalle y la explicación línea a línea. Los bloqueos se listan arriba y hay que resolverlos antes de aprobar."),
                P("Aprobar", "Botón «Aprobar» con Payroll.Severance.Approve: fecha del comprobante (por defecto el corte, entre el corte y hoy) y fecha de pago de los intereses. Sale el comprobante NM: cancela las provisiones acumuladas, deja la cuenta por pagar a cada fondo de cesantías y la de intereses a los empleados."),
                P("Consignación por fondo", "Pestaña Consignación: un bloque por fondo con sus empleados, la base, los días y el valor; el total por fondo y el general, y la fecha límite legal como aviso. Quien tenga la ficha sin fondo aparece en un bloque aparte para corregirla antes. El botón de Excel baja la relación con una hoja por fondo; también PDF y Word."),
                P("Marcar consignado", "En cada fondo, «Marcar consignado» (Payroll.Severance.MarkDeposited) pide la fecha y la referencia del pago: queda con tu nombre y en auditoría. El pago al fondo se hace fuera del sistema; aquí sólo se deja constancia, fondo por fondo."),
                P("Intereses al empleado", "Pestaña Relación de pago: cada empleado con sus intereses, banco y cuenta. «Marcar pagados» con fecha, medio y referencia. El comprobante PDF lleva la etiqueta «Intereses a las cesantías 2026»."),
                P("Exportar", "Desde Reportes de nómina: «Consignación de cesantías» (por corrida, y opcionalmente un solo fondo) y las vistas de liquidación especial, en Excel, PDF y Word; cada exportación queda en auditoría."),
                P("Reversar", "Sobre una liquidación aprobada, con Payroll.Severance.Reverse y motivo: asiento espejo, corrida marcada como reversada, saldo inicial liberado. Si hay intereses marcados como pagados o fondos marcados como consignados, el sistema lo dice: retirá las marcas antes."),
                P("Descartar", "Un borrador se descarta con motivo y el año vuelve a estar libre para calcular."),
            ],
            ["cesantias", "intereses a las cesantias", "fondo de cesantias", "consignacion", "14 de febrero", "31 de enero", "cuenta por pagar", "liquidacion especial", "excluidos", "marcar consignado", "reversar"],
            ["Cooperativa activa.", "Fondo de cesantías en la ficha de cada empleado; conceptos con cuentas contables; parámetros legales del año; período contable del corte abierto.", "Saldos iniciales digitados para quien ingresó antes del arranque.", "Permiso Payroll.Severance.Calculate para calcular y descartar; Approve para aprobar; MarkDeposited para marcar la consignación; Reverse para reversar; View para consultar; Payroll.Payments.Mark para marcar el pago de los intereses."],
            ["prima-de-servicios", "vacaciones", "liquidacion-definitiva", "saldos-iniciales-de-prestaciones", "nomina-cesantias"], [], TipoDeTema.Proceso));

        t.Add(Proceso("vacaciones", "Vacaciones", Modulos.Nomina, "/nomina/vacaciones",
            "El saldo de vacaciones de cada empleado no se guarda: se deriva de los días causados (quince hábiles por año, proporcionales a los días trabajados menos suspensiones), más el saldo inicial, menos lo disfrutado, compensado y pagado al retiro, más o menos los ajustes con motivo. Desde aquí se registra el disfrute o la compensación en dinero, con vista previa obligatoria de los días hábiles según la semana laboral de la empresa y el calendario de festivos, y cada registro nace con su liquidación: los días se pagan por anticipado sobre el salario ordinario y la nómina ordinaria de los períodos cubiertos recibe una novedad de ausencia que no paga esos días (decisión D-01, configurable en Políticas).",
            [
                P("Nómina → Liquidaciones → Vacaciones", "Pestaña Saldos por empleado, a la fecha elegida: causado, inicial, disfrutado, compensado, ajustes y pendiente. Clic abre la ficha con la explicación paso a paso y los movimientos.", "/nomina/vacaciones", "Abrir Vacaciones"),
                P("Registrar disfrute", "«Registrar»: empleado, fechas desde y hasta. «Ver días hábiles» es obligatorio antes de guardar: muestra los hábiles, los de calendario y cada día saltado (domingos, sábados si la semana es de lunes a viernes, festivos con su nombre), con la semana laboral que se usó. Lo que ves ahí es lo que queda congelado en el movimiento. Al guardar se calcula la liquidación en borrador."),
                P("Compensación en dinero", "La misma acción con «Compensación»: los días a pagar sin disfrutar, hasta el máximo compensable que la pantalla muestra (la mitad de lo causado, por la ley). Un exceso se rechaza con el máximo permitido."),
                P("Sobre un período aprobado", "Un disfrute cuyas fechas caen en un período de nómina ya aprobado no se registra en silencio: el sistema avisa y pide aceptar el retroactivo; la novedad de ausencia va entonces al período abierto siguiente con referencia al original."),
                P("Ajustes", "«Ajustar» suma o resta días con motivo (una conciliación con el sistema anterior, un error de digitación). Queda como movimiento con tu nombre y el saldo se recalcula al instante."),
                P("Aprobar", "Pestaña Liquidaciones: la corrida en borrador con el detalle y la explicación. «Aprobar» (Payroll.Vacations.Approve) pide la fecha del comprobante —por defecto el fin del disfrute— y saca el comprobante NM contra la provisión de vacaciones; el movimiento pasa a liquidado y la novedad de ausencia queda registrada en la nómina ordinaria de los períodos cubiertos."),
                P("Relación de pago y comprobante", "La liquidación aprobada tiene su relación de pago con la fecha de pago propia y el comprobante PDF del empleado con la etiqueta «Vacaciones». Se marca pagada como cualquier corrida."),
                P("Exportar", "Reportes de nómina: «Saldos de vacaciones» a una fecha y «Movimientos de vacaciones» entre fechas (opcionalmente de un empleado), en Excel, PDF y Word; y las vistas de liquidación especial por corrida."),
                P("Anular y reversar", "Un movimiento registrado se anula con motivo y las fechas quedan libres; si tenía un borrador, se descarta con él. Una liquidación aprobada se reversa (Payroll.Vacations.Reverse) con asiento espejo: el movimiento vuelve a registrado y las novedades de ausencia de los períodos aún abiertos se anulan. Si hay pago marcado, retiralo antes."),
            ],
            ["vacaciones", "dias habiles", "festivos", "semana laboral", "disfrute", "compensacion", "saldo de vacaciones", "ausencia", "anticipado", "retroactivo", "ajuste", "liquidacion especial"],
            ["Cooperativa activa.", "Semana laboral y política de pago anticipado en Políticas de la empresa; festivos del año en el calendario.", "Saldos iniciales digitados para quien ingresó antes del arranque.", "Permiso Payroll.Vacations.Register para registrar disfrutes, compensaciones y ajustes; Calculate para recalcular y descartar; Approve para aprobar; Reverse para reversar; View para consultar."],
            ["politicas-de-nomina", "festivos", "saldos-iniciales-de-prestaciones", "prima-de-servicios", "liquidacion-definitiva", "novedades-de-nomina"], [], TipoDeTema.Proceso));

        t.Add(Proceso("liquidacion-definitiva", "Liquidación definitiva por retiro", Modulos.Nomina, "/nomina/liquidacion-definitiva",
            "Cuando un contrato termina: se registra la terminación con la fecha y un motivo del catálogo (renuncia, despido con o sin justa causa, vencimiento del término, mutuo acuerdo, fin de obra, período de prueba, muerte, pensión) y nace en el mismo paso la liquidación definitiva en borrador: salario pendiente, cesantías e intereses del año, prima del semestre, vacaciones pendientes en dinero, indemnización si el motivo la genera, y las deducciones de ley. Cartera propone los descuentos por créditos y libranzas del empleado; se pueden bajar con motivo, nunca subir. Aprobarla contabiliza, aplica los descuentos en Cartera, cierra la ficha del empleado y deja el documento para firma.",
            [
                P("Nómina → Liquidaciones → Liquidación definitiva", "Lista de terminaciones por año y estado (registrada, liquidada, reintegrado, anulada) con la versión y el estado de su corrida. Clic abre la definitiva; desde la ficha del empleado, «Terminar contrato» llega aquí con el empleado elegido.", "/nomina/liquidacion-definitiva", "Abrir Liquidación definitiva"),
                P("Registrar la terminación", "«Registrar terminación»: empleado, fecha de retiro, motivo del catálogo, tipo de contrato DIAN y, si es a término fijo u obra, la fecha de fin pactada (la indemnización la usa). Notas opcionales. Se crea la terminación y la corrida en borrador en un solo paso; la ficha sigue vigente hasta aprobar."),
                P("Revisar", "Rubros con su valor y la explicación paso a paso; lo que no aplica se lista con el porqué (por ejemplo, prima ya pagada en el semestre). Los avisos del cálculo —saldo inicial faltante, provisión no informada— no bloquean, pero conviene resolverlos y recalcular."),
                P("Descuentos de Cartera", "Tabla con cada obligación: saldo, cuotas pendientes, lo propuesto y lo aplicado. Con Payroll.Settlements.AdjustDeduction se puede bajar lo aplicado escribiendo el motivo; subirlo no se permite. El neto después de descuentos se ve al pie y un descuento mayor al neto se señala."),
                P("Sanción moratoria", "«Sanción si pagara hoy» es informativo: un día de salario por cada día de retraso desde el retiro, con el tope legal. Nunca entra como línea; sirve para no dejar la definitiva sin pagar."),
                P("Aprobar", "Botón «Aprobar» con Payroll.Settlements.Approve: fecha del comprobante (por defecto la fecha de retiro) y confirmación. Sale el comprobante NM, se aplican los pagos en Cartera con lo descontado, la ficha del empleado queda retirada y se genera el documento de liquidación para firma. Un reingreso posterior es una ficha nueva."),
                P("Documento para firma", "«Documento» descarga el PDF con los rubros, las deducciones y los descuentos para la firma del empleado; queda como adjunto de la terminación."),
                P("Relación de pago y exportar", "La definitiva tiene su relación de pago y su comprobante PDF con la etiqueta «Liquidación definitiva». Reportes de nómina: «Terminaciones» entre fechas y las vistas de liquidación especial por corrida."),
                P("Reversar y descartar", "Reversar una definitiva aprobada (Payroll.Settlements.Reverse) pide motivo: asiento espejo, los pagos aplicados en Cartera se reversan, la ficha vuelve a estar vigente (reintegro) y la terminación queda registrada para corregir. Descartar el borrador anula la terminación sin rastro contable; la ficha nunca dejó de estar vigente."),
                P("Motivos de retiro", "Con Payroll.Settlements.Manage, «Motivos de retiro» administra el catálogo: código, nombre, si genera indemnización, si exige fecha de fin de contrato y la base legal. Los sembrados no se retiran; se pueden desactivar los propios."),
            ],
            ["liquidacion definitiva", "retiro", "terminacion", "despido", "renuncia", "indemnizacion", "justa causa", "descuento", "cartera", "libranza", "sancion moratoria", "documento para firma", "motivos de retiro", "reintegro"],
            ["Cooperativa activa.", "Ficha del empleado con fecha de ingreso, salario, afiliaciones y tipo de contrato; conceptos con cuentas contables; parámetros legales; período contable de la fecha de retiro abierto.", "Permiso Payroll.Settlements.Calculate para registrar la terminación, recalcular y descartar; Approve para aprobar; Reverse para reversar; AdjustDeduction para bajar descuentos; Manage para el catálogo de motivos; View para consultar."],
            ["empleados", "prima-de-servicios", "cesantias-e-intereses", "vacaciones", "cartera-de-creditos", "descuento-de-nomina"], ["/nomina/liquidacion-definitiva/{RunId}"], TipoDeTema.Proceso));

        t.Add(Proceso("planilla-pila", "Planilla PILA (aportes a seguridad social)", Modulos.Nomina, "/nomina/pila",
            "Generar cada mes el archivo plano de la Resolución 2388 de 2016 con los aportes a pensión, salud, riesgos laborales, caja, SENA e ICBF de todos los cotizantes, validarlo antes con la lista de inconsistencias del operador, cuadrarlo contra la nómina y cargarlo en Aportes en Línea (planilla E).",
            [
                P("Antes de la primera vez", "En Nómina › EPS, Pensiones, ARL y Cajas cada administradora necesita su «Código PILA» (el del listado del operador, distinto del código de la cooperativa); en la ficha de cada empleado, DIVIPOLA y actividad económica si difieren de la sede; y en «Datos del aportante» (Payroll.Pila.Manage) la clase de aportante, el código PILA de la ARL, el código del operador y la ubicación de la sede."),
                P("Validar", "Elija el año y el mes y «Validar»: lista lo que el operador devolvería como Error (bloqueante: sin EPS, sin código PILA, documento más largo que la Res. 1529/2026, sin DIVIPOLA…) o Alerta (segundo apellido faltante, régimen de transición desconocido), cada una con el enlace a la ficha o al catálogo donde se corrige. No guarda nada.", "/nomina/pila", "Abrir Planilla PILA"),
                P("Generar", "Con las bloqueantes en cero y las alertas reconocidas, «Generar» arma el archivo con las nóminas aprobadas del mes (más vacaciones y definitivas con corte en el mes): una línea por cotizante y una adicional por incapacidad, licencia, vacaciones o suspensión; IBC al peso, aportes al múltiplo de 100, exoneración del art. 114-1 según la política de la empresa. Regenerar crea una versión nueva y deja la anterior como reemplazada, con su archivo."),
                P("Cuadre", "Antes de descargar se muestra el cuadre por subsistema contra los aportes que la nómina liquidó (comprobantes NM). Con diferencia, la descarga la exige reconocida; el fondo de solidaridad lo liquida el operador y una diferencia allí es alerta."),
                P("Cargar y marcar", "Descargue el .txt, cárguelo en Aportes en Línea (Liquidaciones › Adicionar › Cargar archivo › Validar) y, con el número de planilla, «Marcar cargada» (Payroll.Pila.MarkUploaded). Ese período ya no se regenera: una corrección se digita en el operador (planilla N)."),
                P("Cada línea explicada", "En el detalle, seleccione una línea para ver sus 98 campos con el valor, el origen y de qué salió cada uno (línea de la corrida, parámetro con su vigencia, política). Reportes: líneas, cuadre e inconsistencias en Excel y PDF."),
            ],
            ["pila", "planilla", "aportes", "seguridad social", "aportes en linea", "resolucion 2388", "eps", "pension", "arl", "caja", "sena", "icbf", "fondo de solidaridad"],
            ["Nóminas del mes aprobadas.", "Códigos PILA en EPS, Pensiones, ARL y Cajas.", "Datos del aportante completos.", "Parámetros legales vigentes al primer día del mes."],
            ["liquidacion-de-nomina", "empleados", "retencion-procedimiento-2"], [], TipoDeTema.Proceso));

        t.Add(Proceso("retencion-procedimiento-2", "Retención en la fuente: porcentaje fijo (procedimiento 2)", Modulos.Nomina, "/nomina/retencion-procedimiento-2",
            "Calcular en junio y en diciembre el porcentaje fijo de retención de los empleados en procedimiento 2 (ET art. 386) con los doce meses anteriores, aprobarlo como vigencia nueva en la ficha y dejar que la nómina siguiente lo aplique.",
            [
                P("Quién entra", "Los empleados activos con «Procedimiento 2» en Retención y plan de la ficha. El semestre 1 se calcula en junio y rige julio–diciembre; el 2 en diciembre y rige enero–junio del año siguiente."),
                P("Calcular", "«Calcular» (Payroll.WithholdingRate.Calculate) suma los pagos gravables de los doce meses anteriores de las nóminas aprobadas (la prima entra; cesantías e intereses no), resta los aportes obligatorios reales, depura con las deducciones y rentas exentas declaradas en la secuencia de la política P2SecuenciaDepuracion, divide por RETEFTE_P2_DIVISOR (o por los meses de vinculación si son menos de doce) y lleva el promedio a la tabla vigente (o a la del plan). Quien no tiene historia queda en «sin cálculo» con el motivo.", "/nomina/retencion-procedimiento-2", "Abrir Procedimiento 2"),
                P("Revisar", "Seleccione un cálculo para ver el mes a mes con sus corridas, la depuración paso a paso, el divisor y el tramo de la tabla. Exportable a Excel y PDF para la contadora."),
                P("Aprobar", "Por ítem o en lote (Payroll.WithholdingRate.Approve): cierra la vigencia anterior de la ficha la víspera del semestre y abre la nueva con origen «calculado»; la quincena siguiente ya la aplica. Recalcular crea una versión nueva; un cálculo se puede rechazar con motivo."),
            ],
            ["retencion", "procedimiento 2", "porcentaje fijo", "articulo 386", "semestre", "uvt", "tabla 383"],
            ["Empleados en procedimiento 2 con nóminas aprobadas.", "Parámetros RETEFTE_P2_DIVISOR, UVT y tabla vigentes al mes del cálculo."],
            ["empleados", "liquidacion-de-nomina", "planilla-pila"], [], TipoDeTema.Proceso));

        t.Add(Proceso("dispersion-bancaria", "Dispersión bancaria (archivo de pagos)", Modulos.Nomina, "/nomina/dispersion",
            "Pagar la nómina, la prima, los intereses de cesantías, las vacaciones o una definitiva por archivo plano: el programa arma el archivo con el formato del banco desde la relación de pago de la liquidación aprobada, se carga en el portal del banco y, al confirmar el envío, todos los del archivo quedan pagados en una sola acción.",
            [
                P("Antes de la primera vez", "En Maestros › Bancos cada banco destino necesita su «Código de transferencia (ACH)»; en Contabilidad › Plan de cuentas la cuenta del banco pagador lleva banco y número de cuenta; y en Maestros › Formatos bancarios debe existir un formato vigente (CSV-GENERICO viene sembrado y sirve para cualquier banco; el del banco propio se carga con la estructura que publique)."),
                P("Generar", "En la pestaña «Relación de pago» de la liquidación aprobada (o en la barra de la definitiva y de la liquidación de vacaciones), «Archivo de dispersión» (Payroll.Disbursement.Generate): cuenta origen, formato (el vigente del banco de la cuenta, o el genérico), fecha de pago y referencia. La vista previa muestra las primeras líneas y quién quedaría en pendientes con el motivo; recién entonces se genera.", "/nomina/liquidacion", "Abrir Liquidación"),
                P("Pendientes", "Quien no tiene banco, cuenta o código ACH, ya está pagado o ya va en otro archivo no anulado queda fuera con el motivo; se corrige la ficha y se genera otro archivo con ellos, o se marcan pagados a mano en la relación de pago."),
                P("Nómina → Liquidaciones → Dispersión bancaria", "Los archivos por año y estado (generado, enviado, anulado): descarga del archivo tal cual se generó (con su huella SHA-256), líneas, pendientes y el texto exacto de cada registro; exportación a Excel y PDF.", "/nomina/dispersion", "Abrir Dispersión"),
                P("Marcar enviado", "Cuando el banco recibe el archivo, «Marcar enviado» (Payroll.Disbursement.MarkSent) con la fecha, la referencia del banco y la fecha de pago: todos los del archivo quedan pagados por transferencia en una sola acción, con la misma referencia, y la liquidación ya no se reversa. Quien ya tenía marca se deja como estaba y se avisa."),
                P("Anular", "Un archivo generado que no se envió se anula con motivo y sus empleados vuelven a poder ir a otro archivo; uno enviado no se anula: cada marca de pago se retira una a una desde la relación de pago."),
                P("Formatos por banco", "Maestros › Formatos bancarios (Core.BankFileFormats.Manage): el layout campo a campo (origen, largo, alineación, relleno, mapa de equivalencias) o pegado como JSON, con vigencia; un formato que ya generó archivos no cambia de estructura: se cierra su vigencia y se crea la versión nueva. El mismo motor sirve a la consignación de cesantías por fondo y, cuando llegue, a tesorería.", "/maestros/formatos-bancarios", "Abrir Formatos bancarios"),
            ],
            ["dispersion", "archivo plano", "banco", "pago de nomina", "transferencia", "ach", "formato bancario", "marcar enviado", "av villas", "csv"],
            ["Liquidación aprobada con relación de pago.", "Empleados con banco de dispersión, tipo y número de cuenta en la ficha.", "Bancos con código de transferencia (ACH).", "Cuenta bancaria del plan con banco y número de cuenta.", "Formato vigente del banco pagador o el genérico."],
            ["liquidacion-de-nomina", "prima-de-servicios", "cesantias-e-intereses", "vacaciones", "liquidacion-definitiva", "formatos-bancarios"], [], TipoDeTema.Proceso));

        t.Add(Proceso("formatos-bancarios", "Formatos de archivo por banco", Modulos.Maestros, "/maestros/formatos-bancarios",
            "El layout del archivo plano de cada banco como dato con vigencia: cabecera, detalle y totales campo a campo, o pegado como JSON tal como lo entrega el banco. Ligado al banco y compartido por los módulos que pagan por archivo (nómina hoy; tesorería y contabilidad después).",
            [
                P("Maestros → Formatos bancarios", "Lista por ámbito (dispersión de nómina, consignación de cesantías, pagos a proveedores) con banco, vigencia, campos, archivos generados y estado.", "/maestros/formatos-bancarios", "Abrir Formatos bancarios"),
                P("Nuevo formato o copia", "«Nuevo formato», o «Copiar como formato nuevo» desde uno existente (CSV-GENERICO es la plantilla): código, nombre, banco (vacío = genérico), ámbito, vigencia, ancho fijo o delimitado, codificación, fin de línea, formato de montos, plantilla del nombre del archivo y convenio."),
                P("Campos", "Por registro (cabecera, detalle, totales): nombre, origen (NIT de la empresa, cuenta origen, fecha de pago, documento y cuenta del beneficiario, valor, totales…), valor constante, largo, alineación, relleno, tipo, formato de fecha, mapa de equivalencias («1=S;2=D» traduce el valor del ERP al del banco), requerido y si se recorta. El catálogo de orígenes dice en qué registro va cada uno."),
                P("JSON y vista previa", "La pestaña JSON muestra la definición completa y acepta una pegada; la vista previa escribe las primeras líneas con una liquidación aprobada real sin guardar nada."),
                P("Vigencia y versiones", "Un formato que ya generó archivos no cambia de estructura (sólo nombre, fin de vigencia, notas y estado): se cierra su vigencia y se crea la versión nueva; los archivos anteriores conservan la suya. Dos formatos activos del mismo banco y ámbito no se cruzan en el tiempo."),
            ],
            ["formato bancario", "archivo plano", "layout", "ancho fijo", "delimitado", "csv", "banco", "dispersion", "json"],
            ["Permiso Core.BankFileFormats.Manage.", "La estructura publicada por el banco."],
            ["dispersion-bancaria", "cesantias-e-intereses"], [], TipoDeTema.Proceso));

        // ---------------------------------------------------------------- Tesorería --
        t.Add(Proceso("cheques", "Cheques", Modulos.Tesoreria, "/tesoreria/cheques",
            "Girar cheques contra una cuenta bancaria, entregarlos, anularlos y seguir su cobro para la conciliación.",
            [
                P("Tesorería → Cheques", "Por cuenta bancaria y estado: girado, entregado, cobrado, anulado.", "/tesoreria/cheques", "Abrir Cheques"),
                P("Nuevo Cheque", "Cuenta, beneficiario (del registro de personas), valor, fecha y concepto. El número sale de la chequera registrada."),
                P("Entregar y anular", "Entregado cambia el estado; anulado pide motivo y deja el número inutilizable. Cobrado se marca desde la conciliación bancaria."),
            ],
            ["cheque", "girar", "anular", "chequera", "banco", "beneficiario"], ["Cooperativa activa.", "Cuenta bancaria y chequera registradas."],
            ["facturas-de-tesoreria", "maestros-bancos"], [], TipoDeTema.Proceso));

        t.Add(Proceso("facturas-de-tesoreria", "Facturas por pagar y por cobrar", Modulos.Tesoreria, "/tesoreria/facturas",
            "Registrar las obligaciones con proveedores y las cuentas por cobrar a terceros, y aplicarles pagos.",
            [
                P("Tesorería → Facturas CxP/CxC", null, "/tesoreria/facturas", "Abrir Facturas"),
                P("Nuevo", "Tercero, tipo (por pagar o por cobrar), número de factura del tercero, fecha, vencimiento, valor e impuestos. Contabiliza la causación."),
                P("Pagar o cobrar", "Desde la fila: valor, forma de pago y fecha. Parcial o total; el saldo se sigue en la misma pantalla."),
            ],
            ["facturas", "proveedores", "cuentas por pagar", "cuentas por cobrar", "cxp", "cxc", "vencimiento"], ["Cooperativa activa."],
            ["cheques", "tesoreria-flujo-caja"], [], TipoDeTema.Proceso));

        t.Add(Consulta("/tesoreria/flujo-caja", "Flujo de caja", Modulos.Tesoreria,
            "Entradas y salidas de efectivo reales y proyectadas por período, a partir de vencimientos de cartera, facturas y nómina.",
            "Rango de fechas y cuentas bancarias.", "flujo de caja", "efectivo", "proyeccion", "liquidez"));
        t.Add(Maestro("/tesoreria/conceptos", "Conceptos de tesorería", Modulos.Tesoreria, "un concepto de tesorería", null, "conceptos", "tesoreria", "ingreso", "egreso"));

        // ------------------------------------------------------------ Tarjeta Débito --
        t.Add(Proceso("tarjetas-debito", "Tarjetas débito", Modulos.Debito, "/debito/tarjetas",
            "Emitir tarjetas débito ligadas a una cuenta de ahorro, activarlas, bloquearlas y ver sus movimientos.",
            [
                P("Tarjeta Débito → Tarjetas", null, "/debito/tarjetas", "Abrir Tarjetas"),
                P("Nuevo", "Asociado, cuenta de ahorro asociada y convenio (la red). El número lo asigna el convenio."),
                P("Estados", "Emitida, activa, bloqueada, cancelada. Bloquear es reversible; cancelar no."),
                P("Movimientos", "Compras, retiros y comisiones que llegan del convenio, por tarjeta y período.", "/debito/movimientos-tarjeta", "Abrir Movimientos de Tarjeta"),
            ],
            ["tarjeta", "debito", "bloquear", "activar", "convenio", "cajero"], ["Cooperativa activa.", "Convenio configurado."],
            ["cuentas-de-ahorro", "debito-parametros-convenio"], ["/debito/tarjetas/{CardId}"], TipoDeTema.Proceso));
        t.Add(Consulta("/debito/movimientos-tarjeta", "Movimientos de tarjeta débito", Modulos.Debito,
            "Compras, retiros y comisiones por tarjeta, con su estado de conciliación contra el convenio.",
            "Tarjeta o asociado y rango de fechas.", "movimientos", "tarjeta", "compras", "retiros"));
        t.Add(Maestro("/debito/parametros-convenio", "Parámetros de convenio", Modulos.Debito, "un convenio", "La red de tarjetas: comisiones, cuentas y archivos de intercambio.", "convenio", "red", "comisiones"));
        t.Add(Maestro("/debito/datafonos", "Datáfonos", Modulos.Debito, "un datáfono", null, "datafono", "terminal"));
        t.Add(Maestro("/debito/parametros-diarios", "Parámetros diarios", Modulos.Debito, "un parámetro diario", "Topes de retiro y compra por día.", "topes", "limite diario", "retiro"));

        // ------------------------------------------------------------ Maestros core --
        t.Add(Maestro("/maestros/agencias", "Agencias", Modulos.Maestros, "una agencia", "Las oficinas para efectos de cartera y asociados; las sucursales administrativas van en Administración.", "agencias", "oficinas"));
        t.Add(Maestro("/maestros/paises", "Países", Modulos.Maestros, "un país", null, "paises"));
        t.Add(Maestro("/maestros/departamentos", "Departamentos", Modulos.Maestros, "un departamento", null, "departamentos", "division politica"));
        t.Add(Maestro("/maestros/ciudades", "Ciudades", Modulos.Maestros, "una ciudad", "Con código DANE para los reportes.", "ciudades", "municipios", "dane"));
        t.Add(Maestro("/maestros/bancos", "Bancos", Modulos.Maestros, "un banco", "Y las cuentas bancarias de la cooperativa, que usan cheques y conciliación.", "bancos", "cuenta bancaria"));
        t.Add(Maestro("/maestros/empresas", "Empresas", Modulos.Maestros, "una empresa", "Las pagadurías de los asociados para descuento por nómina.", "empresas", "pagaduria", "descuento de nomina"));
        t.Add(Maestro("/maestros/centros-costo", "Centros de costo", Modulos.Maestros, "un centro de costo", null, "centros de costo", "presupuesto"));
        t.Add(Maestro("/maestros/secciones", "Secciones", Modulos.Maestros, "una sección", null, "secciones", "areas"));
        t.Add(Maestro("/maestros/profesiones", "Profesiones", Modulos.Maestros, "una profesión", null, "profesiones", "ocupacion"));
        t.Add(Maestro("/maestros/cargos", "Cargos", Modulos.Maestros, "un cargo", null, "cargos", "puesto"));
        t.Add(Maestro("/maestros/parentescos", "Parentescos", Modulos.Maestros, "un parentesco", null, "parentescos", "beneficiarios"));
        t.Add(Maestro("/maestros/motivos-retiro", "Motivos de retiro", Modulos.Maestros, "un motivo de retiro", null, "motivos", "retiro"));
        t.Add(Maestro("/maestros/enfermedades", "Enfermedades", Modulos.Maestros, "una enfermedad", "Para los registros de salud de asociados y beneficiarios.", "enfermedades", "salud"));
        t.Add(Maestro("/maestros/entidades", "Entidades externas", Modulos.Maestros, "una entidad", "Supersolidaria, Fogacoop, centrales de riesgo, aseguradoras.", "entidades", "supersolidaria", "fogacoop", "centrales de riesgo"));
        t.Add(Maestro("/maestros/comites", "Comités", Modulos.Maestros, "un comité", "Comité de crédito, de educación, de solidaridad, y sus integrantes.", "comites", "junta", "integrantes"));
        t.Add(Maestro("/maestros/deportes", "Deportes", Modulos.Maestros, "un deporte", null, "deportes", "bienestar"));
        t.Add(Maestro("/maestros/actividades-culturales", "Actividades culturales", Modulos.Maestros, "una actividad cultural", null, "actividades", "cultura", "bienestar"));
        t.Add(Maestro("/maestros/convenios", "Convenios", Modulos.Maestros, "un convenio", "Acuerdos con empresas y entidades: descuentos, servicios, recaudo.", "convenios", "acuerdos"));

        // -------------------------------------------------------------- Cumplimiento --
        t.Add(Proceso("habeas-data", "Habeas data: política y consentimientos", Modulos.Cumplimiento, "/compliance/habeas-data/policies",
            "Publicar la política de tratamiento de datos y registrar, por persona, la aceptación o la revocación. Sin consentimiento vigente no se deberían tratar sus datos.",
            [
                P("Cumplimiento → Política de habeas data", "Muestra la versión vigente y las anteriores. Una versión publicada no se edita: se publica otra.", "/compliance/habeas-data/policies", "Abrir la política"),
                P("Publicar una versión nueva", "Texto completo y fecha de vigencia. Al publicarla, las personas deberán aceptarla de nuevo.", "/compliance/habeas-data/policies/publicar", "Abrir Publicar política"),
                P("Registrar el consentimiento de una persona", "Desde su ficha en Personas, o desde Registrar consentimiento: fecha, medio (firma física, correo, portal) y evidencia.", null, null),
                P("Revocar", "Registrar revocación deja constancia de la fecha y el medio. El historial por persona muestra todas las aceptaciones y revocaciones."),
            ],
            ["habeas data", "proteccion de datos", "consentimiento", "politica", "revocar", "ley 1581", "autorizacion"], ["Cooperativa activa.", "Permiso de cumplimiento."],
            ["personas"], ["/compliance/habeas-data/policies/publicar", "/compliance/habeas-data/consents/aceptar/{PersonId}", "/compliance/habeas-data/consents/revocar/{PersonId}", "/compliance/habeas-data/persons/{PersonId}/historial"], TipoDeTema.Proceso));

        // ------------------------------------------------------------- Notificaciones --
        t.Add(Consulta("/notificaciones", "Centro de notificaciones", Modulos.Notificaciones,
            "Los avisos que el sistema generó para vos: aprobaciones pendientes, procesos terminados, alertas. Se marcan como leídos; los correos salen aparte.",
            "Leídas o no leídas, y por tipo.", "notificaciones", "avisos", "alertas", "pendientes"));

        // ----------------------------------------------------------------- Reportes --
        t.Add(Proceso("centro-de-reportes", "Centro de reportes", Modulos.Reportes, "/reportes",
            "Todos los reportes del sistema en un solo lugar, agrupados por módulo. Cada tarjeta lleva a la pantalla que lo genera; los mismos reportes están también dentro de cada módulo del menú, bajo «Reportes».",
            [
                P("SISTEMA → Centro de Reportes", null, "/reportes", "Abrir el Centro de Reportes"),
                P("Elegir el reporte", "«Generar» abre su pantalla. Todos piden parámetros (fecha de corte o período, y filtros) y muestran el resultado en pantalla."),
                P("Exportar o imprimir", "Cada reporte ofrece PDF cuando corresponde; para el resto, la impresión del navegador (Ctrl+P) respeta el diseño."),
                P("Qué hay", "Contabilidad: balance general, estado de resultados, libro mayor, balance de prueba, certificados de retención. Cartera: extracto de crédito, cartera por edades, calificación, extractos, CDT. Nómina: comprobante de pago, liquidación. El inventario heredado se retiró (feature 012); sus informes vuelven con el módulo nuevo."),
            ],
            ["reportes", "informes", "imprimir", "pdf", "exportar", "centro"], ["Cooperativa activa."], [], [], TipoDeTema.Consulta));

        // Los informes contables (balance general, estado de resultados, libro mayor) viven en el módulo
        // de Contabilidad desde la feature 009 E2 («Estados financieros» e «Informes contables», arriba);
        // las rutas /reportes/balance-general, /reportes/estado-resultados y /reportes/libro-mayor nunca
        // tuvieron página.
        t.Add(Reporte("/reportes/extracto-credito", "Extracto de crédito", Modulos.Reportes, "Estado de cuenta de un crédito para el asociado: cuotas pagadas, pendientes, intereses y saldo.", "Asociado y crédito, y período.", "extracto", "credito", "estado de cuenta"));
        t.Add(Reporte("/reportes/cartera-edades", "Cartera por edades", Modulos.Reportes, "La cartera agrupada por tramos de días de mora: base de la provisión y del seguimiento de cobro.", "Fecha de corte, línea y agencia.", "cartera por edades", "mora", "tramos", "provision"));
        t.Add(Reporte("/reportes/nomina", "Reportes de nómina", Modulos.Reportes, "Doce vistas en una pantalla. Sobre la nómina ordinaria: comprobante por empleado, resumen de la corrida por concepto, detalle empleado × concepto, novedades del período e histórico por empleado entre fechas. Sobre las liquidaciones especiales (feature 010): resumen y detalle de una prima, cesantías, vacaciones o definitiva elegida por tipo y corrida; consignación de cesantías por fondo (Excel con una hoja por fondo); saldos de vacaciones a una fecha; movimientos de vacaciones entre fechas; terminaciones entre fechas; y saldos iniciales de prestaciones vigentes a una fecha. Todas se exportan a Excel, PDF y Word y cada exportación queda en auditoría.", "Período (y su corrida), empleado y rango de fechas, o tipo de liquidación y corrida, según la vista.", "comprobante", "desprendible", "nomina", "pago", "prima", "cesantias", "consignacion", "vacaciones", "terminaciones", "saldos iniciales", "excel", "word", "pdf", "reporte"));

        return t;
    }

    // ===================================================================== //
    //  Ayudantes                                                             //
    // ===================================================================== //

    private static PasoDeManual P(string titulo, string? detalle = null, string? ruta = null, string? etiqueta = null) =>
        new(titulo, detalle, ruta, etiqueta);

    private static TemaDeManual Proceso(
        string slug, string titulo, string modulo, string ruta, string resumen,
        PasoDeManual[] pasos, string[] claves, string[] requisitos, string[] relacionados,
        string[] rutasCubiertas, TipoDeTema tipo) =>
        new(slug, titulo, modulo, ruta, resumen, pasos, claves, requisitos, relacionados, rutasCubiertas, tipo);

    private static string SlugDeRuta(string ruta) =>
        ruta.Trim('/').Replace('/', '-').ToLowerInvariant();

    /// <summary>
    /// La guía general de los maestros. Todos se manejan igual y por eso se escribe
    /// una vez; lo particular de cada uno (qué campos pide) lo muestra la pantalla.
    /// </summary>
    private static TemaDeManual Maestro(string ruta, string titulo, string modulo, string singular, string? nota, params string[] claves)
    {
        var resumen = $"Catálogo de {titulo.ToLowerInvariant()}: se consulta, se crea, se corrige y se retira {singular}. Es la guía general de los maestros del sistema; los campos concretos y sus validaciones los muestra la propia pantalla.";
        if (!string.IsNullOrWhiteSpace(nota)) resumen += " " + nota;

        return new TemaDeManual(SlugDeRuta(ruta), titulo, modulo, ruta, resumen,
            [
                P("Abrir la pantalla", $"En el menú lateral, dentro de «{modulo}», elegí «{titulo}». O usá el botón de arriba.", ruta, "Abrir " + titulo),
                P("Ubicar el registro", "Si la pantalla tiene un cuadro «Buscar», escribí parte del nombre o del código y la lista se filtra. Buscá antes de crear: los maestros no admiten duplicados y el sistema lo dirá al guardar."),
                P($"Crear {singular}", "Botón «Nuevo». Se abre un formulario; los campos marcados con asterisco son obligatorios. Al terminar, «Guardar»; «Cancelar» cierra sin cambios."),
                P("Corregir uno existente", "En la fila, el ícono del lápiz abre el mismo formulario con los datos cargados. Guardá al terminar. Los códigos que ya se usaron en movimientos no se cambian."),
                P("Retirar uno", "En la fila, el ícono de la papelera pide confirmación. Lo que ya se usó en otros registros no se borra: queda inactivo y deja de ofrecerse, pero el historial lo conserva."),
                P("Comprobar", "El cambio se ve en la lista de inmediato. Si no aparece, revisá el filtro de búsqueda o «Refrescar»."),
            ],
            [.. claves, "maestro", "catalogo", "crear", "editar", "eliminar", titulo.ToLowerInvariant()],
            ["Tener una cooperativa activa en la sesión.", "Permiso del módulo; sin él la opción no aparece en el menú o la pantalla responde «sin permiso»."],
            ["parametros-del-sistema"], [], TipoDeTema.Maestro);
    }

    private static TemaDeManual Reporte(string ruta, string titulo, string modulo, string resumen, string parametros, params string[] claves) =>
        new(SlugDeRuta(ruta), titulo, modulo, ruta, resumen,
            [
                P("Abrir el reporte", "Desde el Centro de Reportes o desde el menú del módulo, bajo «Reportes».", ruta, "Abrir " + titulo),
                P("Elegir los parámetros", parametros + " Un reporte a una fecha de corte muestra saldos; uno por período muestra movimientos."),
                P("Generar", "El resultado aparece en pantalla. Si tarda, es por el volumen del período elegido: acotalo."),
                P("Leer con criterio", "Un reporte contable sólo es confiable si el período está contabilizado y cuadrado: revisá el balance de prueba ante cualquier duda.", "/contabilidad/informes?vista=trial-balance", "Abrir Balance de Prueba"),
                P("Exportar o imprimir", "PDF cuando la pantalla lo ofrece; si no, la impresión del navegador (Ctrl+P)."),
            ],
            [.. claves, "reporte", "informe", "imprimir", "pdf", titulo.ToLowerInvariant()],
            ["Cooperativa activa.", "Permiso de consulta del módulo."],
            ["centro-de-reportes"], [], TipoDeTema.Reporte);

    private static TemaDeManual Consulta(string ruta, string titulo, string modulo, string resumen, string filtros, params string[] claves) =>
        new(SlugDeRuta(ruta), titulo, modulo, ruta, resumen,
            [
                P("Abrir la pantalla", null, ruta, "Abrir " + titulo),
                P("Filtrar", filtros + " Cuanto más acotado, más rápido responde."),
                P("Leer el detalle", "Al abrir una fila se ve el registro completo y, cuando aplica, el enlace al documento que lo originó."),
                P("Desde aquí no se registra nada", "Es una vista. Lo que haya que corregir se corrige en la pantalla del proceso que lo produjo."),
            ],
            [.. claves, "consulta", "ver", titulo.ToLowerInvariant()],
            ["Cooperativa activa.", "Permiso de consulta del módulo."],
            [], [], TipoDeTema.Consulta);
}
