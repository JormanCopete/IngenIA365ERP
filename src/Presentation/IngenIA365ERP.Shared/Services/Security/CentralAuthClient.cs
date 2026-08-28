using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// T077 — Cliente del flujo de identidad central (Feature 002).
/// Reemplaza a <see cref="AuthClient"/> legacy de Fase 0 que requería tenant
/// en el request de login.
///
/// <para>
/// Mantiene en memoria DOS sets de tokens:
/// <list type="bullet">
///   <item><c>operational</c> — el JWT con <c>purpose=full</c> + <c>active_tenant_id</c>
///         emitido por login mono-tenant o tras MFA/select-tenant.</item>
///   <item><c>challenge</c> — un JWT temporal con <c>purpose</c> acotado
///         (mfa-verify | mfa-enroll | tenant-select) que solo es válido para
///         su endpoint correspondiente.</item>
/// </list>
/// El usuario nunca tiene ambos a la vez: cuando llega el operacional, el
/// challenge se descarta.
/// </para>
/// </summary>
public sealed class CentralAuthClient
{
    private const string RefreshTokenKey = "refresh_token";

    private readonly HttpClient _http;
    private readonly ISecureStorage _storage;
    private readonly AuthenticationStateProvider _authState;

    // Operational session.
    private string? _accessToken;
    private DateTime _accessTokenExpiresAt;
    private string? _refreshToken;

    // Challenge token (temporary, scoped).
    private string? _challengeToken;

    public CentralAuthClient(
        HttpClient http, ISecureStorage storage, AuthenticationStateProvider authState)
    {
        _http = http;
        _storage = storage;
        _authState = authState;
    }

    public string? CurrentAccessToken => _accessToken;

    private bool _restoreAttempted;

    /// <summary>
    /// Feature 003 (US4, FR-113): rehidrata la sesión desde el storage del
    /// navegador tras una recarga. Este servicio es scoped — un F5 lo
    /// reconstruye con los campos vacíos aunque sessionStorage aún tenga los
    /// tokens. Idempotente; retorna true si hay sesión utilizable.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        if (_accessToken is not null) return true;
        if (_restoreAttempted) return false;
        _restoreAttempted = true;

        var stored = await _storage.GetAsync(AuthBearerHandler.TokenKey);
        if (string.IsNullOrWhiteSpace(stored)) return false;

        _accessToken = stored;
        _accessTokenExpiresAt = ReadJwtExpiryUtc(stored) ?? DateTime.UtcNow.AddMinutes(5);
        _refreshToken = await _storage.GetAsync(RefreshTokenKey);
        return true;
    }

    private static DateTime? ReadJwtExpiryUtc(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("exp", out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).UtcDateTime
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// JWT temporal scoped (purpose=mfa-verify, mfa-enroll, tenant-select)
    /// retenido por el cliente para usar en el siguiente request. Útil para
    /// Phase 4b — la página de enrollment MFA forzado usa este token.
    /// </summary>
    public string? CurrentChallengeToken => _challengeToken;

    /// <summary>
    /// US3 — Lista de tenants devuelta en el último LoginResponse que llegó
    /// con challenge=TenantSelection. <c>SelectTenant.razor</c> la consume
    /// directamente para no requerir un request extra a <c>/api/sessions/active-tenants</c>
    /// (que rechazaría el challenge token con purpose=tenant-select).
    /// </summary>
    public IReadOnlyList<ActiveTenantSummary>? LastLoginTenants { get; private set; }

    /// <summary>
    /// Los métodos que le servirían, del último desafío de inscripción. <c>null</c>
    /// cuando el servidor no lo dijo — y entonces la pantalla ofrece los dos, que
    /// es el comportamiento de siempre y el correcto mientras ninguna cooperativa
    /// restrinja.
    /// </summary>
    public IReadOnlyList<string>? MetodosQueLeServirian { get; private set; }

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTime.UtcNow;

    public event EventHandler? Authenticated;
    public event EventHandler? SignedOut;

    // ---------- Login ----------

    public async Task<InvitationApiResult<LoginResponse>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/auth/login", new { email, password }, ct);
            var parsed = await CentralAuthApi.ParseAsync<LoginResponse>(resp, ct);
            if (parsed.IsSuccess && parsed.Value is { } body)
            {
                await RouteLoginResponseAsync(body);
            }
            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<LoginResponse>.NetworkError(ex.Message);
        }
    }

    /// <summary>Feature 003 (US5, FR-116): adopta un challenge token emitido por
    /// otro flujo (accept de invitación) para encadenar al paso MFA
    /// (mfa-challenge / enroll-mfa-forced) sin re-login.</summary>
    public void AdoptChallengeToken(string challengeToken)
    {
        _challengeToken = challengeToken;
    }

    // ---------- MFA verify ----------

    public async Task<InvitationApiResult<LoginResponse>> MfaVerifyAsync(
        string code, bool useRecoveryCode = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_challengeToken))
        {
            return InvitationApiResult<LoginResponse>.Failure(
                "Identity.NoChallengeToken",
                "Falta el token de challenge. Reinicia el login.", 0);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/verify")
            {
                Content = JsonContent.Create(new { code, useRecoveryCode }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _challengeToken);

            var resp = await _http.SendAsync(req, ct);
            var parsed = await CentralAuthApi.ParseAsync<LoginResponse>(resp, ct);
            if (parsed.IsSuccess && parsed.Value is { } body)
            {
                await RouteLoginResponseAsync(body);
            }
            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<LoginResponse>.NetworkError(ex.Message);
        }
    }

    // ---------- Ingreso con passkey ----------

    /// <summary>
    /// Primer viaje: el reto y la lista de llaves que esta persona puede usar.
    /// Va con el challenge token, igual que el verify del TOTP — la contraseña ya
    /// se acertó, lo que falta es el segundo factor.
    /// </summary>
    public async Task<InvitationApiResult<WebAuthnChallengeResponse>> WebAuthnChallengeAsync(
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_challengeToken))
        {
            return InvitationApiResult<WebAuthnChallengeResponse>.Failure(
                "Identity.NoChallengeToken",
                "Falta el token de challenge. Reinicia el login.", 0);
        }

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post, "/api/auth/mfa/webauthn/challenge");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _challengeToken);

            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<WebAuthnChallengeResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<WebAuthnChallengeResponse>.NetworkError(ex.Message);
        }
    }

    /// <summary>
    /// Segundo viaje: la firma. Si verifica, lo que vuelve es exactamente el mismo
    /// <see cref="LoginResponse"/> que tras un TOTP correcto —con su selección de
    /// cooperativa incluida—, así que se encamina por el mismo sitio.
    /// </summary>
    public async Task<InvitationApiResult<LoginResponse>> WebAuthnVerifyAsync(
        string retoId, string respuestaJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_challengeToken))
        {
            return InvitationApiResult<LoginResponse>.Failure(
                "Identity.NoChallengeToken",
                "Falta el token de challenge. Reinicia el login.", 0);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/mfa/webauthn/verify")
            {
                Content = JsonContent.Create(new { retoId, respuestaJson }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _challengeToken);

            var resp = await _http.SendAsync(req, ct);
            var parsed = await CentralAuthApi.ParseAsync<LoginResponse>(resp, ct);
            if (parsed.IsSuccess && parsed.Value is { } body)
            {
                await RouteLoginResponseAsync(body);
            }
            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<LoginResponse>.NetworkError(ex.Message);
        }
    }

    // ---------- Select tenant ----------

    public async Task<InvitationApiResult<SelectTenantResponse>> SelectTenantAsync(
        Guid tenantPublicId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_challengeToken))
        {
            return InvitationApiResult<SelectTenantResponse>.Failure(
                "Identity.NoChallengeToken",
                "Falta el token de challenge. Reinicia el login.", 0);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/sessions/select-tenant")
            {
                Content = JsonContent.Create(new { tenantPublicId }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _challengeToken);

            var resp = await _http.SendAsync(req, ct);
            var parsed = await CentralAuthApi.ParseAsync<SelectTenantResponse>(resp, ct);
            if (parsed.IsSuccess && parsed.Value is { } body)
            {
                await AdoptSessionAsync(body.AccessToken, body.AccessTokenExpiresAt, body.RefreshToken);
            }
            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<SelectTenantResponse>.NetworkError(ex.Message);
        }
    }

    // ---------- Me (identidad de la sesión, T001 feature 003) ----------

    private MeResponse? _me;

    /// <summary>
    /// Identidad de la sesión actual (<c>GET /api/auth/me</c>): email, empresa
    /// activa con nombre, tenants disponibles, estado MFA y códigos de
    /// recuperación restantes. Cacheado por sesión — se invalida al adoptar
    /// una sesión nueva (login/switch/accept) y al cerrar sesión.
    /// </summary>
    public async Task<MeResponse?> GetMeAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        if (_me is not null && !forceRefresh) return _me;
        if (string.IsNullOrWhiteSpace(_accessToken) && !await TryRestoreSessionAsync()) return null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            var resp = await _http.SendAsync(req, ct);
            var parsed = await CentralAuthApi.ParseAsync<MeResponse>(resp, ct);
            _me = parsed.IsSuccess ? parsed.Value : null;
            return _me;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    // ---------- Logout ----------

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout")
                {
                    Content = JsonContent.Create(new { refreshToken = _refreshToken }),
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                await _http.SendAsync(req, ct);
            }
            catch (HttpRequestException ex)
            {
                // Best-effort: si la red falla, limpiar tokens locales igual.
                // El refresh queda activo en Redis hasta su TTL natural (12h);
                // el usuario simplemente vuelve a loguear si se reconecta.
                System.Diagnostics.Debug.WriteLine($"CentralAuth logout HTTP fall: {ex.Message}");
            }
        }

        _accessToken = null;
        _accessTokenExpiresAt = DateTime.MinValue;
        _refreshToken = null;
        _challengeToken = null;
        _me = null;

        _storage.Remove(AuthBearerHandler.TokenKey);
        _storage.Remove(RefreshTokenKey);
        (_authState as CustomAuthStateProvider)?.NotifyUserLogout();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Adopción de sesión (cutover T077) ----------

    /// <summary>
    /// Convierte los tokens centrales en LA sesión de la app: persiste en las
    /// keys que leen <c>CustomAuthStateProvider</c> y <c>AuthBearerHandler</c>
    /// (<c>auth_token</c>/<c>refresh_token</c>) y notifica el cambio de estado
    /// para que <c>AuthorizeRouteView</c> re-evalúe sin re-login.
    /// </summary>
    public async Task AdoptSessionAsync(
        string accessToken, DateTime? accessTokenExpiresAt, string? refreshToken)
    {
        _accessToken = accessToken;
        _accessTokenExpiresAt = accessTokenExpiresAt ?? DateTime.UtcNow.AddMinutes(15);
        _refreshToken = refreshToken;
        _challengeToken = null;
        _me = null; // la identidad cacheada cambia con cada sesión adoptada

        await _storage.SetAsync(AuthBearerHandler.TokenKey, accessToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await _storage.SetAsync(RefreshTokenKey, refreshToken);

        (_authState as CustomAuthStateProvider)?.NotifyUserAuthentication(accessToken);
        Authenticated?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Routing del LoginResponse ----------

    private async Task RouteLoginResponseAsync(LoginResponse body)
    {
        // US3: si hay TenantSelection, retén la lista para SelectTenant.razor.
        if (body.Challenge == "TenantSelection" && body.ActiveTenants is { Count: > 0 })
        {
            LastLoginTenants = body.ActiveTenants;
        }

        // Y si hay que inscribir, qué métodos le servirían — para que la pantalla
        // de inscripción no le ofrezca el que volvería a dejarlo fuera. Se retiene
        // igual que la lista de cooperativas y por el mismo motivo: el token de
        // inscripción no sirve para volver a preguntarlo.
        if (body.Challenge == "MfaEnrollmentRequired")
        {
            MetodosQueLeServirian = body.MetodosAceptados;
        }

        if (body.Challenge == "None"
            && !string.IsNullOrWhiteSpace(body.AccessToken)
            && !string.IsNullOrWhiteSpace(body.RefreshToken))
        {
            await AdoptSessionAsync(body.AccessToken, body.AccessTokenExpiresAt, body.RefreshToken);
        }
        else if (!string.IsNullOrWhiteSpace(body.ChallengeToken))
        {
            // MfaRequired / MfaEnrollmentRequired / TenantSelection — guarda el
            // challenge token; el siguiente request (mfa/verify, select-tenant)
            // lo enviará en Authorization.
            _challengeToken = body.ChallengeToken;
        }
    }
}

// -------------------- DTOs --------------------

public sealed record LoginResponse(
    string Challenge,
    Guid? CentralUserId,
    string? Email,
    bool? IsGlobalMasterAdmin,
    string? AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    string? ChallengeToken,
    string? ChallengeTokenPurpose,
    int? ExpiresInSeconds,
    IReadOnlyList<ActiveTenantSummary>? ActiveTenants,
    Guid? DefaultTenantPublicId,
    bool? AutoSelected,
    Guid? ActiveTenantPublicId,
    string? ActiveTenantName,
    string? Message,
    IReadOnlyList<TenantSummary>? TenantsRequiringMfa,
    int? RecoveryCodesRemaining = null,

    /// <summary>
    /// Con MfaEnrollmentRequired: que metodos le serviran, como literales
    /// ("Totp", "WebAuthn"). La pantalla de inscripcion ofrece SOLO estos; sin
    /// esto ofreceria los dos y el que no sirve vuelve a encerrar a la persona,
    /// con el agravante de que ya cree que lo resolvio.
    /// </summary>
    IReadOnlyList<string>? MetodosAceptados = null);

/// <param name="AdmiteTuMetodo">
/// Si esa cooperativa acepta el metodo con el que la persona acaba de entrar. Se
/// muestra atenuada en vez de esconderla: una cooperativa a la que pertenece y
/// que desaparece sin explicacion es peor que una que aparece diciendo por que.
/// </param>
public sealed record ActiveTenantSummary(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin,
    bool AdmiteTuMetodo = true);

public sealed record TenantSummary(
    Guid TenantPublicId,
    string TenantName);

public sealed record SelectTenantResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    int ExpiresInSeconds,
    SelectedTenantInfo Tenant);

public sealed record SelectedTenantInfo(Guid TenantPublicId, string TenantName);

// -------------------- Me (GET /api/auth/me) --------------------

public sealed record MeResponse(
    Guid CentralUserId,
    string Email,
    bool IsGlobalMasterAdmin,
    bool MfaEnabled,
    MeActiveTenant? ActiveTenant,
    IReadOnlyList<MeAvailableTenant> AvailableTenants,
    Guid? DefaultTenantPublicId,
    int? RecoveryCodesRemaining = null);

public sealed record MeActiveTenant(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin,
    bool IsMfaRequiredByPolicy);

public sealed record MeAvailableTenant(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin);

// -------------------- HTTP envelope helper --------------------

internal static class CentralAuthApi
{
    public static async Task<InvitationApiResult<T>> ParseAsync<T>(
        HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            // 204 No Content (change-password, forgot/reset, suspend/activate,
            // mfa-policy, default-tenant, force-mfa-reset...) — no hay JSON que
            // deserializar; ReadFromJsonAsync lanzaría JsonException.
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent ||
                resp.Content.Headers.ContentLength is 0)
            {
                return typeof(T) == typeof(EmptyResponse)
                    ? InvitationApiResult<T>.Success((T)(object)new EmptyResponse())
                    : InvitationApiResult<T>.Failure("Generic.EmptyResponse",
                        "La respuesta del servidor está vacía.", (int)resp.StatusCode);
            }

            var value = await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return value is null
                ? InvitationApiResult<T>.Failure("Generic.EmptyResponse",
                    "La respuesta del servidor está vacía.", (int)resp.StatusCode)
                : InvitationApiResult<T>.Success(value);
        }

        try
        {
            var envelope = await resp.Content.ReadFromJsonAsync<ErrorEnvelopeDto>(cancellationToken: ct);
            return InvitationApiResult<T>.Failure(
                envelope?.Code ?? "Generic.Failure",
                envelope?.Message ?? "Error sin detalles.",
                (int)resp.StatusCode);
        }
        catch
        {
            return InvitationApiResult<T>.Failure(
                "Generic.Failure", $"Error HTTP {(int)resp.StatusCode}.", (int)resp.StatusCode);
        }
    }

    private sealed record ErrorEnvelopeDto(string? Code, string? Message, string? TraceId);
}
