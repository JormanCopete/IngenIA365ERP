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
    public const string NombreDelClienteHttp = "api";

    /// <summary>Código con el que esta pestaña termina su propia sesión por inactividad.</summary>
    public const string CodigoInactividad = "Identity.Session.Inactivity";

    /// <summary>
    /// Marca de petición que los handlers dejan pasar sin tocar: es la que canjea el
    /// refresh, y adjuntarle un access vencido o intentar renovarla otra vez sería
    /// recursión.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> SinSesion = new("IngenIA365ERP.SinSesion");

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
    private DateTime _accessTokenExpiresAt;
    private string? _refreshToken;
    private DateTime? _vencimientoDeSesion;
    private DateTime _ultimaRotacion;
    private DateTime _ultimaActividad;
    private bool _restaurado;
    private bool _politicaLeida;

    public RenovadorDeSesion(ISecureStorage storage, IHttpClientFactory http, TimeProvider? reloj = null)
    {
        _storage = storage;
        _http = http;
        _reloj = reloj ?? TimeProvider.System;
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

    /// <summary>Una petición con sesión acaba de salir: la pestaña está viva.</summary>
    public void RegistrarActividad() => _ultimaActividad = Ahora;

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
                TimeSpan.FromMinutes(cuerpo.InactivityMinutes),
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
        _accessToken = accessToken;
        _accessTokenExpiresAt = accessTokenExpiresAt ?? LeerVencimientoDelJwt(accessToken) ?? Ahora.AddMinutes(15);
        if (!string.IsNullOrWhiteSpace(refreshToken)) _refreshToken = refreshToken;
        if (refreshTokenExpiresAt is not null) _vencimientoDeSesion = refreshTokenExpiresAt;
        // Tokens nuevos = rotación en el servidor.
        _ultimaRotacion = Ahora;
        if (esActividad) _ultimaActividad = Ahora;

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
        _accessTokenExpiresAt = DateTime.MinValue;
        _refreshToken = null;
        _vencimientoDeSesion = null;
        _restaurado = true;
        _storage.Remove(ClaveAccessToken);
        _storage.Remove(ClaveRefreshToken);
        _storage.Remove(ClaveVencimientoDeSesion);
        _storage.Remove(ClaveUltimaRotacion);
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

    private async Task CortarPorInactividadAsync(CancellationToken ct)
    {
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
    /// Canjea el refresh. Una sola en vuelo: quien llegue mientras otra corre espera
    /// y sale con el resultado de aquélla. <c>true</c> si hay un access nuevo o el
    /// actual ya sirve. <c>false</c> si la red falló (la sesión sigue) o si el
    /// servidor la rechazó (la sesión se terminó y se avisó).
    /// </summary>
    /// <param name="motivo">Por qué se renueva; decide cuándo una renovación ya hecha por otro basta.</param>
    /// <param name="tokenRechazado">Con <see cref="MotivoDeRenovacion.Rechazado"/>, el access que el servidor no aceptó.</param>
    public async Task<bool> RenovarAsync(
        MotivoDeRenovacion motivo = MotivoDeRenovacion.PorVencer,
        string? tokenRechazado = null,
        CancellationToken ct = default)
    {
        if (!await RestaurarAsync()) return false;

        await _unaRenovacionALaVez.WaitAsync(ct);
        try
        {
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

            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
            {
                Content = JsonContent.Create(new { refreshToken = _refreshToken }),
            };
            req.Options.Set(SinSesion, true);

            HttpResponseMessage resp;
            try
            {
                resp = await _http.CreateClient(NombreDelClienteHttp).SendAsync(req, ct);
            }
            catch (HttpRequestException)
            {
                // Sin red no hay veredicto: la sesión sigue y se reintenta en la
                // próxima petición. Terminarla aquí echaría a alguien por un corte
                // de wifi de dos segundos.
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
                    catch (JsonException)
                    {
                        // Un 200 que no es JSON no es la API: un portal cautivo, un proxy
                        // caído. Transitorio, como sin red; la sesión sigue.
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
            _unaRenovacionALaVez.Release();
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
        catch (JsonException)
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
