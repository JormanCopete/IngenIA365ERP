using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Preferencias de interfaz del usuario: tema, densidad, escala tipográfica,
/// contraste, página de inicio y favoritos.
///
/// <para>
/// Guarda en dos lugares a propósito. El navegador es la fuente que se lee al
/// arrancar, porque el tema tiene que aplicarse ANTES del primer pintado y para
/// entonces todavía no hay token ni respuesta de la API; si dependiera del
/// servidor, cada carga empezaría en claro y saltaría a oscuro. El servidor es
/// la fuente que sobrevive al cambio de equipo.
/// </para>
///
/// <para>
/// Cuando difieren manda el servidor, y se aplica al iniciar sesión: es lo que
/// el usuario dejó configurado, aunque sea en otra máquina.
/// </para>
/// </summary>
public sealed class PreferenciasClient(
    HttpClient http,
    CentralAuthClient auth,
    IJSRuntime js,
    ILogger<PreferenciasClient> logger)
{
    private const string RutaApi = "/api/profile/preferencias";

    /// <summary>Lo que hay guardado en este navegador. No toca la red.</summary>
    public async Task<PreferenciasUi> LeerLocalesAsync()
    {
        try
        {
            return await js.InvokeAsync<PreferenciasUi>("erpPreferencias.leer") ?? new PreferenciasUi();
        }
        catch (Exception ex) when (EsFaltaDeNavegador(ex))
        {
            RegistrarSinNavegador(ex, "leer las preferencias locales");
            return new PreferenciasUi();
        }
    }

    /// <summary>
    /// Durante el prerenderizado del servidor no hay navegador todavía, y toda
    /// llamada a JS falla. Es una condición esperada del ciclo de vida, no un
    /// fallo: se distingue acá para no tragarse ninguna otra excepción.
    /// </summary>
    private static bool EsFaltaDeNavegador(Exception ex) =>
        ex is JSException or InvalidOperationException;

    private void RegistrarSinNavegador(Exception ex, string operacion) =>
        logger.LogDebug(ex,
            "Sin navegador disponible al intentar {Operacion}; se resolverá en el primer render.",
            operacion);

    /// <summary>
    /// Aplica una preferencia ya mismo y la guarda. La aplicación local es
    /// inmediata y no espera al servidor: el usuario mueve el control y ve el
    /// cambio; si la red falla, la preferencia igual quedó en el navegador.
    /// </summary>
    public async Task<InvitationApiResult<EmptyResponse>> EstablecerAsync(
        IReadOnlyDictionary<string, string?> preferencias, CancellationToken ct = default)
    {
        await AplicarLocalAsync(preferencias);
        return await GuardarEnServidorAsync(preferencias, ct);
    }

    /// <summary>
    /// Trae las preferencias del servidor y las aplica pisando las locales. Se
    /// llama al iniciar sesión.
    /// </summary>
    public async Task<InvitationApiResult<PreferenciasUsuarioRespuesta>> SincronizarAsync(
        CancellationToken ct = default)
    {
        var token = auth.CurrentAccessToken;
        if (token is null) return NoAutenticado<PreferenciasUsuarioRespuesta>();

        InvitationApiResult<PreferenciasUsuarioRespuesta> resultado;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, RutaApi);
            var resp = await http.SendAsync(req, ct);
            resultado = await CentralAuthApi.ParseAsync<PreferenciasUsuarioRespuesta>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<PreferenciasUsuarioRespuesta>.NetworkError(ex.Message);
        }

        if (resultado.IsSuccess && resultado.Value is { } p)
        {
            try
            {
                await js.InvokeVoidAsync("erpPreferencias.aplicarDesdeServidor", ct, new PreferenciasUi
                {
                    Tema = p.Tema,
                    Densidad = p.Densidad,
                    Escala = p.Escala,
                    Contraste = p.Contraste,
                });
            }
            catch (Exception ex) when (EsFaltaDeNavegador(ex))
            {
                RegistrarSinNavegador(ex, "aplicar las preferencias del servidor");
            }
        }

        return resultado;
    }

    private async Task AplicarLocalAsync(IReadOnlyDictionary<string, string?> preferencias)
    {
        // Sólo las de apariencia tienen efecto visual inmediato; la página de
        // inicio y los favoritos no cambian nada de lo que se está viendo.
        var visual = new PreferenciasUi
        {
            Tema = Buscar(preferencias, "ui.tema"),
            Densidad = Buscar(preferencias, "ui.densidad"),
            Escala = Buscar(preferencias, "ui.escala"),
            Contraste = Buscar(preferencias, "ui.contraste"),
        };

        if (visual is { Tema: null, Densidad: null, Escala: null, Contraste: null })
            return;

        try
        {
            await js.InvokeVoidAsync("erpPreferencias.establecer", visual);
        }
        catch (Exception ex) when (EsFaltaDeNavegador(ex))
        {
            RegistrarSinNavegador(ex, "aplicar una preferencia en el navegador");
        }
    }

    private async Task<InvitationApiResult<EmptyResponse>> GuardarEnServidorAsync(
        IReadOnlyDictionary<string, string?> preferencias, CancellationToken ct)
    {
        var token = auth.CurrentAccessToken;
        if (token is null) return NoAutenticado<EmptyResponse>();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, RutaApi)
            {
                Content = JsonContent.Create(new { preferencias }),
            };
            var resp = await http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<EmptyResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<EmptyResponse>.NetworkError(ex.Message);
        }
    }

    private static string? Buscar(IReadOnlyDictionary<string, string?> d, string clave) =>
        d.TryGetValue(clave, out var v) ? v : null;

    private static InvitationApiResult<T> NoAutenticado<T>() =>
        InvitationApiResult<T>.Failure(
            "Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
}

/// <summary>
/// Forma que intercambia con <c>preferencias.js</c>. No hace falta anotar los
/// nombres: la interoperabilidad de Blazor con JS serializa en camelCase, que
/// es justo lo que el script espera (<c>tema</c>, <c>densidad</c>…).
/// </summary>
public sealed class PreferenciasUi
{
    public string? Tema { get; set; }
    public string? Densidad { get; set; }
    public string? Escala { get; set; }
    public string? Contraste { get; set; }
}

public sealed record PreferenciasUsuarioRespuesta(
    string? Tema,
    string? Densidad,
    string? Escala,
    string? Contraste,
    string? PaginaInicio,
    IReadOnlyList<string> Favoritos);
