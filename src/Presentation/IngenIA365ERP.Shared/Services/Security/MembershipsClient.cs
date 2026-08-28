using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// US4 — Cliente del módulo de gestión de membresías y MFA policy.
/// </summary>
public sealed class MembershipsClient
{
    private readonly HttpClient _http;
    private readonly CentralAuthClient _auth;

    public MembershipsClient(HttpClient http, CentralAuthClient auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<InvitationApiResult<MembersListResponse>> ListAsync(
        Guid tenantPublicId, string? statusFilter = null, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var url = $"/api/tenants/{tenantPublicId}/members?page={page}&pageSize={pageSize}"
            + (statusFilter is null ? "" : $"&status={statusFilter}");
        return await GetAsync<MembersListResponse>(url, ct);
    }

    public Task<InvitationApiResult<EmptyResponse>> SuspendAsync(Guid tenantPublicId, Guid publicId, CancellationToken ct = default) =>
        PostAsync<EmptyResponse>($"/api/tenants/{tenantPublicId}/members/{publicId}/suspend", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActivateAsync(Guid tenantPublicId, Guid publicId, CancellationToken ct = default) =>
        PostAsync<EmptyResponse>($"/api/tenants/{tenantPublicId}/members/{publicId}/activate", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> RevokeAsync(Guid tenantPublicId, Guid publicId, CancellationToken ct = default) =>
        PostAsync<EmptyResponse>($"/api/tenants/{tenantPublicId}/members/{publicId}/revoke", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> PromoteAsync(Guid tenantPublicId, Guid publicId, CancellationToken ct = default) =>
        PostAsync<EmptyResponse>($"/api/tenants/{tenantPublicId}/members/{publicId}/promote-admin", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> DemoteAsync(Guid tenantPublicId, Guid publicId, CancellationToken ct = default) =>
        PostAsync<EmptyResponse>($"/api/tenants/{tenantPublicId}/members/{publicId}/demote-admin", null, ct);

    public Task<InvitationApiResult<MfaPolicyResponse>> GetMfaPolicyAsync(Guid tenantPublicId, CancellationToken ct = default) =>
        GetAsync<MfaPolicyResponse>($"/api/tenants/{tenantPublicId}/mfa-policy", ct);

    /// <param name="metodosAceptados">
    /// Literales ("Totp", "WebAuthn"). null deja los metodos como estaban; NO los
    /// reinicia. Es la misma semantica del comando, y esta escrito en los dos
    /// lados porque este proyecto no referencia ninguno.
    /// </param>
    public Task<InvitationApiResult<MfaPolicyUpdateResponse>> SetMfaPolicyAsync(
        Guid tenantPublicId,
        bool isRequired,
        IReadOnlyList<string>? metodosAceptados = null,
        CancellationToken ct = default) =>
        PutAsync<MfaPolicyUpdateResponse>(
            $"/api/tenants/{tenantPublicId}/mfa-policy",
            new { isRequired, metodosAceptados }, ct);

    // -------------------- Helpers --------------------

    private async Task<InvitationApiResult<T>> GetAsync<T>(string url, CancellationToken ct)
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

    private async Task<InvitationApiResult<T>> PostAsync<T>(string url, object? body, CancellationToken ct)
    {
        var token = _auth.CurrentAccessToken;
        if (token is null) return Unauthorized<T>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            if (body is not null) req.Content = JsonContent.Create(body);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }

    private async Task<InvitationApiResult<T>> PutAsync<T>(string url, object body, CancellationToken ct)
    {
        var token = _auth.CurrentAccessToken;
        if (token is null) return Unauthorized<T>();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, url)
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
        InvitationApiResult<T>.Failure("Identity.NoAccessToken", "Falta el token.", 401);
}

public sealed record MembersListResponse(
    IReadOnlyList<TenantMemberItem> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record TenantMemberItem(
    Guid MembershipPublicId,
    Guid CentralUserId,
    int Status,
    bool IsTenantAdmin,
    DateTime InvitedAt,
    DateTime? ActivatedAt);

public sealed record MfaPolicyResponse(
    Guid TenantPublicId,
    bool IsRequired,
    DateTime? ActivatedAt,
    DateTime? DeactivatedAt,
    IReadOnlyList<string>? MetodosAceptados = null);

/// <param name="MiembrosSinMetodoAceptado">
/// Cuantas personas de la cooperativa tienen segundo factor pero ninguno de los
/// metodos que la politica acepta ahora. No es un error: el sistema las manda a
/// inscribir. Es el numero que hay que ensenar despues de guardar.
/// </param>
public sealed record MfaPolicyUpdateResponse(int MiembrosSinMetodoAceptado);
