using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// T092 — Cliente del módulo de sesiones (US3). Cubre los endpoints
/// <c>/api/sessions/*</c> y <c>/api/profile/default-tenant</c>.
/// </summary>
public sealed class TenantSessionClient
{
    private readonly HttpClient _http;
    private readonly CentralAuthClient _auth;

    public TenantSessionClient(HttpClient http, CentralAuthClient auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<InvitationApiResult<IReadOnlyList<ActiveTenantInfo>>> GetActiveTenantsAsync(
        CancellationToken ct = default)
    {
        return await GetAuthenticatedAsync<IReadOnlyList<ActiveTenantInfo>>(
            "/api/sessions/active-tenants", ct);
    }

    public async Task<InvitationApiResult<SwitchTenantResponse>> SwitchAsync(
        Guid tenantPublicId, CancellationToken ct = default)
    {
        return await PostAuthenticatedAsync<SwitchTenantResponse>(
            "/api/sessions/switch-tenant", new { tenantPublicId }, ct);
    }

    public async Task<InvitationApiResult<EmptyResponse>> SetDefaultAsync(
        Guid? tenantPublicId, CancellationToken ct = default)
    {
        var token = _auth.CurrentAccessToken;
        if (token is null) return Unauthorized<EmptyResponse>();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, "/api/profile/default-tenant")
            {
                Content = JsonContent.Create(new { tenantPublicId }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<EmptyResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<EmptyResponse>.NetworkError(ex.Message);
        }
    }

    private async Task<InvitationApiResult<T>> GetAuthenticatedAsync<T>(string url, CancellationToken ct)
    {
        var token = _auth.CurrentAccessToken;
        if (token is null) return Unauthorized<T>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }

    private async Task<InvitationApiResult<T>> PostAuthenticatedAsync<T>(
        string url, object body, CancellationToken ct)
    {
        var token = _auth.CurrentAccessToken;
        if (token is null) return Unauthorized<T>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }

    private static InvitationApiResult<T> Unauthorized<T>() =>
        InvitationApiResult<T>.Failure(
            "Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
}

public sealed record ActiveTenantInfo(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin,
    bool IsDefault);

public sealed record SwitchTenantResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    int ExpiresInSeconds,
    SwitchedTenantSummary Tenant);

public sealed record SwitchedTenantSummary(
    Guid TenantPublicId,
    string TenantName,
    bool IsTenantAdmin);
