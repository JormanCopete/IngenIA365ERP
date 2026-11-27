using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.ElectronicInvoicing.Channels.Conecta;

/// <summary>El token pedido, o por qué no se pudo (sin valores secretos en el texto).</summary>
public sealed record TokenObtenido(string? Token, string? Falla, bool PeticionSalio)
{
    public bool Exito => Token is not null;
}

/// <summary>
/// La caché del token de Conecta (feature 028, contracts/canal-conecta.md §5), singleton del proceso: OAuth 2.0 client credentials contra el
/// Keycloak del extremo, con los scopes <c>documents:read documents:write</c>. Guarda un token por (extremo, client id, SHA-256 del secreto)
/// hasta 60 s antes de vencer; si el Secret rota, la huella cambia y se pide otro. Un solo pedido a la vez por clave. Nunca registra ni
/// devuelve el secreto. (nuevo)
/// </summary>
public sealed class TokensDeConecta(TimeProvider? reloj = null)
{
    public const string Scopes = "documents:read documents:write";

    /// <summary>Margen antes del vencimiento en que el token se da por vencido.</summary>
    public static readonly TimeSpan Margen = TimeSpan.FromSeconds(60);

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Vence)> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _candados = new(StringComparer.Ordinal);

    /// <summary>La ruta del token en un extremo (un solo host por ambiente: <c>/auth/realms</c> es Keycloak).</summary>
    public static Uri RutaDelToken(Uri extremo) => new(extremo, "/auth/realms/conecta/protocol/openid-connect/token");

    /// <summary>El token vigente o uno nuevo. <paramref name="renovar"/> descarta el guardado (tras un 401).</summary>
    public async Task<TokenObtenido> ObtenerAsync(HttpClient http, Uri extremo, string clientId, string clientSecret, bool renovar, CancellationToken ct)
    {
        var clave = Clave(extremo, clientId, clientSecret);
        if (!renovar && Vigente(clave) is { } guardado) return new TokenObtenido(guardado, null, false);

        var candado = _candados.GetOrAdd(clave, _ => new SemaphoreSlim(1, 1));
        await candado.WaitAsync(ct);
        try
        {
            if (!renovar && Vigente(clave) is { } otro) return new TokenObtenido(otro, null, false);
            _tokens.TryRemove(clave, out _);

            using var cuerpo = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = Scopes,
            });
            HttpResponseMessage respuesta;
            try
            {
                respuesta = await http.PostAsync(RutaDelToken(extremo), cuerpo, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException && !ct.IsCancellationRequested)
            {
                return new TokenObtenido(null, $"No se pudo pedir el token a Conecta ({ex.GetType().Name}).", false);
            }

            using (respuesta)
            {
                if (!respuesta.IsSuccessStatusCode)
                    return new TokenObtenido(null, $"Conecta no entregó el token (HTTP {(int)respuesta.StatusCode}): revise el client id y el secreto.", true);
                var token = await respuesta.Content.ReadFromJsonAsync<TokenConecta>(JsonDeConecta.Opciones, ct);
                if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
                    return new TokenObtenido(null, "Conecta respondió el token sin access_token.", true);
                var vence = _reloj.GetUtcNow().AddSeconds(Math.Max(0, token.ExpiresIn)) - Margen;
                _tokens[clave] = (token.AccessToken, vence);
                return new TokenObtenido(token.AccessToken, null, true);
            }
        }
        finally
        {
            candado.Release();
        }
    }

    /// <summary>Olvida el token de esa clave (tras un 401 que persiste).</summary>
    public void Olvidar(Uri extremo, string clientId, string clientSecret) => _tokens.TryRemove(Clave(extremo, clientId, clientSecret), out _);

    private string? Vigente(string clave) =>
        _tokens.TryGetValue(clave, out var t) && t.Vence > _reloj.GetUtcNow() ? t.Token : null;

    private static string Clave(Uri extremo, string clientId, string clientSecret) =>
        $"{extremo.GetLeftPart(UriPartial.Authority)}|{clientId}|{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret)))}";
}
