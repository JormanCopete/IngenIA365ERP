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

    /// <param name="label">
    /// Nombre del dispositivo. Opcional, y sólo empieza a importar cuando hay más
    /// de un autenticador: con dos filas sin nombre, retirar el correcto es una
    /// apuesta.
    /// </param>
    public async Task<InvitationApiResult<ConfirmMfaEnrollResponse>> ConfirmMfaEnrollAsync(
        string code, bool useChallengeToken = false, string? label = null, CancellationToken ct = default)
    {
        var token = ChooseToken(useChallengeToken);
        if (token is null) return Unauthorized<ConfirmMfaEnrollResponse>();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/confirm")
            {
                Content = JsonContent.Create(new { code, label }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            var parsed = await CentralAuthApi.ParseAsync<ConfirmMfaEnrollResponse>(resp, ct);

            if (parsed.IsSuccess && parsed.Value is { } cuerpo)
            {
                await AdoptarSiVinoSesionAsync(cuerpo.AccessToken, cuerpo.AccessTokenExpiresAt, cuerpo.RefreshToken);
            }

            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ConfirmMfaEnrollResponse>.NetworkError(ex.Message);
        }
    }

    /// <summary>
    /// Cuando la inscripción venía forzada, el servidor devuelve la sesión ya
    /// emitida. Se adopta AQUÍ y no en la pantalla porque son dos pantallas —la de
    /// código y la de passkey— y olvidarlo en una no da ningún error: la persona
    /// ve «listo», pulsa continuar, y aterriza en el login otra vez sin saber por
    /// qué. Es lo que pasaba: los tokens llegaban y se descartaban.
    /// </summary>
    private async Task AdoptarSiVinoSesionAsync(
        string? accessToken, DateTime? expiraEn, string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;
        await _auth.AdoptSessionAsync(accessToken, expiraEn, refreshToken);
    }

    public async Task<InvitationApiResult<EmptyResponse>> DisableMfaAsync(
        string currentPassword, CancellationToken ct = default)
    {
        return await PostAuthenticatedAsync<EmptyResponse>(
            "/api/profile/mfa/disable", new { currentPassword }, ct);
    }

    // -------------------- Credenciales de segundo factor --------------------

    public async Task<InvitationApiResult<ListMfaCredentialsResponse>> ListarCredencialesMfaAsync(
        CancellationToken ct = default) =>
        await EnviarAutenticadoAsync<ListMfaCredentialsResponse>(
            HttpMethod.Get, "/api/profile/mfa/credentials", cuerpo: null, ct);

    public async Task<InvitationApiResult<EmptyResponse>> RenombrarCredencialMfaAsync(
        Guid credencialPublicId, string? label, CancellationToken ct = default) =>
        await EnviarAutenticadoAsync<EmptyResponse>(
            HttpMethod.Patch, $"/api/profile/mfa/credentials/{credencialPublicId}",
            new { label }, ct);

    public async Task<InvitationApiResult<EmptyResponse>> RevocarCredencialMfaAsync(
        Guid credencialPublicId, CancellationToken ct = default) =>
        await EnviarAutenticadoAsync<EmptyResponse>(
            HttpMethod.Delete, $"/api/profile/mfa/credentials/{credencialPublicId}",
            cuerpo: null, ct);

    // -------------------- Passkeys --------------------

    /// <summary>
    /// Primer viaje del alta: pide el reto. Lo que vuelve se le pasa tal cual a
    /// <c>webauthn.js</c>; el cliente no interpreta ni un campo, y es a propósito
    /// —el formato lo fija la librería del servidor, y cualquier lectura aquí
    /// sería una copia que se desincroniza.
    /// </summary>
    /// <param name="useChallengeToken">
    /// True cuando la persona está en la inscripción forzada tras el login y
    /// todavía no tiene sesión: entonces el único token que tiene es el de
    /// <c>purpose=mfa-enroll</c>.
    /// </param>
    public async Task<InvitationApiResult<BeginWebAuthnResponse>> IniciarAltaPasskeyAsync(
        bool useChallengeToken = false, CancellationToken ct = default)
    {
        var token = ChooseToken(useChallengeToken);
        if (token is null) return Unauthorized<BeginWebAuthnResponse>();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/webauthn/begin");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<BeginWebAuthnResponse>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<BeginWebAuthnResponse>.NetworkError(ex.Message);
        }
    }

    /// <summary>Segundo viaje: la respuesta firmada del autenticador.</summary>
    public async Task<InvitationApiResult<ConfirmWebAuthnResponse>> ConfirmarAltaPasskeyAsync(
        string retoId,
        string respuestaJson,
        string? label = null,
        bool useChallengeToken = false,
        CancellationToken ct = default)
    {
        var token = ChooseToken(useChallengeToken);
        if (token is null) return Unauthorized<ConfirmWebAuthnResponse>();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/profile/mfa/webauthn/confirm")
            {
                Content = JsonContent.Create(new { retoId, respuestaJson, label }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _http.SendAsync(req, ct);
            var parsed = await CentralAuthApi.ParseAsync<ConfirmWebAuthnResponse>(resp, ct);

            if (parsed.IsSuccess && parsed.Value is { } cuerpo)
            {
                await AdoptarSiVinoSesionAsync(cuerpo.AccessToken, cuerpo.AccessTokenExpiresAt, cuerpo.RefreshToken);
            }

            return parsed;
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ConfirmWebAuthnResponse>.NetworkError(ex.Message);
        }
    }

    /// <summary>
    /// Envía con el token de sesión y devuelve el resultado ya interpretado.
    /// Existe porque el helper que había sólo sabía hacer POST, y estas rutas usan
    /// GET, PATCH y DELETE.
    /// </summary>
    private async Task<InvitationApiResult<T>> EnviarAutenticadoAsync<T>(
        HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        var token = ChooseToken(useChallenge: false);
        if (token is null) return Unauthorized<T>();

        try
        {
            using var req = new HttpRequestMessage(metodo, ruta);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _http.SendAsync(req, ct);
            return await CentralAuthApi.ParseAsync<T>(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<T>.NetworkError(ex.Message);
        }
    }

    /// <summary>Feature 003 (FR-111): regenera los recovery codes confirmando
    /// identidad con contraseña actual O TOTP (exactamente uno).</summary>
    public async Task<InvitationApiResult<RegenerateRecoveryCodesResponse>> RegenerateRecoveryCodesAsync(
        string? currentPassword, string? totpCode, CancellationToken ct = default)
    {
        return await PostAuthenticatedAsync<RegenerateRecoveryCodesResponse>(
            "/api/profile/mfa/recovery-codes/regenerate",
            new { currentPassword, totpCode }, ct);
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

// El begin no trae códigos de recuperación: los válidos vienen en el confirm,
// abajo. Los traía, y eran otros — se descartaban al confirmar.
// El begin no trae códigos de recuperación: los válidos vienen en el confirm.
// Los traía, y eran otros — se descartaban al confirmar.
public sealed record BeginMfaEnrollResponse(
    string SecretBase32,
    string OtpAuthUri,
    string QrPngDataUri,
    int ExpiresInSeconds);

/// <summary>Una credencial de segundo factor, tal como la ve su dueño.</summary>
/// <param name="Tipo">
/// <c>Totp</c> o <c>WebAuthn</c>. Desde que hay dos clases de autenticador, una
/// lista que no lo diga obliga a adivinar: retirar «iPhone» significa cosas muy
/// distintas si era la app de códigos o la llave del dispositivo.
/// </param>
/// <param name="Label">NULL en las trasladadas: ese dato no existía antes.</param>
/// <param name="LastUsedAt">
/// Último ingreso correcto con ella. Es lo que permite distinguir el teléfono que
/// se tiene en la mano del que se perdió cuando ninguno tiene nombre.
/// </param>
public sealed record CredencialMfaDto(
    Guid PublicId,
    string Tipo,
    string? Label,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? LastUsedAt);

/// <summary>
/// Los dos tipos de autenticador que puede traer <see cref="CredencialMfaDto.Tipo"/>.
///
/// <para>
/// Es una copia de <c>TiposDeCredencialMfa</c> de Application, y lo es porque este
/// proyecto no referencia ninguno: sus DTO son todos espejo, igual que los de
/// cualquier cliente HTTP. Que las dos copias digan lo mismo lo comprueba una
/// prueba de arquitectura, no la confianza.
/// </para>
/// </summary>
public static class TiposDeCredencialMfa
{
    public const string Totp = "Totp";
    public const string WebAuthn = "WebAuthn";
}

public sealed record BeginWebAuthnResponse(string OpcionesJson, string RetoId);

/// <param name="CodigosDeRecuperacion">
/// Sólo llegan si era la PRIMERA credencial de la persona. Con la segunda viene
/// vacía: emitir códigos nuevos invalidaría los que ya guardó.
/// </param>
/// <param name="AccessToken">
/// Sólo cuando la inscripción venía forzada. <see cref="ProfileClient"/> ya lo
/// adoptó antes de devolver esto; la pantalla no tiene que hacer nada con él.
/// </param>
public sealed record ConfirmWebAuthnResponse(
    Guid CredencialPublicId,
    IReadOnlyList<string> CodigosDeRecuperacion,
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAt = null,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresAt = null,
    Guid? ActiveTenantPublicId = null,
    string? ActiveTenantName = null);

/// <param name="OpcionesJson">Se le pasa entero a <c>webauthn.js</c>, sin leerlo.</param>
public sealed record WebAuthnChallengeResponse(string OpcionesJson, string RetoId);

public sealed record ListMfaCredentialsResponse(
    IReadOnlyList<CredencialMfaDto> Credenciales,
    int CodigosDeRecuperacionRestantes);

public sealed record RegenerateRecoveryCodesResponse(
    IReadOnlyList<string> RecoveryCodes);

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
