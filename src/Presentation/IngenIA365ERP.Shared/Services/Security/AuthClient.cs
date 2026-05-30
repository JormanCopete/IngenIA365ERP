using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Cliente del módulo Auth (T056). Tipado, con interceptor 401 → /api/auth/refresh
/// → reintento automático del request original. El refresh y access viven en
/// memoria de la app — para una sesión, no persistencia local (mitigación XSS).
/// </summary>
public sealed class AuthClient
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _accessTokenExpiresAt;
    private string? _refreshToken;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthClient(HttpClient http) => _http = http;

    public string? CurrentAccessToken => _accessToken;
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTime.UtcNow;

    public event EventHandler? Authenticated;
    public event EventHandler? SignedOut;

    public async Task<LoginChallengeResponse?> LoginAsync(string tenantSubdomainOrNit, string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSubdomainOrNit, username, password
        });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<LoginChallengeResponse>();
    }

    public async Task<bool> VerifyMfaAsync(string mfaChallengeToken, string? totpCode, bool useBackupCode,
        string? backupCode, Guid? branchPublicId)
    {
        var resp = await _http.PostAsJsonAsync("/api/auth/mfa/verify", new
        {
            mfaChallengeToken, totpCode, useBackupCode, backupCode, branchPublicId
        });
        if (!resp.IsSuccessStatusCode) return false;
        var tokens = await resp.Content.ReadFromJsonAsync<AuthTokensResponse>();
        if (tokens is null) return false;
        SetTokens(tokens);
        return true;
    }

    public async Task<MfaEnrollmentStartResponse?> EnrollMfaStartAsync(string password)
    {
        var resp = await SendAuthenticatedAsync(() => Request(HttpMethod.Post, "/api/auth/mfa/enroll/start",
            new { password }));
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MfaEnrollmentStartResponse>()
            : null;
    }

    public async Task<List<string>?> EnrollMfaConfirmAsync(string enrollmentToken, string totpCode)
    {
        var resp = await SendAuthenticatedAsync(() => Request(HttpMethod.Post, "/api/auth/mfa/enroll/confirm",
            new { enrollmentToken, totpCode }));
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<MfaBackupCodesResponse>();
        return body?.BackupCodes.ToList();
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var resp = await SendAuthenticatedAsync(() => Request(HttpMethod.Post, "/api/auth/password/change",
            new { currentPassword, newPassword }));
        return resp.IsSuccessStatusCode;
    }

    public async Task<MfaResetRequestResponse?> RequestMfaResetAsync(Guid targetUserPublicId, string reason,
        Guid? evidenceAttachmentPublicId)
    {
        var resp = await SendAuthenticatedAsync(() => Request(HttpMethod.Post, "/api/auth/mfa/reset/request",
            new { targetUserPublicId, reason, evidenceAttachmentPublicId }));
        return resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<MfaResetRequestResponse>()
            : null;
    }

    public async Task<string?> ApproveMfaResetAsync(Guid requestPublicId)
    {
        var resp = await SendAuthenticatedAsync(() => Request(HttpMethod.Post,
            $"/api/auth/mfa/reset/{requestPublicId}/approve", new { }));
        if (!resp.IsSuccessStatusCode) return null;
        var body = await resp.Content.ReadFromJsonAsync<MfaResetApprovalResponse>();
        return body?.Status;
    }

    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(_refreshToken))
        {
            try
            {
                await _http.PostAsJsonAsync("/api/auth/logout", new { refreshToken = _refreshToken });
            }
            catch (HttpRequestException)
            {
                // El backend está caído / sin red. El refresh queda inválido
                // por TTL y el access ya expirará pronto — la sesión local
                // siempre se cierra abajo. Logout es idempotente por diseño.
            }
        }
        ClearTokens();
    }

    // === Interceptor 401 → /refresh ===

    /// <summary>
    /// Envía un request autenticado. Si responde 401 y hay refresh token, intenta
    /// rotarlo (con lock para evitar duplicados) y reintenta una sola vez.
    /// </summary>
    public async Task<HttpResponseMessage> SendAuthenticatedAsync(Func<HttpRequestMessage> requestFactory)
    {
        AttachBearer(out var attempt1);
        var first = attempt1; // captured for reuse below
        var req = requestFactory();
        first.AttachTo(req);

        var resp = await _http.SendAsync(req);
        if (resp.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrEmpty(_refreshToken))
        {
            return resp;
        }

        var refreshed = await TryRefreshAsync();
        if (!refreshed) { ClearTokens(); return resp; }

        // Reintento.
        AttachBearer(out var attempt2);
        var retry = requestFactory();
        attempt2.AttachTo(retry);
        return await _http.SendAsync(retry);
    }

    private async Task<bool> TryRefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            // Otro hilo pudo haber rotado mientras esperábamos.
            if (IsAuthenticated) return true;

            var resp = await _http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = _refreshToken });
            if (!resp.IsSuccessStatusCode) return false;
            var tokens = await resp.Content.ReadFromJsonAsync<AuthTokensResponse>();
            if (tokens is null) return false;
            SetTokens(tokens);
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void SetTokens(AuthTokensResponse t)
    {
        _accessToken = t.AccessToken;
        _accessTokenExpiresAt = t.AccessTokenExpiresAt;
        _refreshToken = t.RefreshToken;
        Authenticated?.Invoke(this, EventArgs.Empty);
    }

    private void ClearTokens()
    {
        _accessToken = null;
        _accessTokenExpiresAt = default;
        _refreshToken = null;
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    private void AttachBearer(out BearerToken token)
    {
        token = new BearerToken(_accessToken);
    }

    private static HttpRequestMessage Request<TBody>(HttpMethod method, string url, TBody body)
    {
        var req = new HttpRequestMessage(method, url);
        req.Content = JsonContent.Create(body);
        return req;
    }

    private readonly struct BearerToken
    {
        private readonly string? _token;
        public BearerToken(string? token) { _token = token; }
        public void AttachTo(HttpRequestMessage req)
        {
            if (!string.IsNullOrEmpty(_token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }
}

public sealed record LoginChallengeResponse(string MfaChallengeToken, bool MustChangePassword);

public sealed record AuthTokensResponse(
    string AccessToken, string RefreshToken,
    DateTime AccessTokenExpiresAt, DateTime RefreshTokenExpiresAt,
    AuthenticatedUser? User);

public sealed record AuthenticatedUser(Guid PublicId, string Username,
    AuthenticatedTenant Tenant, AuthenticatedBranch? Branch, IReadOnlyList<string> Roles);
public sealed record AuthenticatedTenant(Guid PublicId, string Name);
public sealed record AuthenticatedBranch(Guid PublicId, string Name);

public sealed record MfaEnrollmentStartResponse(string Secret, string QrCodeSvg, string EnrollmentToken);
public sealed record MfaBackupCodesResponse(IReadOnlyList<string> BackupCodes);
public sealed record MfaResetRequestResponse(Guid RequestPublicId, DateTime ExpiresAt);
public sealed record MfaResetApprovalResponse(string Status);
