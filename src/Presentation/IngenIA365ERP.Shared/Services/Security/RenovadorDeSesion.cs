using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Dueño de la sesión operativa en el cliente: los dos tokens, sus vencimientos y
/// la renovación silenciosa.
///
/// <para>
/// <b>Por qué existe.</b> El servidor emitía un access token de quince minutos y un
/// refresh de doce horas, y el cliente guardaba los dos… y nunca usaba el segundo
/// más que para cerrar sesión. A los quince minutos cada petición respondía 401 y
/// la persona volvía al login. Este servicio cierra esa mitad: renueva el access
/// antes de que venza y, si un 401 se cuela igual, renueva y reintenta una vez
/// (ver <see cref="RenovacionDeSesionHandler"/>).
/// </para>
///
/// <para>
/// <b>Lifetime: singleton, a propósito.</b> <c>IHttpClientFactory</c> resuelve los
/// <c>DelegatingHandler</c> en su propio scope de DI, así que un servicio scoped
/// sería otra instancia para el handler que para la pantalla. Todo lo durable vive
/// en <see cref="ISecureStorage"/> (singleton en los tres hosts), y lo que hay en
/// memoria es caché de eso. Es la misma regla que ya fija <c>Program.cs</c> para
/// <c>ISecureStorage</c> e <c>ITenantService</c>.
/// </para>
///
/// <para>
/// <b>Una renovación a la vez.</b> El refresh rota: el token viejo queda invalidado
/// al canjearse, y un segundo canje con el mismo token es «reuso» y mata la familia
/// entera. Con varias peticiones concurrentes venciendo a la vez —una pantalla que
/// carga tres listas— eso pasaría en el primer minuto. El semáforo hace que sólo la
/// primera canjee y las demás esperen y usen lo que trajo.
/// </para>
/// </summary>
public sealed class RenovadorDeSesion
{
    public const string ClaveAccessToken = AuthBearerHandler.TokenKey;
    public const string ClaveRefreshToken = "refresh_token";
    public const string ClaveVencimientoDeSesion = "session_expires_at";
    public const string ClaveUltimaRotacion = "session_rotated_at";

    /// <summary>
    /// 2026-11-17: el grupo de ventanas de esta sesión. Nace con el ingreso y se copia con el resto de la sesión a una pestaña
    /// duplicada o a la ventana que se abre con «Abrir en ventana nueva»: todas las del grupo cuentan juntas la inactividad y
    /// cerrar sesión en una cierra todas (ver <see cref="ICoordinacionEntrePestanas"/>).
    /// </summary>
    public const string ClaveGrupo = "session_group";
    public const string NombreDelClienteHttp = "api";

    /// <summary>Código con el que esta pestaña termina su propia sesión por inactividad.</summary>
    public const string CodigoInactividad = "Identity.Session.Inactivity";

    /// <summary>Código con el que una ventana termina su sesión porque la persona la cerró en otra ventana del grupo.</summary>
    public const string CodigoCerradaEnOtraVentana = "Identity.Session.ClosedInOtherWindow";

    /// <summary>Cada cuánto, como mucho, una ventana anuncia su actividad al grupo.</summary>
    private static readonly TimeSpan IntervaloDeAnuncio = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Marca de petición que los handlers dejan pasar sin tocar: es la que canjea el
    /// refresh, y adjuntarle un access vencido o intentar renovarla otra vez sería
    /// recursión.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> SinSesion = new("IngenIA365ERP.SinSesion");

    /// <summary>
    /// Marca de una petición que la pantalla hace sola, cada tanto, sin que la persona haga nada (2026-11-26): lleva la sesión
    /// y la renueva si hace falta, pero <b>no cuenta como actividad</b>. El aviso del correo en prueba consultaba cada cinco
    /// minutos y cada consulta contaba: una pestaña olvidada no llegaba nunca a la inactividad de la cooperativa.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> DeFondo = new("IngenIA365ERP.DeFondo");

    /// <summary>
    /// Cuánto antes del vencimiento del access se renueva. Un minuto cubre el reloj
    /// del navegador desfasado y el viaje de la petición; más que eso sólo gasta
    /// rotaciones.
    /// </summary>
    public static readonly TimeSpan MargenDeRenovacion = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Los límites que el servidor hace cumplir en el refresh, leídos de
    /// <c>GET /api/auth/session-policy</c> para que el número viva en un solo sitio.
    /// Hasta que se leen —o si no se pueden leer— valen los mismos valores por
    /// defecto que trae el servidor.
    /// </summary>
    public PoliticaDeSesion Politica { get; private set; } = PoliticaDeSesion.PorDefecto;

    private readonly ISecureStorage _storage;
    private readonly IHttpClientFactory _http;
    private readonly TimeProvider _reloj;
    private readonly SemaphoreSlim _unaRenovacionALaVez = new(1, 1);

    private string? _accessToken;
    /// <summary>El access que acaba de rotar: un cliente que lo leyó antes de la renovación lo manda todavía.</summary>
    private string? _accessTokenAnterior;
    private DateTime _accessTokenExpiresAt;
    private string? _refreshToken;
    private DateTime? _vencimientoDeSesion;
    private DateTime _ultimaRotacion;
    private DateTime _ultimaActividad;
    private bool _restaurado;
    private bool _politicaLeida;
    private string? _grupo;
    private DateTime _actividadAnunciada = DateTime.MinValue;

    private readonly ICoordinacionEntrePestanas _pestanas;

    public RenovadorDeSesion(
        ISecureStorage storage, IHttpClientFactory http, TimeProvider? reloj = null, ICoordinacionEntrePestanas? pestanas = null)
    {
        _storage = storage;
        _http = http;
        _reloj = reloj ?? TimeProvider.System;
        _pestanas = pestanas ?? SinCoordinacionEntrePestanas.Instancia;
    }

    public string? AccessToken => _accessToken;
    public DateTime AccessTokenExpiresAt => _accessTokenExpiresAt;
    public string? RefreshToken => _refreshToken;

    /// <summary>
    /// Hasta cuándo puede vivir esta sesión, contando desde el ingreso. Lo dice el
    /// servidor en cada respuesta que trae tokens (<c>refreshTokenExpiresAt</c>) y es
    /// un tope absoluto: la renovación no lo corre. Es lo que
    /// <c>AvisoDeVencimientoDeSesion</c> cuenta hacia atrás.
    /// </summary>
    public DateTime? VencimientoDeSesion => _vencimientoDeSesion;

    public bool TieneSesion => !string.IsNullOrWhiteSpace(_accessToken);

    /// <summary>
    /// Si ese Bearer es el access de <b>esta</b> sesión —el vigente o el que acaba de rotar— y
    /// no un token temporal de desafío (login, MFA, elegir cooperativa). Es lo que le permite a
    /// <c>RenovacionDeSesionHandler</c> tratar como suya una petición a la que un cliente le
    /// puso la cabecera a mano.
    /// </summary>
    public bool EsTokenDeSesion(string? token) =>
        !string.IsNullOrEmpty(token)
        && (string.Equals(token, _accessToken, StringComparison.Ordinal)
            || string.Equals(token, _accessTokenAnterior, StringComparison.Ordinal));

    public bool AccessTokenVigente => TieneSesion && _accessTokenExpiresAt > Ahora;

    /// <summary>
    /// Última petición con sesión que salió de esta pestaña (o el ingreso, o la
    /// recarga de la página). Es lo que cuenta como «actividad»: mover el ratón o
    /// escribir en un formulario sin guardar no lo es, y el aviso avisa antes.
    /// </summary>
    public DateTime UltimaActividad => _ultimaActividad;

    /// <summary>Cuánto falta para que esta pestaña cierre su sesión por inactividad. Nunca negativo.</summary>
    public TimeSpan InactividadRestante
    {
        get
        {
            if (!TieneSesion) return TimeSpan.Zero;
            var restante = Politica.Inactividad - (Ahora - _ultimaActividad);
            return restante < TimeSpan.Zero ? TimeSpan.Zero : restante;
        }
    }

    /// <summary>Una petición con sesión acaba de salir: la pestaña está viva, y lo sabe el resto de su grupo.</summary>
    public void RegistrarActividad()
    {
        _ultimaActividad = Ahora;
        if (_grupo is null || _ultimaActividad - _actividadAnunciada < IntervaloDeAnuncio) return;
        _actividadAnunciada = _ultimaActividad;
        _ = AnunciarActividadAsync(_grupo, _ultimaActividad);
    }

    /// <summary>El anuncio no hace esperar a la petición que lo disparó; si falla, la ventana sigue con su propia cuenta.</summary>
    private async Task AnunciarActividadAsync(string grupo, DateTime cuando)
    {
        try
        {
            await _pestanas.AnunciarActividadAsync(grupo, cuando);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo anunciar la actividad al grupo de ventanas: {ex.Message}");
        }
    }

    /// <summary>El grupo de ventanas de esta sesión; <c>null</c> sin sesión.</summary>
    public string? Grupo => _grupo;

    /// <summary>
    /// La persona cierra sesión en esta ventana: las demás de su grupo se cierran también (lo ven en su próximo latido). Va
    /// antes de <see cref="Limpiar"/>, que olvida el grupo.
    /// </summary>
    public async Task AnunciarCierreAsync()
    {
        if (string.IsNullOrWhiteSpace(_grupo)) _grupo = await _storage.GetAsync(ClaveGrupo);
        if (string.IsNullOrWhiteSpace(_grupo)) return;
        try
        {
            await _pestanas.AnunciarCierreAsync(_grupo);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Sin anuncio, las otras ventanas salen al vencer su access (el servidor ya no les canjea el refresh).
            System.Diagnostics.Debug.WriteLine($"No se pudo anunciar el cierre al grupo de ventanas: {ex.Message}");
        }
    }

    /// <summary>El grupo de la sesión que hay en el storage, o uno nuevo si la sesión no lo traía (una de antes del 2026-11-17).</summary>
    private async Task AsegurarGrupoAsync()
    {
        if (!string.IsNullOrWhiteSpace(_grupo)) return;
        _grupo = await _storage.GetAsync(ClaveGrupo);
        if (!string.IsNullOrWhiteSpace(_grupo)) return;
        _grupo = Guid.NewGuid().ToString("N");
        await _storage.SetAsync(ClaveGrupo, _grupo);
    }

    /// <summary>Renovada o adoptada: los tokens cambiaron y quien los cachee debe releer.</summary>
    public event Action? SesionRenovada;

    /// <summary>
    /// El servidor rechazó el refresh (vencida, revocada, reusada, política de
    /// métodos). Trae el código del sobre de error. Ya no hay sesión local: quien
    /// escuche tiene que llevar a la persona al login.
    /// </summary>
    public event Action<string>? SesionTerminada;

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    // ---------- Estado ----------

    /// <summary>
    /// Rehidrata desde el storage (tras un F5 el proceso arranca vacío y el
    /// sessionStorage aún tiene los tokens). Idempotente. <c>true</c> si hay sesión.
    /// </summary>
    public async Task<bool> RestaurarAsync()
    {
        if (TieneSesion) return true;
        if (_restaurado) return false;
        _restaurado = true;

        var access = await _storage.GetAsync(ClaveAccessToken);
        if (string.IsNullOrWhiteSpace(access)) return false;

        _accessToken = access;
        _accessTokenExpiresAt = LeerVencimientoDelJwt(access) ?? Ahora.AddMinutes(5);
        _refreshToken = await _storage.GetAsync(ClaveRefreshToken);
        _vencimientoDeSesion = LeerFecha(await _storage.GetAsync(ClaveVencimientoDeSesion));
        // Recargar la página es actividad de la persona; la rotación, en cambio, se
        // conserva: si no consta, se asume ahora y el mantenimiento se adelanta un
        // ciclo, que es el lado seguro.
        _ultimaRotacion = LeerFecha(await _storage.GetAsync(ClaveUltimaRotacion)) ?? Ahora;
        _ultimaActividad = Ahora;
        await AsegurarGrupoAsync();
        return true;
    }

    /// <summary>
    /// Lee los límites de sesión del servidor. Una vez por proceso; si falla, quedan
    /// los valores por defecto y se vuelve a intentar la próxima vez.
    /// </summary>
    public async Task CargarPoliticaAsync(CancellationToken ct = default)
    {
        if (_politicaLeida) return;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session-policy");
            req.Options.Set(SinSesion, true);
            using var resp = await _http.CreateClient(NombreDelClienteHttp).SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return;
            var cuerpo = await resp.Content.ReadFromJsonAsync<RespuestaDePolitica>(cancellationToken: ct);
            if (cuerpo is null || cuerpo.InactivityMinutes < 1 || cuerpo.MaxDurationHours < 1) return;
            Politica = new PoliticaDeSesion(
                _inactividadDeLaCooperativa ?? TimeSpan.FromMinutes(cuerpo.InactivityMinutes),
                TimeSpan.FromHours(cuerpo.MaxDurationHours));
            _politicaLeida = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Sin política del servidor valen los defectos, que son los mismos que él
            // trae de fábrica. No es un error de la persona y no hay nada que mostrarle.
        }
    }

    /// <summary>
    /// Si la cooperativa activa deja que el uso del computador cuente como actividad (parámetro
    /// <c>Security.Session.CountDeviceActivity</c>). Apagado mientras no se haya leído.
    /// </summary>
    public bool ContarActividadDelEquipo { get; private set; }

    /// <summary>
    /// 2026-11-17: si «Abrir en ventana nueva» lleva la sesión a la ventana nueva (parámetro
    /// <c>Security.Session.NewWindowInheritsSession</c>, encendido por defecto). Apagado, la ventana nueva pide entrar.
    /// Mientras no se haya leído vale encendido, que es el valor de fábrica.
    /// </summary>
    public bool VentanasHeredanSesion { get; private set; } = true;

    private bool _politicaDeLaCooperativaLeida;
    private DateTime _proximoIntentoDePoliticaDeLaCooperativa = DateTime.MinValue;

    /// <summary>
    /// 2026-11-23: los minutos de inactividad de la cooperativa activa (su política de identidad, de 15 a 120), los mismos que el
    /// refresh hace cumplir. Nulo mientras no se lean o si la API no los publica: rige el general.
    /// </summary>
    private TimeSpan? _inactividadDeLaCooperativa;

    private void AplicarInactividadDeLaCooperativa(int? minutos)
    {
        _inactividadDeLaCooperativa = minutos is >= 2 and <= 24 * 60 ? TimeSpan.FromMinutes(minutos.Value) : null;
        if (_inactividadDeLaCooperativa is { } propia) Politica = Politica with { Inactividad = propia };
    }

    /// <summary>
    /// Lee de la cooperativa activa si cuenta el uso del computador. Una vez por ingreso o cambio de cooperativa; si falla,
    /// reintenta al minuto. Sale por <see cref="SinSesion"/> con el token puesto aquí para que la consulta NO cuente como
    /// actividad: el latido la llama y, si contara, la pestaña se mantendría viva sola.
    /// </summary>
    public async Task CargarPoliticaDeLaCooperativaAsync(CancellationToken ct = default)
    {
        if (_politicaDeLaCooperativaLeida || Ahora < _proximoIntentoDePoliticaDeLaCooperativa) return;
        if (!await RestaurarAsync() || string.IsNullOrWhiteSpace(_accessToken)) return;
        _proximoIntentoDePoliticaDeLaCooperativa = Ahora.AddMinutes(1);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/sesion/politica-de-la-cooperativa");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            req.Options.Set(SinSesion, true);
            using var resp = await _http.CreateClient(NombreDelClienteHttp).SendAsync(req, ct);
            if (resp.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden)
            {
                // Una API que no la publica, o una sesión sin cooperativa: vale apagado y no se insiste.
                ContarActividadDelEquipo = false;
                VentanasHeredanSesion = true;
                _politicaDeLaCooperativaLeida = true;
                return;
            }
            if (!resp.IsSuccessStatusCode) return;
            var cuerpo = await resp.Content.ReadFromJsonAsync<RespuestaDePoliticaDeLaCooperativa>(cancellationToken: ct);
            ContarActividadDelEquipo = cuerpo?.CountDeviceActivity == true;
            VentanasHeredanSesion = cuerpo?.NewWindowInheritsSession ?? true;
            AplicarInactividadDeLaCooperativa(cuerpo?.InactivityMinutes);
            _politicaDeLaCooperativaLeida = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Sin respuesta vale apagado, que es como funcionaba antes; se reintenta al minuto.
        }
    }

    /// <summary>
    /// La persona está usando el computador (en cualquier programa) y la cooperativa lo deja contar: es actividad. Quien
    /// lo detecta es el aviso de sesión; aquí sólo se decide si cuenta.
    /// </summary>
    public void RegistrarActividadDelEquipo()
    {
        if (ContarActividadDelEquipo && TieneSesion) RegistrarActividad();
    }

    /// <summary>
    /// Convierte una respuesta con tokens en LA sesión: memoria y storage.
    /// <paramref name="refreshTokenExpiresAt"/> es el tope de la sesión; si una
    /// puerta no lo trae, se conserva el que ya había (una elevación tras inscribir
    /// no reinicia el reloj) y si no había ninguno, no se inventa.
    /// </summary>
    /// <param name="esActividad">
    /// Entrar o cambiar de cooperativa es actividad de la persona. Una renovación en
    /// silencio —incluida la de mantenimiento de una pestaña abierta— NO lo es: si
    /// contara, la pestaña abandonada se mantendría viva a sí misma para siempre.
    /// </param>
    public async Task AdoptarAsync(
        string accessToken, DateTime? accessTokenExpiresAt, string? refreshToken, DateTime? refreshTokenExpiresAt,
        bool esActividad = true)
    {
        _restaurado = true;
        var esIngreso = _accessToken is null;
        if (_accessToken is not null && !string.Equals(_accessToken, accessToken, StringComparison.Ordinal))
            _accessTokenAnterior = _accessToken;
        _accessToken = accessToken;
        _accessTokenExpiresAt = accessTokenExpiresAt ?? LeerVencimientoDelJwt(accessToken) ?? Ahora.AddMinutes(15);
        if (!string.IsNullOrWhiteSpace(refreshToken)) _refreshToken = refreshToken;
        if (refreshTokenExpiresAt is not null) _vencimientoDeSesion = refreshTokenExpiresAt;
        // Un ingreso abre un grupo nuevo aunque el storage guarde uno de una sesión anterior (que pudo cerrarse en otra
        // ventana); cambiar de cooperativa o renovar lo conservan.
        if (esIngreso) _grupo = null;
        if (esIngreso) _storage.Remove(ClaveGrupo);
        await AsegurarGrupoAsync();
        // Tokens nuevos = rotación en el servidor.
        _ultimaRotacion = Ahora;
        if (esActividad)
        {
            _ultimaActividad = Ahora;
            // Ingreso o cambio de cooperativa: su política se vuelve a leer.
            _politicaDeLaCooperativaLeida = false;
            _proximoIntentoDePoliticaDeLaCooperativa = DateTime.MinValue;
        }

        await _storage.SetAsync(ClaveAccessToken, accessToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await _storage.SetAsync(ClaveRefreshToken, refreshToken);
        if (_vencimientoDeSesion is { } vence)
            await _storage.SetAsync(ClaveVencimientoDeSesion, vence.ToString("o", CultureInfo.InvariantCulture));
        await _storage.SetAsync(ClaveUltimaRotacion, _ultimaRotacion.ToString("o", CultureInfo.InvariantCulture));

        SesionRenovada?.Invoke();
    }

    /// <summary>Olvida la sesión en memoria y en storage. No avisa al servidor: eso es del logout.</summary>
    public void Limpiar()
    {
        _accessToken = null;
        _accessTokenAnterior = null;
        _accessTokenExpiresAt = DateTime.MinValue;
        _refreshToken = null;
        _vencimientoDeSesion = null;
        _restaurado = true;
        _grupo = null;
        _actividadAnunciada = DateTime.MinValue;
        ContarActividadDelEquipo = false;
        VentanasHeredanSesion = true;
        _politicaDeLaCooperativaLeida = false;
        _storage.Remove(ClaveAccessToken);
        _storage.Remove(ClaveRefreshToken);
        _storage.Remove(ClaveVencimientoDeSesion);
        _storage.Remove(ClaveUltimaRotacion);
        _storage.Remove(ClaveGrupo);
    }

    // ---------- Inactividad ----------

    /// <summary>
    /// Un latido por segundo desde el layout. Hace dos cosas: si esta pestaña lleva
    /// <see cref="PoliticaDeSesion.Inactividad"/> sin actividad, la cierra; y si sigue
    /// activa pero la última rotación se acerca a ese mismo límite, rota sola —el
    /// servidor mide la inactividad entre rotaciones, porque no ve cada petición, y
    /// sin esto expulsaría a alguien que lleva veinte minutos leyendo un informe.
    /// </summary>
    public async Task VigilarAsync(CancellationToken ct = default)
    {
        if (!await RestaurarAsync()) return;

        // El grupo primero: si otra ventana cerró la sesión, ésta también; si otra tuvo actividad, cuenta aquí.
        if (await MirarElGrupoAsync()) return;

        if (InactividadRestante == TimeSpan.Zero)
        {
            await CortarPorInactividadAsync(ct);
            return;
        }

        if (Ahora - _ultimaRotacion >= Politica.Inactividad - Politica.MargenDeMantenimiento)
            await RenovarAsync(motivo: MotivoDeRenovacion.MantenerViva, ct: ct);
    }

    /// <summary>
    /// La persona pulsó «seguir trabajando» en el aviso: cuenta como actividad y se
    /// rota ya, para que el servidor también lo sepa. <c>true</c> si la sesión sigue.
    /// </summary>
    public async Task<bool> SeguirTrabajandoAsync(CancellationToken ct = default)
    {
        if (!await RestaurarAsync()) return false;
        RegistrarActividad();
        return await RenovarAsync(motivo: MotivoDeRenovacion.Explicita, ct: ct);
    }

    /// <summary><c>true</c> si la sesión se cerró en otra ventana del grupo y ésta acaba de terminar la suya.</summary>
    private async Task<bool> MirarElGrupoAsync()
    {
        if (string.IsNullOrWhiteSpace(_grupo)) return false;
        EstadoDelGrupo estado;
        try
        {
            estado = await _pestanas.LeerGrupoAsync(_grupo);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo leer el grupo de ventanas: {ex.Message}");
            return false;
        }

        if (estado.Cerrada)
        {
            // Sin avisar al servidor: la que cerró ya revocó el refresh.
            await TerminarAsync(CodigoCerradaEnOtraVentana);
            return true;
        }

        if (estado.UltimaActividad is { } otra && otra > _ultimaActividad)
            _ultimaActividad = otra > Ahora ? Ahora : otra;
        return false;
    }

    private async Task CortarPorInactividadAsync(CancellationToken ct)
    {
        // Si otra pestaña con esta misma sesión (una duplicada) ya canjeó nuestro refresh, la sesión sigue viva allá: esta
        // pestaña se cierra sola, sin avisar al servidor, porque el logout revocaría la familia y sacaría también a la que
        // se está usando (2026-11-14).
        if (!string.IsNullOrWhiteSpace(_refreshToken) && await _pestanas.SucesorDeAsync(_refreshToken) is not null)
        {
            await TerminarAsync(CodigoInactividad);
            return;
        }

        // Avisar al servidor es cortesía, no requisito: el refresh ya no le serviría a
        // nadie pasado el límite. Si la red falla, se corta igual.
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
            {
                Content = JsonContent.Create(new { refreshToken = _refreshToken }),
            };
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            req.Options.Set(SinSesion, true);
            using var _ = await _http.CreateClient(NombreDelClienteHttp).SendAsync(req, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Sin red no hay a quién avisar; la sesión local se cierra igual.
        }

        await TerminarAsync(CodigoInactividad);
    }

    // ---------- Renovación ----------

    /// <summary>
    /// El access token para la próxima petición: el actual si le queda más de
    /// <see cref="MargenDeRenovacion"/>, o uno recién canjeado. Si la renovación no
    /// pudo hacerse por la red, devuelve el que hay aunque esté por vencer —la
    /// petición saldrá, y si vuelve 401 el handler lo intenta otra vez—. <c>null</c>
    /// sin sesión.
    /// </summary>
    public async Task<string?> TokenVigenteAsync(CancellationToken ct = default)
    {
        if (!await RestaurarAsync()) return null;
        if (!EstaPorVencer()) return _accessToken;
        if (string.IsNullOrWhiteSpace(_refreshToken)) return _accessToken;

        await RenovarAsync(motivo: MotivoDeRenovacion.PorVencer, ct: ct);
        return _accessToken;
    }

    public enum MotivoDeRenovacion
    {
        /// <summary>Al access le queda menos del margen. Si otra petición ya renovó, no se repite.</summary>
        PorVencer,
        /// <summary>El servidor rechazó el access con 401. Se renueva salvo que ya lo hayan reemplazado.</summary>
        Rechazado,
        /// <summary>Mantener fresca la rotación de una pestaña activa. Si ya rotó hace poco, no se repite.</summary>
        MantenerViva,
        /// <summary>La persona lo pidió («seguir trabajando»): se rota sí o sí, para que el servidor lo vea.</summary>
        Explicita,
    }

    /// <summary>
    /// Cuánto espera <b>quien pide</b> el canje ya en curso. Pasado, para él es transitorio como sin red: sale con el token que
    /// hay y se reintenta en la próxima petición. El canje <b>no</b> se abandona: sigue hasta que el servidor responda y adopta lo
    /// que traiga (ver <see cref="RenovarAsync"/>).
    /// </summary>
    public TimeSpan EsperaDelCanje { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Canjea el refresh. Una sola en vuelo: quien llegue mientras otra corre espera
    /// y sale con el resultado de aquélla. <c>true</c> si hay un access nuevo o el
    /// actual ya sirve. <c>false</c> si la red falló o el canje no terminó dentro de
    /// <see cref="EsperaDelCanje"/> (la sesión sigue) o si el servidor la rechazó (la
    /// sesión se terminó y se avisó).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El canje no es de quien lo pidió.</b> <paramref name="ct"/> sólo vale para esperar el turno y para dejar de esperar el
    /// resultado; la petición del canje y la lectura de su respuesta terminan aunque quien la pidió ya no la necesite. Hasta el
    /// 2026-11-12 viajaban con el token de la petición que disparó la renovación: los buscadores que cancelan su consulta en cada
    /// tecla abortaban el canje después de que el servidor ya había rotado el refresh, el cliente se quedaba con el viejo, lo volvía
    /// a canjear y el servidor, viendo un reuso, invalidaba la familia: la persona salía al login a la hora de trabajo («Refresh
    /// token reusado detectado»).
    /// </para>
    /// <para>
    /// <b>Y ningún reloj del cliente lo corta.</b> Desde el 2026-11-12 hasta el 2026-11-26 el canje llevaba su propia espera de
    /// treinta segundos. En una pestaña en segundo plano el navegador despierta los temporizadores una vez por minuto: el servidor
    /// rotaba en milisegundos, pero la respuesta se procesaba un minuto después, cuando la espera ya había vencido; el cliente la
    /// descartaba como «sin respuesta», se quedaba con el refresh viejo y a los cinco minutos (el aviso del correo en prueba) lo
    /// volvía a canjear: reuso, familia invalidada, y la persona encontraba la sesión cerrada a los 20 o 25 minutos aunque la
    /// cooperativa tuviera 60 (PDN, 2026-11-26). Ahora la petición sale sin token de cancelación y con el cliente HTTP sin tiempo
    /// límite; la espera sólo decide cuánto aguarda quien pidió. Una red colgada la corta el proxy (Cloudflare responde 524 a los
    /// cien segundos), no el cliente.
    /// </para>
    /// </remarks>
    /// <param name="motivo">Por qué se renueva; decide cuándo una renovación ya hecha por otro basta.</param>
    /// <param name="tokenRechazado">Con <see cref="MotivoDeRenovacion.Rechazado"/>, el access que el servidor no aceptó.</param>
    /// <param name="ct">El de quien pide: espera el turno y el resultado, nunca el canje.</param>
    public async Task<bool> RenovarAsync(
        MotivoDeRenovacion motivo = MotivoDeRenovacion.PorVencer,
        string? tokenRechazado = null,
        CancellationToken ct = default)
    {
        if (!await RestaurarAsync()) return false;

        // Esperar el turno sí respeta a quien llama: si ya no necesita el token, no se queda en la fila.
        await _unaRenovacionALaVez.WaitAsync(ct);

        // Con el turno tomado, el canje sigue solo; quien llama deja de esperarlo si se cancela o pasada la espera, pero el canje
        // termina, adopta los tokens nuevos (o decide que la sesión terminó) y suelta el turno. Quien llegue después espera ese
        // turno y encuentra los tokens ya adoptados.
        var canje = CanjearConElTurnoAsync(motivo, tokenRechazado);
        try
        {
            return await canje.WaitAsync(EsperaDelCanje, ct);
        }
        catch (TimeoutException)
        {
            return false; // transitorio para quien pidió; el canje no se abandona
        }
    }

    /// <summary>El canje, con el turno ya tomado; lo suelta al terminar, pase lo que pase.</summary>
    private async Task<bool> CanjearConElTurnoAsync(MotivoDeRenovacion motivo, string? tokenRechazado)
    {
        // Sin token de cancelación a propósito (ver «Y ningún reloj del cliente lo corta» en RenovarAsync): lo que el servidor ya
        // rotó tiene que llegar a adoptarse, tarde lo que tarde la pestaña en procesarlo.
        var ct = CancellationToken.None;
        IAsyncDisposable? turnoDelNavegador = null;
        try
        {
            // El turno de todas las pestañas del navegador (2026-11-14, ver ICoordinacionEntrePestanas): una pestaña duplicada
            // tiene el mismo refresh, y si las dos lo canjean el servidor ve un reuso y mata la sesión de ambas. Con el turno
            // tomado, si otra ya canjeó este refresh se adoptan sus tokens en vez de canjearlo otra vez.
            try
            {
                turnoDelNavegador = await _pestanas.TomarTurnoAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return false; // otra pestaña retuvo el turno toda la espera: transitorio, como sin red
            }
            await AdoptarLoQueCanjeoOtraPestanaAsync();

            // Otra petición pudo renovar mientras esperábamos el semáforo.
            var yaSirve = motivo switch
            {
                MotivoDeRenovacion.Rechazado => !string.Equals(_accessToken, tokenRechazado, StringComparison.Ordinal),
                MotivoDeRenovacion.MantenerViva => Ahora - _ultimaRotacion < Politica.Inactividad - Politica.MargenDeMantenimiento,
                MotivoDeRenovacion.Explicita => false,
                _ => !EstaPorVencer(),
            };
            if (yaSirve) return true;
            if (string.IsNullOrWhiteSpace(_refreshToken)) return false;

            var refreshQueSeGasta = _refreshToken;
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
            {
                Content = JsonContent.Create(new { refreshToken = refreshQueSeGasta }),
            };
            req.Options.Set(SinSesion, true);

            HttpResponseMessage resp;
            try
            {
                // El cliente HTTP sin tiempo límite: HttpClient.Timeout (cien segundos) también es un reloj del cliente, y en una
                // pestaña en segundo plano descartaría una respuesta que el servidor ya dio.
                var cliente = _http.CreateClient(NombreDelClienteHttp);
                cliente.Timeout = Timeout.InfiniteTimeSpan;
                resp = await cliente.SendAsync(req, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Sin red no hay veredicto: la sesión sigue y se reintenta en la próxima
                // petición. Terminarla aquí echaría a alguien por un corte de wifi de dos
                // segundos. OperationCanceledException ya no la provoca este cliente (no
                // lleva token ni tiempo límite); queda por si la plataforma corta la petición.
                return false;
            }

            using (resp)
            {
                if (resp.IsSuccessStatusCode)
                {
                    RespuestaDeRefresh? cuerpo;
                    try
                    {
                        cuerpo = await resp.Content.ReadFromJsonAsync<RespuestaDeRefresh>(cancellationToken: ct);
                    }
                    catch (Exception ex) when (ex is JsonException or HttpRequestException or OperationCanceledException)
                    {
                        // Un 200 que no es JSON no es la API: un portal cautivo, un proxy
                        // caído. Un cuerpo que no termina de llegar, igual. Transitorio, como
                        // sin red; la sesión sigue.
                        return false;
                    }
                    if (cuerpo is null || string.IsNullOrWhiteSpace(cuerpo.AccessToken))
                    {
                        await TerminarAsync("Identity.RefreshToken.RespuestaVacia");
                        return false;
                    }

                    await AdoptarAsync(
                        cuerpo.AccessToken, cuerpo.AccessTokenExpiresAt,
                        cuerpo.RefreshToken, cuerpo.RefreshTokenExpiresAt,
                        esActividad: false);
                    // Para la pestaña gemela, si la hay: lo que trajo el canje queda bajo el refresh que se gastó.
                    await _pestanas.PublicarSucesorAsync(refreshQueSeGasta, new SucesorDeSesion(
                        cuerpo.AccessToken, cuerpo.AccessTokenExpiresAt, cuerpo.RefreshToken, cuerpo.RefreshTokenExpiresAt, _ultimaRotacion));
                    return true;
                }

                if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
                    or HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity)
                {
                    // Un veredicto del servidor: el refresh no vale y no va a valer.
                    await TerminarAsync(await LeerCodigoAsync(resp, ct));
                    return false;
                }

                // 5xx y similares: transitorio, misma regla que sin red.
                return false;
            }
        }
        finally
        {
            if (turnoDelNavegador is not null) await turnoDelNavegador.DisposeAsync();
            _unaRenovacionALaVez.Release();
        }
    }

    /// <summary>
    /// Si otra pestaña con esta misma sesión ya canjeó el refresh que tenemos, sus tokens están en el buzón: se adoptan (y los
    /// que ella haya canjeado después, en cadena). Adoptar no es actividad: es la misma rotación que hizo la otra.
    /// </summary>
    private async Task AdoptarLoQueCanjeoOtraPestanaAsync()
    {
        for (var vuelta = 0; vuelta < 50 && !string.IsNullOrWhiteSpace(_refreshToken); vuelta++)
        {
            var sucesor = await _pestanas.SucesorDeAsync(_refreshToken!);
            if (sucesor is null || string.IsNullOrWhiteSpace(sucesor.AccessToken)
                || string.Equals(sucesor.RefreshToken, _refreshToken, StringComparison.Ordinal))
                return;
            await AdoptarAsync(sucesor.AccessToken, sucesor.AccessTokenExpiresAt, sucesor.RefreshToken, sucesor.RefreshTokenExpiresAt,
                esActividad: false);
            // La rotación es la de la otra pestaña, no ahora: el mantenimiento se calcula desde ella.
            _ultimaRotacion = sucesor.RotadoEn;
            await _storage.SetAsync(ClaveUltimaRotacion, _ultimaRotacion.ToString("o", CultureInfo.InvariantCulture));
            if (string.IsNullOrWhiteSpace(sucesor.RefreshToken)) return;
        }
    }

    private bool EstaPorVencer() => _accessTokenExpiresAt <= Ahora.Add(MargenDeRenovacion);

    private Task TerminarAsync(string codigo)
    {
        Limpiar();
        SesionTerminada?.Invoke(codigo);
        return Task.CompletedTask;
    }

    private static async Task<string> LeerCodigoAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var sobre = await resp.Content.ReadFromJsonAsync<SobreDeError>(cancellationToken: ct);
            return string.IsNullOrWhiteSpace(sobre?.Code) ? $"Http.{(int)resp.StatusCode}" : sobre.Code;
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or OperationCanceledException)
        {
            return $"Http.{(int)resp.StatusCode}";
        }
    }

    // ---------- Lectura de tokens ----------

    /// <summary>Claim <c>exp</c> del JWT, en UTC. <c>null</c> si no es un JWT o no lo trae.</summary>
    public static DateTime? LeerVencimientoDelJwt(string jwt)
    {
        try
        {
            var partes = jwt.Split('.');
            if (partes.Length < 2) return null;
            var payload = partes[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("exp", out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).UtcDateTime
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static DateTime? LeerFecha(string? iso)
    {
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var f))
            return null;
        return f.Kind switch
        {
            DateTimeKind.Utc => f,
            DateTimeKind.Local => f.ToUniversalTime(),
            _ => DateTime.SpecifyKind(f, DateTimeKind.Utc),
        };
    }

    private sealed record RespuestaDeRefresh(
        string AccessToken,
        DateTime? AccessTokenExpiresAt,
        string? RefreshToken,
        DateTime? RefreshTokenExpiresAt);

    private sealed record SobreDeError(string? Code, string? Message, string? TraceId);

    private sealed record RespuestaDePolitica(int InactivityMinutes, int MaxDurationHours);

    private sealed record RespuestaDePoliticaDeLaCooperativa(bool CountDeviceActivity, bool? NewWindowInheritsSession = null,
        int? InactivityMinutes = null);
}

/// <summary>Los dos límites de una sesión, tal como los hace cumplir el servidor.</summary>
public sealed record PoliticaDeSesion(TimeSpan Inactividad, TimeSpan DuracionMaxima)
{
    public static readonly PoliticaDeSesion PorDefecto = new(TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));

    /// <summary>
    /// Cuánto antes del límite de inactividad rota sola una pestaña activa: dos
    /// minutos, o un cuarto del límite si éste es muy corto.
    /// </summary>
    public TimeSpan MargenDeMantenimiento =>
        TimeSpan.FromTicks(Math.Min(TimeSpan.FromMinutes(2).Ticks, Inactividad.Ticks / 4));
}
