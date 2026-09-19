using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// US5 — Cliente del master admin: register tenant con admin + force MFA reset.
/// </summary>
public sealed class SaasAdminClient
{
    private readonly HttpClient _http;
    private readonly CentralAuthClient _auth;

    public SaasAdminClient(HttpClient http, CentralAuthClient auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<InvitationApiResult<RegisterTenantWithAdminResponse>> RegisterTenantWithAdminAsync(
        RegisterTenantWithAdminBody body, CancellationToken ct = default)
    {
        return await PostAsync<RegisterTenantWithAdminResponse>(
            "/api/saas/tenants/with-admin", body, ct);
    }

    public async Task<InvitationApiResult<EmptyResponse>> ForceMfaResetAsync(
        Guid centralUserPublicId, string reason, CancellationToken ct = default)
    {
        return await PostAsync<EmptyResponse>(
            $"/api/saas/users/{centralUserPublicId}/force-mfa-reset",
            new { reason }, ct);
    }

    private async Task<InvitationApiResult<T>> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        var token = _auth.CurrentAccessToken;
        if (token is null)
            return InvitationApiResult<T>.Failure(
                "Identity.NoAccessToken", "Falta el token.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body),
            };
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }
}

public sealed record RegisterTenantWithAdminBody(
    string Name,
    string SchemaName,
    string? Subdomain,
    string Nit,
    string LegalName,
    string? LegalAddress,
    string? TaxRegime,
    string ContactEmail,
    string? ContactPhone,
    string PlanType,
    int MaxUsers,
    long StorageLimitMb,
    string FirstAdminEmail);

/// <summary>
/// Espejo de <c>RegisterTenantWithAdminResult</c>. <see cref="CorreoEnviado"/>
/// distingue "cooperativa creada e invitación enviada" de "cooperativa creada
/// pero el correo no salió" — antes ambos casos llegaban idénticos.
/// </summary>
public sealed record RegisterTenantWithAdminResponse(
    Guid TenantPublicId,
    Guid InvitationPublicId,
    DateTime InvitationExpiresAt,
    bool CorreoEnviado,
    string? MotivoCorreoNoEnviado);
