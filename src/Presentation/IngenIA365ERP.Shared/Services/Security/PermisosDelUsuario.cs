using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Los permisos efectivos del usuario en la cooperativa activa, para que la interfaz oculte lo
/// que la API negaría (feature 008, FR-011). Se piden <b>una vez por cooperativa</b> a
/// <c>GET /api/admin/permissions/mine</c> y se recuerdan hasta que la sesión cambia
/// (<see cref="CentralAuthClient.Authenticated"/> al entrar o al cambiar de cooperativa,
/// <see cref="CentralAuthClient.SignedOut"/> al salir). Varios <c>PermissionGate</c> en la misma
/// pantalla comparten una sola petición en vuelo.
///
/// <para>
/// <b>Falla cerrado, en dos grados.</b> Sin sesión o sin cooperativa activa —el prerender del
/// Web, las pantallas de ingreso, el maestro sin cooperativa— no hay nada que preguntar: la
/// respuesta es «ningún permiso» <b>en silencio</b> y sin tocar la red. Con sesión y cooperativa
/// pero la llamada falla, la respuesta también es «ningún permiso», y además se avisa una sola
/// vez (Principio IX): la pantalla se ve sin botones y la persona sabe por qué.
/// </para>
///
/// <para>
/// El maestro global no lleva permisos por cooperativa (la puerta de la API lo deja pasar por
/// un atajo); aquí se lo trata igual: <see cref="TieneAsync"/> responde <c>true</c> a todo.
/// </para>
/// </summary>
public sealed class PermisosDelUsuario : IDisposable
{
    private readonly HttpClient _http;
    private readonly CentralAuthClient _auth;
    private readonly AuthenticationStateProvider _authState;
    private readonly INotificationService _notification;

    private Task<Estado>? _enVuelo;
    private bool _avisado;

    public PermisosDelUsuario(
        HttpClient http,
        CentralAuthClient auth,
        AuthenticationStateProvider authState,
        INotificationService notification)
    {
        _http = http;
        _auth = auth;
        _authState = authState;
        _notification = notification;
        _auth.Authenticated += OnSesionCambio;
        _auth.SignedOut += OnSesionCambio;
        // Restaurar la sesión tras un F5 no pasa por Authenticated pero sí cambia el estado de
        // autenticación; sin esto un gate que preguntó antes de la restauración se quedaría cerrado.
        _authState.AuthenticationStateChanged += OnEstadoCambio;
    }

    /// <summary>Se dispara al invalidar (cambio de sesión o de cooperativa) para que los gates se recalculen.</summary>
    public event Action? Cambiaron;

    /// <summary>¿Tiene este código en la cooperativa activa? El maestro global siempre sí.</summary>
    public async Task<bool> TieneAsync(string codigo, CancellationToken ct = default)
    {
        var estado = await ObtenerAsync(forzar: false, ct);
        return estado.EsMaestro || estado.Codigos.Contains(codigo);
    }

    /// <summary>¿Tiene alguno de estos códigos?</summary>
    public async Task<bool> TieneAlgunoAsync(IEnumerable<string> codigos, CancellationToken ct = default)
    {
        var estado = await ObtenerAsync(forzar: false, ct);
        return estado.EsMaestro || codigos.Any(estado.Codigos.Contains);
    }

    /// <summary>Los códigos concedidos (vacío si no hay sesión, cooperativa o la carga falló) y si es el maestro.</summary>
    public async Task<Estado> ObtenerAsync(bool forzar = false, CancellationToken ct = default)
    {
        if (forzar) _enVuelo = null;
        var tarea = _enVuelo ??= CargarAsync(ct);
        var estado = await tarea;
        // Un «nada» por falta de sesión o cooperativa es provisional: la sesión puede
        // restaurarse un instante después (F5) y la próxima consulta tiene que volver a mirar.
        // Lo que sí se recuerda es una respuesta de la API, buena o mala.
        if (estado.Provisional && ReferenceEquals(_enVuelo, tarea)) _enVuelo = null;
        return estado;
    }

    /// <summary>Olvida lo cargado; la próxima consulta vuelve a la API.</summary>
    public void Invalidar()
    {
        _enVuelo = null;
        _avisado = false;
        Cambiaron?.Invoke();
    }

    private async Task<Estado> CargarAsync(CancellationToken ct)
    {
        // Grado 1: sin sesión o sin cooperativa no hay a quién preguntar. Silencio.
        var token = _auth.CurrentAccessToken;
        if (token is null) return Estado.SinSesion;

        var user = (await _authState.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true) return Estado.SinSesion;

        if (string.Equals(user.FindFirst("is_global_master_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            return Estado.Maestro;

        if (string.IsNullOrEmpty(user.FindFirst("active_tenant_id")?.Value)) return Estado.SinSesion;

        // Grado 2: hay sesión y cooperativa; si la API no responde, cerrado y avisando.
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/admin/permissions/mine");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            var resultado = await CentralAuthApi.ParseAsync<PermisosMiosDto>(resp, ct);
            if (!resultado.IsSuccess)
            {
                await AvisarAsync(resultado.ErrorMessage ?? $"HTTP {resultado.StatusCode}");
                return Estado.Nada;
            }
            var dto = resultado.Value!;
            return dto.IsGlobalMasterAdmin
                ? Estado.Maestro
                : new Estado(false, dto.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase));
        }
        catch (HttpRequestException ex)
        {
            await AvisarAsync(ex.Message);
            return Estado.Nada;
        }
        catch (System.Text.Json.JsonException ex)
        {
            await AvisarAsync($"respuesta inesperada: {ex.Message}");
            return Estado.Nada;
        }
    }

    private async Task AvisarAsync(string detalle)
    {
        if (_avisado) return;
        _avisado = true;
        // Un fallo aquí no puede tumbar la pantalla: el gate ya quedó cerrado.
        try
        {
            await _notification.ErrorAsync(
                $"No se pudieron cargar tus permisos; las acciones quedan ocultas hasta recargar. Detalle: {detalle}");
        }
        catch (Exception ex)
        {
            // La notificación falló (JS interop sin circuito, p. ej.); el gate cerrado ya es la
            // respuesta correcta, y el motivo queda en la consola del navegador (Principio IX).
            Console.Error.WriteLine($"[PermisosDelUsuario] Sin permisos ({detalle}) y no se pudo avisar: {ex.Message}");
        }
    }

    private void OnSesionCambio(object? sender, EventArgs e) => Invalidar();

    private void OnEstadoCambio(Task<AuthenticationState> _) => Invalidar();

    public void Dispose()
    {
        _auth.Authenticated -= OnSesionCambio;
        _auth.SignedOut -= OnSesionCambio;
        _authState.AuthenticationStateChanged -= OnEstadoCambio;
    }

    /// <summary>
    /// Lo que se sabe del usuario: si es el maestro y qué códigos tiene.
    /// <paramref name="Provisional"/> marca el «nada» por falta de sesión o cooperativa, que no
    /// se recuerda; el «nada» que respondió la API sí.
    /// </summary>
    public sealed record Estado(bool EsMaestro, IReadOnlySet<string> Codigos, bool Provisional = false)
    {
        private static readonly IReadOnlySet<string> Vacio = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static readonly Estado Nada = new(false, Vacio);
        public static readonly Estado SinSesion = new(false, Vacio, Provisional: true);
        public static readonly Estado Maestro = new(true, Vacio);
    }
}
