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
        public const string Cartera = "Cartera";
        public const string Captaciones = "Captaciones y aportes";
        public const string Inventario = "Inventario";
        public const string Compras = "Compras";
        public const string Ventas = "Ventas";
        public const string Nomina = "Nómina";
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
        Modulos.Asociados, Modulos.Maestros, Modulos.Contabilidad, Modulos.Cartera, Modulos.Captaciones,
        Modulos.Inventario, Modulos.Compras, Modulos.Ventas, Modulos.Nomina, Modulos.Tesoreria,
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
    /// <c>/cartera/ahorros/{id}</c> en «Cuentas de ahorro».
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

        // Feature 013, fase 2 (T0095): la plataforma que pide Cartera —perfil regulatorio, canales externos y participantes de la integración—.
        t.Add(Proceso("admin-perfil-regulatorio", "Perfil regulatorio de la cooperativa", Modulos.Administracion, "/admin/perfil-regulatorio",
            "El tipo de organización, la actividad financiera, el nivel de supervisión, la categoría del fondo de empleados, el grupo NIIF y la inscripción como operador de libranza, por vigencias. De él dependen el modelo de deterioro, los plazos del SICSES y lo que exige Cartera.",
            [
                P("Administración → Perfil regulatorio", "Muestra la versión vigente en fichas y el historial de versiones con su fuente, motivo y soporte. Si el grupo NIIF no coincide con el de la configuración contable, la pantalla lo avisa. Sin perfil, registre la primera versión.", "/admin/perfil-regulatorio", "Abrir el Perfil regulatorio"),
                P("Nueva versión", "Tipo de organización (de la lista que llega con los valores nacionales vigentes), categoría del fondo si es fondo de empleados, actividad financiera autorizada, nivel de supervisión, grupo NIIF (o el de la configuración contable), inscripción como operadora de libranza con su número, fecha desde, fuente (resolución, certificado o acta) y motivo. Rige desde la fecha indicada; la anterior queda en el historial."),
                P("Corregir", "Una versión no se edita: «Corregir» registra una corrección que la reemplaza, con su motivo, y queda marcada como corrección en el historial."),
            ],
            ["perfil regulatorio", "tipo de organizacion", "supervision", "nivel de supervision", "grupo niif", "fondo de empleados", "operador de libranza", "actividad financiera", "supersolidaria"],
            ["Valores nacionales replicados en la cooperativa (tipos de organización).", "Permiso Core.RegulatoryProfile.View; para registrar o corregir, Core.RegulatoryProfile.Manage."],
            ["cartera-valores-nacionales", "cartera-perdida-esperada", "cartera-sicses-y-centrales", "contabilidad-indicadores-regulatorios"], [], TipoDeTema.Maestro));

        t.Add(Proceso("admin-canales-externos", "Canales externos", Modulos.Administracion, "/admin/canales-externos",
            "Qué proveedor atiende cada clase de canal —mensajes de texto, WhatsApp, depósito de valores, centrales de riesgo y listas restrictivas— desde una fecha, y la prueba de su credencial. Hoy SMS y WhatsApp no tienen proveedor: van por el canal simulado, que no tiene validez legal.",
            [
                P("Administración → Canales externos", "Lista por clase con central, canal, vigencia, credencial, última verificación y estado. Mientras una clase use el canal simulado, la pantalla avisa que lo que se envíe o consulte por ella es de prueba. Sin canales, los mensajes y consultas externas no salen.", "/admin/canales-externos", "Abrir Canales externos"),
                P("Nueva vigencia", "Clase, la central si es de riesgo, y el canal: «Simulado (sin validez legal)», «Carga propia de listas» (sólo para listas restrictivas) u «Otro proveedor…» con su código y la clave de la credencial. Fecha desde y motivo. La vigencia anterior de la misma clase se cierra la víspera; lo ya hecho conserva su canal."),
                P("La credencial", "No se escribe aquí: soporte la carga en el secreto del ambiente y la pantalla sólo la nombra. Después de guardar, «Verificar» prueba el canal vigente y muestra el resultado."),
            ],
            ["canales externos", "sms", "whatsapp", "centrales de riesgo", "listas restrictivas", "deposito de valores", "proveedor", "simulado", "credencial", "verificar"],
            ["Permiso Core.ExternalChannels.View; para crear vigencias y verificar, Core.ExternalChannels.Manage."],
            ["cartera-mensajes", "cartera-sicses-y-centrales", "cumplimiento-listas-restrictivas"], [], TipoDeTema.Maestro));

        t.Add(Proceso("admin-participantes-de-integracion", "Participantes de la integración", Modulos.Administracion, "/admin/participantes-de-integracion",
            "Quién participa en la integración entre módulos: los destinos que reciben los mensajes (Contabilidad, Cartera, Comercial, Tesorería, Recaudo) y los recaudadores de obligaciones, con las versiones que adoptan y si cuentan hoy. Un destino que no cuenta no recibe nada: sus mensajes esperan.",
            [
                P("Administración → Participantes de la integración", "Lista con código, clase, papel, destino, si está habilitado, vigencia, versiones adoptadas y si cuenta hoy (y por qué no). «Ver también los retirados» muestra la historia.", "/admin/participantes-de-integracion", "Abrir Participantes"),
                P("Registrar participante", "Código, clase (Recaudo, Nómina, Aportes, Tesorería, Contabilidad, Cartera, Comercial o doble de prueba), papel (recaudador de obligaciones o destino de mensajes, con su destino), habilitado, si es un doble de prueba, versiones adoptadas, fecha desde y motivo. Si ya hay una vigencia del mismo participante, papel y destino, se cierra la víspera."),
                P("Retirar", "Con fecha y motivo: deja de contar desde el día siguiente y sus mensajes pendientes esperan. Nada se borra."),
                P("Lo que no se hace aquí", "Cartera nace registrada e inhabilitada: la habilita su activación, no esta pantalla. Los dobles de prueba (recaudador y tesorería de prueba) sólo cuentan fuera de producción."),
            ],
            ["participantes", "integracion", "destino de mensajes", "recaudador", "recaudo", "tesoreria", "doble de prueba", "habilitar", "retirar", "versiones"],
            ["Permiso Core.IntegrationParticipants.View; para registrar o retirar, Core.IntegrationParticipants.Manage."],
            ["cartera-dobles-de-prueba", "cartera-activacion", "cartera-bandeja-de-mensajes", "inventario-bandeja-de-mensajes"], [], TipoDeTema.Maestro));

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
                P("Datos de vinculación", "Fecha de ingreso, agencia, tipo de asociado, el grupo del asociado (opcional: la clasificación libre de la cooperativa, que reemplazó al antiguo centro de costo del asociado) y la información socioeconómica que pida la cooperativa."),
                // Sin botón: la pantalla de beneficiarios no existe todavía (tampoco tiene enlace en el menú).
                P("Beneficiarios", "Quiénes reciben en caso de fallecimiento, con parentesco y porcentaje; deben sumar cien."),
                P("Guardar", "El asociado queda activo y puede abrir productos (ahorros, créditos, aportes)."),
            ],
            ["asociado", "afiliar", "vincular", "ingreso", "beneficiarios", "socio"], ["Cooperativa activa.", "La persona en el registro único."],
            ["personas", "maestros-grupos-del-asociado", "captaciones-aportes"], [], TipoDeTema.Proceso));

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
                P("Operaciones de Inventario pendientes", "Si Inventario tiene operaciones del mes que Contabilidad aún no recibe, cerrar lo avisa con cuántas hay por estado y la más antigua: «Procesar ahora» ordena el lote del mes (Accounting.InventoryBatches.Run) y «Cerrar de todos modos» cierra igual y queda en la auditoría (lo pendiente se rechazará por período cerrado y se recupera reabriendo el mes)."),
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
                P("Contabilidad → Saldos de apertura", "«Plantilla Excel» descarga la hoja con los encabezados; el contador la llena desde el sistema anterior con una fila por auxiliar (y por tercero y documento donde la cuenta lo exige): importes sin miles y con hasta dos decimales.", "/contabilidad/apertura", "Abrir Saldos de apertura"),
                P("Elegí la fecha", "Es el corte de tus saldos anteriores. Se propone la víspera del primer período y podés moverla hasta el fin del primer ejercicio; lo único que no se admite es un mes ya cerrado. Cualquiera sea, la apertura es saldo inicial y nunca movimiento del mes."),
                P("Importar", "Cada fila se valida con las reglas de su cuenta. Si una falla, la pantalla muestra fila, columna y problema, y no se guarda nada: corregí el archivo y volvé a importar. Sin errores queda un borrador; no tiene que cuadrar para importarse."),
                P("Corregir mientras sea borrador", "«Editar» abre el comprobante y ahí agregás, cambiás o quitás cuentas línea a línea; «Cambiar la fecha» la mueve sin tocar las líneas; «Descartar» lo elimina; y volver a importar reemplaza todas las líneas por las del archivo nuevo."),
                P("Contabilizar", "Revisá el borrador y contabilizalo (cuatro ojos si la empresa lo exige). Queda como la única apertura vigente: para cargar otra, reversá ésta primero. Desde ahí ya no se edita. En el balance de prueba la apertura es saldo inicial, no movimiento del mes."),
            ],
            ["apertura", "saldos iniciales", "saldos de apertura", "migración", "sistema anterior", "plantilla", "importar saldos", "fecha de corte"], ["Cooperativa activa.", "Permiso Accounting.Opening.Manage."],
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

        // ------------------------------------------------------ Cartera: puesta en marcha --
        // Feature 013, US8 (T0761): la carga de la cartera vigente por partes, su cuadre contra Contabilidad y la activación con el acta.
        // El procedimiento completo, con lo que debe existir antes y las verificaciones, está en docs/operaciones/cartera-puesta-en-marcha.md.
        t.Add(Proceso("cartera-migracion", "Migración de la cartera vigente", Modulos.Cartera, "/cartera/migracion",
            "Cargar la cartera que la cooperativa ya tiene, con fecha de corte, en 17 partes; revisar cada archivo, aplicar la carga, cuadrarla contra Contabilidad, conciliar las ventas a crédito y aceptar las diferencias con un acta.",
            [
                P("Cartera → Migración", "Lista de cargas con su estado: borrador, aplicada, reemplazada o activada. «Carga nueva» pide la fecha de corte (el último día que la cartera anterior manejó).", "/cartera/migracion", "Abrir Migración"),
                P("Cargar las partes", "Por cada parte, descargá su plantilla (encabezados en la fila 1), diligenciala y subila: primero se revisa sin guardar nada (errores con fila y columna, y la revisión en Excel) y después se aplica. Una parte admite varios archivos; «Reemplazar la parte» deja sin efecto los anteriores. Créditos, cuotas, saldos y riesgo son obligatorias."),
                P("Aplicar la carga", "Aplicar sólo guarda la carga: valida entre partes (todo o nada), calcula el cuadre y concilia las ventas. Los créditos nacen al activar. Otra carga aplicada con el mismo corte deja la anterior reemplazada."),
                P("Cuadre, ventas y diferencias", "La pestaña «Cuadre» compara con Contabilidad por concepto, modalidad, sub-segmento, categoría y conjunto de cuentas. «Ventas» muestra las ventas a crédito hasta el corte; las que no se migraron se marcan como canceladas antes del corte o fuera de Cartera, con motivo. «Diferencias» se aceptan con el punto de una sesión cerrada, por una persona distinta de quien cargó."),
            ],
            ["migracion", "carga", "corte", "cartera vigente", "cuadre", "conciliacion de ventas", "diferencias", "acta", "puesta en marcha"],
            ["Contabilidad iniciada con su apertura al corte.", "Personas, líneas de crédito y oficinas registradas.", "Permiso Lending.Migration.Load (y Lending.Migration.AcceptDifferences para aceptar)."],
            ["cartera-activacion", "cartera-plantillas", "captaciones-migracion"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-activacion", "Activación de Cartera", Modulos.Cartera, "/cartera/activacion",
            "Activar Cartera el día siguiente al corte: revisar los requisitos y avisos, los cobros provisionales y el acta, y hacer nacer en una sola operación los créditos y todo lo del deudor que trae la carga.",
            [
                P("Cartera → Activación", "Lista de requisitos con su estado y quién arregla cada uno, con el enlace a su pantalla. Se exigen igual en todos los ambientes; fuera de producción el recaudador y la tesorería pueden ser los dobles de prueba.", "/cartera/activacion", "Abrir Activación"),
                P("Avisos", "Lo que Comercial ajusta el mismo día (fecha de habilitación, política sin respuesta, quién registra la cuenta por cobrar) y las cuentas del concepto de descuento de Cartera en Nómina, que hay que reconocer."),
                P("Cobros provisionales", "Si después del corte se recaudaron ventas a crédito, descargá la plantilla, subila y revisala: queda guardada para la activación, que vuelve a validarla."),
                P("Activar", "La fecha es la del día siguiente al corte, dentro del plazo y con el mes contable abierto. Elegí el punto del acta que aprueba la activación y confirmá. Si algo falla no queda nada: se corrige y se vuelve a activar."),
            ],
            ["activacion", "activar cartera", "requisitos", "cobros provisionales", "acta", "puesta en marcha", "fecha de corte"],
            ["Una carga aplicada con las diferencias aceptadas.", "Permiso Lending.Activation.Execute."],
            ["cartera-migracion", "cartera-dobles-de-prueba", "admin-participantes-de-integracion", "captaciones-activacion"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: documentos al deudor --
        // Feature 013, US13 (T0793): el plan de pagos, el paz y salvo y el certificado de saldo con su código de verificación; K5 (T0976) suma el
        // extracto, el certificado tributario y la comparación del PDF. El detalle, en docs/manual/cartera-documentos-al-deudor.md.
        t.Add(Proceso("cartera-documentos", "Documentos al deudor y su verificación", Modulos.Cartera, "/cartera/documentos",
            "Emitir en un paso el plan de pagos, el paz y salvo de una obligación o general, el certificado de saldo, el extracto de un período y el certificado tributario de un año, con número, código de verificación y QR; reimprimir el mismo PDF, enviarlo por correo, anularlo, verificar un código y comparar un PDF con el emitido.",
            [
                P("Cartera → Documentos", "Lista de documentos emitidos por crédito o deudor, clase, estado y fecha. El estado se calcula: vigente, sujeto a firmeza hasta la fecha que dice o anulado. Desde el expediente del crédito, el botón «Documentos» abre esta pantalla con el crédito elegido.", "/cartera/documentos", "Abrir Documentos"),
                P("Emitir", "Elegí la clase, el crédito y el titular (deudor o codeudor), la fecha de corte si es un certificado de saldo, el período si es un extracto o el año si es un certificado tributario, y si se descarga o se envía al correo del titular. Se confirma una vez. El paz y salvo se niega si queda saldo o si hay una venta a crédito en reproceso, y dice cuáles."),
                P("Extracto y certificado tributario", "El extracto trae el saldo inicial, las operaciones del período por fecha valor (la causación agregada por mes) y el saldo final; hasta cinco años y no después de hoy. El certificado tributario trae, por crédito, los intereses pagados con fecha valor en el año, netos de los pagos reversados, y el saldo al 31 de diciembre; sólo de años terminados, con los créditos de vivienda aparte."),
                P("Reimprimir, enviar y anular", "Desde el detalle: «Descargar PDF» baja el mismo archivo que se emitió (idéntico); «Enviar por correo» lo pone en la cola de mensajes al deudor; «Anular» pide motivo. Reversar el pago o el desembolso que respaldaba un documento lo anula solo."),
                P("Verificar un código", "Cartera → Verificar documento, o el QR del PDF con sesión iniciada: dice si el documento es auténtico, su estado y sus datos esenciales. «Comparar con un PDF…» dice si el archivo que tenés es idéntico al emitido: el navegador calcula su huella y el archivo no se sube. Sólo verifican usuarios de la cooperativa.", "/cartera/verificar", "Abrir Verificar documento"),
            ],
            ["documentos", "paz y salvo", "plan de pagos", "certificado de saldo", "codigo de verificacion", "qr", "verificar", "reimprimir", "anular documento", "firmeza",
                "extracto", "certificado tributario", "intereses pagados", "declaracion de renta", "comparar pdf", "huella"],
            ["Cartera activada.", "Firmante configurado en los parámetros (Documentos.Firmante.Nombre y .Cargo).", "Permisos Lending.Documents.Read, .Issue, .Reprint, .Void y .Verify según la acción."],
            ["cartera-activacion"], ["/cartera/verificar"], TipoDeTema.Proceso));
        // Feature 013, US15 (T0800): el centro de informes de Cartera.
        t.Add(Consulta("/cartera/informes", "Informes de cartera", Modulos.Cartera,
            "Cartera por edades, colocación, recaudo, movimientos de un crédito, usura, mensajes al deudor, conciliación de la migración y las vistas de gestión; y, para Captaciones y aportes, saldos, cuentas por estado, aportes por asociado e intereses y retención. Todo exportable a Excel, PDF y Word.",
            "Elegí el informe, el rango o la fecha de corte, la sucursal y la línea (sólo las de su alcance traen filas) y los filtros propios del informe.",
            "cartera por edades", "colocacion", "recaudo", "movimientos", "usura", "mensajes al deudor", "conciliacion de la migracion",
            "calidad", "cosechas", "concentracion", "documentos emitidos"));
        // Feature 013, US15, K5 (T0986): las vistas de gestión de K5 —calidad, cosechas, concentración, documentos emitidos— y el tablero. Las
        // definiciones (a validar por la contadora), los permisos y la auditoría de las exportaciones, en docs/manual/cartera-informes-y-tablero.md.
        t.Add(Proceso("cartera-informes-y-tablero", "Indicadores de gestión y tablero de cartera", Modulos.Cartera, "/cartera/tablero",
            "Seguir la cartera de un vistazo en el tablero (saldo, colocación, calidad, deterioro, recaudo frente al esperado, cosechas, promesas, mensajes y alertas) y profundizar en los informes de calidad, cosechas, concentración y documentos emitidos, siempre dentro de las oficinas y líneas de su alcance.",
            [
                P("Cartera → Tablero de cartera", "Cada ficha muestra la cifra al corte y la del mes anterior; un clic lleva al informe o la pantalla que la explica. Elegí la oficina y la línea (sólo las de su alcance) y la fecha de corte.", "/cartera/tablero", "Abrir el tablero"),
                P("Calidad de la cartera", "Informe «Calidad de la cartera»: cartera bruta, vencida, en riesgo (calificadas B a E), deterioro y cobertura del último cierre del mes, en total o por oficina, línea o modalidad. Vencida es lo que tiene más días de mora que el parámetro «Días de cartera vencida».", "/cartera/informes?vista=quality", "Abrir calidad"),
                P("Cosechas", "Informe «Cosechas»: por mes de desembolso, la mora a 30, 60 y 90 días en cada mes de maduración, sobre lo desembolsado. Sale de los cierres de cada mes.", "/cartera/informes?vista=vintages", "Abrir cosechas"),
                P("Concentración", "Informe «Concentración»: los mayores deudores y grupos conectados con su porcentaje del límite individual y la señal cuando están cerca o lo superan; también por línea, sector y pagaduría. Por deudor y por grupo exige permiso para ver datos de los deudores.", "/cartera/informes?vista=concentration", "Abrir concentración"),
                P("Documentos emitidos y exportar", "Informe «Documentos emitidos»: los documentos al deudor del rango con su estado a hoy. Todo informe se exporta a Excel, PDF o Word con el permiso de exportar (y el de datos personales si trae deudores); cada exportación queda en la auditoría."),
            ],
            ["tablero", "indicadores", "calidad de la cartera", "cartera vencida", "cartera en riesgo", "cobertura", "deterioro", "cosechas", "concentracion",
                "limite individual", "grupo conectado", "documentos emitidos", "recaudo frente al esperado"],
            ["Cartera activada y al menos un cierre del mes para la calidad, el deterioro y las cosechas.", "Patrimonio técnico vigente en Contabilidad para el porcentaje del límite individual.",
                "Permisos Lending.Dashboard.View para el tablero, Lending.Reports.View para los informes, Lending.Loans.Read para los que traen deudores y Lending.Reports.Export (más Lending.Reports.ExportPersonalData) para exportar."],
            ["cartera-documentos"], [], TipoDeTema.Proceso));
        // Feature 013, US16, K5 (T1010): el formato de cartera del SICSES y los archivos para las centrales de riesgo. El cotejo de los layouts, los
        // casos dorados y qué hacer con los excluidos, en docs/operaciones/cartera-sicses-y-centrales.md.
        t.Add(Proceso("cartera-sicses-y-centrales", "Formato SICSES y archivos para centrales de riesgo", Modulos.Cartera, "/cartera/sicses",
            "Validar y generar el formato de cartera de créditos del SICSES de un mes cerrado y el archivo de novedades de cada central de riesgo, descargarlos, cargarlos en el portal y dejar el radicado; en las centrales, ver quién queda fuera y por qué.",
            [
                P("Cartera → Formato SICSES", "Elegí el año y el mes (con su cierre mensual hecho). «Validar» no guarda nada: lista las inconsistencias por crédito y campo y la fecha límite del reporte. Esa fecha es informativa —nada se bloquea por ella— y sale de la periodicidad que fije la cooperativa en los parámetros de Cartera (meses de corte y días de plazo), descontando los festivos.", "/cartera/sicses", "Abrir Formato SICSES"),
                P("Generar", "Con inconsistencias bloqueantes queda una versión «validada» sin archivo, con lo encontrado; corregí el dato en el maestro (o reabrí y volvé a cerrar el mes) y generá de nuevo. Sin bloqueantes se piden reconocer las advertencias: el layout con campos sin cotejar y el cierre con el modelo obligatorio no disponible. Ninguna va dentro del archivo. Regenerar deja la versión anterior reemplazada, con su archivo."),
                P("Descargar y marcar cargado", "«Descargar» baja el archivo por un enlace de 60 segundos y queda en la auditoría. Después de cargarlo en el portal, «Marcar cargado» con el radicado: el período ya no se regenera y la corrección va en el siguiente."),
                P("Cartera → Centrales de riesgo", "Elegí la central y el período: el archivo lleva las novedades desde el último archivo cargado de esa central. Sólo van quienes autorizaron el reporte con evidencia y, si el dato es negativo, con el aviso previo con constancia de su episodio; los demás quedan excluidos con su motivo y la gestión que lo corrige. La mora por debajo del rango que la cooperativa define como negativo va con sus días reales y sin marca negativa.", "/cartera/centrales", "Abrir Centrales de riesgo"),
                P("Excluidos", "Informe «Excluidos de centrales»: por central y período, quién quedó fuera y qué hacer (obtener la autorización, enviar el aviso previo o registrar su constancia, esperar la reclamación al seguro o el fin de la insolvencia).", "/cartera/informes?vista=bureau-exclusions", "Abrir excluidos"),
            ],
            ["sicses", "supersolidaria", "formato de cartera", "reporte regulatorio", "centrales de riesgo", "datacredito", "transunion", "radicado", "cargar en el portal",
                "excluidos", "aviso previo", "autorizacion de reporte", "layout", "cotejar"],
            ["Cartera activada y el cierre mensual del período hecho (para el SICSES).", "Parámetro Centrales.CodigosDeSuscriptor con el código de la cooperativa en cada central.",
                "Permisos Lending.RegulatoryFiles.Read para ver y descargar, Lending.RegulatoryReports.Generate para el SICSES, Lending.CreditBureauFiles.Generate para las centrales y Lending.RegulatoryFiles.MarkUploaded para marcar cargado."],
            ["cartera-informes-y-tablero", "cartera-documentos", "cartera-avisos-previos", "cartera-autorizaciones"], ["/cartera/centrales"], TipoDeTema.Proceso));
        // Feature 013, K6a (T1040): la pérdida esperada del Anexo 2 —obligada o adoptada—, el historial de mora y cómo leer el cierre. El registro y el cotejo
        // del Anexo 2, la carga complementaria y la activación de una obligada, en docs/operaciones/cartera-perdida-esperada.md; para la contadora,
        // docs/manual/cartera-perdida-esperada.md.
        t.Add(Proceso("cartera-perdida-esperada", "Pérdida esperada (Anexo 2) y modelo de deterioro", Modulos.Cartera, "/cartera/cierres",
            "Ver con qué modelo se deteriora cada segmento, adoptar o desistir voluntariamente de la pérdida esperada con decisión del consejo, completar el historial de mora que la migración no trajo y leer la probabilidad de incumplimiento, la pérdida dado el incumplimiento y la exposición de cada crédito en el cierre.",
            [
                P("Cartera → Cierres → Modelo de deterioro", "La tabla dice, para cada segmento, si va por altura de mora (Anexo 1) o por pérdida esperada (Anexo 2) y por qué: obligada por su tipo de organización, adopción voluntaria o no obligada. Consumo con y sin libranza y comercial de persona natural son los segmentos del Anexo 2.", "/cartera/cierres", "Abrir Cierres"),
                P("Historial de mora", "«Revisar» lista los créditos vivos que no tienen los meses de mora que pide la pérdida esperada. Los meses que la migración no trajo se cargan con la plantilla «Historial de mora complementario» (crédito, mes, mora máxima y la fuente), antes o después de activar y hasta el primer cierre en pérdida esperada. Volver a cargar un mes no borra nada: manda la carga más reciente."),
                P("Adoptar o desistir (no obligadas)", "«Proponer la adopción» da el identificador de la propuesta: llevalo al acta del consejo como punto «Cambio del modelo de deterioro». Subí a la propuesta el estudio de impacto y el soporte del aviso a la Superintendencia, indicá la fecha del aviso, desde cuándo rige (no antes del aviso más la antelación de la norma; si no alcanza, la pantalla dice la fecha más temprana) y el punto del acta aprobado. El cambio rige desde el primer cierre de su fecha y nunca recalcula un cierre anterior. Una cooperativa obligada no adopta ni desiste."),
                P("Leer el cierre", "En el detalle del cierre, «Por qué» de un crédito en pérdida esperada muestra la probabilidad de incumplimiento (y la tabla que se usó), la pérdida dado el incumplimiento (y la garantía), la exposición, el puntaje y si está en incumplimiento. La categoría contable es la homologada del Anexo 2. Si un crédito no tenía el historial completo, el cierre aplicó la regla de meses faltantes y lo dice."),
            ],
            ["perdida esperada", "anexo 2", "modelo de referencia", "probabilidad de incumplimiento", "pi", "pdi", "exposicion", "historial de mora",
                "meses faltantes", "adopcion voluntaria", "desistimiento", "obligada", "obligacion sobrevenida", "deterioro", "homologacion", "circular 100"],
            ["Cartera activada; el Anexo 2 registrado en la plataforma (y, para adoptar, con la probabilidad de incumplimiento de la aplicación voluntaria).",
                "La regla de meses faltantes (Riesgo.ReglaMesesFaltantes) y, con «Fija», sus días.",
                "Permisos Lending.Ratings.Read para ver, Lending.ArrearsHistory.Import para el historial y Lending.ImpairmentModel.Manage para adoptar o desistir."],
            ["cartera-activacion", "cartera-sicses-y-centrales"], [], TipoDeTema.Proceso));
        // Feature 013, K6b (T1061): vivienda con la Ley 546 de 1999, en pesos o en UVR. El registro de la UVR día a día y del tope, en
        // docs/operaciones/valores-nacionales.md §3a; para la contadora y el jefe de cartera, docs/manual/cartera-vivienda-y-uvr.md.
        t.Add(Proceso("cartera-vivienda-y-uvr", "Vivienda (Ley 546) y créditos en UVR", Modulos.Cartera, "/cartera/lineas",
            "Crear líneas de vivienda en pesos o en UVR bajo el tope de vivienda, simular el plan en UVR, radicar con la hipoteca y la marca VIS, desembolsar con hipoteca de primer grado y leer los saldos de un crédito en UVR a la UVR del día.",
            [
                P("Cartera → Líneas → Nueva versión", "Modalidad Vivienda: tasa fija, que no supere el menor entre el tope de vivienda y la usura del día; plazo entre los límites de vivienda. «Unidad del capital» ofrece UVR sólo si el catálogo de modalidades la admite, y una línea en UVR no desembolsa por tramos. El detalle de la línea muestra el tope y los cinco límites vigentes con su fuente.", "/cartera/lineas", "Abrir Líneas"),
                P("Simular en UVR", "El simulador muestra, además del plan en pesos, el plan en UVR: monto, cuotas y saldo en UVR, y la equivalencia en pesos a la UVR del desembolso. Esa equivalencia es una proyección: cada cuota se paga a la UVR del día del pago.", "/cartera/simulador", "Abrir Simulador"),
                P("Radicar y desembolsar", "En la solicitud de vivienda marque si el inmueble es de interés social (VIS): elige el porcentaje financiable. Registre la hipoteca sobre la solicitud antes de radicar: su valor es el del inmueble. El desembolso exige una hipoteca de primer grado constituida o vigente."),
                P("Leer un crédito en UVR", "En el crédito, la liquidación y el estado de cuenta muestran la UVR del día, el capital y lo que cuesta cancelar en UVR y en pesos. A una fecha sin UVR publicada la UVR es proyectada y sólo se muestra. El libro trae la corrección monetaria de cada día."),
            ],
            ["vivienda", "ley 546", "uvr", "unidad de valor real", "tope de vivienda", "vis", "interes social", "hipoteca", "primer grado",
                "correccion monetaria", "porcentaje financiable", "cuota ingreso", "plazo de vivienda"],
            ["El tope de tasa de vivienda (TOPE_TASA_VIVIENDA) y, para créditos en UVR, la UVR de cada día registrados en la plataforma y replicados.",
                "Los cinco límites de vivienda sembrados «pendiente de validar» y confirmados por el dueño.",
                "Permisos Lending.CreditLines.Manage para la línea, Lending.Guarantees.Manage para la hipoteca y Lending.Disbursements.Execute para desembolsar."],
            ["cartera-migracion", "cartera-perdida-esperada"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: el día a día --
        // Feature 013, fase 24 (T1074): los temas que faltaban del módulo —líneas, simulador, solicitudes y actas, desembolsos, créditos y
        // liquidación, cierres, bandeja de mensajes y valores nacionales—. El detalle de cada uno, en docs/manual/cartera-*.md (índice en
        // docs/manual/cartera.md).
        t.Add(Proceso("cartera-lineas", "Líneas de crédito y usura", Modulos.Cartera, "/cartera/lineas",
            "Crear y versionar las líneas de crédito (tasa, plazos, montos, seguros, garantías, documentos, puntaje), ver la holgura frente a la usura del día y cerrar o reabrir una línea; los créditos vivos conservan la versión con la que nacieron.",
            [
                P("Cartera → Líneas", "Lista con estado, modalidad, versión vigente, tasa, holgura de usura y la marca «cerrada por usura desde …». Sólo aparecen las líneas de su alcance.", "/cartera/lineas", "Abrir Líneas"),
                P("Alta y versión nueva", "La línea nace con su primera versión; un cambio de tasa, plazo o reglas es una versión nueva con vigencia, nunca una edición: los créditos ya desembolsados siguen con la suya. La carga masiva usa la plantilla de líneas (revisar sin guardar y luego aplicar)."),
                P("Usura", "Ninguna tasa ni ninguna tasa efectiva con cargos supera la usura de su modalidad del día. Cuando llega una usura menor, la línea que queda por encima se cierra sola para créditos nuevos y avisa; se reabre con una versión que quepa."),
                P("Puntaje y equivalencias", "La pestaña «Puntaje» del detalle define el modelo de puntaje de la línea; la de equivalencias dice qué línea usa cada plazo de las ventas a crédito de Comercial."),
            ],
            ["lineas de credito", "linea", "usura", "tasa", "version de linea", "cerrar linea", "puntaje", "equivalencias", "seguros", "garantias exigidas"],
            ["La usura de cada modalidad registrada y replicada.", "Permisos Lending.CreditLines.View para consultar y Lending.CreditLines.Manage para crear, versionar, cerrar o reabrir."],
            ["cartera-simulador", "cartera-valores-nacionales", "cartera-vivienda-y-uvr", "captaciones-lineas", "cartera-atribuciones"], ["/cartera/lineas/{Id:guid}"], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-simulador", "Simulador de crédito", Modulos.Cartera, "/cartera/simulador",
            "Simular el plan de pagos de una línea —capital, interés, seguros y saldo por cuota, tasa efectiva, tasa con cargos y la explicación de cada tramo— sin registrar nada, imprimirlo o enviarlo por correo a la persona.",
            [
                P("Cartera → Simulador", "Elegí la línea, el monto, las cuotas, la periodicidad y, si la línea lo admite, la gracia o las cuotas extraordinarias. Lo que la línea no admite se señala en el campo con sus límites.", "/cartera/simulador", "Abrir Simulador"),
                P("Leer la simulación", "Una simulación que pasa la usura no es error: lo dice y explica por qué; radicar, aprobar y desembolsar sí lo bloquean. Sin usura registrada para el día no simula y avisa a quien la registra."),
                P("Imprimir o enviar", "El PDF dice «Simulación – no constituye aprobación». Enviar por correo exige la autorización de tratamiento de datos, el correo registrado y la ventana legal de contacto. Ninguna de las dos cosas crea una solicitud."),
            ],
            ["simulador", "simulacion", "plan de pagos", "cuota", "tasa efectiva", "tasa con cargos", "enviar simulacion"],
            ["Una línea vigente y la usura del día replicada.", "Permiso Lending.Simulations.Run."],
            ["cartera-lineas", "cartera-solicitudes-y-actas"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-solicitudes-y-actas", "Solicitudes, decisiones y actas", Modulos.Cartera, "/cartera/solicitudes",
            "Armar una solicitud desde una simulación, radicarla, analizarla y decidirla en el nivel que le corresponde —individual o en una sesión del órgano con su acta—, y anularla o desistirla.",
            [
                P("Cartera → Solicitudes", "Lista de solicitudes de su alcance con su estado. «Nueva solicitud» parte de una simulación y guarda el borrador con las partes, los documentos, las autorizaciones y las garantías.", "/cartera/solicitudes", "Abrir Solicitudes"),
                P("Radicar y analizar", "Radicar corre las validaciones (usura, capacidad, concentración, listas, centrales, bloqueos) y calcula el nivel de decisión por la matriz de atribuciones. El analista registra su análisis y puede volver a correr las validaciones."),
                P("Decidir", "Quien tiene el permiso del nivel decide desde el detalle o desde «Mis pendientes». Si el nivel es colegiado, la solicitud va como punto de una sesión del órgano.", "/cartera/aprobaciones", "Abrir Aprobaciones"),
                P("Sesiones y actas", "Convocar, registrar la asistencia, los puntos y los votos (por la secretaría, desde la sesión propia o en presencia con llave o código) y cerrar el acta: decide, numera y guarda el PDF. Lo cerrado no se edita; un voto mal registrado se reemplaza con motivo.", "/cartera/actas", "Abrir Actas"),
            ],
            ["solicitud", "radicar", "analisis", "decision", "atribuciones", "nivel de decision", "aprobaciones", "actas", "sesion", "votos", "quorum", "comite", "consejo"],
            ["Cartera activada para crear o radicar solicitudes.", "Permisos Lending.Applications.* según la acción, el del nivel de decisión (Lending.Decisions.*) y Lending.Sessions.* para las actas."],
            ["cartera-simulador", "cartera-desembolsos", "cartera-atribuciones", "cartera-cupos", "cartera-vinculados", "cumplimiento-listas-restrictivas"], ["/cartera/solicitudes/nueva", "/cartera/solicitudes/{Id:guid}", "/cartera/aprobaciones", "/cartera/actas"], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-desembolsos", "Desembolsos, garantías y pagaré", Modulos.Cartera, "/cartera/desembolsos",
            "Desembolsar una solicitud aprobada con sus garantías y su pagaré, recoger créditos propios, seguir la orden de giro con Tesorería, generar el archivo de desembolsos y anular un crédito pendiente de giro.",
            [
                P("Garantías y pagaré", "En el detalle de la solicitud se registran y perfeccionan las garantías y el pagaré (físico o desmaterializado). Lo que la línea exige debe estar antes de desembolsar."),
                P("Cartera → Desembolsos", "La pestaña «Desembolsar» muestra la vista previa (lo que se gira, lo que se recoge, los descuentos y lo que falta) y desembolsa: el crédito nace con sus condiciones selladas y su plan.", "/cartera/desembolsos", "Abrir Desembolsos"),
                P("Órdenes de giro y archivo", "«Órdenes de giro» sigue lo que Tesorería gira; se pide anular una orden, se ordena un giro nuevo o se anula un crédito pendiente de giro. «Archivos de desembolsos» genera el archivo plano del banco."),
            ],
            ["desembolso", "desembolsar", "garantias", "pagare", "orden de giro", "tesoreria", "archivo de desembolsos", "recogida", "tramos"],
            ["Solicitud aprobada y vigente; la usura del día replicada; el mes contable abierto.", "Permisos Lending.Disbursements.Execute (atribución con monto), Lending.Guarantees.Manage y Lending.PromissoryNotes.Manage."],
            ["cartera-solicitudes-y-actas", "cartera-creditos-y-liquidacion"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-creditos-y-liquidacion", "Créditos, estado de cuenta y liquidación", Modulos.Cartera, "/cartera/creditos",
            "Buscar un crédito y ver su resumen, estado de cuenta, plan con sus versiones, operaciones, libro, garantías y giro; liquidarlo a cualquier fecha —también con lo que se sabía en un momento dado— y simular o reversar un pago.",
            [
                P("Cartera → Créditos", "Busque por número o deudor, línea, oficina, estado, modalidad o mora mínima. Los saldos de la lista son la última foto diaria, con su fecha; la cifra exacta a una fecha es la liquidación.", "/cartera/creditos", "Abrir Créditos"),
                P("El expediente", "Las pestañas del crédito muestran el estado de cuenta, el plan y sus versiones, las operaciones con su comprobante, el libro, la calificación, las garantías y el giro."),
                P("Liquidar", "La pestaña «Liquidación» da lo que cuesta cancelar a una fecha, por concepto y con la explicación; «con la información hasta» reproduce lo que se sabía en ese momento. Cada consulta queda en la auditoría."),
                P("Pagos", "Los pagos los aplica quien recauda (Recaudo o Nómina) por el contrato de obligaciones; aquí se simulan y, con permiso, se reversan con motivo. Lo causado se corrige por la causación de cada día, no a mano."),
            ],
            ["creditos", "credito", "estado de cuenta", "liquidacion", "saldo para cancelar", "plan de pagos", "libro", "operaciones", "reversar pago", "simular pago", "causacion"],
            ["Cartera activada.", "Permisos Lending.Loans.Read para el expediente y Lending.Payoffs.Read para liquidar."],
            ["cartera-desembolsos", "cartera-cierres", "cartera-documentos", "cartera-novedades", "cartera-cobranza", "cartera-condonaciones"], ["/cartera/creditos/{Id:guid}"], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-cierres", "Calificación y cierre mensual", Modulos.Cartera, "/cartera/cierres",
            "Revisar los requisitos y ordenar el cierre del mes —calificación de cada crédito, arrastre, deterioro y los hechos para Contabilidad—, leer el detalle y el cuadre de un cierre y reabrirlo con motivo.",
            [
                P("Cartera → Cierres", "Muestra el cierre base y los mensuales con su estado, y los requisitos del mes: cierre anterior, mes abierto en Contabilidad, causación al día, valores nacionales al día y ninguna corrida en curso.", "/cartera/cierres", "Abrir Cierres"),
                P("Ordenar", "Ordenar sólo encola: el proceso corre el cierre por tandas y deja la calificación, el deterioro y los hechos fechados el último día del mes. Contabilidad no cierra el mes mientras Cartera no lo haya cerrado."),
                P("Detalle y cambios", "El detalle de un cierre muestra las líneas por crédito, el reporte de cambios de categoría con su causa, el cuadre y las partes. Lo cerrado no se edita."),
                P("Reabrir", "Reabrir pide motivo y deja el cierre reemplazado; el nuevo cierre del mes se ordena otra vez."),
            ],
            ["cierre mensual", "cierres", "calificacion", "arrastre", "deterioro", "categoria", "reabrir cierre", "cierre base", "sarc"],
            ["Cartera activada; la causación al día hasta fin de mes; el Anexo 1 y la política del consejo vigentes.", "Permisos Lending.Ratings.Read para consultar y Lending.MonthlyClose.Execute (y .Reopen) para ordenar y reabrir."],
            ["cartera-creditos-y-liquidacion", "cartera-perdida-esperada", "cartera-informes-y-tablero", "cartera-evaluaciones", "cartera-castigos", "contabilidad-cartera-lotes"], ["/cartera/cierres/{Id:guid}"], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-bandeja-de-mensajes", "Bandeja de mensajes de Cartera", Modulos.Cartera, "/cartera/bandeja-de-mensajes",
            "Ver lo que llegó a Cartera (ventas a crédito de Comercial, resultados del giro, de la pagaduría y de los pagos) con su evaluación y su respuesta, y los hechos que Cartera emitió a Contabilidad con su estado, para reprocesar lo que falló.",
            [
                P("Cartera → Bandeja de mensajes", "La pestaña «Recibidos» lista lo que llegó, cómo se evaluó y qué respondió Cartera; un recibo que no se puede procesar se marca no reprocesable con motivo.", "/cartera/bandeja-de-mensajes", "Abrir la bandeja"),
                P("Emitidos", "La pestaña «Emitidos» muestra los hechos para Contabilidad con su estado. Uno rechazado se reprocesa después de corregir la causa (casi siempre una regla de la matriz contable); uno que no pasaba se envía cuando ya debe pasar."),
                P("Conciliación", "La conciliación contable compara Cartera con Contabilidad por conjuntos de cuentas y explica cada diferencia hecho por hecho.", "/cartera/conciliacion-contable", "Abrir la conciliación"),
            ],
            ["bandeja de mensajes", "mensajes recibidos", "hechos emitidos", "reprocesar", "rechazo contable", "ventas a credito", "conciliacion contable"],
            ["Permisos Lending.Messages.Read para ver, Lending.Messages.Reprocess para reprocesar y Lending.Messages.SendNotApplicable para enviar lo que no pasaba."],
            ["cartera-creditos-y-liquidacion", "cartera-cierres", "contabilidad-cartera-matriz", "contabilidad-cartera-lotes"], ["/cartera/conciliacion-contable"], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-valores-nacionales", "Valores nacionales: usura, UVR, festivos y límites", Modulos.Cartera, "/admin/valores-nacionales",
            "Los valores que fija la ley y son iguales para todas las cooperativas —salario mínimo, UVT, usura por modalidad, UVR, festivos, tope de vivienda y límites legales—: el maestro los registra una vez en la plataforma y cada cooperativa recibe una réplica que Cartera y Nómina leen.",
            [
                P("Administración → Valores nacionales", "Sólo el maestro, con segundo factor, registra un valor con su vigencia, unidad y fuente (las tasas como fracción: 26,50 % es 0,2650), o carga varios con la plantilla. Arriba aparece lo que falta para los próximos días.", "/admin/valores-nacionales", "Abrir Valores nacionales"),
                P("Corregir", "Nada se edita ni se borra: «Corregir» registra otra fila con motivo. Corregir una usura o una UVR que ya se causó encola en cada cooperativa la corrección de los días causados."),
                P("La réplica", "Cada cooperativa recibe los valores por la réplica; si falta un valor que Cartera necesita (la usura del día, la UVR de un crédito en UVR), no adivina: no simula o no causa ese día y avisa con la alerta de valor nacional faltante."),
            ],
            ["valores nacionales", "usura", "uvr", "festivos", "salario minimo", "uvt", "tope de vivienda", "limites legales", "replica", "corregir valor"],
            ["Ser el maestro de la plataforma, con segundo factor, para registrar o corregir.", "La réplica al día en cada cooperativa (la tarea programada de valores nacionales)."],
            ["cartera-lineas", "cartera-vivienda-y-uvr", "cartera-cierres", "admin-perfil-regulatorio"], [], TipoDeTema.Proceso));


        // ------------------------------------------------------ Cartera: mensajes, alertas y autorizaciones --
        // Feature 013, fase 4 (US17): los mensajes al deudor con la ventana de contacto de la Ley 2300, las alertas del módulo y las autorizaciones
        // del titular por finalidad. SMS y WhatsApp siguen simulados hasta que haya proveedor; lo real sale por correo.
        t.Add(Proceso("cartera-mensajes", "Mensajes al deudor", Modulos.Cartera, "/cartera/mensajes",
            "Consultar todos los mensajes enviados a deudores y codeudores con su historia (en cola, diferido, excluido, enviado, entregado, fallido, simulado o cancelado), enviar uno a mano o programado dentro de la ventana que permite la Ley 2300 y cancelar con motivo el que todavía no salió.",
            [
                P("Cartera → Mensajes al deudor", "Lista de mensajes con su fecha de pedido, destinatario y su papel, crédito, canal, propósito, plantilla y estado (con el próximo intento y las razones si se difirió o se excluyó). Filtre por destinatario, propósito, canal, estado y rango de fechas y pulse «Buscar».", "/cartera/mensajes", "Abrir Mensajes al deudor"),
                P("Ver la historia", "«Historia» muestra el texto que se envió, el destino, si tiene autorización vinculada y cada evento en orden: cuándo, el detalle, el destino y el número de intento."),
                P("Enviar un mensaje", "«Enviar mensaje» pide el destinatario, el canal, el propósito (cobranza, aviso previo, informativo, entrega de documentos o aviso de gastos de cobranza), la plantilla publicada de ese canal y propósito, la hora (vacía = ahora) y las variables que no pone el sistema. «Ver la ventana permitida» dice antes de enviar si sale ya, a qué hora sale o por qué no: horario, domingos y festivos, contactos por día y por semana y autorización del canal con evidencia. Lo que cae fuera se difiere o se excluye; nunca sale."),
                P("Cancelar", "«Cancelar…» aparece sólo en los mensajes que todavía no han salido y pide el motivo. El mensaje queda cancelado en su historia; no se borra."),
                P("Canales", "Los mensajes reales salen por correo electrónico. SMS y WhatsApp todavía no tienen proveedor: aparecen como «(simulado)» y no son constancia en producción. Las referencias personales nunca se contactan: sólo se escribe al deudor o a un codeudor."),
            ],
            ["mensajes", "mensajes al deudor", "ley 2300", "ventana de contacto", "correo", "sms", "whatsapp", "simulado", "diferido", "excluido", "cancelar mensaje", "historia del mensaje"],
            ["Plantillas de mensaje publicadas para el canal y el propósito.", "Autorización del canal de contacto del titular con evidencia.",
                "Permisos Lending.DebtorMessages.Read para consultar, Lending.DebtorMessages.Send para enviar y Lending.DebtorMessages.Cancel para cancelar."],
            ["cartera-plantillas-de-mensaje", "cartera-autorizaciones", "cartera-campanas", "cartera-avisos-previos", "cartera-bandeja-de-mensajes"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-alertas", "Alertas de Cartera", Modulos.Cartera, "/cartera/alertas",
            "Atender las alertas del módulo de Cartera que le corresponden según su permiso y su alcance de oficinas y líneas, dejando constancia de lo que se hizo, y configurar por tipo quién las recibe, si también van por correo, sus umbrales y si están habilitadas.",
            [
                P("Cartera → Alertas", "La pestaña «Bandeja» lista las alertas con su fecha, tipo, severidad, asunto, estado y cuántas veces se repitió; muchas traen un enlace a la pantalla donde se corrige. Filtre por estado (pendientes, atendidas o todas), tipo, severidad y fechas y pulse «Buscar».", "/cartera/alertas", "Abrir Alertas"),
                P("Atender", "«Atender…» en una alerta pendiente pide una nota con lo que se hizo. Queda atendida con quién, cuándo y la nota, a la vista de todos los que la reciben."),
                P("Tipos de alerta", "La pestaña «Tipos» muestra cada tipo con su descripción, los permisos destinatarios y cuántos usuarios activos los tienen, los canales (aplicación y correo), los umbrales y la vigencia. Un tipo sin destinatario le llega al administrador y se marca."),
                P("Nueva vigencia de un tipo", "«Nueva vigencia» registra desde una fecha y con motivo los permisos destinatarios (separados por coma), si también va por correo (la aplicación siempre va), los umbrales (clave=valor) y si está habilitado. La vigencia anterior queda en el historial."),
            ],
            ["alertas", "bandeja de alertas", "atender alerta", "severidad", "destinatarios", "umbrales", "tipos de alerta", "correo", "notificacion"],
            ["Permisos Lending.Alerts.View para ver, Lending.Alerts.Attend para atender y Lending.Alerts.Manage para cambiar los tipos.", "Oficinas y líneas en el alcance del usuario para recibir las alertas que las nombran."],
            ["cartera-alcances", "notificaciones", "cartera-bandeja-de-mensajes"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-autorizaciones", "Autorizaciones del titular", Modulos.Cartera, "/cartera/autorizaciones",
            "Consultar y registrar las autorizaciones de una persona por finalidad (tratamiento general, trámite de crédito, consulta y reporte a centrales y canal de contacto) con la versión de la política que aceptó, su origen y su evidencia. Sin evidencia no se consulta ni se reporta a centrales ni se hace cobranza.",
            [
                P("Cartera → Autorizaciones del titular", "Busque al titular: la tabla muestra cada autorización con su finalidad, canal, estado (y la fecha si fue revocada), fecha de otorgamiento, versión de la política, origen y si tiene evidencia.", "/cartera/autorizaciones", "Abrir Autorizaciones"),
                P("Las que no tienen evidencia", "Una autorización vigente sin evidencia (por ejemplo, una migrada) se marca «Sin evidencia: bloquea centrales y cobranza» y arriba aparece el aviso. Se corrige registrando una nueva con su soporte."),
                P("Registrar la decisión", "«Registrar decisión» pide la finalidad, el canal si es de canal de contacto (SMS, WhatsApp, correo, llamada o visita), si acepta o no acepta, la fecha y notas. Se registra contra la versión vigente de la política de datos de esa finalidad. El soporte firmado se adjunta a la persona; sin él la autorización queda «sin evidencia»."),
                P("Revocar", "Esta pantalla no revoca: la revocatoria es una solicitud del titular que se tramita en Cumplimiento y aquí aparece como revocada con su fecha."),
            ],
            ["autorizaciones", "habeas data", "titular", "consentimiento", "centrales de riesgo", "canal de contacto", "evidencia", "politica de datos", "revocatoria", "tratamiento de datos"],
            ["La política de datos publicada para la finalidad.", "Permisos Compliance.HabeasData.ViewHistory para consultar y Compliance.HabeasData.RecordConsent para registrar."],
            ["habeas-data", "cumplimiento-solicitudes-del-titular", "cartera-mensajes", "cartera-sicses-y-centrales", "personas"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: cupos --
        // Feature 013, US2 (cupos, bloqueos, embargos, saldos de aportes y listas restrictivas) y US6 (reservas de número de las ventas a crédito).
        t.Add(Proceso("cartera-cupos", "Cupos de crédito", Modulos.Cartera, "/cartera/cupos",
            "Consultar el disponible de una persona, otorgar y terminar cupos consolidados, por línea o rotativos, fijar la preferencia de abonos, registrar y levantar bloqueos y embargos, cargar los saldos de aportes y revisar las reservas de número de las ventas a crédito.",
            [
                P("Cartera → Cupos", "Elija la persona y pulse «Consultar»: aparecen sus cupos (clase, línea, monto, origen, estado, vigencia y abonos), sus bloqueos y embargos y sus saldos de aportes y ahorro permanente con su corte.", "/cartera/cupos", "Abrir Cupos"),
                P("Otorgar un cupo", "«Otorgar cupo» pide la clase (consolidado, por línea o rotativo), la línea, el monto, la fecha desde, cómo se aplican los abonos a capital y el motivo. Si supera el monto operativo de quien lo otorga o la matriz de atribuciones lo lleva a otro nivel, queda esperando la decisión, sin error. En cada cupo: «Terminar…», «Revalidar» y «Cambiar abonos»."),
                P("Bloqueos y embargos", "«Registrar bloqueo o embargo» pide la clase (manual o embargo) y el motivo; «Levantar…» en uno vigente pide la fecha y el motivo."),
                P("Saldos de aportes", "«Saldos de aportes» abre la carga por plantilla de los saldos de aportes y ahorro permanente. Mientras Aportes no responda en línea, el cupo y los requisitos usan la última carga con corte a la fecha."),
                P("Reservas y listas restrictivas", "«Reservas de número» muestra los números que Comercial reservó al confirmar ventas a crédito, con su estado, venta, vencimiento y el crédito que lo usó; una reserva vencida nunca se reasigna. El oficial de cumplimiento ve además las coincidencias en listas restrictivas pendientes y las libera con «Liberar…»."),
            ],
            ["cupos", "cupo de credito", "disponible", "rotativo", "consolidado", "bloqueo", "embargo", "saldos de aportes", "abonos a capital", "reservas de numero", "listas restrictivas"],
            ["Líneas de crédito y matriz de atribuciones vigentes.", "Permisos Lending.CreditLimits.Read para consultar, Lending.CreditLimits.Assign para otorgar, terminar y cargar saldos, Lending.PrepaymentPreferences.Manage para los abonos, Lending.Blocks.Manage para bloqueos, Lending.NumberReservations.Read para las reservas y Compliance.RestrictiveLists.Release para liberar coincidencias."],
            ["cartera-lineas", "cartera-solicitudes-y-actas", "cartera-atribuciones", "cumplimiento-listas-restrictivas", "ventas-oficina"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: dobles de prueba --
        // Feature 013, US3 y US5 (K2 suma la libranza): la tesorería y el recaudador de prueba. Sólo existen fuera de producción.
        t.Add(Proceso("cartera-dobles-de-prueba", "Dobles de prueba de Tesorería y Recaudo", Modulos.Cartera, "/cartera/dobles-de-prueba",
            "Sólo fuera de producción: hacer de Tesorería (dar el resultado de las órdenes de giro de los desembolsos) y de Recaudo (consultar las obligaciones de una persona, simular y aplicar pagos, reversarlos, resolver saldos a favor y entregar archivos de descuento y su respuesta) mientras esos módulos no existen.",
            [
                P("Cartera → Dobles de prueba", "La pantalla advierte que es un doble de prueba que no existe en producción; en producción dice que no está disponible y no ofrece nada. Tiene tres pestañas: «Tesorería», «Recaudador» y «Libranza».", "/cartera/dobles-de-prueba", "Abrir Dobles de prueba"),
                P("Tesorería", "Lista las órdenes de giro recibidas con su crédito, forma, beneficiario, neto y estado. «Resultado…» emite el resultado (girada, rechazada, cheque no cobrado o anulada) con la referencia de Tesorería, el valor y el motivo."),
                P("Recaudador: pagar", "Elija la persona y pulse «Listar obligaciones» (opcionalmente donde es codeudor). Elija una, indique modalidad, valor, fecha valor, cuota, elección del deudor, medio, recibo y la clave del pago, y pulse «Simular» y «Aplicar». Repetir la misma clave sirve para probar que el pago no se aplica dos veces; «Clave nueva» propone otra."),
                P("Recaudador: reversar y saldos a favor", "«Ver la redistribución» y «Reversar» deshacen un pago por su clave con motivo. «Resolver» aplica un saldo a favor o un anticipo a otra obligación o lo resuelve de otra forma. Abajo se ven los reversos que Cartera le avisó al recaudador."),
                P("Libranza", "Lista los archivos de descuento generados: «Confirmar envío» los deja enviados y «Cargar respuesta…» recibe la respuesta de la pagaduría en el formato de vuelta del convenio; «Emitir el resultado» la entrega a Cartera."),
            ],
            ["dobles de prueba", "pruebas", "tesoreria de prueba", "recaudador de prueba", "orden de giro", "pago de prueba", "reverso", "saldo a favor", "anticipo", "libranza", "fuera de produccion"],
            ["Ambiente distinto de producción con los participantes de prueba registrados (recaudador TST y tesorería TES).", "Permiso Lending.TestDoubles.Operate."],
            ["cartera-desembolsos", "cartera-creditos-y-liquidacion", "cartera-archivos-de-descuento", "cartera-activacion"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: novedades, condonaciones, castigos y evaluación --
        // Feature 013, K3, US11 (novedades y condonaciones) y US14 (castigo por lote y evaluación de cartera). El detalle, en
        // docs/manual/cartera-novedades-y-condonaciones.md y docs/manual/cartera-castigo-y-evaluacion.md.
        t.Add(Proceso("cartera-novedades", "Novedades de crédito", Modulos.Cartera, "/cartera/novedades",
            "Tramitar los cambios a un crédito vigente: prórroga, cambio de fecha de pago o de tasa, novación, refinanciación (también la que recoge créditos en mora) y cambio de oficina. El sistema decide si es reestructuración o modificación; se simula, se recogen los consentimientos, se radica, la decide quien tiene la atribución y se aplica.",
            [
                P("Cartera → Novedades", "Lista de novedades con sus créditos, mecanismo, causa, estado, nivel, quién la pidió y fecha. Filtre por estado y causa y pulse «Consultar»; «Ver» abre el detalle.", "/cartera/novedades", "Abrir Novedades"),
                P("Nueva novedad", "«Nueva novedad» busca el crédito por número (puede elegir varios en una refinanciación), pide el mecanismo, por qué la pide el deudor (a petición suya o por dificultades de pago), las cuotas del plan nuevo, si se capitaliza el interés, la tasa nueva, el día de pago y el motivo. La causa la calcula el sistema: si la motiva el deterioro de la capacidad de pago es reestructuración, cualquiera sea el mecanismo."),
                P("Simular", "«Simular» muestra el saldo, lo recogido y capitalizado, el capital y el plan nuevo, la calificación hoy y después, el número de reestructuraciones, la usura y la validación contable. Novación y refinanciación trasladan el saldo a un crédito nuevo enlazado; no giran dinero nuevo."),
                P("Consentimientos", "Cada codeudor, avalista o garante registra su decisión con «Registrar…»; aceptar exige el documento firmado. Para quien no consintió se decide no aplicar la novedad o aplicarla con la garantía extinguida, con motivo; si la garantía restante no alcanza, se puede usar una excepción de cobertura que aprueba otra persona."),
                P("Radicar, decidir y aplicar", "«Radicar» la envía al nivel que diga la matriz de atribuciones (o al órgano, si es de persona vinculada); «Decidir…» registra aprobó, aprobó con condiciones, negó o devolvió; «Aplicar» regenera el plan. «Anular» la retira mientras no se aplique. La insolvencia del deudor no es una novedad: se registra en la pestaña «Eventos» del expediente del crédito.", "/cartera/creditos", "Abrir Créditos"),
            ],
            ["novedades", "reestructuracion", "modificacion", "prorroga", "novacion", "refinanciacion", "cambio de tasa", "cambio de fecha", "consentimiento", "codeudor", "excepcion de cobertura", "insolvencia"],
            ["Crédito vivo y matriz de atribuciones vigente.", "Permisos Lending.Novelties.Read para consultar, Lending.Novelties.Request para pedir, simular, radicar y anular y Lending.Novelties.Apply para aplicar; decidir exige la atribución del nivel."],
            ["cartera-creditos-y-liquidacion", "cartera-atribuciones", "cartera-acuerdos-de-pago", "cartera-condonaciones", "cartera-solicitudes-y-actas"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-condonaciones", "Condonaciones", Modulos.Cartera, "/cartera/condonaciones",
            "Pedir, decidir y aplicar la condonación de intereses corrientes y de mora (también en suspenso), seguros y honorarios de cobranza de un crédito y, sólo con el nivel de excepción, de capital. Cada concepto no supera su saldo y la decisión la toma quien tiene la atribución.",
            [
                P("Cartera → Condonaciones", "Lista con el crédito, los conceptos, el total, los días de mora, el estado, el nivel y quién la pidió. Filtre por estado y pulse «Consultar».", "/cartera/condonaciones", "Abrir Condonaciones"),
                P("Nueva condonación", "«Nueva condonación» busca el crédito por número; agregue cada concepto con su valor con «Agregar concepto» («Quitar» lo retira) y escriba el motivo. Los días de mora y si el deudor es vinculado se fijan al pedirla y la llevan al nivel que diga la matriz; por encima de la atribución del gerente va al nivel siguiente."),
                P("Decidir", "«Decidir…» registra la decisión del nivel; la condonación siempre lleva motivo."),
                P("Aplicar o anular", "«Aplicar» registra la condonación en el crédito y pasa a Contabilidad contra el deterioro del último cierre. «Anular…» la retira con motivo mientras no se haya aplicado."),
            ],
            ["condonacion", "condonar", "intereses", "interes de mora", "seguros", "honorarios", "capital", "atribucion", "nivel de excepcion", "perdon de deuda"],
            ["Crédito vivo con saldo en los conceptos y matriz de atribuciones vigente.", "Permisos Lending.Loans.Read para consultar, Lending.Condonations.Request para pedir y anular y Lending.Condonations.Apply para aplicar; decidir exige la atribución del nivel."],
            ["cartera-novedades", "cartera-atribuciones", "cartera-creditos-y-liquidacion", "cartera-castigos"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-castigos", "Castigos de cartera", Modulos.Cartera, "/cartera/castigos",
            "Armar un lote de créditos para castigar, verificar cada uno contra la norma y la política, llevarlo al consejo, aplicarlo con el punto del acta y registrar el reporte al supervisor.",
            [
                P("Cartera → Castigos", "Lista de lotes con su estado, número de créditos, total, acta, quién lo preparó y cuándo se aplicó. Filtre por estado, pulse «Consultar» y «Ver» para el detalle.", "/cartera/castigos", "Abrir Castigos"),
                P("Armar el lote", "«Nuevo lote» busca el primer crédito, pide los soportes (informe motivado, concepto de irrecuperabilidad y, si la consulta de aportes no está disponible, el cruce de aportes) y por qué se propone el castigo; «Armar el lote». En el detalle, «Agregar crédito…» suma otros y «Quitar» los retira. Cada crédito muestra saldo, deterioro, mora y los requisitos; el que no cumple queda en «Rechazados» con lo que le falta."),
                P("Enviar al consejo", "«Enviar al consejo» lo deja en el consejo: se decide en Actas con un punto «Decisión» sobre el lote. Los avisos del lote dicen lo que de la norma sigue pendiente de validar, con el enlace para corregirlo.", "/cartera/actas", "Abrir Actas"),
                P("Aplicar", "Aprobado, «Aplicar el castigo» castiga cada crédito; el que ya no cumple se excluye con su motivo y el resto se castiga. El castigado sigue causando en suspenso. «Anular lote» lo retira con motivo mientras no se aplique."),
                P("Reporte al supervisor", "Si el tipo de entidad debe reportarlo, el lote muestra la fecha de vencimiento del reporte (informativa). «Registrar el reporte al supervisor…» pide el soporte y el radicado."),
            ],
            ["castigo", "castigar cartera", "lote de castigo", "irrecuperable", "informe motivado", "consejo", "acta", "reporte al supervisor", "deterioro", "cartera castigada"],
            ["Créditos con deterioro total y los soportes adjuntos.", "Permisos Lending.Loans.Read para consultar, Lending.WriteOffs.Prepare para armar, enviar y anular y Lending.WriteOffs.Apply para aplicar y registrar el reporte."],
            ["cartera-solicitudes-y-actas", "cartera-evaluaciones", "cartera-cierres", "cartera-condonaciones"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-evaluaciones", "Evaluación de cartera", Modulos.Cartera, "/cartera/evaluaciones",
            "Preparar la evaluación de cartera con un corte y un mes de efecto, proponer por crédito una categoría distinta de la de las reglas con su justificación, llevarla al comité de riesgos y al consejo en Actas y aplicarla para que entre al cierre de su mes de efecto.",
            [
                P("Cartera → Evaluación de cartera", "Lista de evaluaciones con corte, mes de efecto, estado, número de créditos y los puntos del comité y del consejo. Filtre por estado, pulse «Consultar» y «Ver» para el detalle.", "/cartera/evaluaciones", "Abrir Evaluación de cartera"),
                P("Preparar", "«Nueva evaluación» pide el corte (último día del mes), el mes de efecto (AAAA-MM) y los criterios aplicados; busque cada crédito por número, «Agregar», elija la categoría propuesta y escriba la justificación. «Preparar» la guarda. Si se prepara fuera de la periodicidad de la norma, avisa pero no impide."),
                P("Comité y consejo", "Preparada, llévela al comité de riesgos en Actas con un punto «Recomendación» sobre esta evaluación; recomendada, el consejo la decide con un punto «Decisión».", "/cartera/actas", "Abrir Actas"),
                P("Aplicar o descartar", "Aprobada, «Aplicar» la deja como la evaluación de cada crédito desde el cierre de su mes de efecto; la última aplicada reemplaza a las anteriores. «Descartar evaluación» la retira con motivo mientras no se aplique."),
            ],
            ["evaluacion de cartera", "calificacion", "categoria", "comite de riesgos", "consejo", "mes de efecto", "corte", "recalificacion", "riesgo de credito", "cierre"],
            ["Cartera activada.", "Permisos Lending.Ratings.Read para consultar, Lending.PortfolioEvaluations.Prepare para preparar y descartar y Lending.PortfolioEvaluations.Apply para aplicar."],
            ["cartera-cierres", "cartera-solicitudes-y-actas", "cartera-castigos", "cartera-perdida-esperada"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: cobranza --
        // Feature 013, K4, US12: etapas por días de mora, reparto de gestores, agenda y gestiones con la ventana de la Ley 2300, acuerdos de pago,
        // campañas, aviso previo al reporte negativo, reclamos y cobro jurídico. El detalle, en docs/manual/cartera-cobranza-y-aviso-previo.md.
        t.Add(Proceso("cartera-etapas-de-cobranza", "Etapas de cobranza y reglas de reparto", Modulos.Cartera, "/cartera/etapas-de-cobranza",
            "Definir las etapas de cobranza por días de mora (preventiva, administrativa, prejurídica y jurídica) con sus acciones, plantillas y gasto de cobranza, en versiones con vigencia, y las reglas que reparten los créditos en mora entre los gestores.",
            [
                P("Cartera → Etapas de cobranza", "La pestaña «Etapas» muestra la versión vigente a la fecha elegida: código, nombre, rango de días de mora, acciones sugeridas, plantillas y gasto de cobranza. Las etapas que trae el sistema se marcan pendientes de validar hasta que la cooperativa confirme rangos, acciones y gasto.", "/cartera/etapas-de-cobranza", "Abrir Etapas de cobranza"),
                P("Nueva versión de etapas", "«Nueva versión» pide la fecha desde, el motivo y cada etapa con código, nombre, mora desde y hasta, si es la jurídica, sus acciones, los códigos de plantilla de mensaje y, si cobra gasto de cobranza, sobre qué, la tarifa y el tope (sin pasar del techo nacional). Reemplaza a la vigente desde su fecha y no toca lo ya registrado."),
                P("Reglas de reparto", "La pestaña «Reglas de reparto» lista las reglas por prioridad: con qué casan (etapa, línea, rango de saldo), cómo reparten (por cantidad de créditos o por saldo) y entre qué gestores con su peso. Sin reglas, los créditos se asignan a mano."),
                P("Nueva regla o cerrar vigencia", "«Nueva regla» pide prioridad, forma de reparto, etapa, línea, saldo desde y hasta, fecha desde, si todos los créditos del deudor van al mismo gestor y los gestores con su peso. «Cerrar vigencia…» la termina con fecha y motivo. Se evalúan de menor a mayor prioridad y reparte la primera que casa."),
            ],
            ["etapas de cobranza", "dias de mora", "preventiva", "administrativa", "prejuridica", "juridica", "gasto de cobranza", "reglas de reparto", "gestores", "asignacion", "vigencia"],
            ["Plantillas de mensaje de cobranza publicadas.", "Permisos Lending.Collections.Read para consultar, Lending.CollectionStages.Manage para las versiones de etapas y Lending.Collections.Assign para las reglas."],
            ["cartera-cobranza", "cartera-agenda", "cartera-plantillas-de-mensaje", "cartera-juridico"], [], TipoDeTema.Maestro));

        t.Add(Proceso("cartera-cobranza", "Cobranza: asignación de gestores y gastos", Modulos.Cartera, "/cartera/cobranza",
            "Ver y cambiar qué gestor tiene cada crédito en mora, repartir con las reglas con vista previa, asignar uno a mano y causar los gastos de cobranza que ya se le informaron al deudor.",
            [
                P("Cartera → Cobranza", "La pestaña «Asignaciones» es la historia: desde cuándo, crédito, deudor, gestor, origen y motivo. Filtre por gestor y pulse «Consultar». Reasignar agrega una fila; no borra la anterior. El crédito que se pone al día sale de la cobranza solo; si vuelve a caer en mora es un episodio nuevo.", "/cartera/cobranza", "Abrir Cobranza"),
                P("Repartir", "«Repartir» elige qué créditos (etapa, línea, rango de saldo), cómo (por cantidad o por saldo), entre qué gestores y el motivo. «Vista previa» muestra cómo quedaría sin escribir nada, con los que ninguna regla cubre; «Aplicar el reparto» lo registra."),
                P("Asignar a mano", "«Asignar a mano» pone un crédito en manos de un gestor con motivo."),
                P("Gastos por causar", "«Gastos por causar» lista los gastos de cobranza que el ciclo diario propuso, con su etapa, valor y el aviso al deudor. «Causar» sólo procede cuando el aviso está enviado o entregado: el gasto lo causa una persona, nunca el proceso solo. Aquí también llegan los honorarios y gastos del cobro jurídico a cargo del deudor."),
            ],
            ["cobranza", "gestor", "asignacion", "repartir", "reasignar", "gastos de cobranza", "causar gasto", "aviso de gastos", "cartera en mora", "salida de cobranza"],
            ["Etapas de cobranza vigentes y, para repartir, reglas de reparto.", "Permisos Lending.Collections.Read para consultar, Lending.Collections.Assign para asignar y repartir y Lending.CollectionStages.Manage para causar gastos."],
            ["cartera-etapas-de-cobranza", "cartera-agenda", "cartera-mensajes", "cartera-juridico"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-agenda", "Agenda de cobranza y gestiones", Modulos.Cartera, "/cartera/agenda",
            "Ver lo que el gestor tiene que hacer en un día (promesas que vencen, seguimientos, cuotas de acuerdos y créditos que cambiaron de etapa) y registrar cada gestión con su resultado y su promesa de pago, dentro de la ventana de contacto de la Ley 2300.",
            [
                P("Cartera → Agenda", "La pestaña «Agenda» muestra los compromisos del día y del gestor elegidos (por defecto, usted) con crédito, deudor, fecha, valor y detalle. Pulse «Consultar».", "/cartera/agenda", "Abrir Agenda"),
                P("Registrar una gestión", "«Registrar gestión» o «Gestionar…» en un compromiso pide a quién contactó (sólo el deudor o un codeudor del crédito: las referencias personales no se ofrecen), el canal, cuándo, el resultado y si hubo contacto directo. Antes de guardar se ve lo que dice la ventana de la Ley 2300; las gestiones con contacto directo también cuentan en el tope y fijan el canal de la semana."),
                P("Promesa de pago", "Si el resultado es «Promesa de pago», se piden la fecha y el valor prometidos. La pestaña «Promesas» lista las promesas con lo prometido, lo pagado y su estado."),
                P("Historia y anulación", "La pestaña «Gestiones» lista las del rango de fechas. «Anular…» deja una gestión anulada con motivo; no se borra y la corrección es una gestión nueva."),
            ],
            ["agenda", "gestion de cobranza", "promesa de pago", "llamada", "contacto directo", "ley 2300", "seguimiento", "gestor", "resultado de gestion", "anular gestion"],
            ["Créditos asignados al gestor.", "Permisos Lending.Collections.Read para consultar y Lending.Collections.RecordActivity para registrar y anular."],
            ["cartera-cobranza", "cartera-acuerdos-de-pago", "cartera-mensajes", "cartera-etapas-de-cobranza"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-acuerdos-de-pago", "Acuerdos de pago", Modulos.Cartera, "/cartera/acuerdos-de-pago",
            "Proponer un acuerdo de pago con cuotas sobre lo vencido de un crédito, aprobarlo o rechazarlo por una persona distinta de quien lo propuso y seguir su cumplimiento, que se deriva de los pagos.",
            [
                P("Cartera → Acuerdos de pago", "Lista con el crédito, el deudor, lo vencido al acordar, lo acordado, las cuotas, el estado y el cumplimiento (al día, cumplido o incumplido). Filtre por estado y pulse «Consultar».", "/cartera/acuerdos-de-pago", "Abrir Acuerdos de pago"),
                P("Nuevo acuerdo", "«Nuevo acuerdo» pide el crédito, las cuotas con su fecha y valor («Agregar cuota», «Quitar») y el motivo; «Proponer». Las cuotas cubren lo vencido: si cambia el plazo, la tasa o la cuota del crédito, se tramita como novedad."),
                P("Aprobar o rechazar", "Otra persona lo aprueba con «Aprobar» o lo rechaza con «Rechazar…» y motivo."),
                P("Cumplimiento y cancelación", "El cumplimiento lo calcula el sistema con los pagos vigentes; el incumplido devuelve el crédito a la etapa que le corresponde por su mora. «Cancelar…» lo termina con motivo."),
            ],
            ["acuerdo de pago", "acuerdo", "cuotas", "vencido", "cumplimiento", "incumplido", "aprobar acuerdo", "cobranza", "normalizar"],
            ["Crédito en mora.", "Permisos Lending.Collections.Read para consultar, Lending.PaymentAgreements.Create para proponer y cancelar y Lending.PaymentAgreements.Approve para aprobar y rechazar."],
            ["cartera-novedades", "cartera-agenda", "cartera-cobranza"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-campanas", "Campañas de cobranza", Modulos.Cartera, "/cartera/campanas",
            "Enviar un mensaje de cobranza a los deudores de un grupo de créditos (por etapa, línea o mora mínima), ver antes a quiénes les llega y a quiénes se excluye y por qué, y programar el envío dentro de la ventana de la Ley 2300.",
            [
                P("Cartera → Campañas", "Lista de campañas con código, nombre, canal, plantilla, hora de salida, estado y los conteos de destinatarios, en cola, diferidos, excluidos, enviados y fallidos. Filtre por estado y pulse «Consultar».", "/cartera/campanas", "Abrir Campañas"),
                P("Nueva campaña", "«Nueva campaña» pide código, nombre, canal, plantilla, cuándo sale, la etapa, la línea y la mora mínima; «Crear en borrador»."),
                P("Vista previa", "«Vista previa» dice cuántos recibirían el mensaje y quiénes quedan excluidos y por qué: no autorizó el canal, falleció, está en insolvencia, la cobranza está suspendida o ya se le contactó por otro canal esa semana. Sólo se escribe al deudor o a un codeudor."),
                P("Programar o cancelar", "«Programar» crea un mensaje por destinatario, con la constancia de los excluidos; cada uno sale en la primera hora permitida. «Cancelar…» cancela con motivo los que siguen en cola; los que ya salieron no se retiran. Por SMS o WhatsApp el envío es simulado hasta que haya proveedor."),
            ],
            ["campanas", "campana de cobranza", "mensajes masivos", "envio masivo", "excluidos", "ley 2300", "plantilla", "correo", "sms", "whatsapp"],
            ["Plantilla de mensaje de cobranza publicada para el canal.", "Permiso Lending.Campaigns.Manage."],
            ["cartera-mensajes", "cartera-plantillas-de-mensaje", "cartera-etapas-de-cobranza", "cartera-autorizaciones"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-avisos-previos", "Avisos previos y reclamos sobre el reporte", Modulos.Cartera, "/cartera/avisos-previos",
            "Enviar y dejar constancia del aviso previo al reporte negativo en las centrales de riesgo, uno por episodio de mora, y tramitar los reclamos del titular sobre el reporte con su plazo.",
            [
                P("Cartera → Avisos previos", "La pestaña «Avisos previos» lista por crédito el episodio de mora, las comunicaciones hechas frente a las requeridas, la fecha de la constancia, desde cuándo se puede reportar y el estado. Filtre por estado y pulse «Consultar». Quien recae en mora necesita un aviso nuevo.", "/cartera/avisos-previos", "Abrir Avisos previos"),
                P("Enviar el aviso", "«Enviar…» elige el canal; sale con la plantilla de aviso previo y dentro de la ventana de la Ley 2300 (si hoy no se puede, a la primera hora permitida) y queda la constancia del envío. Hoy los avisos reales salen por correo: SMS y WhatsApp siguen simulados y en producción un envío simulado no es constancia."),
                P("Registrar una carta", "«Registrar carta…» deja la constancia de una carta: el adjunto de la guía, el número de la comunicación, la fecha de envío, la transportadora y el número de guía."),
                P("Reclamos", "La pestaña «Reclamos sobre el reporte» lista los reclamos con su titular, crédito, clase, fecha de recibido, vencimiento y estado. «Registrar reclamo» pide titular, clase (reclamo o suplantación), fecha, canal por el que llegó y descripción. «Responder…» registra la respuesta y si se le da la razón; «Prorrogar…» amplía el plazo una sola vez y «Desistir…» lo cierra, ambos con motivo. Vencido sin respuesta se da por aceptado."),
            ],
            ["aviso previo", "reporte negativo", "centrales de riesgo", "constancia", "carta", "guia", "reclamo", "suplantacion", "episodio de mora", "habeas data financiero"],
            ["Plantilla de aviso previo publicada y autorización de reporte a centrales.", "Permisos Lending.PriorNotices.Read y Lending.CreditReportClaims.Read para consultar, Lending.PriorNotices.Send para enviar, Lending.PriorNotices.RegisterEvidence para la constancia y Lending.CreditReportClaims.Manage para los reclamos."],
            ["cartera-sicses-y-centrales", "cartera-mensajes", "cartera-autorizaciones", "habeas-data"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-juridico", "Cobro jurídico", Modulos.Cartera, "/cartera/juridico",
            "Llevar los procesos de cobro jurídico de los créditos en la etapa jurídica: abrirlos con el abogado, registrar cada actuación con sus adjuntos, reanudar a mano el que la insolvencia suspendió y cerrarlos.",
            [
                P("Cartera → Cobro jurídico", "Lista de procesos con deudor, créditos, abogado, despacho y radicado, inicio, estado y última actuación. Filtre por estado y pulse «Consultar».", "/cartera/juridico", "Abrir Cobro jurídico"),
                P("Abrir proceso", "«Abrir proceso» pide un crédito que ya esté en la etapa jurídica, el abogado (una persona del maestro), el despacho, el radicado, el tipo de proceso y la fecha de inicio."),
                P("Actuaciones", "«Actuación…» registra la clase (radicación, auto, embargo o secuestro, audiencia, sentencia, honorarios, gasto, cambio de estado u otra), la fecha, el valor, el adjunto y la descripción. Una actuación no se edita. Los honorarios o gastos a cargo del deudor se le informan antes de causarse y se causan desde Cobranza, pestaña «Gastos por causar».", "/cartera/cobranza", "Abrir Cobranza"),
                P("Suspensión y reanudación", "Una insolvencia abierta del deudor suspende el proceso sola, pero nunca lo reanuda: «Reanudar…» lo hace una persona, con motivo, cuando la insolvencia ya no está en negociación, y el proceso vuelve al estado que tenía."),
                P("Cerrar", "«Cerrar…» lo termina como concluido o retirado, con motivo."),
            ],
            ["cobro juridico", "proceso juridico", "abogado", "demanda", "radicado", "embargo", "actuacion", "honorarios", "insolvencia", "reanudar proceso"],
            ["Crédito en la etapa jurídica y el abogado registrado como persona.", "Permisos Lending.Collections.Read para consultar y Lending.LegalProcesses.Manage para abrir, actuar, reanudar y cerrar."],
            ["cartera-etapas-de-cobranza", "cartera-cobranza", "cartera-creditos-y-liquidacion", "personas"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: libranza --
        // Feature 013, K2, US10: convenios de pagaduría, archivos de descuento y no aplicados (la 014 suma grupos de archivo y equivalencias). El
        // detalle, en docs/manual/cartera-libranza-y-pagadurias.md.
        t.Add(Proceso("cartera-pagadurias", "Pagadurías y convenios de libranza", Modulos.Cartera, "/cartera/pagadurias",
            "Registrar las pagadurías con su convenio de libranza en versiones (fechas de la nómina, formatos de ida y vuelta, tope, ventana de giro, regla del no aplicado y códigos de respuesta), sus contactos, grupos de archivo y equivalencias, y ver lo descontado pendiente de giro.",
            [
                P("Cartera → Pagadurías", "La pestaña «Convenios» lista las pagadurías con la versión que rige y los créditos vinculados; «Ver» abre el detalle. «Carga masiva» trae la plantilla de convenios (hojas de convenios, contactos y códigos); la pagaduría debe existir antes como persona jurídica en Maestros → Personas.", "/cartera/pagadurias", "Abrir Pagadurías"),
                P("Nueva pagaduría", "«Nueva pagaduría» pide el código, la pagaduría, el nombre del convenio, si es la nómina propia de la cooperativa y su primera versión."),
                P("Versiones y códigos", "En el detalle, la pestaña «Versiones» muestra cada versión con periodicidad, corte, envío y pago, formatos, tope, ventana y regla del no aplicado; «Nueva versión» cierra la anterior la víspera (lo usado por un archivo enviado no se edita). «Códigos de respuesta» muestra los de la versión que rige."),
                P("Contactos, grupos y equivalencias", "«Contactos» guarda las personas del convenio con su papel. «Grupos y equivalencias» fija, con fecha y motivo, los grupos de archivo (cada uno con su formato de ida y vuelta) y qué líneas de crédito, aportes o ahorro agrupa cada código de la pagaduría y cómo se reparte (por prioridad o a prorrata); cada cambio registra una versión nueva del convenio."),
                P("Pendientes de giro", "«Pendientes de giro» muestra a una fecha lo descontado y no girado por período, los días de mora de la pagaduría y si superó el umbral. Mientras esté dentro del umbral, lo descontado y no girado no causa mora al deudor."),
            ],
            ["pagadurias", "libranza", "convenio", "pagaduria", "nomina", "formato de ida", "formato de vuelta", "codigos de respuesta", "pendientes de giro", "umbral", "equivalencias"],
            ["La pagaduría registrada como persona jurídica y los formatos bancarios de ida y vuelta.", "Permisos Lending.PayrollAgreements.View para consultar, Lending.PayrollAgreements.Manage para escribir y Lending.PayrollDeductionFiles.Read para los pendientes de giro."],
            ["cartera-archivos-de-descuento", "cartera-no-aplicados", "formatos-bancarios", "personas"], ["/cartera/pagadurias/{Id:guid}"], TipoDeTema.Maestro));

        t.Add(Proceso("cartera-archivos-de-descuento", "Archivos de descuento de libranza", Modulos.Cartera, "/cartera/archivos-de-descuento",
            "Generar por pagaduría y período el archivo de descuentos de nómina con sus altas, cambios y retiros, descargarlo y seguir su envío, que confirma Recaudo.",
            [
                P("Cartera → Archivos de descuento", "Abajo, la lista de archivos con pagaduría, período, versión, líneas, total, estado y fecha de envío, filtrable por pagaduría y estado.", "/cartera/archivos-de-descuento", "Abrir Archivos de descuento"),
                P("Vista previa", "Elija la pagaduría, el año, el mes, la quincena si aplica y el grupo de archivo si el convenio separa archivos, y pulse «Vista previa»: muestra el nombre, la fecha límite de envío y las líneas con su novedad frente al último archivo enviado, sin guardar nada. Avisa cuántas obligaciones o cuentas no tienen código de la pagaduría y no van en ningún archivo."),
                P("Generar", "«Generar» guarda el archivo inmutable con su huella y lo entrega a Recaudo. Regenerar antes del envío pide motivo y deja la versión anterior reemplazada; después del envío la corrección va como novedad del período siguiente."),
                P("Detalle, descargar y anular", "El detalle muestra período, versión, huella, conteos, el envío confirmado con su radicado y las líneas. «Descargar» baja el archivo por un enlace que dura 60 segundos y queda en la auditoría. «Anular…» con motivo, sólo antes del envío. «Enviado» lo marca Recaudo, no esta pantalla."),
            ],
            ["archivo de descuento", "libranza", "descuentos de nomina", "pagaduria", "generar archivo", "novedades", "altas", "retiros", "recaudo", "huella"],
            ["Convenio de pagaduría vigente con sus formatos.", "Permisos Lending.PayrollDeductionFiles.Read para consultar y descargar y Lending.PayrollDeductionFiles.Generate para generar y anular."],
            ["cartera-pagadurias", "cartera-no-aplicados", "cartera-dobles-de-prueba", "cartera-informes"], ["/cartera/archivos-de-descuento/{Id:guid}"], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-no-aplicados", "Cuotas no aplicadas por la pagaduría", Modulos.Cartera, "/cartera/no-aplicados",
            "Seguir las cuotas que la pagaduría o la nómina propia no descontaron, con su motivo, y resolverlas: pasar a pago por caja, enviar a cobranza o cerrar.",
            [
                P("Cartera → No aplicados", "Lista dentro de su alcance con crédito, deudor, cuota, vencimiento, origen, motivo, lo esperado, lo no descontado, el seguimiento y desde cuándo. Filtre por pagaduría, origen y estado (abiertos o resueltos) y pulse «Consultar».", "/cartera/no-aplicados", "Abrir No aplicados"),
                P("Resolver", "«Resolver…» en una abierta pide qué se hace (pasar a pago por caja, enviar a cobranza o cerrar) y el motivo. Se avisa al deudor por sus canales autorizados."),
                P("La cuota sigue exigible", "Resolver no paga la cuota: sigue exigible y su mora corre según las reglas del crédito."),
            ],
            ["no aplicados", "libranza", "no descontado", "pagaduria", "nomina propia", "pago por caja", "enviar a cobranza", "seguimiento", "descuento de nomina"],
            ["Permisos Lending.PayrollDeductionFiles.Read para consultar y Lending.PayrollNotApplied.Manage para resolver."],
            ["cartera-pagadurias", "cartera-archivos-de-descuento", "cartera-cobranza"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------ Cartera: gobierno --
        // Feature 013, fase 4 (US17): la matriz de atribuciones con sus montos operativos y las personas vinculadas; US2 suma los grupos conectados.
        // El detalle, en docs/manual/cartera-gobierno-y-seguridad.md.
        t.Add(Proceso("cartera-atribuciones", "Matriz de atribuciones", Modulos.Cartera, "/cartera/atribuciones",
            "Publicar la matriz que decide a qué nivel (persona con permiso u órgano colegiado) va cada decisión de crédito según el monto consolidado del deudor y sus condiciones, probar a qué nivel iría un caso y fijar por rol los montos operativos de desembolsar, otorgar cupos y otras atribuciones.",
            [
                P("Cartera → Atribuciones", "La pestaña «Matriz» muestra la versión vigente con sus niveles (rango, clase, permiso u órgano, tope y marcas), sus reglas y su huella; «Ver todas las versiones» trae las anteriores.", "/cartera/atribuciones", "Abrir Atribuciones"),
                P("Nueva versión", "«Nueva versión» publica la matriz completa desde una fecha, con la fuente (acta o acuerdo) y el motivo: los niveles («Agregar nivel») y las reglas por sujeto, monto, mora, mecanismo, causa y excepción con su nivel mínimo («Agregar regla»). La vigente rige hasta la víspera de la nueva y lo ya enrutado conserva la versión que lo enrutó."),
                P("¿A qué nivel iría?", "Indique el sujeto, el monto consolidado, el plazo, el puntaje, los días de mora, la garantía, la fecha y si es excepción a la política y pulse «Consultar»: dice el nivel con la versión de la matriz, sin registrar nada, y lo que no se exceptúa en ningún nivel."),
                P("Montos operativos", "La pestaña «Montos operativos» fija por rol el tope de desembolsar, asignar cupos, confirmar validaciones fallidas y fechar antes del cierre con «Fijar monto» (vacío = sin límite), con fecha y motivo. El límite de un usuario es el mayor de sus roles; las decisiones de crédito no usan estos montos sino la matriz."),
            ],
            ["atribuciones", "matriz de atribuciones", "niveles de aprobacion", "monto consolidado", "comite de credito", "consejo", "montos operativos", "tope", "excepcion", "aprobacion de credito"],
            ["Roles y órganos de gobierno registrados.", "Permisos Lending.Attributions.View para consultar y Lending.Attributions.Manage para publicar y fijar montos; para elegir el rol, ver roles de Seguridad."],
            ["cartera-solicitudes-y-actas", "cartera-vinculados", "roles-y-permisos", "cartera-cupos", "cartera-desembolsos"], [], TipoDeTema.Maestro));

        t.Add(Proceso("cartera-vinculados", "Personas vinculadas y grupos conectados", Modulos.Cartera, "/cartera/vinculados",
            "Registrar con vigencia quiénes son vinculados de la cooperativa (consejo, junta, representante legal, aportantes significativos y sus cónyuges y parientes), cuya decisión de crédito va al consejo, y los grupos de personas con un riesgo común para medir la concentración.",
            [
                P("Cartera → Personas vinculadas", "La pestaña «Vínculos» lista persona, documento, fundamento, a quién está vinculada, parentesco, porcentaje de aportes, vigencia y origen. Filtre por persona, fundamento y fecha y pulse «Buscar». Consultarla queda en la auditoría.", "/cartera/vinculados", "Abrir Personas vinculadas"),
                P("Registrar un vínculo", "«Registrar vínculo» pide la persona, el fundamento, el parentesco y su grado, el porcentaje de aportes, desde y hasta y el soporte (acta de elección, certificado o registro civil). «Terminar…» le pone fecha final con motivo. «Plantilla» abre la carga masiva."),
                P("Grupos conectados", "La pestaña «Grupos conectados» lista los grupos con su fundamento, miembros, desde y estado. «Registrar grupo» pide el nombre, el fundamento, por qué forman un riesgo común y los miembros; «Agregar miembro…» y «Terminar…» lo actualizan. La concentración se mide por deudor y por grupo con el mismo límite."),
            ],
            ["vinculados", "personas vinculadas", "consejo", "junta", "representante legal", "parientes", "conyuge", "grupos conectados", "riesgo comun", "concentracion"],
            ["Las personas registradas en el maestro.", "Permisos Lending.RelatedParties.Read para consultar y Lending.RelatedParties.Manage para registrar, terminar y cargar."],
            ["cartera-atribuciones", "cartera-solicitudes-y-actas", "cartera-informes-y-tablero", "personas"], [], TipoDeTema.Maestro));

        // ------------------------------------------------------ Cartera: configuración --
        // Feature 013, fase 4 (US17): parámetros con vigencia (US9 suma la pestaña de riesgo y US13 las imágenes de los documentos), plantillas de
        // mensaje versionadas, registro de plantillas de carga y alcance de oficinas y líneas por usuario.
        t.Add(Proceso("cartera-parametros", "Parámetros de Cartera", Modulos.Cartera, "/cartera/parametros",
            "Consultar y cambiar con vigencia los parámetros del módulo, con su valor vigente, su fuente, los límites legales que los acotan y si el valor por defecto sigue pendiente de validar; además, la política de riesgo y las imágenes de los documentos al deudor.",
            [
                P("Cartera → Parámetros", "La pestaña «Parámetros» agrupa las claves por prefijo con su descripción, valor vigente (y los de cada línea), fuente, límite legal y lo programado. Las pendientes de validar se marcan; las de una entrega que todavía no llegó dicen «Disponible con …» y no se editan.", "/cartera/parametros", "Abrir Parámetros"),
                P("Nueva vigencia", "Nada se edita en su sitio: «Nueva vigencia» pide el valor, el ámbito (general o una línea de crédito), desde cuándo, la fuente legal y el motivo. La cooperativa sólo puede ser más estricta que el límite legal. Algunas claves exigen además su permiso propio."),
                P("Historial", "«Historial» muestra cada vigencia con ámbito, valor, desde, hasta, motivo, fuente legal, autor y fecha. Sin vigencias rige el valor por defecto."),
                P("Riesgo y documentos", "La pestaña «Riesgo» muestra la norma, la política de riesgo de crédito del consejo y la efectiva, con sus versiones. La pestaña «Documentos» carga el logotipo y la imagen de la firma de los documentos al deudor."),
            ],
            ["parametros", "configuracion", "vigencia", "limite legal", "pendiente de validar", "politica de riesgo", "sarc", "logotipo", "firma", "historial"],
            ["Permisos Lending.Parameters.View para consultar y Lending.Parameters.Manage para registrar vigencias (más el permiso propio de las claves que lo piden)."],
            ["cartera-lineas", "cartera-documentos", "cartera-perdida-esperada", "parametros-del-sistema"], [], TipoDeTema.Maestro));

        t.Add(Proceso("cartera-plantillas-de-mensaje", "Plantillas de mensaje", Modulos.Cartera, "/cartera/plantillas-de-mensaje",
            "Redactar los textos de los mensajes al deudor por código, propósito y canal, en versiones: una publicada no se edita, se publica otra desde una fecha.",
            [
                P("Cartera → Plantillas de mensaje", "Lista con código, propósito, canal, versión, vigencia y texto; las pendientes de revisar se marcan. Filtre por propósito y canal y pulse «Buscar». «Historial» muestra todas las versiones de una plantilla.", "/cartera/plantillas-de-mensaje", "Abrir Plantillas de mensaje"),
                P("Nueva plantilla o versión", "«Nueva plantilla» o «Nueva versión» piden código, propósito, canal, asunto, el texto con las variables entre dobles llaves, desde cuándo y el motivo. Sólo se admiten las variables del propósito, que la pantalla lista. En WhatsApp se pide el identificador de la plantilla aprobada por el proveedor."),
                P("Vista previa y publicar", "«Vista previa» muestra el texto con datos de ejemplo y sus advertencias (por ejemplo, el largo de un SMS), sin guardar. «Publicar» la deja vigente desde su fecha. SMS y WhatsApp siguen simulados hasta que haya proveedor; los mensajes reales salen por correo."),
            ],
            ["plantillas de mensaje", "texto del mensaje", "variables", "correo", "sms", "whatsapp", "carta", "version", "vista previa", "publicar"],
            ["Permisos Lending.MessageTemplates.View para consultar y Lending.MessageTemplates.Manage para crear y publicar."],
            ["cartera-mensajes", "cartera-campanas", "cartera-etapas-de-cobranza", "cartera-avisos-previos"], [], TipoDeTema.Maestro));

        t.Add(Proceso("cartera-plantillas", "Plantillas de carga de Cartera", Modulos.Cartera, "/cartera/plantillas",
            "Descargar las plantillas Excel de Cartera e importarlas con revisión previa, todo o nada, según lo que permite su rol.",
            [
                P("Cartera → Plantillas de carga", "Lista de plantillas con sus hojas, la entrega en que está disponible, si trae datos personales y qué permiso importa.", "/cartera/plantillas", "Abrir Plantillas de carga"),
                P("Descargar", "«Vacía» baja el libro con los encabezados en la fila 1 y las instrucciones en otra hoja. Una plantilla de una entrega que no llegó se descarga para prepararla, pero no se importa."),
                P("Importar", "«Importar…» abre la carga: suba el archivo y pulse «Revisar». Si hay errores no se guarda nada y «Revisión en Excel» devuelve el mismo libro con los errores de cada fila; sin errores, revise avisos y cambios y pulse «Aplicar». Ninguna plantilla crea personas."),
            ],
            ["plantillas", "plantillas de carga", "importar", "excel", "carga masiva", "revision", "errores", "descargar plantilla"],
            ["Las personas registradas en el maestro antes de cargar.", "Permiso Lending.Templates.View para ver y el permiso de importación de cada plantilla para importarla."],
            ["cartera-migracion", "cartera-pagadurias", "cartera-vinculados", "cartera-cupos"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cartera-alcances", "Alcance de usuarios en Cartera", Modulos.Cartera, "/cartera/alcances",
            "Definir qué líneas de crédito opera cada usuario y ver qué oficinas tiene asignadas: fuera de su alcance un crédito, una solicitud o una alerta no existen para él.",
            [
                P("Cartera → Alcance de usuarios", "Lista de usuarios con su estado; busque por nombre y pulse «Editar alcance».", "/cartera/alcances", "Abrir Alcance de usuarios"),
                P("Oficinas", "Las oficinas las asigna Seguridad y aquí sólo se ven. Sin oficinas asignadas el usuario no ve ningún crédito; con el permiso correspondiente ve todas.", "/security/users", "Abrir Usuarios"),
                P("Líneas de crédito", "Agregue las líneas con «Agregar línea», retire las que sobran y pulse «Guardar alcance». Con el permiso correspondiente opera todas las líneas."),
            ],
            ["alcance", "alcance de usuarios", "oficinas", "sucursales", "lineas de credito", "permisos", "seguridad", "usuarios", "visibilidad"],
            ["Usuario creado y con oficinas asignadas en Seguridad.", "Permiso Lending.Scopes.Manage."],
            ["usuarios", "roles-y-permisos", "sucursales", "cartera-lineas", "cartera-alertas"], [], TipoDeTema.Maestro));

        // ------------------------------------------------------ Captaciones y aportes --
        // Feature 014: los temas de ahorros, aportes y CDT heredados se retiraron con sus pantallas; los de las captaciones nuevas
        // tienen su módulo, como el grupo «Captaciones y aportes» del menú. Índice en docs/manual/cartera-captaciones.md.
        // ------------------------------------------------------ Captaciones y aportes: configuración --
        // Feature 014, US1 (T070): las líneas de captación y de aportes viven en el catálogo común de líneas, filtrado por naturaleza; US6 (T179) suma
        // las reglas de tasa negociada del CDAT. El detalle, en docs/manual/cartera-captaciones-lineas-y-apertura.md.
        t.Add(Proceso("captaciones-lineas", "Líneas de captación y de aportes", Modulos.Captaciones, "/cartera/lineas?naturaleza=captacion",
            "Registrar y versionar las líneas de ahorro a la vista, contractual, permanente y CDAT, y la línea de aportes sociales: tasa y tramos, mínimos, cálculo de intereses, retención, GMF, límites, inactividad y las condiciones propias del CDAT y del contractual. Cada cambio es una versión nueva con fecha y motivo; lo anterior se sigue viendo a su fecha.",
            [
                P("Captaciones y aportes → Líneas de captación", "Lista de líneas con su clase (a la vista, contractual, permanente o CDAT), estado y resumen de la versión vigente. Se busca por código o nombre y «Ver cerradas» incluye las cerradas. Los botones «Crédito», «Captación» y «Aportes» cambian de naturaleza dentro del mismo catálogo.", "/cartera/lineas?naturaleza=captacion", "Abrir Líneas de captación"),
                P("Nueva línea", "«Nueva línea» pide código (la nomenclatura de la cooperativa), nombre y clase, y la primera versión: vigencia y motivo, elegibilidad (exige asociado, cotitulares, menores, retiros parciales), tasa fija o indexada con tramos por saldo (o por plazo y monto en el CDAT), método de saldo, liquidación y convención de días, mínimos para abrir, mantener y ganar interés, tarifa de retención, si grava GMF, límites de retiro y de efectivo, y días para inactividad. Si no se marca «Activarla ya», queda en borrador. «Registrar línea» guarda."),
                P("Condiciones del CDAT y del contractual", "En un CDAT: plazo mínimo y máximo, prórroga automática y su tasa, días hábiles de gracia, qué pasa si no se prorroga, la línea a la vista destino y la penalidad de la redención anticipada. En un contractual: periodicidad y cuota mínima, plazo en meses, tasa bonificada y cuándo se reconoce, cuotas atrasadas toleradas, qué pasa si incumple, los destinos que el asociado puede elegir al vencer y la tasa castigo."),
                P("Ver, versionar y cerrar", "«Ver» abre la línea: la versión vigente a la fecha que se elija y el historial de versiones. «Nueva versión» parte de la vigente y rige desde su fecha (nunca antes de hoy una vez activadas las captaciones); lo ya causado no cambia y un cambio de tasa de una línea a la vista se avisa a los titulares con la anticipación del reglamento. Con un motivo, «Cerrar a cuentas nuevas» o «Reabrir» (o «Activar» si estaba en borrador). En un CDAT que admite tasa negociada, ahí mismo se registran y terminan las reglas de tasa negociada con su margen máximo."),
                P("Línea de aportes", "Con el botón «Aportes» se administra la línea de aportes sociales: mínimo no reducible por asociado (valor, porcentaje del ingreso o salarios mínimos), cuota periódica con su periodicidad y redondeo, reparto con una línea de ahorro permanente, aportes extraordinarios, límite de participación y plazo de devolución.", "/cartera/lineas?naturaleza=aportes", "Abrir Líneas de aportes"),
                P("Cargar líneas desde Excel", "Las líneas de captación (hojas de líneas y de tramos) y la línea de aportes también se cargan con su plantilla en Plantillas de Cartera, con revisión previa; los cargos y la tasa variable se configuran en pantalla.", "/cartera/plantillas", "Abrir Plantillas de Cartera"),
            ],
            ["lineas de captacion", "linea de aportes", "ahorro a la vista", "cdat", "ahorro contractual", "ahorro permanente", "tasa", "tramos",
                "version de linea", "tasa negociada", "retencion", "gmf", "inactividad", "cuota de aportes"],
            ["Valores nacionales y parámetros de Cartera al día para las tasas indexadas y la retención.",
                "Permisos Lending.DepositLines.View y Lending.DepositLines.Manage para las líneas de captación; Lending.ContributionLines.View y Lending.ContributionLines.Manage para la de aportes."],
            ["cartera-lineas", "captaciones-cuentas", "captaciones-cdat", "captaciones-aportes", "cartera-plantillas", "cartera-parametros"], [], TipoDeTema.Maestro));

        // ------------------------------------------------------ Captaciones y aportes: cuentas --
        // Feature 014, US2 (T079) apertura y titularidad; US4 (T124) movimientos, cargos y débito automático; US5 (T139) intereses y cancelación;
        // US7 (T185) contrato del ahorro contractual; US13 (T199) documentos al titular. Detalle en docs/manual/cartera-captaciones-*.md.
        t.Add(Proceso("captaciones-cuentas", "Cuentas de ahorro y aportes", Modulos.Captaciones, "/cartera/ahorros",
            "Abrir cuentas de ahorro y de aportes con su titular, cotitulares, firmantes y beneficiarios, y trabajar la ficha de cada cuenta: operaciones con su comprobante y reverso, retiro por orden de giro y traslados, cargos, débito automático, GMF, intereses y retención, contrato del ahorro contractual, documentos al titular y cancelación.",
            [
                P("Captaciones y aportes → Cuentas de ahorro y aportes", "Lista de cuentas del alcance con titular, línea, clase, oficina, estado, saldo y marcas. Se busca por número, documento o nombre y se filtra por naturaleza (captación o aportes) y estado. «Ver» abre la ficha.", "/cartera/ahorros", "Abrir Cuentas"),
                P("Abrir cuenta", "«Abrir cuenta» va en tres pasos: el titular (se busca en el maestro y, si no está, «Crear persona»), la línea y la oficina; luego cotitulares con su proporción, firmantes con su límite, representante legal y beneficiarios que suman 100 %, con los soportes del titular (en un contractual, también la cuota, el plazo y la instrucción al vencer); y por último la exención del GMF (una cuenta por persona), la constancia de beneficiarios y la declaración de origen de fondos. La cuenta nace activa o en apertura, retenida por listas restrictivas o esperando el depósito; «Aprobar la apertura» en la ficha la hace otra persona, con motivo."),
                P("Ficha: datos, partes y GMF", "Muestra titular, clase, estado, manejo, saldo por concepto y exención del GMF. «Cambiar partes y beneficiarios» cierra lo que cambia ayer y lo nuevo rige desde hoy (nada se borra; un cambio de beneficiarios lleva la constancia firmada) y «Ver la historia» muestra las partes anteriores. Con un motivo se marca o se termina la exención del GMF y, cuando el titular cumple la mayoría de edad, se registra «Datos actualizados»."),
                P("Operaciones, cargos y débito automático", "«Operaciones» lista lo aplicado con su «Comprobante» y «Reversar» (el reverso nunca deja la cuenta en sobregiro; lo de un recaudador lo reversa él). «Ver el libro» muestra los movimientos por concepto. «Retiro por orden de giro» y «Trasladar a otra cuenta» se simulan antes de aplicar (firmas, disponible con el GMF, cargos); si superan la atribución quedan esperando un aprobador presente con segundo factor. El efectivo entra y sale por Recaudo, no por esta pantalla. También se causan, condonan o reversan cargos y se autoriza o revoca el débito automático con la autorización firmada."),
                P("Intereses, contrato y cancelación", "«Simular la liquidación» a cualquier fecha explica día por día saldo, base, tramo, tasa e interés, con la retención; «Liquidar el período» liquida a mano un período vencido. En un contractual se ve el contrato: cumplimiento, proyección, plan de cuotas y la instrucción al vencimiento, que se guarda o se retira con motivo (sin instrucción, al vencer queda a disposición del titular y no se mueve dinero). «Ver lo que dejaría la cancelación» muestra lo que se liquidaría y lo que la impide; «Cancelar la cuenta» entrega por traslado o por orden de giro."),
                P("Documentos al titular", "En la ficha: el saldo a una fecha, el extracto de un período en pantalla y «Emitir» extracto, certificado de saldo (con o sin bloqueos y embargos), certificado tributario o de retención, para descargar o enviar por correo, con la lista de documentos emitidos y su PDF. Los avisos al titular (inactividad, cambio de tasa, mayoría de edad) salen por correo electrónico; SMS y WhatsApp todavía no se envían: esperan proveedor y aparecen como simulados."),
            ],
            ["cuenta de ahorro", "abrir cuenta", "titular", "cotitular", "beneficiarios", "firmantes", "retiro", "traslado", "reverso", "gmf",
                "exencion gmf", "intereses", "retencion en la fuente", "debito automatico", "cancelar cuenta", "extracto"],
            ["Las captaciones o los aportes activados y la línea vigente.", "Recaudo y Tesorería registrados para consignar y girar (en prueba, los dobles de prueba).",
                "Permisos Lending.Accounts.Read para consultar, Lending.Accounts.Open para abrir y aprobar la apertura, Lending.Accounts.Parties.Manage para las partes, Lending.GmfExemptions.Manage, Lending.Accounts.Withdraw, Lending.Accounts.Transfer, Lending.Accounts.Reverse, Lending.Charges.Manage y Lending.Charges.Waive, Lending.DirectDebits.Manage, Lending.Interest.Settle, Lending.Accounts.Close y Lending.Documents.Issue según la acción."],
            ["captaciones-lineas", "captaciones-aportes", "captaciones-cdat", "captaciones-aprobaciones", "captaciones-embargos", "personas", "cartera-documentos"],
            ["/cartera/ahorros/{Id:guid}"], TipoDeTema.Proceso));

        // Feature 014, US3 (T094): el resumen de aportes de un asociado y las devoluciones y cesiones por aprobar. Detalle en
        // docs/manual/cartera-captaciones-aportes-y-retiro-del-asociado.md §1–§4.
        t.Add(Proceso("captaciones-aportes", "Aportes sociales", Modulos.Captaciones, "/cartera/aportes",
            "Consultar los aportes de un asociado por clase, con el mínimo irreducible, el ahorro permanente y las cuotas vencidas; pedir devoluciones sin retiro y cesiones a otro asociado, y aprobarlas o negarlas.",
            [
                P("Captaciones y aportes → Aportes sociales", "La pestaña «Asociado» busca por documento o nombre y muestra el resumen; «Ver la ficha de aportes» abre la ficha. Si los aportes no están activados, el saldo que se ve es el del libro hasta hoy.", "/cartera/aportes", "Abrir Aportes sociales"),
                P("Ficha de aportes", "El resumen a una fecha («Consultar»), las cuentas de aportes y de ahorro permanente o contractual, y las cuotas con su estado (pendientes, abonadas, no descontadas con su motivo, pagadas, anuladas). La cuota no se paga aquí: la pagan Nómina, la pagaduría o la caja. Desde la ficha se emiten la constancia y el paz y salvo de aportes y los certificados."),
                P("Pedir devolución o cesión", "«Pedir devolución» (sin retiro del asociado: sólo lo que excede el mínimo, sin créditos vivos y sin bajar el capital mínimo de la cooperativa) o «Pedir cesión» (a otro asociado, si los estatutos lo permiten), con cuenta, valor, causa y motivo. Queda por aprobar."),
                P("Aprobar o negar", "Las pestañas «Devoluciones» y «Cesiones» listan las solicitudes por estado; «Aprobar» o «Negar» con motivo, por una persona distinta de quien pidió."),
            ],
            ["aportes sociales", "aportes", "asociado", "minimo irreducible", "cuota de aportes", "devolucion de aportes", "cesion de aportes",
                "ahorro permanente", "constancia de aportes", "paz y salvo de aportes"],
            ["La línea de aportes vigente y los aportes activados.", "Permisos Lending.Accounts.Read para consultar y Lending.Contributions.Manage para pedir, aprobar o negar."],
            ["captaciones-lineas", "captaciones-cuentas", "captaciones-retiros-asociado", "captaciones-asamblea", "registro-de-asociados"],
            ["/cartera/aportes/{Id:guid}"], TipoDeTema.Proceso));

        // Feature 014, US6 (T179): CDAT —lista, calendario, tasas negociadas, constitución y ficha—. Detalle en
        // docs/manual/cartera-captaciones-cdat-y-contractual.md §1–§6.
        t.Add(Proceso("captaciones-cdat", "CDAT", Modulos.Captaciones, "/cartera/cdat",
            "Constituir CDAT con su simulación, decidir tasas negociadas, y desde la ficha fondear, cambiar la instrucción al vencimiento, renovar y redimir; con el calendario de vencimientos de los próximos días.",
            [
                P("Captaciones y aportes → CDAT", "Lista del alcance con título, línea, capital, plazo, tasa, plazo de emisión, vencimiento, estado e instrucción; se filtra por estado, plazo de emisión y rango de vencimiento. Arriba, las tasas negociadas por decidir («Decidir»); abajo, los vencimientos de los próximos días.", "/cartera/cdat", "Abrir CDAT"),
                P("Simular", "«Constituir CDAT»: línea, oficina, monto, plazo en días, periodicidad y modalidad de intereses (vencidos o anticipados) y, si la línea lo admite, el margen negociado con su razón. «Simular» muestra la tasa, el plan con la retención estimada, el vencimiento hábil y el plazo de emisión.", "/cartera/cdat/nuevo", "Constituir CDAT"),
                P("Constituir", "Con la simulación: titular y beneficiarios (con su constancia), a dónde van los intereses y el vencimiento (cuenta a la vista o orden de giro), qué hacer al vencer (prorrogar, renovar con condiciones nuevas o cancelar y pagar) y el fondeo: consignación del monto exacto por caja o recaudador, o traslado desde una cuenta de ahorro del titular. «Constituir» se niega si algo cambió desde la simulación. Nace activo si se fondea por traslado; si no, en apertura esperando el dinero o la decisión de la tasa."),
                P("Ficha del CDAT", "Condiciones vigentes, plan de intereses por versión, versiones y títulos. Según el estado: «Fondear por traslado», «Instrucción al vencimiento», «Renovar» (entre el vencimiento y el fin de la gracia, con cambio de monto si se quiere; nace un título nuevo) y «Redimir» con su vista previa; una redención sobre la atribución espera un aprobador. La tasa negociada en espera la aprueba o niega quien tiene el nivel (con el punto de acta si decide un órgano) y quien la pidió puede retirarla."),
                P("Documentos", "En la ficha también están el saldo, el extracto, los certificados y «Emitir el título pendiente» si el título no salió al constituir."),
            ],
            ["cdat", "certificado de deposito", "constituir cdat", "plazo", "tasa negociada", "renovar", "prorroga", "redimir", "vencimiento",
                "titulo", "fondeo", "plan de intereses"],
            ["Una línea de CDAT vigente (y sus reglas de tasa negociada si se negocia) y las captaciones activadas.",
                "Permisos Lending.Accounts.Read para consultar, Lending.TermDeposits.Constitute para constituir y fondear, Lending.TermDeposits.Renew, Lending.TermDeposits.Redeem, Lending.NegotiatedRates.Request y Lending.NegotiatedRates.Approve según la acción."],
            ["captaciones-lineas", "captaciones-cuentas", "captaciones-aprobaciones"],
            ["/cartera/cdat/nuevo", "/cartera/cdat/{Id:guid}"], TipoDeTema.Proceso));

        // Feature 014, US11 (T192): los embargos sobre las cuentas. Detalle en docs/manual/cartera-captaciones-bloqueos-embargos-y-documentos.md §3–§4.
        t.Add(Proceso("captaciones-embargos", "Embargos", Modulos.Captaciones, "/cartera/embargos",
            "Registrar el oficio de embargo con su soporte, ver lo retenido en cada cuenta con el límite inembargable, responder al juzgado, consignar por orden de giro y levantar la medida.",
            [
                P("Captaciones y aportes → Embargos", "Lista por persona y estado con autoridad, oficio, medida, retenido, consignado y la fecha para responder (informativa). «Ver» abre el embargo.", "/cartera/embargos", "Abrir Embargos"),
                P("Registrar embargo", "Persona embargada, juzgado o entidad (del maestro), autoridad, radicado, número y fecha del oficio, recibido, demandante, valor de la medida (vacío = sin límite), si es por alimentos y la cuenta de depósitos judiciales; el oficio se sube con «Subir el oficio» y se elige. Rige al registrarse. Los aportes no se embargan."),
                P("Lo retenido", "Por cuenta: saldo, lo protegido por el límite inembargable y lo embargable, con la explicación. Lo retenido se recalcula cada noche."),
                P("Responder y consignar", "«Ver borrador» arma la respuesta con la plantilla de la cooperativa; «Registrar la respuesta enviada» deja la fecha. «Consignar al juzgado» con valor y motivo sale por orden de giro a Tesorería."),
                P("Levantar", "Con el oficio del desembargo y el valor (parcial, o vacío para todo), «Levantar». Lo levanta una persona distinta de quien lo registró."),
            ],
            ["embargo", "oficio", "juzgado", "limite inembargable", "desembargo", "alimentos", "cobro coactivo", "retenido", "consignar al juzgado", "respuesta al juzgado"],
            ["El valor nacional del límite inembargable registrado en la central.",
                "Permisos Lending.Accounts.Read para consultar, Lending.Garnishments.Register para registrar, responder y consignar, y Lending.Garnishments.Release para levantar."],
            ["captaciones-cuentas", "cartera-valores-nacionales", "cartera-parametros"], [], TipoDeTema.Proceso));

        // Feature 014, US10 (T106): el retiro del asociado. Detalle en docs/manual/cartera-captaciones-aportes-y-retiro-del-asociado.md §5–§8.
        t.Add(Proceso("captaciones-retiros-asociado", "Retiro del asociado", Modulos.Captaciones, "/cartera/retiros-asociado",
            "Radicar el retiro de un asociado, registrar la decisión del órgano con el acta, liquidar los aportes y ahorros con el cruce contra sus créditos y devolver el saldo por orden de giro a Tesorería.",
            [
                P("Captaciones y aportes → Retiros de asociado", "Lista por estado y causa. «Radicar retiro» pide la causa, el detalle, la fecha de la solicitud y una nota; hay una sola solicitud viva por persona. «Ver» abre la ficha.", "/cartera/retiros-asociado", "Abrir Retiros de asociado"),
                P("Decidir", "Radicado, quien radicó puede «Desistir»; otra persona registra la aprobación o la negación con el punto del acta cerrada y la fecha del retiro."),
                P("Ver la liquidación", "Aprobado, «Ver la liquidación» a una fecha: aportes por clase, ahorros, cruce con los créditos en orden de prelación, descuento por pérdidas si el acta lo dispuso, la devolución y el efecto sobre el capital mínimo irreducible de la entidad. Si los créditos siguen debiendo, se elige si pasan a pagar por caja o conservan su forma de pago."),
                P("Liquidar y devolver", "«Liquidar (orden de giro a Tesorería)» con motivo genera la liquidación (descargable) y las órdenes de devolución. Si la devolución perforaría el mínimo irreducible queda pendiente y se avisa al gerente; «Aprobar devolución» la hace otra persona que quien liquidó."),
            ],
            ["retiro del asociado", "retiro", "liquidacion de aportes", "devolucion de aportes", "cruce", "acta", "minimo irreducible",
                "desistir", "orden de giro", "perdidas"],
            ["Aportes activados, el acta del órgano con su punto cerrado y Tesorería registrada.",
                "Permisos Lending.MembershipWithdrawals.File para radicar, Lending.MembershipWithdrawals.Approve para decidir y aprobar la devolución, y Lending.MembershipWithdrawals.Settle para liquidar."],
            ["captaciones-aportes", "captaciones-fallecimientos", "cartera-creditos-y-liquidacion"],
            ["/cartera/retiros-asociado/{Id:guid}"], TipoDeTema.Proceso));

        // Feature 014, US12 (T205) y respuestas del dueño E260/E261: revalorización, excedentes y amortización. Detalle en
        // docs/manual/cartera-captaciones-asamblea-inactividad-y-fallecimiento.md §1–§4.
        t.Add(Proceso("captaciones-asamblea", "Decisiones de asamblea", Modulos.Captaciones, "/cartera/asamblea",
            "Registrar la revalorización de aportes, la distribución de excedentes y la amortización que decide la asamblea, simularlas y aplicarlas con el acta; reversarlas enteras si hay que corregir.",
            [
                P("Captaciones y aportes → Asamblea", "Lista de decisiones por ejercicio y estado con base, fondo y lo aplicado. Sólo las ve quien tiene alcance total. «Ver» abre la decisión.", "/cartera/asamblea", "Abrir Asamblea"),
                P("Registrar decisión", "Clase, ejercicio, fondo aprobado en el acta, porcentaje o valor total decidido, base (la de la entidad, saldo promedio diario, saldo al 31 de diciembre o, en excedentes, la base subida por plantilla) y destino (aportes, cuenta de ahorro u orden de giro). Para corregir una reversada se indica cuál."),
                P("Base de los retornos por plantilla", "En excedentes, la base de los retornos la define la asamblea: después de registrar la decisión, suba la base de cada asociado con la plantilla «Base de los retornos de la asamblea» y vuelva a simular; quien no está en el archivo no participa.", "/cartera/plantillas", "Abrir Plantillas de Cartera"),
                P("Simular", "«Simular» muestra por asociado la base, lo calculado, lo aplicado, la retención y el destino, el límite del IPC y lo que queda en el fondo. «Descartar…» descarta el borrador con motivo."),
                P("Aplicar o reversar", "Otra persona que quien registró sube el acta firmada, indica el punto del acta cerrada y el motivo y elige «Aplicar con esta simulación». «Reversar entera…» con motivo deshace todo; la corrección es otra decisión."),
            ],
            ["asamblea", "revalorizacion de aportes", "excedentes", "retornos", "amortizacion de aportes", "ipc", "acta", "simular", "base de retornos", "retencion"],
            ["El IPC vigente en los valores nacionales y el acta de la asamblea en Actas.", "Para retener, los parámetros de retención de la asamblea con concepto y tarifa (vacíos no retienen).",
                "Permisos Lending.Assembly.Register para registrar y simular y Lending.Assembly.Approve para aplicar y reversar; alcance total."],
            ["captaciones-aportes", "cartera-plantillas", "cartera-valores-nacionales", "cartera-parametros"], [], TipoDeTema.Proceso));

        // Feature 014, US14 (T212): el fallecimiento del titular y su entrega. Detalle en
        // docs/manual/cartera-captaciones-asamblea-inactividad-y-fallecimiento.md §7.
        t.Add(Proceso("captaciones-fallecimientos", "Fallecimiento del titular", Modulos.Captaciones, "/cartera/fallecimientos",
            "Registrar el fallecimiento de un titular de cuentas de ahorro con el registro civil y entregar los saldos a los beneficiarios, sin juicio de sucesión o por la sucesión, con la aprobación de otra persona.",
            [
                P("Captaciones y aportes → Fallecimientos", "Lista de casos por estado con titular, fecha de defunción, ruta y lo entregado. «Ver» abre el caso.", "/cartera/fallecimientos", "Abrir Fallecimientos"),
                P("Registrar fallecimiento", "Titular fallecido, fecha de la defunción y el registro civil de defunción subido como soporte. Registrar bloquea todas las cuentas del titular y anula los retiros que esperaban aprobación."),
                P("Preparar la entrega", "En el caso se ven las cuentas y los beneficiarios vigentes en la fecha de la defunción. Se elige la ruta (a los beneficiarios, sin juicio de sucesión hasta el tope vigente, o por sucesión), los destinatarios con su porcentaje según el soporte, se suben los soportes requeridos, «Ver la vista previa» y «Registrar la entrega»."),
                P("Aprobar y entregar", "Una persona distinta de quien registró la entrega elige «Aprobar y entregar»: sale por orden de giro (sin cuenta de destino, a su cuenta registrada) o por traslado. «Anular un registro errado…» deshace un registro con motivo. Los aportes se devuelven por el retiro del asociado."),
            ],
            ["fallecimiento", "defuncion", "registro civil", "beneficiarios", "sucesion", "sin juicio de sucesion", "entrega", "titular fallecido", "bloqueo"],
            ["El tope de entrega sin juicio de sucesión registrado en los valores nacionales y los soportes que exige el parámetro de la cooperativa.",
                "Permisos Lending.DeceasedCases.Register para registrar y preparar y Lending.DeceasedCases.Approve para aprobar."],
            ["captaciones-cuentas", "captaciones-retiros-asociado", "captaciones-no-reclamados", "cartera-valores-nacionales"], [], TipoDeTema.Proceso));

        // Feature 014, US14 (T212): los saldos no reclamados. Detalle en docs/manual/cartera-captaciones-asamblea-inactividad-y-fallecimiento.md §6.
        t.Add(Proceso("captaciones-no-reclamados", "Saldos no reclamados", Modulos.Captaciones, "/cartera/no-reclamados",
            "Ver los saldos no reclamados —lo devuelto y no cobrado y el saldo de cuentas inactivas cuando el reglamento lo ordena—, trasladarlos a mano y pagarlos al titular que aparece.",
            [
                P("Captaciones y aportes → No reclamados", "Lista por estado (retenidos o pagados) con titular, origen, cuenta, valor y fecha. El proceso diario traslada solo lo que cumple el plazo de la cooperativa.", "/cartera/no-reclamados", "Abrir No reclamados"),
                P("Trasladar a no reclamados", "«Trasladar a no reclamados» recibe identificadores de cuentas inactivas (sólo si el reglamento lo ordena) y de órdenes de giro devueltas, uno por línea; es todo o nada."),
                P("Pagar al titular", "«Pagar…» con orden de giro a su cuenta de depósito registrada o traslado a una cuenta de ahorro suya, y motivo."),
            ],
            ["no reclamados", "saldos no reclamados", "cuentas abandonadas", "inactivas", "giro devuelto", "pagar al titular", "traslado"],
            ["Para las cuentas inactivas, el parámetro CuentasAbandonadas.TrasladoHabilitado encendido; para lo devuelto, Captaciones.DiasParaNoReclamados.",
                "Permisos Lending.Accounts.Read para consultar y Lending.Unclaimed.Manage para trasladar y pagar."],
            ["captaciones-cuentas", "captaciones-fallecimientos", "cartera-parametros"], [], TipoDeTema.Proceso));

        // Feature 014, US15 (T217): el fondo y el riesgo de liquidez. Detalle en docs/manual/cartera-captaciones-liquidez-e-informes.md §1–§5.
        t.Add(Proceso("captaciones-fondo-de-liquidez", "Fondo de liquidez", Modulos.Captaciones, "/cartera/fondo-de-liquidez",
            "Ver a una fecha el fondo de liquidez requerido frente al constituido, medir el riesgo de liquidez (IRL y brecha por bandas), registrar las inversiones del fondo y tomar las fotos con sus alertas.",
            [
                P("Captaciones y aportes → Fondo de liquidez", "Elija la fecha y «Actualizar»: la base por clase y banda, el porcentaje y el requerido según la categoría y el régimen vigente (con la transición), el constituido, la diferencia y la alerta (faltante, margen o al día).", "/cartera/fondo-de-liquidez", "Abrir Fondo de liquidez"),
                P("Riesgo de liquidez", "Elija «Indicador de riesgo de liquidez (IRL)» o «Brecha de liquidez»: entradas, salidas contractuales y no contractuales y lo acumulado por banda, con el IRL más bajo frente al mínimo de la entidad."),
                P("Inversiones del fondo", "«Registrar inversión» con instrumento, número del título o cuenta, valor, tipo de título, intereses causados, fechas y soporte; «Terminar…» la cierra desde una fecha con motivo."),
                P("Fotos", "«Tomar la foto de la fecha» guarda la medición con quién la tomó; se ven las de los últimos 90 días. Los depósitos asegurados por ahorrador están en los informes de Cartera."),
            ],
            ["fondo de liquidez", "liquidez", "irl", "brecha de liquidez", "riesgo de liquidez", "inversiones", "constituido", "requerido", "faltante", "foto"],
            ["Captaciones activadas y los parámetros del fondo y del riesgo de liquidez vigentes.",
                "Permisos Lending.LiquidityFund.View para consultar y Lending.LiquidityFund.Manage para registrar inversiones y tomar fotos."],
            ["captaciones-reportes-regulatorios", "cartera-informes", "cartera-parametros"], [], TipoDeTema.Proceso));

        // Feature 014, US17 (T222): SICSES de captaciones y aportes y exógena. Detalle en docs/manual/cartera-captaciones-liquidez-e-informes.md §7.
        t.Add(Proceso("captaciones-reportes-regulatorios", "Reportes regulatorios de captaciones", Modulos.Captaciones, "/cartera/captaciones/reportes-regulatorios",
            "Validar, generar, descargar y marcar cargados los formatos del SICSES de captaciones y aportes de un mes cerrado y los de la exógena de un año cerrado.",
            [
                P("Captaciones y aportes → Reportes regulatorios", "Elija el formato: SICSES de captaciones (9026), cotitulares (9068), aportes por asociado (9013), saldos diarios del ahorro ordinario (169), fondo de liquidez (9067), o exógena 1019 y 1020. Se ve el layout vigente y cuántos campos están cotejados; uno sin cotejar lo advierte en cada generación.", "/cartera/captaciones/reportes-regulatorios", "Abrir Reportes regulatorios"),
                P("Validar", "Año y mes (o sólo año en la exógena) y «Validar»: no guarda nada y lista las inconsistencias. La fecha de reporte es informativa."),
                P("Generar", "«Generar»: con inconsistencias bloqueantes queda una versión validada sin archivo, a corregir en los maestros o las cuentas; sin bloqueantes se reconocen las advertencias («Reconocer y generar»), que nunca van dentro del archivo. Regenerar deja la versión anterior reemplazada."),
                P("Descargar y marcar cargado", "En «Versiones generadas», «Descargar» y, después de cargarlo en el portal, «Marcar cargado» con el radicado, su fecha y el comprobante del cargue; el período queda cerrado."),
            ],
            ["sicses", "reporte regulatorio", "exogena", "1019", "1020", "9026", "9013", "supersolidaria", "dian", "radicado", "layout", "cotejar"],
            ["El mes (o el año) cerrado en Cartera.",
                "Permisos Lending.RegulatoryFiles.Read para ver y descargar, Lending.RegulatoryReports.Generate para validar y generar y Lending.RegulatoryFiles.MarkUploaded para marcar cargado."],
            ["captaciones-fondo-de-liquidez", "cartera-sicses-y-centrales"], [], TipoDeTema.Proceso));

        // Feature 014, decisión del dueño del 2026-10-18 (D53): los niveles de aprobación de los movimientos de cuentas sobre la atribución.
        t.Add(Proceso("captaciones-aprobaciones", "Aprobación de movimientos de cuentas", Modulos.Captaciones, "/cartera/captaciones/aprobaciones",
            "Fijar por versiones los niveles de aprobación de los retiros y de las consignaciones en efectivo que superan la atribución de quien los registra: cada nivel con su umbral y el permiso de quien aprueba.",
            [
                P("Captaciones y aportes → Aprobación de movimientos", "Lista de políticas por movimiento con versión, vigencia, niveles, motivo y autor. Sin política rige la regla fija de un nivel: lo aprueba otra persona con el mismo permiso y un monto que alcance.", "/cartera/captaciones/aprobaciones", "Abrir Aprobación de movimientos"),
                P("Nueva versión", "«Nueva versión»: el movimiento (retiro o consignación en efectivo sobre la atribución), desde cuándo rige, los niveles en orden con umbral y permiso de quien aprueba («Agregar nivel», «Quitar») y el motivo. Los umbrales no bajan; sin niveles, deja de exigir aprobación desde la fecha. Nada se edita en su sitio."),
                P("Montos de cada rol", "Los montos de la atribución de cada rol se fijan en Atribuciones. Quien aprueba debe tener en su alcance la oficina y la línea de la cuenta, y lo hace presente con su segundo factor.", "/cartera/atribuciones", "Abrir Atribuciones"),
            ],
            ["aprobacion", "atribucion", "niveles", "umbral", "retiro", "consignacion en efectivo", "aprobador", "politica", "segundo factor"],
            ["Permisos Lending.Attributions.View para consultar y Lending.Attributions.Manage para registrar versiones."],
            ["cartera-atribuciones", "captaciones-cuentas", "captaciones-cdat"], [], TipoDeTema.Maestro));

        // ------------------------------------------------------ Captaciones y aportes: puesta en marcha --
        // Feature 014, US9 (T159): saldos iniciales al corte y activación por naturaleza; E262 avisa cuántas cuentas migradas quedarían inactivas.
        // El procedimiento, en docs/operaciones/captaciones-puesta-en-marcha.md.
        t.Add(Proceso("captaciones-migracion", "Saldos iniciales de captaciones y aportes", Modulos.Captaciones, "/cartera/captaciones/migracion",
            "Cargar al corte los saldos iniciales de aportes o de captaciones, una naturaleza a la vez, por partes con su plantilla; aplicar la carga, cuadrarla contra Contabilidad, cotejarla con las cifras de referencia y aceptar las diferencias con acta.",
            [
                P("Captaciones y aportes → Saldos iniciales", "Elija la naturaleza (captaciones o aportes): lista de cargas con corte, estado, cuentas, plazo para activar y diferencias. «Carga nueva» pide la fecha de corte y notas; un borrador anterior queda reemplazado.", "/cartera/captaciones/migracion", "Abrir Saldos iniciales"),
                P("Partes", "Por parte se descarga la plantilla (encabezados en la fila 1) y se suben uno o varios archivos con su revisión previa por fila y columna, todo o nada; «Reemplazar la parte» deja sin efecto los anteriores. En aportes, «Tomar de los saldos cargados» llena la plantilla de cuentas. La revisión avisa cuántas cuentas migradas quedarían inactivas la primera noche tras activar: la cuenta migrada se mide como cualquier otra."),
                P("Aplicar la carga", "«Aplicar la carga…» valida entre partes y calcula el cuadre; los errores salen con tanda, fila y columna. Las cuentas nacen al activar, con un saldo inicial por concepto al corte y sin hechos contables."),
                P("Cuadre, cifras y diferencias", "«Cuadre» compara con Contabilidad por clase y cuentas contables; «Cifras de referencia» registra los totales por línea y concepto que entrega la cooperativa y los coteja con lo cargado; «Diferencias» se aceptan con el punto de un acta cerrada y motivo."),
            ],
            ["saldos iniciales", "migracion", "carga", "corte", "plantilla", "cuadre", "cifras de referencia", "diferencias", "acta", "puesta en marcha", "inactividad"],
            ["Contabilidad iniciada con su apertura al corte y las líneas de captación y aportes registradas.",
                "Permisos Lending.Deposits.Migration para cargar, Lending.Reconciliation.View para el cuadre y Lending.Migration.AcceptDifferences para aceptar diferencias."],
            ["captaciones-activacion", "captaciones-lineas", "cartera-migracion"], [], TipoDeTema.Proceso));

        // Feature 014, US9 (T159): la activación de aportes o de captaciones, cada una aparte y después de los créditos.
        t.Add(Proceso("captaciones-activacion", "Activación de captaciones y aportes", Modulos.Captaciones, "/cartera/captaciones/activacion",
            "Activar los aportes o las captaciones, cada naturaleza aparte y después de los créditos: revisar los requisitos y avisos, elegir el acta y hacer nacer las cuentas con su saldo inicial.",
            [
                P("Captaciones y aportes → Activación", "Elija la naturaleza: la tabla de requisitos con su estado, el detalle y quién lo arregla, y los avisos que no bloquean. Cada requisito pendiente lleva el enlace a la pantalla que lo resuelve.", "/cartera/captaciones/activacion", "Abrir Activación"),
                P("Requisitos", "Créditos de Cartera activados antes; carga aplicada y diferencias aceptadas (si hay carga); fecha en plazo; mes contable abierto (si está cerrado sólo avisa); un recaudador con el contrato de cuentas y el destino de Tesorería registrados (fuera de producción sirven los dobles de prueba); el perfil regulatorio que autoriza captar; la matriz contable para cada línea activa; los roles sugeridos de captaciones creados. Revise también las cuentas del concepto de nómina del descuento de aportes o ahorros.", "/admin/participantes-de-integracion", "Abrir Participantes de integración"),
                P("Activar", "Con carga, la fecha es el día siguiente al corte; sin carga (cooperativa sin historia) la elige quien activa. Elija el punto del acta de activación, el motivo y marque que revisó los avisos; «Activar…» y confirme. Si la carga sigue en borrador, primero se aplica en Saldos iniciales."),
                P("Lo que nació", "Al terminar se ve el resumen de lo nacido; los títulos de los CDAT migrados se emiten al activar las captaciones."),
            ],
            ["activacion", "activar captaciones", "activar aportes", "requisitos", "acta", "puesta en marcha", "recaudo", "tesoreria", "matriz contable", "perfil regulatorio"],
            ["Cartera de créditos activada antes (lo que a su vez exige los valores nacionales al día), la matriz contable de captaciones parametrizada con la contadora y Recaudo y Tesorería registrados. Todavía no se ha activado en ningún ambiente real por falta de estos tres.",
                "Permiso Lending.Deposits.Activate."],
            ["captaciones-migracion", "cartera-activacion", "contabilidad-cartera-matriz", "admin-participantes-de-integracion", "admin-perfil-regulatorio", "cartera-dobles-de-prueba"],
            [], TipoDeTema.Proceso));

        // --------------------------------------------------------------- Inventario --
        // Feature 012, US12 (T437): el rol vendedor pasó a Ventas; seguridad, aprobaciones, parámetros y alertas.
        t.Add(Maestro("/ventas/vendedores", "Vendedores", Modulos.Ventas, "un vendedor",
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
                P("Editar alcance", "En la fila del usuario, «Editar alcance» abre sus bodegas."),
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

        // Feature 012, US7 (T540): la integración de Inventario con Contabilidad —el lado contable (enmienda de la 009) y el de
        // Inventario—. Un tema por opción del menú.
        t.Add(Proceso("contabilidad-inventario-matriz", "Matriz contable de Inventario", Modulos.Contabilidad, "/contabilidad/inventario/matriz",
            "Con qué cuentas contabiliza Contabilidad cada operación de Inventario. Inventario no conoce cuentas: manda el grupo contable, la bodega, el medio de pago, la tarifa o la causa, y la regla más específica vigente a la fecha de la operación dice la cuenta.",
            [
                P("Contabilidad → Matriz contable", "Una pestaña por familia de rol: inventario y costo, ventas, compras, impuestos y retenciones, medios de pago, caja, traslados y puentes.", "/contabilidad/inventario/matriz", "Abrir la Matriz"),
                P("Nueva regla", "Operación, rol, las dimensiones que el rol exige (*) o admite, la cuenta (sólo auxiliares habilitadas para Inventario), la vigencia, notas y motivo. Al elegir la cuenta se ven sus reglas: tercero, cruce, centro, sucursal, base y tarifas."),
                P("Qué gana", "La más específica: bodega o punto de venta pesan 16, centro de costo 8, sucursal 4 y grupo 2."),
                P("Nueva versión o desactivar", "Una versión nueva cierra la vigente la víspera; no puede empezar en o antes del último mensaje ya contabilizado con esa clave (lo pasado se corrige con un comprobante manual). Desactivar cierra la vigencia con motivo; nunca borra."),
                P("Plantilla 16", "Se revisa sin guardar nada y se aplica todo o nada, con los errores por fila y columna. tarifaPorcentaje va en puntos (19 = 19 %)."),
            ],
            ["matriz", "cuentas", "inventario", "reglas", "plantilla 16", "grupo contable", "medio de pago", "tarifa"],
            ["Permiso Accounting.InventoryRules.View; para crear, versionar, desactivar o importar, Accounting.InventoryRules.Manage."],
            ["contabilidad-inventario-completitud", "contabilidad-inventario-tipos-de-comprobante", "plan-de-cuentas"], [], TipoDeTema.Proceso));

        t.Add(Proceso("contabilidad-inventario-tipos-de-comprobante", "Tipos de comprobante de Inventario", Modulos.Contabilidad, "/contabilidad/inventario/tipos-de-comprobante",
            "Qué tipo de comprobante y qué documento cruce usa cada operación de Inventario, en general o para un tipo de documento en particular. La semilla deja FV, EI, SI, NV, CP, TR, AC y CJ.",
            [
                P("Contabilidad → Tipos de comprobante (Inventario)", null, "/contabilidad/inventario/tipos-de-comprobante", "Abrir"),
                P("Cambiar", "Elegí el tipo de comprobante —sólo los de uso «Módulo» de Inventario y activos— y el cruce, con motivo. El cambio queda en la auditoría."),
            ],
            ["tipos de comprobante", "mapeo", "inventario", "documento cruce"],
            ["Permiso Accounting.InventoryRules.View; para cambiar, Accounting.InventoryRules.Manage."],
            ["contabilidad-inventario-matriz"], [], TipoDeTema.Maestro));

        t.Add(Proceso("contabilidad-inventario-completitud", "Completitud de la matriz", Modulos.Contabilidad, "/contabilidad/inventario/completitud",
            "Lo que le falta a la matriz a una fecha para que todo lo que Inventario usa se pueda contabilizar. Vacía, se pueden poner los tipos en línea.",
            [
                P("Contabilidad → Completitud de la matriz", "Elegí la fecha y «Consultar».", "/contabilidad/inventario/completitud", "Abrir la Completitud"),
                P("Las cinco listas", "Combinaciones en uso sin regla, medios de pago sin cuenta, reglas con una cuenta que ya no es elegible, tarifas distintas entre el catálogo y la cuenta, y operaciones sin tipo de comprobante. Cada fila lleva a donde se corrige."),
                P("Avisos", "No impiden contabilizar: un impuesto por unidad con una cuenta que exige base, o mercancía por facturar con una cuenta que exige cruce."),
            ],
            ["completitud", "matriz", "reglas faltantes", "puesta en marcha"],
            ["Permiso Accounting.InventoryRules.View."],
            ["contabilidad-inventario-matriz", "contabilidad-inventario-tipos-de-comprobante"], [], TipoDeTema.Consulta));

        t.Add(Proceso("contabilidad-inventario-lotes", "Lotes de contabilización de Inventario", Modulos.Contabilidad, "/contabilidad/inventario/lotes",
            "Los lotes con que Contabilidad recibe lo de Inventario que no pasa en línea —programados, al cierre de turno o de período, manuales, reprocesos y envíos posteriores— y el lote manual de un rango.",
            [
                P("Contabilidad → Lotes de contabilización", "Filtrá por fechas, estado y disparador; «Ver» abre el lote con sus comprobantes y sus rechazos.", "/contabilidad/inventario/lotes", "Abrir los Lotes"),
                P("Lote manual", "Pestaña «Lote manual»: rango y tipo de documento, «Previsualizar» muestra los comprobantes que se generarían y los documentos que se excluyen con quién los corrige. «Ordenar el lote», con motivo, procesa exactamente lo previsualizado en segundo plano."),
                P("Rechazos", "Cada rechazo dice quién lo corrige; corregido, se reprocesa desde la bandeja de mensajes de Inventario."),
            ],
            ["lotes", "contabilizacion", "inventario", "resumido", "lote manual", "rechazos"],
            ["Permiso Accounting.InventoryBatches.View; para ordenar, Accounting.InventoryBatches.Run."],
            ["inventario-bandeja-de-mensajes", "cierre-de-periodo"], ["/contabilidad/inventario/lotes/{Id}"], TipoDeTema.Proceso));

        // Feature 013, US7 (T0664–T0667): la integración de Cartera con Contabilidad —el lado contable—; la feature 014 suma a la misma matriz
        // las captaciones y los aportes (familias «Captaciones» y «Aportes y fondos», filtro «Naturaleza»). Un tema por opción del menú
        // Contabilidad › Cartera. El detalle, en docs/manual/cartera-integracion-contable.md y cartera-captaciones-integracion-contable.md.
        t.Add(Proceso("contabilidad-cartera-matriz", "Matriz contable de Cartera", Modulos.Contabilidad, "/contabilidad/cartera/matriz",
            "Con qué cuentas contabiliza Contabilidad cada hecho de Cartera, de Captaciones y de Aportes. Cartera no conoce cuentas: manda el hecho con sus dimensiones (línea, modalidad, categoría, concepto, clase de recaudador, motivo…) y la regla vigente de mayor peso a la fecha contable del hecho dice la cuenta de cada rol. La matriz empieza vacía: no se siembra ninguna regla.",
            [
                P("Contabilidad → Cartera → Matriz contable", "Elija la naturaleza (créditos, captaciones y aportes, o todas) y la familia de rol en los botones: cartera, intereses y deterioro; orden y castigada; recaudo, desembolso y Comercial; pasivos con el deudor; seguros y terceros; resultados; diferidos; contingentes; captaciones; aportes y fondos. Filtre por rol, cuenta y fecha, con «Sólo vigentes», y «Buscar».", "/contabilidad/cartera/matriz", "Abrir la Matriz"),
                P("Plantilla", "El botón «Plantilla» descarga la hoja vacía o con datos: las reglas vigentes y futuras y las combinaciones sugeridas sin cuenta, marcadas «pendiente de validar». Escriba en cada fila la cuenta auxiliar que decida la contadora; la que vuelva sin cuenta se ignora. En captaciones y aportes la cuenta del catálogo oficial viene sólo como sugerencia y no se aplica sola. Primero se revisa sin guardar nada (errores por fila y columna) y después se aplica todo o nada."),
                P("Nueva regla", "Rol, las dimensiones que el rol exige (*) y las que admite, centro de costo opcional, la cuenta (sólo auxiliares habilitadas para Cartera), vigencia, notas y motivo. Al elegir la cuenta se ven sus reglas (tercero, cruce, centro, sucursal). El hecho sólo se puede fijar en los roles de resultado; en los de saldo, orden, pasivo, puente, diferido y contingentes vale para todos."),
                P("Qué regla gana", "La vigente de mayor peso: línea de crédito 512, empleado 256, gracia 128, sub-segmento 64, reestructurado 32, por cuenta del asociado 16, sucursal 4 y hecho 2. Como son potencias de dos no hay empates. El centro de costo se asigna con la regla, pero no pesa. «¿A qué cuenta va?» resuelve una combinación a una fecha y muestra la regla ganadora y las candidatas."),
                P("Nueva versión o desactivar", "«Nueva versión» cierra la vigente la víspera de la fecha nueva, que no puede ser anterior al último hecho ya contabilizado con esa regla. «Desactivar» cierra la vigencia con motivo y nunca borra; «Versiones» muestra la historia. Lo ya contabilizado no cambia: Cartera no se reversa desde Comprobantes, se corrige con un comprobante nuevo de su propio hecho y fecha."),
                P("Si falta una regla", "El parámetro de Cartera «Falta de regla» arranca en «Bloquear»: sin regla, la operación no se confirma (el pago, la aprobación de la nómina con descuento de cartera o la tanda diaria se detienen) hasta completar la matriz. Con «Advertir» se aplica todo y la contadora recibe la alerta. Revise la Completitud antes de activar Cartera.", "/contabilidad/cartera/completitud", "Abrir la Completitud"),
            ],
            ["matriz", "cuentas", "cartera", "captaciones", "aportes", "reglas", "plantilla", "peso", "dimensiones", "vigencia", "a que cuenta va", "cuif", "bloquear"],
            ["Contabilidad iniciada con su plan de cuentas y las cuentas habilitadas para Cartera.", "Permiso Accounting.LendingRules.View; para crear, versionar, desactivar o importar, Accounting.LendingRules.Manage."],
            ["contabilidad-cartera-completitud", "contabilidad-cartera-tipos-de-comprobante", "contabilidad-cartera-lotes", "plan-de-cuentas", "cartera-parametros", "cartera-activacion"], [], TipoDeTema.Proceso));

        t.Add(Proceso("contabilidad-cartera-tipos-de-comprobante", "Tipos de comprobante de Cartera", Modulos.Contabilidad, "/contabilidad/cartera/tipos-de-comprobante",
            "Qué tipo de comprobante y qué documento cruce usa cada hecho de Cartera, en general o para una clase de recaudador en particular (gana la más específica). La semilla deja las filas «pendiente de validar» hasta que la contadora las confirme.",
            [
                P("Contabilidad → Cartera → Tipos de comprobante", "La tabla muestra hecho, clase de recaudador, tipo de comprobante, documento cruce, origen (semilla o de la cooperativa) y estado. Arriba se cuentan las filas pendientes de validar.", "/contabilidad/cartera/tipos-de-comprobante", "Abrir"),
                P("Validar o cambiar", "«Validar o cambiar…» o «Cambiar…» abre la fila: elija el tipo de comprobante —sólo los de uso «Módulo» de Cartera y activos— y el cruce, con motivo. Guardar una fila pendiente, aunque quede igual, la marca validada. El cambio queda en la auditoría."),
                P("Nuevo mapeo", "Para un hecho y una clase de recaudador que no tengan fila. Una fila sin clase vale para todo el hecho; una con clase, sólo para los recaudos de esa clase (por ejemplo, lo que recauda Nómina)."),
            ],
            ["tipos de comprobante", "mapeo", "cartera", "documento cruce", "clase de recaudador", "pendiente de validar", "desembolso", "recaudo"],
            ["Tipos de comprobante de uso «Módulo» para Cartera creados y activos.", "Permiso Accounting.LendingRules.View; para cambiar, Accounting.LendingRules.Manage."],
            ["contabilidad-cartera-matriz", "contabilidad-cartera-completitud", "comprobante-contable"], [], TipoDeTema.Maestro));

        t.Add(Proceso("contabilidad-cartera-completitud", "Completitud de la matriz de Cartera", Modulos.Contabilidad, "/contabilidad/cartera/completitud",
            "Lo que le falta a la matriz a una fecha para que todo lo que Cartera, Captaciones y Aportes usan se pueda contabilizar. Vacía, la matriz está completa y nada se detiene por falta de regla.",
            [
                P("Contabilidad → Cartera → Completitud de la matriz", "Elija la fecha y la naturaleza (créditos, captaciones y aportes, o todas) y «Consultar». Arriba, los totales por lista.", "/contabilidad/cartera/completitud", "Abrir la Completitud"),
                P("Las cuatro listas", "Combinaciones en uso sin regla (con las líneas que la usan, el último uso y la cuenta sugerida), clases de recaudador sin cuenta puente, reglas con una cuenta que ya no es elegible y hechos sin tipo de comprobante. Cada fila dice quién lo corrige y lleva a la pantalla: «Crear la regla», «Plan de cuentas» o «Mapear»."),
                P("Avisos", "No impiden contabilizar, pero conviene leerlos: un puente cuya cuenta exige documento cruce, una cuenta de la causación resumida que exige tercero o cruce (obliga a una línea por crédito y por día) o una naturaleza de cuenta distinta de la del rol."),
            ],
            ["completitud", "matriz", "reglas faltantes", "cuenta puente", "recaudador", "cuenta no elegible", "puesta en marcha", "cartera"],
            ["Permiso Accounting.LendingRules.View."],
            ["contabilidad-cartera-matriz", "contabilidad-cartera-tipos-de-comprobante", "plan-de-cuentas", "cartera-activacion"], [], TipoDeTema.Consulta));

        t.Add(Proceso("contabilidad-cartera-lotes", "Lotes de contabilización de Cartera", Modulos.Contabilidad, "/contabilidad/cartera/lotes",
            "Los lotes con que Contabilidad recibe los hechos de Cartera que no pasan en línea —la causación diaria, el cierre mensual, los programados, los manuales, los reprocesos y los envíos posteriores— y el lote manual de un rango de fechas.",
            [
                P("Contabilidad → Cartera → Lotes de contabilización", "Pestaña «Lotes»: filtre por fechas, estado y disparador y «Buscar». Se resaltan los atrasados y los que tienen rechazos. «Ver» abre el lote con sus totales, sus comprobantes (por hecho o resumidos), sus rechazos y sus hechos con los créditos de cada uno.", "/contabilidad/cartera/lotes", "Abrir los Lotes"),
                P("Lote manual", "Pestaña «Lote manual»: rango de fechas, horario y sucursal; «Previsualizar» muestra los comprobantes que se generarían, con sus líneas, y los hechos que se excluyen con quién los corrige. «Ordenar el lote», con motivo, procesa exactamente lo previsualizado en segundo plano; cada comprobante lleva la fecha contable de sus hechos, nunca la del lote."),
                P("Rechazos", "Cada rechazo dice qué pasa y quién lo corrige. «Mensajes del lote» y «Mensaje» llevan a la bandeja de mensajes de Cartera, desde donde se reprocesa una vez corregido."),
                P("Cierre del mes", "Con Cartera activada, Contabilidad no cierra un mes que Cartera no cerró (o cuyo cierre se reabrió): primero se cierra Cartera, se procesa el lote del cierre y después se cierra el período contable.", "/contabilidad/periodos", "Abrir Períodos"),
            ],
            ["lotes", "contabilizacion", "cartera", "causacion", "cierre mensual", "resumido", "lote manual", "rechazos", "reproceso", "previsualizar"],
            ["Permiso Accounting.LendingBatches.View; para ordenar, Accounting.LendingBatches.Run; para abrir los mensajes, Lending.Messages.Read."],
            ["cartera-bandeja-de-mensajes", "cartera-cierres", "contabilidad-periodos", "contabilidad-cartera-matriz"], ["/contabilidad/cartera/lotes/{Id:guid}"], TipoDeTema.Proceso));

        // Feature 013, US7 (T0667): el patrimonio técnico y la relación de solvencia que registra la contadora y que Cartera lee para sus límites.
        t.Add(Proceso("contabilidad-indicadores-regulatorios", "Indicadores regulatorios", Modulos.Contabilidad, "/contabilidad/indicadores-regulatorios",
            "Registrar con su soporte el patrimonio técnico y la relación de solvencia de cada corte. Cartera usa el último vigente de cada tipo para sus límites (por ejemplo, el cupo individual y la concentración).",
            [
                P("Contabilidad → Cartera → Indicadores regulatorios", "Filtre por tipo y fecha y «Consultar». La lista muestra corte, valor, fuente, soporte, hasta cuándo vale y si está vigente; el vigente de cada tipo va resaltado. Un indicador vale los meses que diga el parámetro de vigencia desde su corte; sin ese parámetro ninguno se juzga vencido.", "/contabilidad/indicadores-regulatorios", "Abrir Indicadores"),
                P("Registrar indicador", "Tipo, fecha de corte, valor (el patrimonio técnico en pesos; la solvencia en porcentaje, 12 = 12 %), fuente y notas. Suba primero el soporte en el mismo diálogo y elíjalo de la lista; queda bloqueado con el indicador."),
                P("Corregir", "Un indicador no se edita: «Reemplazar…» registra otro del mismo tipo y corte, y el anterior queda en la historia como reemplazado. «Descargar» baja el soporte."),
            ],
            ["indicadores regulatorios", "patrimonio tecnico", "relacion de solvencia", "solvencia", "soporte", "limites", "cupo individual", "concentracion", "vigencia"],
            ["Permiso Accounting.RegulatoryIndicators.View; para registrar o reemplazar, Accounting.RegulatoryIndicators.Manage."],
            ["cartera-cupos", "cartera-informes-y-tablero", "admin-perfil-regulatorio"], [], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-bandeja-de-mensajes", "Bandeja de mensajes", Modulos.Inventario, "/inventario/bandeja-de-mensajes",
            "Lo que Inventario le mandó a Contabilidad y qué pasó con cada mensaje, en orden de emisión: pendiente, en lote, procesado con su comprobante, rechazado con el motivo, o «no aplica».",
            [
                P("Inventario → Bandeja de mensajes", "Filtros por estado, destino, tipo, documento, fechas y validación previa; los contadores de arriba filtran por estado.", "/inventario/bandeja-de-mensajes", "Abrir la Bandeja"),
                P("Ver", "El contenido del mensaje tal como se emitió, cada intento de entrega, de qué depende y quién lo espera. «Espera al mensaje X» dice qué tiene que pasar antes."),
                P("Reprocesar", "Marcá los rechazados, ya corregidos, y «Reprocesar» con motivo: arrastra a los que esperaban y cada uno se contabiliza con su fecha original."),
                P("Enviar lo que no aplicaba", "Para un tipo que antes no pasaba y ahora sí: rango, vista previa con toda la clausura de dependencias y «Enviar» con motivo."),
            ],
            ["mensajes", "bandeja", "contabilidad", "rechazados", "reprocesar", "no aplica", "validacion previa"],
            ["Permiso Inventory.Messages.View; reprocesar, Inventory.Messages.Reprocess; enviar después, Inventory.Messages.SendNotApplicable."],
            ["contabilidad-inventario-lotes", "inventario-conciliacion"], [], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-conciliacion", "Conciliación con Contabilidad", Modulos.Inventario, "/inventario/conciliacion",
            "El inventario valorizado contra el saldo contable, por conjunto de cuentas, a una fecha, con lo que explica la diferencia.",
            [
                P("Inventario → Conciliación con Contabilidad", "Elegí la fecha y «Conciliar»; Excel o PDF con Inventory.Reports.Export.", "/inventario/conciliacion", "Abrir la Conciliación"),
                P("Las cinco secciones", "Conjuntos de cuentas (valorizado, saldo, diferencia, pendientes, en lote, rechazados y sin explicar), detalle por bodega, lo movido por tipos que no pasan, ventas a crédito de esos tipos y bodegas todavía fuera del módulo con sus cifras."),
                P("Profundizar", "Una fila con mensaje abre ese mensaje en la bandeja."),
            ],
            ["conciliacion", "valorizado", "saldo contable", "diferencia", "cuadre"],
            ["Permisos Inventory.Reports.View e Inventory.Reconciliation.View."],
            ["inventario-bandeja-de-mensajes", "inventario-activacion"], [], TipoDeTema.Reporte));

        t.Add(Proceso("inventario-activacion", "Activación de bodegas", Modulos.Inventario, "/inventario/activacion",
            "Pasar una bodega del sistema anterior al módulo nuevo con cuadre contable: el valorizado de su conjunto de cuentas contra el saldo contable al corte.",
            [
                P("Inventario → Activación de bodegas", "Elegí la bodega y la fecha de corte.", "/inventario/activacion", "Abrir la Activación"),
                P("Revisar", "Por conjunto: saldo contable, esta bodega, las activas que comparten cuentas y, aparte, las que siguen fuera del módulo con sus cifras al corte; la diferencia y los mensajes que la explican. Los bloqueos dicen qué falta."),
                P("Activar", "Sin diferencia, «Activar la bodega». Con diferencia, aceptarla con motivo exige Inventory.Warehouses.AcceptActivationDifference."),
            ],
            ["activar", "bodega", "puesta en marcha", "cuadre", "cifras de referencia", "corte"],
            ["Permiso Inventory.Warehouses.Activate.", "Saldo inicial confirmado y cifras de referencia de las bodegas que comparten cuentas."],
            ["inventario-saldo-inicial", "inventario-cifras-de-referencia", "inventario-conciliacion"], [], TipoDeTema.Proceso));

        // Feature 012, I1 (T983): un tema por opción del menú de decisiones-transversales §2.11. Los catálogos simples
        // comparten CatalogoDeInventario (Nuevo, Editar, Inactivar/Reactivar con motivo, Plantilla) y por eso su guía.
        t.Add(CatalogoDeInventario("/inventario/categorias", "Categorías", Modulos.Inventario, "una categoría",
            "Árbol de hasta 5 niveles con su ruta legible («ALIMENTOS › GRANOS»). Cambiar el padre mueve la rama entera; una categoría no queda debajo de sí misma. Con subcategorías o productos activos no se inactiva. Plantilla 5.",
            "categorias", "arbol", "subcategoria", "plantilla 5"));
        t.Add(CatalogoDeInventario("/inventario/marcas", "Marcas", Modulos.Inventario, "una marca",
            "La marca entra a la búsqueda de sus productos. Se puede inactivar aunque tenga productos activos: ellos la conservan. Plantilla 4.",
            "marcas", "plantilla 4"));
        t.Add(CatalogoDeInventario("/inventario/unidades", "Unidades de medida", Modulos.Inventario, "una unidad",
            "Cada unidad lleva sus decimales y el código UN/ECE que exige la factura electrónica. Los decimales sólo bajan si ninguna cantidad registrada usa más; una unidad de un producto activo no se inactiva; las sembradas se corrigen pero no se borran. Plantilla 3.",
            "unidades", "medida", "decimales", "un/ece", "plantilla 3"));
        t.Add(CatalogoDeInventario("/inventario/grupos-contables", "Grupos contables", Modulos.Inventario, "un grupo contable",
            "Inventario no conoce cuentas: envía el grupo y la matriz de Contabilidad lo traduce. Por eso el código no cambia nunca. Un grupo con productos inventariables activos no se inactiva. Plantilla 2.",
            "grupos contables", "matriz", "cuentas", "plantilla 2"));
        t.Add(CatalogoDeInventario("/inventario/causas-de-ajuste", "Causas de ajuste", Modulos.Inventario, "una causa de ajuste",
            "Las causas de los ajustes y bajas. El código lo usa la matriz contable y no cambia; las que usa el sistema («diferencia de conteo», «reclamación al transportador») no se inactivan.",
            "causas", "ajuste", "baja", "motivo"));
        t.Add(CatalogoDeInventario("/ventas/canales", "Canales de venta", Modulos.Ventas, "un canal de venta",
            "Los canales que usan los tipos de documento, los puntos de venta y las listas de precios. Uno en uso por alguno de ellos vigente no se inactiva.",
            "canales", "venta", "punto de venta", "lista de precios"));

        t.Add(Proceso("inventario-productos", "Productos", Modulos.Inventario, "/inventario/productos",
            "El catálogo de productos: se busca con el lector o por filtros, se crea, se corrige en su detalle y cambia de estado con motivo. Un producto es inventariable, servicio, combo, kit o plantilla de variantes, y puede controlar lote, vencimiento o serie (ver «Variantes, combos, kits, lotes y series»).",
            [
                P("Inventario → Productos", "Filtrá o leé el código de barras para ir directo al detalle.", "/inventario/productos", "Abrir Productos"),
                P("Nuevo producto", "Código (hasta 20, en mayúsculas, único), nombre, clase (inventariable, servicio, combo, kit o plantilla), unidad base, grupo contable (no en la plantilla), concepto de retención (no en la plantilla ni en el combo), IVA y, si aplica, «Controla lote», «Controla vencimiento» (exige lote) y «Controla serie»."),
                P("Detalle", "Pestañas Datos («Se compra» y «Se vende» dicen dónde se ofrece), Unidades (el factor dice cuántas unidades base tiene cada empaque: caja × 12 = 12), Códigos de barras (el de un empaque elige esa unidad al leerlo), Impuestos, Imágenes y Grupo contable. Con movimientos, la unidad base, el factor de una unidad usada y el grupo no cambian por Datos."),
                P("Cambiar de grupo contable", "Pestaña Grupo contable, «Cambiar grupo»: fecha efectiva y motivo; lo confirmado antes conserva su grupo. Pide el permiso Inventory.Catalog.ReclassifyAccountingGroup."),
                P("Estado o borrar", "«Estado» pasa a activo, inactivo (no se ofrece en documentos nuevos) o bloqueado (ningún movimiento salvo el conteo y la recepción de lo que ya viajaba), con motivo. «Borrar» sólo sirve para un producto que nunca estuvo en un documento."),
                P("Plantilla 6", "«Plantilla» descarga, revisa y aplica todo el catálogo por archivo: se revisa primero y se aplica todo o nada."),
            ],
            ["productos", "codigo de barras", "lector", "unidad base", "empaque", "iva", "bloquear", "inactivar", "plantilla 6"],
            ["Permiso Inventory.Catalog.View; para crear y corregir, Inventory.Catalog.Manage.", "Unidades, categorías, marcas y grupos contables cargados."],
            ["inventario-grupos-contables", "inventario-unidades", "inventario-plantillas", "inventario-catalogo-avanzado"], ["/inventario/productos/{Id}"], TipoDeTema.Maestro));

        // Feature 012, I6 (T936–T941): el catálogo avanzado.
        t.Add(Proceso("inventario-catalogo-avanzado", "Variantes, combos, kits, lotes y series", Modulos.Inventario, "/inventario/productos",
            "Una plantilla genera variantes (talla, color…) con código, códigos de barras y existencia propios; un combo se vende como una línea y descuenta sus componentes; un kit se arma con un ensamble; y un producto puede llevar lote, vencimiento o serie.",
            [
                P("Atributos", "En el detalle de una plantilla, pestaña Variantes, «Atributos…»: cada atributo (TALLA, COLOR) con sus valores y su orden. Un código que ya usan variantes no cambia; el nombre sí.", "/inventario/productos", "Abrir Productos"),
                P("Generar variantes", "Marque los atributos y los valores: la vista previa muestra cada combinación con su código y nombre propuestos (se pueden cambiar, y agregar un código de barras). «Generar» crea sólo las que faltan; cada variante hereda categoría, marca, unidades, grupo contable, impuestos y seguimiento. La plantilla no entra a documentos: se vende una variante."),
                P("Componentes", "En un combo o un kit, pestaña Componentes: agregue los productos y la cantidad por unidad (en la unidad base de cada uno) y «Guardar componentes». El cambio rige para lo que se venda o ensamble después."),
                P("Ensamblar kits", "Ajustes → «Nuevo ajuste» con un tipo de clase Ensamble: elija el kit y cuántos, revise los componentes que saldrán con su disponible y «Proponer líneas». Al confirmar salen los componentes y entra el kit al costo de lo consumido.", "/inventario/ajustes/nuevo", "Nuevo ajuste"),
                P("Lotes y vencimientos", "En una entrada (ajuste positivo, recepción) cada línea pide el lote y, si el producto lo controla, el vencimiento. En una salida (ajuste, traslado, venta) se propone el lote que vence primero; los vencidos y próximos a vencer se marcan, y vender uno vencido lo bloquea o lo avisa según el parámetro Ventas.LoteVencido."),
                P("Series", "Cada serie es una unidad: se lee con el lector en la línea (o en el POS, después de leer el producto). Una serie que ya está en existencia no vuelve a entrar, ni sale una que no está en la bodega."),
                P("Consultar", "Existencias muestra la cantidad por ubicación y lote, y cada lote abre su kardex; el Kardex filtra por lote y muestra el lote o la serie de cada movimiento.", "/inventario/existencias", "Abrir Existencias"),
            ],
            ["variantes", "talla", "color", "plantilla", "combo", "kit", "ensamble", "componentes", "lote", "vencimiento", "serie", "fefo"],
            ["Permiso Inventory.Catalog.View; para crear y cambiar, Inventory.Catalog.Manage. Lotes y series: Inventory.Stock.View."],
            ["inventario-productos", "inventario-ajustes", "inventario-existencias", "inventario-kardex", "inventario-conteos"], ["/inventario/productos/{Id}"], TipoDeTema.Maestro));

        t.Add(Proceso("inventario-bodegas", "Bodegas", Modulos.Inventario, "/inventario/bodegas",
            "Las bodegas del alcance agrupadas por sucursal. Toda bodega nace sin activar —sólo admite su saldo inicial— con su ubicación general; la primera bodega operativa de una sucursal trae su bodega de tránsito, que no se crea a mano.",
            [
                P("Inventario → Bodegas", "«Ver inactivas» trae también las retiradas. Una bodega nueva sólo la ve quien tiene alcance total hasta que se la asignen.", "/inventario/bodegas", "Abrir Bodegas"),
                P("Nueva bodega", "Código, nombre y sucursal. Si la sucursal no tiene municipio, la pantalla avisa por la ReteICA."),
                P("Detalle", "Pestañas Datos, Ubicaciones (una por defecto; ninguna con existencia se inactiva), Mínimos y máximos (por producto: 0 ≤ mínimo ≤ punto de reorden ≤ máximo) y Saldo inicial y activación."),
                P("Activar", "Desde «Saldo inicial y activación»: compara el saldo inicial con el saldo contable y con las cifras de referencia. Si la contabilidad no está iniciada o no responde la consulta de saldos, en producción la bodega no se activa: la pantalla lo dice como bloqueo (ver «Activación de bodegas»)."),
                P("Plantilla 7", "Carga bodegas y ubicaciones por archivo."),
            ],
            ["bodegas", "ubicaciones", "transito", "activar", "minimos", "maximos", "reorden", "plantilla 7"],
            ["Permiso Inventory.Warehouses.View; para crear y corregir, Inventory.Warehouses.Manage; para activar, Inventory.Warehouses.Activate.", "Sucursales creadas."],
            ["inventario-saldo-inicial", "inventario-alcances", "sucursales"], ["/inventario/bodegas/{Id}"], TipoDeTema.Maestro));

        t.Add(Proceso("inventario-tipos-de-documento", "Tipos de documento de inventario", Modulos.Inventario, "/inventario/tipos-de-documento",
            "Los tipos de documento por grupo y clase. La clase es fija del sistema y decide el efecto; cada tipo elige su clase al crearse y parametriza su consecutivo, los campos que exige, las bodegas y el canal. La semilla deja uno por clase al iniciar el módulo.",
            [
                P("Inventario → Tipos de documento", null, "/inventario/tipos-de-documento", "Abrir Tipos de documento"),
                P("Nuevo tipo", "Clase, código, nombre, consecutivo y los campos que exige (tercero, centro de costo, referencia externa, motivo, fecha futura…). La clase y el código no se cambian después."),
                P("Ver", "El historial de consecutivos, el modo de paso a contabilidad y la política de aprobación vigentes, estos dos de sólo lectura: se cambian en Parámetros y en Políticas de aprobación."),
                P("Cambiar prefijo o número", "Es un consecutivo nuevo con motivo que cierra el anterior la víspera de la fecha elegida; el número no puede quedar en o bajo uno ya emitido con ese prefijo. Las clases que numera la DIAN no tienen consecutivo propio."),
                P("Inactivar", "Con motivo. No se inactiva con borradores o documentos en aprobación, ni el último activo de una clase que el sistema genera solo."),
            ],
            ["tipos de documento", "clase", "consecutivo", "prefijo", "numeracion", "plantilla 8"],
            ["Permiso Inventory.DocumentTypes.View; para cambiar, Inventory.DocumentTypes.Manage."],
            ["inventario-parametros", "inventario-politicas-de-aprobacion"], [], TipoDeTema.Maestro));

        t.Add(Proceso("inventario-existencias", "Existencias", Modulos.Inventario, "/inventario/existencias",
            "Cuánto hay de un producto y dónde, en tres pasos: leerlo, ver sus bodegas y ubicaciones con lo que va en tránsito, y abrir su kardex. La existencia es la suma del kardex, no un saldo guardado.",
            [
                P("Inventario → Existencias", "Leé o escribí el producto, o filtrá la lista por bodega, «Sólo con existencia» o «En o bajo el punto de reorden».", "/inventario/existencias", "Abrir Existencias"),
                P("Leer las columnas", "Física, reservada, disponible y en tránsito hacia la bodega; mínimo, punto de reorden y posición cuando la bodega tiene política. Promedio y valor sólo aparecen con Inventory.Costs.Read."),
                P("Detalle", "El código del producto abre sus bodegas y ubicaciones —con el lote, que se filtra y abre su kardex— y el enlace a su kardex."),
            ],
            ["existencias", "stock", "disponible", "transito", "reservado", "reorden"],
            ["Permiso Inventory.Stock.View. Sólo se ven las bodegas del alcance."],
            ["inventario-kardex", "inventario-integridad"], [], TipoDeTema.Consulta));

        t.Add(Proceso("inventario-kardex", "Kardex", Modulos.Inventario, "/inventario/kardex",
            "Los movimientos de un producto en orden: la primera fila es el saldo al día anterior a «desde» y cada línea lleva su documento, entrada, salida y saldo.",
            [
                P("Inventario → Kardex", "Leé o escribí el producto, elegí la bodega, el lote si hace falta y el rango (hasta 5 años) y «Consultar». «Mostrar ajustes de costo» agrega esas líneas; con lote, sólo sus movimientos y su saldo.", "/inventario/kardex", "Abrir Kardex"),
                P("Leer", "Costo y valor llegan vacíos sin Inventory.Costs.Read, y la nota lo dice. El número del documento abre su pantalla."),
                P("Exportar", "Excel o PDF con Inventory.Reports.Export."),
                P("Desde un comprobante contable", "«Ver en Inventario» en un comprobante abre el documento que lo originó en su pantalla; si esa clase de documento no tiene pantalla propia, muestra lo esencial y remite a este kardex."),
            ],
            ["kardex", "movimientos", "saldo", "entradas", "salidas", "costo promedio", "ver en inventario"],
            ["Permiso Inventory.Reports.View."],
            ["inventario-existencias", "inventario-informes", "comprobante-contable"], ["/inventario/documentos/{Id:guid}"], TipoDeTema.Consulta));

        t.Add(Proceso("inventario-ajustes", "Ajustes de inventario", Modulos.Inventario, "/inventario/ajustes",
            "Ajustes positivos y negativos, consumos internos y bajas, con su causa y sus soportes. Lo confirmado no se edita ni se borra: se anula, y la anulación es un documento contrario.",
            [
                P("Inventario → Ajustes", "Filtros por clase, estado, bodega, fecha y número.", "/inventario/ajustes", "Abrir Ajustes"),
                P("Nuevo ajuste", "Tipo, bodega, fecha, causa, centro de costo, motivo y líneas (el lector llena producto y empaque). El costo sólo se digita en un ajuste positivo y con Inventory.Adjustments.SetUnitCost.", "/inventario/ajustes/nuevo", "Nuevo ajuste"),
                P("Guardar y revisar", "El borrador se guarda con avisos, que se marcan en la línea que nombran. Ctrl+S guarda; Ctrl+Enter confirma."),
                P("Lote, serie y ensamble", "Un producto con lote o serie los pide en la línea (en la salida se propone el lote que vence primero). Un tipo de clase Ensamble muestra el formulario del kit: kit, cantidad y «Proponer líneas»."),
                P("Soportes", "Actas de destrucción, denuncias y demás se suben mientras el ajuste está en borrador o en aprobación."),
                P("Confirmar", "Si la política o el monto lo piden, queda en aprobación, congelado y sin número, hasta la última aprobación. Las acciones posibles las dice el servidor."),
                P("Descartar o anular", "Descartar un borrador y anular uno confirmado piden motivo."),
            ],
            ["ajustes", "baja", "consumo interno", "causa", "anular", "soportes", "aprobacion"],
            ["Permiso Inventory.Adjustments.View; para crear, Inventory.Adjustments.Create.", "Causas de ajuste creadas y la bodega activa."],
            ["inventario-causas-de-ajuste", "inventario-aprobaciones", "inventario-kardex"], ["/inventario/ajustes/nuevo", "/inventario/ajustes/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-traslados", "Traslados entre bodegas", Modulos.Inventario, "/inventario/traslados",
            "El traslado en dos pasos: el origen despacha y la mercancía queda en tránsito; el destino recibe lo que llegó. Faltantes y sobrantes quedan como diferencias que otra persona aprueba.",
            [
                P("Inventario → Traslados", "Pestaña Traslados (los de las bodegas cuyo origen o destino ve) y pestaña de diferencias.", "/inventario/traslados", "Abrir Traslados"),
                P("Nuevo traslado", "Tipo, origen de su alcance, destino entre todas las bodegas operativas y activas, fecha, motivo y líneas.", "/inventario/traslados/nuevo", "Nuevo traslado"),
                P("Despachar", "Si la política lo pide queda en aprobación; lo despacha la última aprobación."),
                P("Recibir", "Cantidad que llegó por línea, ubicación de llegada y sobrante declarado. Lo que no llega queda como faltante en tránsito."),
                P("Resolver una diferencia", "«Resolver»: salida según el tipo, cantidad, causa y motivo; queda en aprobación de otra persona."),
                P("Anular", "Un despacho no recibido se anula con motivo: la mercancía vuelve del tránsito al origen."),
            ],
            ["traslados", "despacho", "recibir", "transito", "faltante", "sobrante", "diferencias"],
            ["Permiso Inventory.Transfers.View; para crear, Inventory.Transfers.Create.", "Las dos bodegas activas."],
            ["inventario-existencias", "inventario-aprobaciones"], ["/inventario/traslados/nuevo", "/inventario/traslados/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-conteos", "Conteos físicos", Modulos.Inventario, "/inventario/conteos",
            "El conteo de toda una bodega o cíclico (por categorías, ubicaciones, productos o clase ABC), ciego o no, con su foto, la captura con lector —por lote cuando el producto lo controla— y el ajuste que aprueba alguien que no abrió ni contó.",
            [
                P("Inventario → Conteos", "Los conteos del alcance con su estado; el valor de la diferencia con Inventory.Costs.Read.", "/inventario/conteos", "Abrir Conteos"),
                P("Nuevo conteo", "Bodega, alcance (con «Clase ABC» se elige A, B o C: la lista sale al abrir, con el valor de lo que salió de la bodega en los doce meses anteriores), si es ciego y los contadores; «Crear el conteo»."),
                P("Abrir (tomar la foto)", "Congela el teórico a esa hora. Con conteos abiertos con foto el mes no se cierra."),
                P("Capturar", "Código y Enter: cada lectura suma y se envía por tandas; una cantidad negativa corrige. Con lote, escriba el lote antes de leer: acompaña las lecturas hasta que se cambie. En un conteo ciego quien sólo captura no ve el teórico."),
                P("Cerrar el conteo", "Compara, exige el reconteo de las líneas marcadas y numera."),
                P("Generar ajuste", "Muestra la fecha del ajuste (la de la foto o la de la aprobación, según el parámetro) y lo crea en aprobación."),
            ],
            ["conteo", "inventario fisico", "ciclico", "ciego", "lector", "foto", "reconteo", "diferencias"],
            ["Permiso Inventory.Counts.View; para crear y abrir, Inventory.Counts.Open."],
            ["inventario-ajustes", "inventario-aprobaciones", "inventario-periodos"], ["/inventario/conteos/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-integridad", "Integridad del kardex", Modulos.Inventario, "/inventario/integridad",
            "Comprueba que existencias y costo sean la suma exacta del kardex y, si no, lo reconstruye sin tocar un solo hecho. La misma verificación corre cada noche.",
            [
                P("Inventario → Integridad del kardex", "Elegí bodegas y productos; sin elegir, se verifica todo lo del alcance.", "/inventario/integridad", "Abrir Integridad"),
                P("Verificar", "Cada diferencia con lo esperado, lo real y la diferencia. Con diferencias se levanta la alerta de integridad."),
                P("Reconstruir", "Recalcula las proyecciones desde el kardex, con motivo y el permiso Inventory.Integrity.Rebuild. Sin filtros puede tardar minutos."),
            ],
            ["integridad", "verificar", "reconstruir", "proyecciones", "kardex"],
            ["Permiso Inventory.Integrity.Verify."],
            ["inventario-kardex", "inventario-alertas"], [], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-periodos", "Períodos de inventario", Modulos.Inventario, "/inventario/periodos",
            "Los meses de inventario desde la puesta en marcha. Se cierran en orden; al cerrar queda fijado el valorizado por grupo contable y bodega. El valorizado a cualquier fecha se consulta en la misma pantalla.",
            [
                P("Inventario → Períodos", "Los meses aparecen desde el corte del primer saldo inicial.", "/inventario/periodos", "Abrir Períodos"),
                P("Revisar cierre", "Bloquea un mes que no terminó o con conteos abiertos con foto; avisan los borradores, lo que está en aprobación, los traslados sin recibir y los mensajes pendientes."),
                P("Cerrar el mes", "Con avisos, marcá «Reconozco los avisos y cierro de todos modos». Pide Inventory.Periods.Close."),
                P("Reabrir", "Sólo el último mes cerrado, con motivo y el permiso especial Inventory.Periods.Reopen."),
                P("Valorizado a una fecha", "Fecha, «Incluir tránsito» y «Consultar»; Excel para exportarlo. Los valores, con Inventory.Costs.Read."),
            ],
            ["periodos", "cierre", "mes", "reabrir", "valorizado", "corte"],
            ["Permiso Inventory.Periods.View."],
            ["inventario-conteos", "inventario-informes"], [], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-saldo-inicial", "Saldo inicial de inventario", Modulos.Inventario, "/inventario/saldo-inicial",
            "El saldo inicial bodega por bodega con la plantilla 14: se revisa sin guardar nada, se aplica todo o nada y queda en borradores por bodega fechados en su corte, que se confirman con aprobación de otra persona.",
            [
                P("Inventario → Saldo inicial", null, "/inventario/saldo-inicial", "Abrir Saldo inicial"),
                P("Importar la plantilla", "«Revisar» da los errores por fila y columna y el resumen por bodega (líneas, cantidad y valor por grupo contable); «Aplicar» crea los borradores, partidos en tramos de 4.000 líneas."),
                P("Confirmar", "Al elegir un documento, las acciones son las que manda el servidor: confirmar (normalmente queda en aprobación), descartar o anular con motivo mientras la bodega no opera."),
                P("Activar la bodega", "Desde el detalle de la bodega, pestaña «Saldo inicial y activación».", "/inventario/bodegas", "Abrir Bodegas"),
            ],
            ["saldo inicial", "apertura", "plantilla 14", "puesta en marcha", "corte", "activar"],
            ["Permiso Inventory.OpeningBalance.Load.", "Productos y bodegas cargados; conteo físico de la bodega antes de la carga."],
            ["inventario-bodegas", "inventario-cifras-de-referencia", "inventario-plantillas"], [], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-cifras-de-referencia", "Cifras de referencia", Modulos.Inventario, "/inventario/cifras-de-referencia",
            "Las existencias y valores de referencia a una fecha, sólo para comparar: nunca mueven el inventario. Cada importación es un lote; importar otra vez una fecha y bodega deja el anterior como historia.",
            [
                P("Inventario → Cifras de referencia", null, "/inventario/cifras-de-referencia", "Abrir Cifras de referencia"),
                P("Importar la plantilla 15", "Se revisa y se aplica todo o nada. Un código que no está en el catálogo nuevo no es error: queda sin resolver, con su grupo contable."),
                P("Leer", "Por fecha, bodega y grupo contable, y los códigos sin producto en el catálogo. Los sin resolver suman al valorizado de su grupo pero no al comparativo por producto."),
                P("Comparar", "Los comparativos por producto y bodega están en el centro de informes de Inventario.", "/inventario/informes", "Abrir Informes"),
            ],
            ["referencia", "comparativo", "cifras", "plantilla 15", "marcha paralela"],
            ["Permiso Inventory.LegacyFigures.Import."],
            ["inventario-saldo-inicial", "inventario-informes"], [], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-informes", "Informes de inventario", Modulos.Inventario, "/inventario/informes",
            "El centro de informes del módulo. El selector trae las vistas que publica cada parte (kardex, existencias, valorizado, documentos, reorden y quiebres, comparativos con las cifras de referencia, entre otras) y aparecen aquí sin tocar la pantalla.",
            [
                P("Inventario → Informes", "Elegí la vista; se editan el rango o el corte, la bodega y los filtros propios de la vista.", "/inventario/informes", "Abrir Informes"),
                P("Consultar y profundizar", "La fila de un documento abre el documento; la de un producto o una bodega, su kardex."),
                P("Exportar", "Excel, Word o PDF con Inventory.Reports.Export; las vistas con datos de clientes piden además Inventory.Reports.ExportPersonalData."),
            ],
            ["informes", "reportes", "valorizado", "documentos", "reorden", "quiebres", "exportar", "margen", "rotacion", "abc", "sin movimiento",
             "por vencer", "sugerido de compras", "faltantes"],
            ["Permiso Inventory.Reports.View."],
            ["inventario-kardex", "inventario-tablero", "centro-de-reportes"], [], TipoDeTema.Reporte));

        // I6, US17 (T969): el tablero (§28).
        t.Add(Proceso("inventario-tablero", "Tablero de inventario", Modulos.Inventario, "/inventario/tablero",
            "Las cifras clave del inventario y del comercio en una pantalla, por sucursal y bodega: valor del inventario, rotación y días, margen del mes, ventas del día y del mes frente al período anterior, reorden, quiebres, próximos a vencer, mensajes, lotes, documentos DIAN, tipos fiscales sin paso a contabilidad y alertas. Cada ficha lleva a su detalle con un clic.",
            [
                P("Inventario → Tablero", "Filtrá por sucursal o bodega (sólo las de su alcance) y la fecha de corte.", "/inventario/tablero", "Abrir el tablero"),
                P("Leer las fichas", "El borde dice la severidad: amarillo pide atención, rojo es un problema (quiebres, rechazos, margen negativo). Las ventas muestran el período anterior debajo."),
                P("Ir al detalle", "Un clic en la ficha abre el informe o la bandeja con los mismos filtros: valorizado, rotación, margen por categoría, ventas por caja, reorden, por vencer, mensajes, lotes, documentos electrónicos, parámetros o alertas."),
                P("Tipos de alerta sin destinatario", "Si una alerta se levantó sin nadie activo con su permiso, el aviso lo dice: asigne el permiso destinatario del tipo en Alertas."),
            ],
            ["tablero", "indicadores", "kpi", "rotacion", "margen", "ventas del dia", "quiebres", "vencer", "sin destinatario"],
            ["Permiso Inventory.Dashboard.View; las fichas de valor y margen, además Inventory.Costs.Read; mensajes y lotes, Inventory.Messages.View; DIAN, ElectronicInvoicing.Documents.View; alertas, Inventory.Alerts.View; tipos sin paso, Inventory.DocumentTypes.View."],
            ["inventario-informes", "inventario-alertas"], [], TipoDeTema.Consulta));

        t.Add(Proceso("inventario-plantillas", "Plantillas de parametrización", Modulos.Inventario, "/inventario/plantillas",
            "Las dieciséis plantillas de la puesta en marcha en su orden de carga: cada una sólo cita lo que cargaron las anteriores. Las que todavía no se importan dicen desde qué entrega.",
            [
                P("Inventario → Plantillas", "Cada fila dice lo que usted puede hacer con ella (descargar, importar).", "/inventario/plantillas", "Abrir Plantillas"),
                P("Descargar", "«Plantilla vacía» o «Plantilla con los datos actuales». Los encabezados van en la fila 1."),
                P("Revisar", "Errores por hoja, fila y columna, sin guardar nada. «Revisión en Excel» devuelve el mismo libro con el resultado de cada fila."),
                P("Aplicar", "Todo o nada: con un error no se guarda ninguna fila. Un código que ya existe se actualiza con las mismas reglas que la edición; la plantilla nunca borra."),
            ],
            ["plantillas", "importar", "excel", "carga masiva", "parametrizacion", "puesta en marcha"],
            ["Permiso Inventory.Catalog.View; importar cada plantilla pide el permiso de su catálogo."],
            ["inventario-productos", "inventario-saldo-inicial"], [], TipoDeTema.Proceso));

        // ------------------------------------------------------------------ Compras --
        t.Add(Proceso("compras-compra-directa", "Compra directa", Modulos.Compras, "/compras/compra-directa",
            "La compra en tres pasos —abrir, leer los productos y confirmar— que crea y confirma la recepción y la factura del proveedor en una sola operación. Un doble clic no compra dos veces.",
            [
                P("Compras → Compra directa", null, "/compras/compra-directa", "Abrir Compra directa"),
                P("Abrir la compra", "Proveedor, bodega y la factura del proveedor: prefijo, número, CUFE, emisión, vencimiento y forma de pago."),
                P("Leer los productos", "Con el lector; el empaque leído es la unidad."),
                P("Confirmar la compra", "Impuestos y retenciones los calcula el servidor con el catálogo vigente a la fecha. El resultado muestra los dos números o, si la política o el monto lo piden, la recepción en aprobación con la factura en borrador enlazada."),
            ],
            ["compra", "proveedor", "recepcion", "factura", "lector", "impuestos", "retenciones"],
            ["Permiso Inventory.Purchases.Confirm.", "Proveedor registrado (o se crea con la autorización de datos) y la bodega activa."],
            ["compras-recepciones", "compras-facturas-proveedor", "inventario-aprobaciones"], [], TipoDeTema.Proceso));

        t.Add(Proceso("compras-recepciones", "Recepciones de compra", Modulos.Compras, "/compras/recepciones",
            "La entrada de mercancía del proveedor a una bodega, con lo que falta facturar y lo devuelto por documento.",
            [
                P("Compras → Recepciones", "Filtros por proveedor, estado, bodega, fechas y número.", "/compras/recepciones", "Abrir Recepciones"),
                P("Nueva recepción", "Tipo, bodega que recibe, fecha, proveedor, remisión o guía, municipio de la operación (se propone el de la sucursal) y líneas leídas con el lector. Cada guardado trae la vista previa de impuestos.", "/compras/recepciones/nueva", "Nueva recepción"),
                P("Confirmar", "Si la política lo pide queda en aprobación, congelada y sin número."),
                P("Después", "La recepción confirmada muestra por línea lo que falta facturar y lo devuelto, y sus facturas y devoluciones."),
                P("Anular", "Con motivo: crea el documento contrario al costo con que entró. Con factura o devolución vigente no se anula; anúlelas primero."),
            ],
            ["recepcion", "entrada", "compra", "remision", "proveedor", "pendiente por facturar"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create."],
            ["compras-facturas-proveedor", "compras-devoluciones", "compras-compra-directa"], ["/compras/recepciones/nueva", "/compras/recepciones/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("compras-facturas-proveedor", "Facturas de proveedor", Modulos.Compras, "/compras/facturas-proveedor",
            "La factura del proveedor contra lo que falta facturar de sus recepciones y los servicios sin recepción, con impuestos, retenciones y el seguimiento de los eventos RADIAN.",
            [
                P("Compras → Facturas de proveedor", "Filtros por proveedor, estado, forma de pago, eventos pendientes, fechas y número.", "/compras/facturas-proveedor", "Abrir Facturas"),
                P("Registrar factura", "Proveedor, número y CUFE; se puede prellenar desde el XML del proveedor (se lee y se descarta, no se guarda). Si el NIT del XML no existe, se crea el proveedor con la autorización de datos.", "/compras/facturas-proveedor/nueva", "Nueva factura"),
                P("Facturar lo pendiente de la recepción", "Trae las líneas no facturadas. Impuestos y retenciones con su explicación; las diferencias de costo con la recepción, con Inventory.Costs.Read."),
                P("Confirmar", "Queda la foto de impuestos y retenciones."),
                P("Eventos RADIAN", "Acuse (030) y recibo del bien (032): «Emitir acuse y recibo» los transmite por el canal de facturación electrónica (Inventory.Purchases.EmitRadianEvent). Uno emitido fuera del ERP se registra con dónde y cuándo (Inventory.Purchases.RegisterRadianEvent). Mientras el canal sea el simulado, no llegan a la DIAN."),
                P("Anular el registro", "Con motivo; con notas vigentes no se anula y los eventos RADIAN no cambian."),
            ],
            ["factura de proveedor", "cufe", "xml", "radian", "acuse", "retenciones", "iva"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create."],
            ["compras-recepciones", "compras-notas-proveedor", "compras-cruce", "maestros-impuestos", "facturacion-electronica"], ["/compras/facturas-proveedor/nueva", "/compras/facturas-proveedor/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("compras-notas-proveedor", "Notas de proveedor", Modulos.Compras, "/compras/notas-proveedor",
            "Las notas crédito y débito del proveedor contra una factura confirmada suya, por valor o por cantidad, línea por línea.",
            [
                P("Compras → Notas de proveedor", null, "/compras/notas-proveedor", "Abrir Notas"),
                P("Nueva nota", "Factura, clase, líneas y si cambia el costo de lo recibido. «Guardar y calcular impuestos» trae los impuestos y retenciones de la foto de la factura en proporción."),
                P("Confirmar la nota", "La nota crédito no pasa de lo que queda de la factura."),
                P("Anular", "Con Inventory.Purchases.Void."),
            ],
            ["nota credito", "nota debito", "proveedor", "descuento", "devolucion en valor"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create."],
            ["compras-facturas-proveedor"], [], TipoDeTema.Proceso));

        t.Add(Proceso("compras-devoluciones", "Devoluciones a proveedor", Modulos.Compras, "/compras/devoluciones",
            "La devolución se arma desde las líneas de una recepción confirmada con lo devolvible (recibido menos lo ya devuelto) y sale al costo con que entró.",
            [
                P("Compras → Devoluciones", null, "/compras/devoluciones", "Abrir Devoluciones"),
                P("Nueva devolución", "Elegí la recepción; las cantidades van en la unidad de la recepción. «Guardar» deja el borrador con sus avisos."),
                P("Confirmar la devolución", "La diferencia entre el costo de entrada y el promedio la registra el sistema como ajuste de costo."),
            ],
            ["devolucion", "proveedor", "recepcion", "costo"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create."],
            ["compras-recepciones"], [], TipoDeTema.Proceso));

        // ----------------------------------------------------- Compras completas y costeo avanzado (feature 012, I5) --
        t.Add(Proceso("compras-solicitudes", "Solicitudes de compra", Modulos.Compras, "/compras/solicitudes",
            "Lo que una dependencia necesita, para cuándo y quién lo pide. La solicitud no mueve inventario: deja constancia de la necesidad hasta que se ordena.",
            [
                P("Compras → Solicitudes", "Filtros por estado, fechas y número.", "/compras/solicitudes", "Abrir Solicitudes"),
                P("Nueva solicitud", "Tipo, bodega que la necesita, para cuándo, quién la pide y los productos leídos con el lector (el empaque leído es la unidad).", "/compras/solicitudes/nueva", "Nueva solicitud"),
                P("Enviar a aprobación", "La política del tipo decide si queda en aprobación; sin política se confirma."),
                P("Crear orden", "Confirmada, cada línea muestra lo pendiente por ordenar y «Crear orden» abre la orden con ese pendiente."),
            ],
            ["solicitud", "requisicion", "necesidad", "compra", "pendiente por ordenar"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create."],
            ["compras-ordenes", "inventario-aprobaciones"], ["/compras/solicitudes/nueva", "/compras/solicitudes/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("compras-ordenes", "Órdenes de compra", Modulos.Compras, "/compras/ordenes",
            "La orden al proveedor con precios, entrega esperada y condiciones de pago. Se aprueba por la política del tipo y por el límite de monto de quien confirma, se envía al proveedor en PDF y se recibe en una o varias entregas.",
            [
                P("Compras → Órdenes de compra", "Filtros por proveedor, estado, fechas y número.", "/compras/ordenes", "Abrir Órdenes"),
                P("Nueva orden", "Proveedor, bodega que recibe, entrega esperada, condiciones y líneas con precio. Desde una solicitud llega prellenada con lo pendiente por ordenar.", "/compras/ordenes/nueva", "Nueva orden"),
                P("Confirmar", "Si el monto pasa el límite de quien confirma o la política lo pide, queda en aprobación y la pantalla dice por qué."),
                P("Ver PDF y enviar al proveedor", "El PDF de la orden confirmada; el envío va al correo del proveedor en el maestro o al que se indique."),
                P("Recibir contra esta orden", "Abre la recepción prellenada con lo pendiente por recibir de cada línea. Lo recibido de más sólo entra dentro de la tolerancia de la cooperativa."),
                P("Cerrar saldo", "Con motivo, cierra lo pendiente por recibir: la orden ya no admite recepciones."),
            ],
            ["orden de compra", "pedido", "proveedor", "pdf", "pendiente por recibir", "aprobacion por monto"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create; confirmar, enviar y cerrar el saldo, Inventory.Purchases.Confirm."],
            ["compras-solicitudes", "compras-recepciones", "compras-cruce"], ["/compras/ordenes/nueva", "/compras/ordenes/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("compras-cruce", "Cruce a tres vías", Modulos.Compras, "/compras/cruce",
            "Al confirmar una factura de proveedor cuyas recepciones vienen de una orden, cada línea se compara con lo ordenado y lo recibido, en cantidad y en precio, con las tolerancias vigentes. Lo que excede queda retenido hasta que se aprueba.",
            [
                P("Compras → Cruce a tres vías", "Las líneas retenidas, aprobadas y rechazadas con lo ordenado, recibido y facturado, los precios, las diferencias y la tolerancia que se usó. Los precios se ven con el permiso de costos.", "/compras/cruce", "Abrir el cruce"),
                P("Decidir", "Una línea retenida se aprueba o rechaza en Inventario → Aprobaciones. Aprobada la última, la factura se confirma y la diferencia de precio ajusta el costo de lo que sigue en existencia y el costo de venta de lo vendido; rechazada, la factura vuelve a borrador.", "/inventario/aprobaciones", "Abrir Aprobaciones"),
                P("Facturado de más", "Facturar más de lo recibido nunca entra por tolerancia: se corrige la factura o se registra otra recepción."),
                P("En la factura", "La pestaña «Cruce» de la factura de proveedor muestra su cruce línea por línea; el informe «Cruce de compras a tres vías» lo exporta."),
            ],
            ["cruce", "tres vias", "tolerancia", "diferencia de precio", "retenida", "factura de proveedor"],
            ["Permiso Inventory.Purchases.View; decidir, el permiso del nivel de aprobación. Las tolerancias se registran en Parámetros de inventario (Compras.Tolerancia*)."],
            ["compras-ordenes", "compras-facturas-proveedor", "inventario-aprobaciones", "inventario-parametros"], [], TipoDeTema.Proceso));

        t.Add(Proceso("compras-costos-adicionales", "Costos adicionales de compra", Modulos.Compras, "/compras/costos-adicionales",
            "El flete o el seguro de una compra se reparte entre lo recibido por valor, cantidad, peso, volumen o a mano. Lo que sigue en existencia sube de costo y lo ya vendido va a costo de venta.",
            [
                P("Compras → Costos adicionales", null, "/compras/costos-adicionales", "Abrir Costos adicionales"),
                P("Repartir un flete o seguro", "La factura del servicio (confirmada), las recepciones que cubre, el método y el monto (por defecto lo que queda sin repartir de esa factura).", "/compras/costos-adicionales/nueva", "Repartir"),
                P("Calcular el reparto", "Una fila por línea de recepción con la base, lo asignado y el residuo del redondeo; con el permiso de costos, la porción a inventario y a costo de venta."),
                P("Confirmar", "Ajusta el costo en el kardex sin reescribir las recepciones. Una recepción con costos adicionales vigentes no se anula."),
            ],
            ["flete", "seguro", "prorrateo", "costos adicionales", "landed cost", "reparto"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create; confirmar, Inventory.Purchases.Confirm."],
            ["compras-recepciones", "compras-facturas-proveedor", "inventario-kardex"], ["/compras/costos-adicionales/nueva", "/compras/costos-adicionales/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("inventario-costeo-avanzado", "Costeo: PEPS, fechas anteriores y cambio de método", Modulos.Inventario, "/inventario/parametros",
            "La cooperativa elige promedio ponderado o PEPS y el ámbito del costo. Con fechas anteriores permitidas, un documento puede registrarse antes de movimientos ya confirmados: el costo de esos documentos se ajusta sin reescribirlos.",
            [
                P("Ver el impacto antes de confirmar", "En recepciones, facturas de proveedor, ajustes y devoluciones, si la fecha es anterior al último movimiento de algún producto, la pantalla lo avisa y «Ver impacto en costos» muestra los documentos afectados con la porción a inventario y a costo de venta."),
                P("Cambiar el método o el ámbito", "En Parámetros de inventario, «Cambiar…» en Costeo.Metodo o Costeo.Ambito: el primer día de un período abierto, sin movimientos desde ese día, con justificación y acta. No se deshace.", "/inventario/parametros", "Abrir Parámetros"),
                P("Comparar los dos valorizados", "El informe «Valorizado por cambio de método» muestra por grupo contable el valor por promedio ponderado y por PEPS.", "/inventario/informes?vista=method-change-valuation", "Abrir el informe"),
                P("En el kardex", "«Motivo» distingue los ajustes de costo (retroactivo, diferencia de precio, costos adicionales, cambio de método) y, en PEPS, «Capas consumidas» dice de qué entradas salió cada salida.", "/inventario/kardex", "Abrir el kardex"),
            ],
            ["peps", "fifo", "promedio ponderado", "retroactivo", "fecha anterior", "cambio de metodo", "capas", "impacto en costos"],
            ["Ver el impacto, Inventory.Costs.Read; cambiar el método, Inventory.Parameters.Manage e Inventory.Costing.Manage."],
            ["inventario-parametros", "inventario-kardex", "inventario-informes"], [], TipoDeTema.Proceso));

        // ------------------------------------------------- Ventas y punto de venta (feature 012, I3, T644) --
        t.Add(Proceso("ventas-pos", "Vender en el punto de venta", Modulos.Ventas, "/pos",
            "La venta en caja, pensada para el lector y el teclado: cada lectura suma una línea, la venta vive en el servidor desde la primera y se cobra con uno o varios medios. Un doble clic o una conexión que se cae no venden dos veces.",
            [
                P("Punto de venta → Vender en el POS", "Hace falta una sesión de caja abierta; si no la tiene, la pantalla lleva a abrirla. Un punto que cobra en oficina no ofrece el POS.", "/pos", "Abrir el POS"),
                P("Leer los productos", "El campo de lectura siempre tiene el foco. Enter lee el código exacto (el de un empaque trae su unidad); «3*» antes de leer multiplica; un código desconocido abre la búsqueda (F2)."),
                P("Cambiar la venta", "F3 cantidad de la línea elegida (flechas para elegir), F4 cliente (se busca o se crea con su autorización de datos; con «el comprador pide factura» pasa a factura), F6 vendedor, F7 descuento o precio digitado, Supr quita la línea."),
                P("Descuento sobre su tope", "La línea queda esperando la aprobación de alguien con un tope suficiente: desde su bandeja, o en persona en la caja con su llave (passkey) o el código de su autenticador. Nunca con contraseña."),
                P("Cobrar (F10)", "El panel muestra lo que falta, lo que sobra y las vueltas. La tecla o el botón de un medio agrega un pago por lo que falta. De la tarjeta sólo se digitan los últimos cuatro dígitos; el datáfono se propone el de la caja. F10 otra vez confirma."),
                P("Entregar", "La tirilla sale en la respuesta del cobro y se imprime; si la venta quedó en aprobación, se espera y se entrega al aprobarse. Ctrl+Alt+R reimprime la última con la marca «COPIA»."),
                P("Suspender y recuperar", "F8 suspende con un rótulo; F9 la recupera en cualquier caja del punto. Ctrl+Alt+D descarta la venta con motivo."),
            ],
            ["pos", "punto de venta", "caja", "lector", "cobrar", "tirilla", "suspender", "vueltas", "bono", "descuento", "atajos"],
            ["Permiso Inventory.Pos.Sell.", "Sesión de caja abierta en un punto con POS.", "Medios de pago, lista de precios general y la caja con su tipo de venta POS."],
            ["ventas-caja", "ventas-medios-de-pago", "ventas-precios", "ventas-puntos-de-venta"], [], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-caja", "Caja: sesiones, movimientos y cierre", Modulos.Ventas, "/pos/sesiones",
            "La caja se abre con su base, recibe movimientos (retiros, ingresos de base, reclasificaciones), se cierra con arqueo por medio de pago y el día de cada punto se consolida en su cierre del día.",
            [
                P("Punto de venta → Sesiones de caja", "Su sesión abierta arriba; si no tiene, elija punto y caja libre y cuente la base por denominaciones. Una caja tiene una sola sesión abierta y un cajero una sola.", "/pos/sesiones", "Abrir Sesiones"),
                P("Movimientos de caja", "Retiro parcial a caja fuerte, traslado a otra caja, retiro para consignar, ingreso de base o reclasificación de un pago entre medios (Visa a Mastercard sin tocar la venta). Si su política lo pide quedan en aprobación. Cada uno tiene su comprobante con firmas.", "/pos/movimientos-de-caja", "Abrir Movimientos"),
                P("Cerrar la caja", "Cada medio se cuenta como dice su forma de arqueo: el efectivo por denominaciones, las tarjetas por el lote de cada datáfono, los que se cotejan por referencia marcándolas. Con arqueo ciego no se ve lo esperado. Una diferencia fuera de la tolerancia pide motivo; el retiro de cierre lleva el efectivo a donde se diga."),
                P("Cierre del día", "Con todas las sesiones del día cerradas, consolida por medio, adquirente y datáfono. Reabrirlo pide motivo y el siguiente es otra versión.", "/pos/cierre-del-dia", "Abrir Cierre del día"),
            ],
            ["caja", "sesion", "base", "arqueo", "denominaciones", "datafono", "lote", "retiro", "caja fuerte", "cierre del dia", "sobrante", "faltante"],
            ["Permisos Inventory.CashSessions.View, .Open y .Close; Inventory.CashMovements.Create y .Approve; Inventory.DayClose.Execute y .Reopen."],
            ["ventas-pos", "ventas-medios-de-pago"], ["/pos/sesiones/{Id}", "/pos/sesiones/{Id}/cierre"], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-medios-de-pago", "Medios de pago", Modulos.Maestros, "/maestros/medios-de-pago",
            "El catálogo de cómo se paga: efectivo, tarjetas, transferencias, bonos, créditos… con su forma de captura, su arqueo, su tolerancia y dónde se ofrece cada uno. También las franquicias, los adquirentes, los datáfonos de cobro y las denominaciones del efectivo.",
            [
                P("Maestros → Medios de pago", "Pestañas Medios, Franquicias, Adquirentes, Datáfonos de cobro y Denominaciones.", "/maestros/medios-de-pago", "Abrir Medios de pago"),
                P("Un medio", "Código, clase, tecla rápida del POS, código DIAN; si pide referencia y cuál (el número del bono, con referencia única, no se puede usar dos veces); si da vueltas o admite pago parcial; cómo se arquea y con qué tolerancia; la comisión esperada; en los créditos, plazo, cuotas y periodicidad."),
                P("Dónde se ofrece", "En todos los puntos, canales y tipos de venta, o sólo en los elegidos. Los créditos sólo aparecen con un cliente identificado y a quien tenga el permiso de vender a crédito."),
                P("Cambios", "Editar un medio pide motivo y queda en su historia. Plantilla 11 para cargarlos en bloque."),
            ],
            ["medios de pago", "tarjeta", "franquicia", "adquirente", "datafono", "denominaciones", "bono", "credito", "tolerancia", "plantilla 11"],
            ["Permiso Core.PaymentMeans.View; para cambiar, Core.PaymentMeans.Manage."],
            ["ventas-pos", "ventas-caja"], [], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-puntos-de-venta", "Puntos de venta y cajas", Modulos.Ventas, "/ventas/puntos-de-venta",
            "Cada punto es de una sucursal, vende por un canal y tiene su bodega por defecto; dice si usa el POS o cobra en oficina. Sus cajas tienen bodega, datáfono propuesto, formato de impresión y los tipos de documento de sus seis roles.",
            [
                P("Ventas → Puntos de venta", "La lista con sus cajas y la sesión abierta de cada una.", "/ventas/puntos-de-venta", "Abrir Puntos de venta"),
                P("Nueva caja", "Código, nombre, bodega de la sucursal del punto, datáfono por defecto, tirilla de 58 u 80 mm o carta, copias y el tipo de cada rol: venta POS, factura a pedido del comprador, nota de ajuste POS, nota crédito de factura y los dos de contingencia."),
                P("Plantilla 10", "Carga puntos y cajas en bloque."),
            ],
            ["punto de venta", "caja", "tirilla", "roles", "datafono", "plantilla 10"],
            ["Permiso Inventory.PointsOfSale.View; para cambiar, Inventory.PointsOfSale.Manage."],
            ["ventas-pos", "ventas-caja"], [], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-precios", "Listas de precios y topes de descuento", Modulos.Ventas, "/ventas/listas-de-precios",
            "El precio de venta sale de la lista más específica vigente para el cliente, su segmento, el canal y la sucursal; sin ninguna, de la general. Cada rol tiene su descuento máximo por línea y por total.",
            [
                P("Ventas → Listas de precios", "Cada lista tiene ámbito (vacío es la general) y vigencia; el ámbito, el código y si incluye impuestos no cambian.", "/ventas/listas-de-precios", "Abrir Listas"),
                P("Precios de una lista", "Se agregan o cambian por producto y unidad y se guardan juntos con un motivo. Abajo, «Consultar el precio que gana» dice el precio, la lista que ganó y las candidatas."),
                P("Topes de descuento", "Por rol, en porcentaje de la línea y del total, con vigencia. A quien tiene varios roles le aplica el mayor; sobre su tope, el descuento pide aprobación.", "/ventas/topes-de-descuento", "Abrir Topes"),
                P("Plantillas 12 y 13", "Listas con sus precios y topes en bloque."),
            ],
            ["precios", "lista de precios", "descuento", "tope", "segmento", "canal", "plantilla 12", "plantilla 13"],
            ["Permiso Inventory.Prices.View; para cambiar listas, Inventory.Prices.Manage; topes, Inventory.DiscountCaps.Manage."],
            ["ventas-pos", "ventas-oficina"], ["/ventas/listas-de-precios/{Id}"], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-oficina", "Ventas de oficina: facturas, notas y entrega", Modulos.Ventas, "/ventas/documentos",
            "La consulta de las ventas y sus notas, la factura de oficina con pagos múltiples, la nota crédito sobre una venta confirmada, la entrega de la tirilla o la carta y la reimpresión con «COPIA».",
            [
                P("Ventas → Documentos de venta", "Filtros por clase, estado, fechas, cliente, número y pendientes de entrega.", "/ventas/documentos", "Abrir Documentos"),
                P("Nueva factura", "Tipo, bodega, cliente, vendedor y productos; el precio sale de la lista que gana. Guardar trae el total; después se registran los pagos (los que se arquean van a su sesión de caja) y se confirma.", "/ventas/facturas/nueva", "Nueva factura"),
                P("Nota crédito", "Desde la venta confirmada («Nota crédito»): por cantidad (con devolución a una bodega) o por valor, o la anulación total, con los reintegros por medio de pago."),
                P("Entregar y reimprimir", "La primera entrega sale sin marca (tirilla, carta en PDF o por correo); las siguientes son reimpresiones con «COPIA», auditadas."),
                P("Anular", "En un documento electrónico (hoy todas las facturas y notas), «Anular» emite en una sola confirmación la nota crédito total o la nota de ajuste del documento equivalente. En uno que no es electrónico, crea el documento contrario con su propia fecha, con motivo (Inventory.Sales.Void)."),
            ],
            ["factura", "venta", "nota credito", "devolucion", "reintegro", "entregar", "reimprimir", "copia"],
            ["Permiso Inventory.Sales.View; para registrar, Inventory.Sales.Create; confirmar, Inventory.Sales.Confirm."],
            ["ventas-pos", "ventas-precios", "ventas-informes"], ["/ventas/documentos/{Id}", "/ventas/notas-credito/nueva"], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-informes", "Informes de ventas", Modulos.Ventas, "/ventas/informes",
            "Ventas por sesión, por caja y por medio de pago, la sesión de caja, el cierre del día, los pagos con tarjeta, los movimientos de caja, las diferencias de arqueo, los bonos redimidos, las aprobaciones de descuento y los indicios de deterioro.",
            [
                P("Ventas → Informes de ventas", "Elija el informe, el rango y el punto; los filtros propios de cada informe aparecen solos.", "/ventas/informes", "Abrir Informes"),
                P("Exportar", "Excel, PDF o Word con Inventory.Reports.Export; los que traen datos de clientes piden además Inventory.Reports.ExportPersonalData."),
            ],
            ["informes", "ventas", "caja", "arqueo", "tarjetas", "bonos", "deterioro", "exportar"],
            ["Permiso Inventory.Reports.View."],
            ["ventas-oficina", "ventas-caja"], [], TipoDeTema.Proceso));

        // Feature 012, I6 (T894–T901, T983): el ciclo comercial y las promociones.
        t.Add(Proceso("ventas-cotizaciones", "Cotizaciones", Modulos.Ventas, "/ventas/cotizaciones",
            "La cotización ofrece precios con vigencia sin mover la existencia. Confirmada y vigente, se convierte en pedido.",
            [
                P("Ventas → Cotizaciones", "«Nueva cotización»: tipo, bodega, cliente, vendedor, vigencia y productos. El precio sale de la lista que gana y las promociones vigentes se ven en cada línea.", "/ventas/cotizaciones", "Abrir Cotizaciones"),
                P("Confirmar", "Exige la vigencia; no reserva ni emite nada."),
                P("Convertir en pedido", "Crea el borrador del pedido con las líneas y precios cotizados. Se puede convertir más de una vez (pedidos parciales); vencida, ya no."),
                P("Anular", "Con motivo; no tiene efecto sobre la existencia."),
            ],
            ["cotizacion", "vigencia", "convertir en pedido", "precio", "promocion"],
            ["Permiso Inventory.Sales.View; para registrar, Inventory.Sales.Create; confirmar, Inventory.Sales.Confirm; anular, Inventory.Sales.Void."],
            ["ventas-pedidos", "ventas-promociones", "ventas-oficina"], [], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-pedidos", "Pedidos y reservas", Modulos.Ventas, "/ventas/pedidos",
            "El pedido confirmado reserva lo pedido en la bodega hasta la fecha de vencimiento de la reserva; lo reservado deja de estar disponible para otras ventas.",
            [
                P("Ventas → Pedidos", "Cada línea muestra físico, reservado y disponible en la bodega del pedido. Si no alcanza el disponible, no se confirma.", "/ventas/pedidos", "Abrir Pedidos"),
                P("La reserva", "Vence en la fecha que se ve en el pedido y se libera sola; la remisión o la factura desde el pedido la consumen."),
                P("Remisionar o facturar", "Desde el pedido confirmado: «Remisionar» despacha sin facturar y «Facturar» factura lo pendiente."),
                P("Anular", "Libera lo que siga reservado; lo ya remisionado o facturado queda como está."),
            ],
            ["pedido", "reserva", "disponible", "remisionar", "facturar"],
            ["Permiso Inventory.Sales.View; para registrar, Inventory.Sales.Create; confirmar, Inventory.Sales.Confirm; anular, Inventory.Sales.Void."],
            ["ventas-cotizaciones", "ventas-remisiones", "ventas-oficina"], [], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-remisiones", "Remisiones y factura desde remisiones", Modulos.Ventas, "/ventas/remisiones",
            "La remisión despacha la mercancía y reconoce su costo sin facturarla; después se factura una o varias remisiones del mismo cliente sin volver a descargar.",
            [
                P("Ventas → Remisiones", "Desde un pedido («Remisionar») o directa. La lista dice cuántos días lleva cada una sin facturar.", "/ventas/remisiones", "Abrir Remisiones"),
                P("Facturar remisiones", "Elija las remisiones confirmadas del mismo cliente y «Facturar remisiones»: la factura trae lo pendiente de cada línea, de sólo lectura."),
                P("Sin facturar al cierre", "Una remisión vieja sin facturar levanta una alerta y el cierre del período pide aceptarla con motivo."),
                P("Anular", "Sin facturar, la mercancía vuelve al costo con que salió; ya facturada, no se anula."),
            ],
            ["remision", "despacho", "factura desde remisiones", "sin facturar", "cierre"],
            ["Permiso Inventory.Sales.View; para registrar, Inventory.Sales.Create; confirmar, Inventory.Sales.Confirm; anular, Inventory.Sales.Void."],
            ["ventas-pedidos", "ventas-oficina"], [], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-notas-debito", "Notas débito", Modulos.Ventas, "/ventas/notas-debito",
            "La nota débito aumenta lo cobrado en una factura confirmada (intereses, gastos, un mayor valor) sin mover la existencia, y se transmite a la DIAN como la factura.",
            [
                P("Ventas → Notas débito", "Elija la factura, «Nueva nota débito», el concepto de corrección, las líneas y guarde para ver el total.", "/ventas/notas-debito", "Abrir Notas débito"),
                P("Pagos", "Se registran como en la factura. Con un medio de crédito el pago queda esperando la aprobación del crédito provisional."),
                P("Estado electrónico", "Confirmada, se ve su estado ante la DIAN; validada, no se anula: se corrige con otra nota."),
            ],
            ["nota debito", "intereses", "concepto de correccion", "credito provisional", "dian"],
            ["Permiso Inventory.Sales.View; para registrar, Inventory.Sales.Create; confirmar, Inventory.Sales.Confirm."],
            ["ventas-oficina", "facturacion-electronica"], [], TipoDeTema.Proceso));

        t.Add(Proceso("ventas-promociones", "Promociones", Modulos.Ventas, "/ventas/promociones",
            "Las promociones con vigencia se aplican solas al vender, en oficina y en el POS, como descuento en la línea que baja la base de los impuestos; el documento dice cuál se aplicó.",
            [
                P("Ventas → Promociones", "Código, nombre, clase (porcentaje, valor por unidad, lleve N pague M, precio por cantidad o precio de paquete), vigencia y motivo.", "/ventas/promociones", "Abrir Promociones"),
                P("Ámbitos", "Productos, categorías (con sus subcategorías), segmentos y canales; vacío es todo lo vendido. Entre clases se exigen todas; dentro de una clase basta una."),
                P("Acumulable", "Entre las no acumulables gana la de mayor descuento; una línea con promoción no admite descuento manual."),
                P("Editar", "Ya aplicada en un documento confirmado, sólo cambian el nombre, el fin de la vigencia y si está activa."),
            ],
            ["promocion", "3x2", "lleve pague", "descuento", "vigencia", "paquete"],
            ["Permiso Inventory.Prices.View; para crear y editar, Inventory.Prices.Manage."],
            ["ventas-precios", "ventas-cotizaciones", "ventas-pos"], [], TipoDeTema.Proceso));

        // Feature 012, I4 (T753–T759): facturación electrónica ante la DIAN.
        t.Add(Proceso("facturacion-electronica", "Facturación electrónica ante la DIAN", Modulos.Ventas, "/admin/facturacion-electronica",
            "Toda factura, nota crédito, documento equivalente POS y documento soporte se numera con una resolución vigente, lleva su código único y QR, se transmite por el canal configurado y se entrega al comprador después de validarse. Las contingencias no detienen la venta.",
            [
                P("Administración → Facturación electrónica", "La vigencia de la configuración (modo, canal, ambiente, software y correo al comprador), los canales de la instalación y la preparación para emitir con lo que falta y quién lo corrige. La credencial no se escribe aquí: la carga soporte en el secreto del ambiente y aquí sólo se verifica.", "/admin/facturacion-electronica", "Abrir Configuración"),
                P("Maestros → Resoluciones DIAN", "Registre cada resolución (factura, documento equivalente, documento soporte y contingencia) con su prefijo, rango y vigencia, y asóciela al canal desde una fecha. Con números emitidos sólo se retira.", "/maestros/resoluciones-dian", "Abrir Resoluciones"),
                P("Ventas → Documentos electrónicos", "La bandeja con el estado de cada documento, sus versiones, transmisiones y mensajes traducidos. «Reintentar ahora» y «Consultar a la DIAN»; un rechazo se corrige por el caso a (datos de la contraparte, sin cambio económico), el caso b (reemplazo con el mismo número) o el caso c (anulación sin reemplazo).", "/ventas/documentos-electronicos", "Abrir la bandeja"),
                P("Ventas → Contingencias DIAN", "La 03 la declara una persona (o el circuito del canal) y la 04 sólo el canal. Al cerrar se fija el plazo de transmisión; las constancias se adjuntan al evento y la bitácora se exporta.", "/ventas/contingencias-dian", "Abrir Contingencias"),
                P("Anular o pedir factura", "Un documento validado se anula con su nota total desde el detalle de la venta, en una sola confirmación; sobre un documento equivalente POS, «Pedir factura» emite la nota de ajuste y la factura a nombre del comprador."),
                P("Hoy: canal simulado, sin validez fiscal", "Mientras la cooperativa no tenga proveedor tecnológico, en todos los ambientes —también en producción— se trabaja con el canal SIMULADO en ambiente «Habilitación (pruebas)»: no necesita credencial cargada (basta «Verificar credencial»). Registre resoluciones de prueba y asócielas al canal. Los documentos salen con la leyenda «SIN VALIDEZ FISCAL» y no llegan a la DIAN; si el comprador tiene correo, le llega esa representación, así que pruebe con terceros de prueba. El canal simulado en ambiente «Producción» queda bloqueado.", "/admin/facturacion-electronica", "Abrir Configuración"),
                P("Qué responde el simulado", "Decide por el último dígito de la identificación del comprador (o del proveedor en el documento soporte y los eventos RADIAN): 1 rechaza la primera vez, 2 valida con notificaciones, 3 queda «en proceso», 4 simula una contingencia de la DIAN y 5 el canal caído; los demás validan. Un rechazo o una contingencia con una cédula que termina así es del simulado, no de los datos."),
                P("El paso al canal real (pendiente)", "Cuando haya proveedor se registra una vigencia nueva de la configuración desde una fecha, con el canal real, su credencial y ambiente. Lo emitido con el simulado no se retransmite al canal real: queda como documento de prueba."),
            ],
            ["factura electronica", "dian", "cufe", "cude", "resolucion", "contingencia", "rechazo", "canal", "documento equivalente", "nota credito", "simulado", "pruebas"],
            ["Permiso ElectronicInvoicing.Settings.View; para configurar, ElectronicInvoicing.Settings.Manage; resoluciones, ElectronicInvoicing.Resolutions.Manage; corregir rechazos, ElectronicInvoicing.Documents.Correct."],
            ["ventas-oficina", "ventas-pos", "compras-documentos-soporte", "ventas-notas-debito", "maestros-impuestos"], ["/ventas/documentos-electronicos", "/ventas/contingencias-dian", "/maestros/resoluciones-dian"], TipoDeTema.Proceso));

        t.Add(Proceso("compras-documentos-soporte", "Documento soporte", Modulos.Compras, "/compras/documentos-soporte",
            "La compra a un proveedor no obligado a facturar se soporta con el documento soporte electrónico, que la cooperativa numera con su resolución y transmite a la DIAN; se corrige con su nota de ajuste.",
            [
                P("Compras → Documentos soporte", "Lista de documentos soporte y de sus notas de ajuste, con filtros por proveedor, estado y fechas.", "/compras/documentos-soporte", "Abrir Documentos soporte"),
                P("Nuevo documento soporte", "Proveedor no obligado, las recepciones que cubre (lo que falta por facturar) y los servicios sin recepción. Guardar calcula impuestos y retenciones; confirmar numera y transmite.", "/compras/documentos-soporte/nuevo", "Nuevo documento soporte"),
                P("Generar los de la semana", "Si la cooperativa genera el documento soporte semanalmente, prepara un borrador por proveedor con las recepciones de la semana."),
                P("Nota de ajuste", "Desde un documento soporte confirmado: valor por línea y el concepto de corrección; siempre disminuye."),
            ],
            ["documento soporte", "no obligado", "nota de ajuste", "cuds", "compras"],
            ["Permiso Inventory.Purchases.View; para registrar, Inventory.Purchases.Create; confirmar, Inventory.Purchases.Confirm."],
            ["compras-facturas-proveedor", "facturacion-electronica"], ["/compras/documentos-soporte/{Id}"], TipoDeTema.Proceso));

        // -------------------------------------------------------------------- Nómina --
        t.Add(Proceso("empleados", "Empleados", Modulos.Nomina, "/nomina/empleados",
            "Las personas vinculadas laboralmente: contrato, cargo, salario, afiliaciones (EPS, ARL, pensión, cesantías) y cuenta de pago.",
            [
                P("Nómina → Empleados", null, "/nomina/empleados", "Abrir Empleados"),
                P("Nuevo Empleado", "Parte de una persona del registro único. Tipo de contrato, fecha de ingreso, cargo, salario base, periodicidad, afiliaciones y, opcional, el centro de costo contable (se busca por código o nombre)."),
                P("Centro de costo", "Se asigna o cambia en «Editar» o en la pestaña «Datos Laborales»; el listado tiene su columna y su filtro. Al aprobar la nómina, cada línea del comprobante lleva el centro del empleado cuando la cuenta del concepto maneja centro de costo, y usa la cuenta de ese centro si el concepto tiene una. Si la cuenta exige centro y el empleado no tiene, la aprobación se detiene y dice a qué empleados asignárselo."),
                P("Detalle", "Historial de contratos, novedades y liquidaciones. «Terminar Contrato» registra la fecha y el motivo y habilita la liquidación definitiva."),
            ],
            ["empleados", "contrato", "salario", "cargo", "afiliaciones", "eps", "arl", "pension", "terminar contrato", "centro de costo"], ["Cooperativa activa.", "La persona en el registro único.", "EPS, ARL, fondos y cargos creados."],
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
            ["empleados", "prima-de-servicios", "cesantias-e-intereses", "vacaciones"], ["/nomina/liquidacion-definitiva/{RunId}"], TipoDeTema.Proceso));

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
            ["debito-parametros-convenio"], ["/debito/tarjetas/{CardId}"], TipoDeTema.Proceso));
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

        // Feature 012 (T171): el catálogo tributario de Core que usan Comercial, Compras y la facturación electrónica.
        t.Add(Proceso("maestros-impuestos", "Impuestos y retenciones", Modulos.Maestros, "/maestros/impuestos",
            "El catálogo tributario de la cooperativa: impuestos y retenciones, sus tarifas con vigencia y los conceptos de retención, que usan Comercial, Compras y la facturación electrónica. Lo sembrado (IVA, INC, bolsas, retención en la fuente por concepto y ReteIVA) queda «pendiente de validar por la contadora».",
            [
                P("Maestros Core → Impuestos y retenciones", "Tres pestañas: «Impuestos», «Tarifas» y «Conceptos de retención». En Tarifas se filtra por fecha de vigencia, municipio y «Sólo pendientes de validar».", "/maestros/impuestos", "Abrir Impuestos"),
                P("Impuestos y conceptos", "«Nuevo impuesto»: código, nombre, clase, forma de cálculo (sobre otro impuesto si aplica), tributo DIAN y motivo. «Nuevo concepto» para los conceptos de retención. Editar cambia el nombre, las notas y si está activo; el código no cambia."),
                P("Tarifas", "«Nueva tarifa»: impuesto, código, tarifa en puntos (19 = 19 %; 0,966 = 9,66 por mil) o valor por unidad, concepto, municipio y actividad (para ICA y ReteICA), bases mínimas en UVT o en pesos, a qué aplica, vigencia, condiciones del sujeto en las retenciones y la norma que la respalda."),
                P("Una tarifa que ya rige no se edita", "Sólo se corrige la que aún no rige. Para cambiarla, «Cerrar vigencia» y luego una vigencia nueva con el mismo código. El ojo muestra la norma y el historial del código. «Marcar revisada», con motivo, deja la tarifa validada sin cambiar su valor."),
                P("Plantilla", "«Plantilla vacía» o «Plantilla con los datos» para diligenciar en Excel; «Importar plantilla» la revisa por hoja, fila y columna y la aplica todo o nada. ReteICA, que es municipal, se carga por la plantilla. La UVT no vive aquí: está en los parámetros legales de Nómina."),
            ],
            ["impuestos", "retenciones", "iva", "inc", "retefuente", "reteiva", "reteica", "tarifas", "conceptos de retencion", "uvt", "plantilla", "pendiente de validar"],
            ["Permiso Core.Taxes.View; para crear, cambiar, cerrar, marcar revisada o importar, Core.Taxes.Manage."],
            ["facturacion-electronica", "compras-facturas-proveedor", "ventas-oficina", "contabilidad-inventario-matriz"], [], TipoDeTema.Maestro));

        // Grupos del asociado: la clasificación libre de los asociados que reemplazó al centro de costo del asociado (retirado); los centros de
        // costo quedan sólo como contables. Detalle en docs/manual/grupos-del-asociado.md.
        t.Add(Proceso("maestros-grupos-del-asociado", "Grupos del asociado", Modulos.Maestros, "/maestros/grupos-del-asociado",
            "La clasificación libre que la cooperativa usa para agrupar a sus asociados: zona, empresa, gremio, agencia… No son centros de costo, no llevan cuentas ni datos de nómina. Reemplazaron al antiguo centro de costo del asociado; los centros de costo quedan sólo para la contabilidad.",
            [
                P("Maestros Core → Grupos del asociado", "Lista con código, nombre, descripción y estado; «Ver inactivos» muestra también los inactivos.", "/maestros/grupos-del-asociado", "Abrir Grupos del asociado"),
                P("Nuevo", "Código (letras, dígitos, punto, guion o guion bajo, sin espacios, en mayúsculas, hasta 10 y único; la pantalla avisa al salir del campo si ya existe), nombre y descripción. El código no cambia una vez creado; el nombre y la descripción sí se editan."),
                P("Inactivar o reactivar", "Con motivo; un grupo no se borra. Uno que tienen asociados no se inactiva: cámbielos de grupo antes. Un grupo inactivo no se asigna, pero quien ya lo tenía lo conserva."),
                P("Asignarlo", "En la ficha del asociado, la lista «Grupo del asociado» (opcional); el listado de asociados filtra por grupo.", "/asociados/registro", "Abrir Registro de asociados"),
            ],
            ["grupos del asociado", "grupo", "clasificacion", "zona", "empresa", "gremio", "agencia", "asociados", "centro de costo"],
            ["Permiso Core.AssociateGroups.View; para crear, editar, inactivar y reactivar, Core.AssociateGroups.Manage."],
            ["registro-de-asociados", "maestros-centros-costo"], [], TipoDeTema.Maestro));

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

        // Feature 013, fase 2 (T0095): las listas restrictivas que carga el oficial de cumplimiento y las solicitudes del titular (Ley 1581).
        t.Add(Proceso("cumplimiento-listas-restrictivas", "Listas restrictivas", Modulos.Cumplimiento, "/compliance/listas-restrictivas",
            "Las listas restrictivas que mantiene el oficial de cumplimiento mientras no haya un proveedor contratado. Cada carga es una versión nueva de su lista, con fuente, fecha de publicación y vigencia; la anterior queda reemplazada.",
            [
                P("Cumplimiento → Listas restrictivas", "La tabla «Cargas» muestra cada lista con su versión, estado, publicación, vigencia, número de entradas, fuente y quién la cargó.", "/compliance/listas-restrictivas", "Abrir Listas restrictivas"),
                P("Ver entradas", "Abre las entradas de esa versión; busque por documento o nombre. Las entradas son lo que publica la lista, no personas del maestro."),
                P("Cargar una lista", "Descargue la plantilla, diligénciela con la lista publicada y súbala: primero se revisa sin guardar nada y, sin errores, se aplica completa. Un documento repetido en el archivo es un error."),
            ],
            ["listas restrictivas", "sarlaft", "lavado de activos", "oficial de cumplimiento", "carga", "plantilla", "version", "consulta"],
            ["Canal de listas restrictivas configurado como carga propia.", "Permiso Compliance.RestrictiveLists.View; para cargar, Compliance.RestrictiveLists.Load."],
            ["admin-canales-externos", "cartera-solicitudes-y-actas", "personas"], [], TipoDeTema.Proceso));

        t.Add(Proceso("cumplimiento-solicitudes-del-titular", "Solicitudes del titular de los datos", Modulos.Cumplimiento, "/compliance/solicitudes-del-titular",
            "Radicar y responder dentro del plazo las consultas, actualizaciones, rectificaciones, supresiones y revocaciones que presenta el titular de los datos (Ley 1581). El plazo lo calcula el sistema en días hábiles desde el recibo.",
            [
                P("Cumplimiento → Solicitudes del titular", "Filtre por estado (recibidas, prorrogadas, respondidas) y por fecha de vencimiento y «Consultar». Las vencidas sin responder se resaltan. «Ver» abre el detalle.", "/compliance/solicitudes-del-titular", "Abrir Solicitudes"),
                P("Radicar solicitud", "Busque al titular, elija la solicitud, la finalidad si es una supresión o revocación (y el canal si es la de canal de contacto), por dónde llegó y la fecha, y escriba lo que pide. Suba los soportes (el escrito del titular) y marque los de esta solicitud."),
                P("Prorrogar", "Una sola vez, con motivo, que hay que informarle al titular."),
                P("Responder", "Decisión (procede, procede en parte o no procede) y la respuesta al titular. Si revoca o suprime una finalidad de crédito y existe un deber de conservar el dato, la solicitud queda «No procede» con la constancia del deber."),
            ],
            ["solicitudes del titular", "habeas data", "ley 1581", "consulta", "rectificacion", "supresion", "revocacion", "prorroga", "plazo", "datos personales"],
            ["Permiso Compliance.DataSubjectRequests.Read; para radicar, .Register; para prorrogar y responder, .Respond."],
            ["habeas-data", "personas", "cartera-autorizaciones"], [], TipoDeTema.Proceso));

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
                P("Qué hay", "Contabilidad: estados financieros, libro mayor, balance de prueba, libro auxiliar, terceros y ejecución del presupuesto. Cartera: cartera por edades, movimientos y usura. Captaciones y aportes: saldos, cuentas por estado, aportes por asociado, intereses y retención, y los reportes regulatorios. Nómina: reportes y liquidación. Inventario: documentos, reorden, margen, rotación, ABC, sin movimiento, por vencer, sugerido de compras y tablero. Cada tarjeta abre el informe ya elegido en su módulo; los extractos y certificados de cada cuenta o crédito se emiten desde su ficha."),
            ],
            ["reportes", "informes", "imprimir", "pdf", "exportar", "centro"], ["Cooperativa activa."], [], [], TipoDeTema.Consulta));

        // Los informes contables (balance general, estado de resultados, libro mayor) viven en el módulo
        // de Contabilidad desde la feature 009 E2 («Estados financieros» e «Informes contables», arriba);
        // las rutas /reportes/balance-general, /reportes/estado-resultados y /reportes/libro-mayor nunca
        // tuvieron página.
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

    /// <summary>
    /// La guía de los catálogos simples de Inventario y Ventas (feature 012), que comparten <c>CatalogoDeInventario</c>: no
    /// tienen papelera sino «Inactivar» con motivo, y cargan por la plantilla del catálogo.
    /// </summary>
    private static TemaDeManual CatalogoDeInventario(string ruta, string titulo, string modulo, string singular, string nota, params string[] claves) =>
        new(SlugDeRuta(ruta), titulo, modulo, ruta,
            $"Catálogo de {titulo.ToLowerInvariant()}: se consulta, se crea, se corrige y se inactiva {singular}. " + nota,
            [
                P("Abrir la pantalla", $"En el menú lateral, dentro de «{modulo}», elegí «{titulo}». «Ver inactivos» trae también los retirados.", ruta, "Abrir " + titulo),
                P($"Crear {singular}", "«Nuevo». El código es la nomenclatura de la cooperativa: en mayúsculas, sin espacios y único; la pantalla avisa si ya existe antes de guardar."),
                P("Corregir", "«Editar» en la fila. El código no se cambia después de creado."),
                P("Inactivar o reactivar", "Pide motivo. Lo inactivo deja de ofrecerse en documentos nuevos y el historial lo conserva; nada se borra."),
                P("Cargar por archivo", "«Plantilla»: se revisa por fila y columna y se aplica todo o nada. Un código que ya existe se actualiza con las mismas reglas que la edición; la plantilla nunca borra."),
            ],
            [.. claves, "catalogo", "crear", "editar", "inactivar", titulo.ToLowerInvariant()],
            ["Tener una cooperativa activa en la sesión.", "Permiso Inventory.Catalog.View; para crear, corregir e inactivar, Inventory.Catalog.Manage."],
            ["inventario-plantillas"], [], TipoDeTema.Maestro);

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
