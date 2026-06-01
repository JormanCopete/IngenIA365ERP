using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// T079l — Cliente del módulo de perfil y recuperación de cuenta (Phase 4b).
/// Cubre los endpoints de <c>/api/profile/*</c> (requieren JWT) y
/// <c>/api/auth/password/*</c> (anónimos para forgot/reset).
///
/// <para>
/// Reusa <see cref="CentralAuthClient"/> para resolver qué token enviar:
/// access (full) para enrollment voluntario, change-password, disable; o
/// challenge (purpose=mfa-enroll) para el flujo forzado tras login.
/// </para>
/// </summary>
public sealed class ProfileClient
{
    private readonly HttpClient _http;
    private readonly CentralAuthClient _auth;

    public ProfileClient(HttpClient http, CentralAuthClient auth)
    {
        _http = http;
        _auth = auth;
    }

    // -------------------- MFA enrollment --------------------

    public async Task<InvitationApiResult<BeginMfaEnrollResponse>> BeginMfaEnrollAsync(
        bool useChallengeToken = false, CancellationToken ct = default)
    {
        var token = ChooseToken(useChallengeToken);
        if (token is null) return Unauthorized<BeginMfaEnrollResponse>();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/enroll");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<BeginMfaEnrollResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<BeginMfaEnrollResponse>.NetworkError(ex.Message);
        }
    }

    public async Task<InvitationApiResult<ConfirmMfaEnrollResponse>> ConfirmMfaEnrollAsync(
        string code, bool useChallengeToken = false, CancellationToken ct = default)
    {
        var token = ChooseToken(useChallengeToken);
        if (token is null) return Unauthorized<ConfirmMfaEnrollResponse>();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
            {
                Content = JsonContent.Create(new { code }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<ConfirmMfaEnrollResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ConfirmMfaEnrollResponse>.NetworkError(ex.Message);
        }
    }

    public async Task<InvitationApiResult<EmptyResponse>> DisableMfaAsync(
        string currentPassword, CancellationToken ct = default)
    {
        return await PostAuthenticatedAsync<EmptyResponse>(
            "/api/profile/mfa/disable", new { currentPassword }, ct);
    }

    // -------------------- Change password --------------------

    public async Task<InvitationApiResult<EmptyResponse>> ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken ct = default)
    {
        return await PostAuthenticatedAsync<EmptyResponse>(
            "/api/profile/password",
            new { currentPassword, newPassword }, ct);
    }

    // -------------------- Forgot / reset --------------------

    public async Task<InvitationApiResult<EmptyResponse>> ForgotPasswordAsync(
        string email, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                "/api/auth/password/forgot", new { email }, ct);
            return await CentralAuthApi.ParseAsync<EmptyResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<EmptyResponse>.NetworkError(ex.Message);
        }
    }

    public async Task<InvitationApiResult<EmptyResponse>> ResetPasswordAsync(
        string token, string newPassword, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                "/api/auth/password/reset", new { token, newPassword }, ct);
            return await CentralAuthApi.ParseAsync<EmptyResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<EmptyResponse>.NetworkError(ex.Message);
        }
    }

    // -------------------- Helpers --------------------

    private string? ChooseToken(bool useChallenge) =>
        useChallenge ? _auth.CurrentChallengeToken : _auth.CurrentAccessToken;

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
            "Identity.NoAccessToken",
            "Falta el token de sesión. Vuelve a iniciar sesión.", 401);
}

// -------------------- DTOs --------------------

public sealed record BeginMfaEnrollResponse(
    string SecretBase32,
    string OtpAuthUri,
    IReadOnlyList<string> RecoveryCodes,
    int ExpiresInSeconds);

public sealed record ConfirmMfaEnrollResponse(
    IReadOnlyList<string> RecoveryCodes,
    string? AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    Guid? ActiveTenantPublicId,
    string? ActiveTenantName);

/// <summary>Marker para endpoints sin body (204 / 200 sin payload).</summary>
public sealed record EmptyResponse();
