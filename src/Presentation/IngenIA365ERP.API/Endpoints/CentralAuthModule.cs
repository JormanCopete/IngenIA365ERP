using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Auth.Login;
using IngenIA365ERP.Application.Identity.Auth.Logout;
using IngenIA365ERP.Application.Identity.Auth.Me;
using IngenIA365ERP.Application.Identity.Auth.MfaVerify;
using IngenIA365ERP.Application.Identity.Auth.WebAuthn;
using IngenIA365ERP.Application.Identity.Auth.Recuperacion;
using IngenIA365ERP.Application.Identity.Auth.RefreshToken;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T071 — Módulo Carter del flujo de identidad central (US2, Feature 002).
/// Sustituye las rutas <c>/login</c>, <c>/mfa/verify</c>, <c>/refresh</c>,
/// <c>/logout</c> del legacy <see cref="AuthEndpoints"/> y añade
/// <c>/me</c>.
///
/// <para>Convención de extracción del contexto del request:</para>
/// <list type="bullet">
///   <item>El email + password del login viene en el body.</item>
///   <item>El <c>challengeToken</c> de MFA/refresh viaja en <c>Authorization: Bearer</c>
///         (validado automáticamente por JwtBearer middleware).</item>
///   <item><c>IpAddress</c>/<c>UserAgent</c> se extraen del HttpContext y se
///         pasan al command para auditoría + telemetría.</item>
/// </list>
/// </summary>
public sealed class CentralAuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var anon = app.MapGroup("/api/auth")
            .WithTags("Identity / Auth (central)")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        anon.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("CentralAuth_Login");

        anon.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("CentralAuth_Refresh");

        // MFA verify: el challengeToken (purpose=mfa-verify) viaja en Bearer.
        // El JWT bearer middleware lo valida; el handler verifica el purpose.
        anon.MapPost("/mfa/verify", VerifyMfaAsync)
            .RequireAuthorization()
            .WithName("CentralAuth_MfaVerify");

        // Ingreso con passkey. Rutas propias y NO un campo más en /mfa/verify:
        // aquello valida un código de 6 a 8 caracteres y tiene ocho pruebas y
        // varias e2e encima construyendo ese cuerpo. Ensancharlo por un método
        // que ni siquiera manda un código habría sido romper el camino de entrada
        // para ahorrarse dos rutas.
        anon.MapPost("/mfa/webauthn/challenge", BeginWebAuthnAssertionAsync)
            .RequireAuthorization()
            .WithName("CentralAuth_WebAuthnChallenge");

        anon.MapPost("/mfa/webauthn/verify", VerifyWebAuthnAssertionAsync)
            .RequireAuthorization()
            .WithName("CentralAuth_WebAuthnVerify");

        // Recuperación del segundo factor por correo.
        //
        // Pedirla exige el token del desafío —o sea, la contraseña ya acertada—,
        // así que este endpoint no sirve de oráculo para averiguar qué correos
        // tienen cuenta. Confirmar y cancelar son anónimos porque se llega desde un
        // enlace del correo, posiblemente en otro navegador o en otro equipo;
        // confirmar pide la contraseña otra vez y cancelar no pide nada.
        anon.MapPost("/mfa/recovery/request", RequestMfaRecoveryAsync)
            .RequireAuthorization()
            .WithName("CentralAuth_MfaRecoveryRequest");

        anon.MapPost("/mfa/recovery/confirm", ConfirmMfaRecoveryAsync)
            .AllowAnonymous()
            .WithName("CentralAuth_MfaRecoveryConfirm");

        anon.MapPost("/mfa/recovery/cancel", CancelMfaRecoveryAsync)
            .AllowAnonymous()
            .WithName("CentralAuth_MfaRecoveryCancel");

        anon.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("CentralAuth_Logout");

        // Los límites de sesión (inactividad, duración máxima), para que el cliente
        // cuente con los mismos números que el servidor hace cumplir en /refresh.
        anon.MapGet("/session-policy", SessionPolicyAsync)
            .AllowAnonymous()
            .WithName("CentralAuth_SessionPolicy");

        anon.MapGet("/me", MeAsync)
            .RequireAuthorization()
            .WithName("CentralAuth_Me");
    }

    // -------- Login --------

    private static async Task<object?> LoginAsync(
        [FromBody] LoginBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new LoginCommand(
            Email: body.Email,
            Password: body.Password,
            IpAddress: GetIp(http),
            UserAgent: GetUserAgent(http)), ct);

    public sealed record LoginBody(string Email, string Password);

    // -------- MFA verify --------

    private static async Task<object?> VerifyMfaAsync(
        [FromBody] MfaVerifyBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new MfaVerifyCommand(
            Code: body.Code,
            UseRecoveryCode: body.UseRecoveryCode,
            IpAddress: GetIp(http),
            UserAgent: GetUserAgent(http)), ct);

    public sealed record MfaVerifyBody(string Code, bool UseRecoveryCode = false);

    // -------- Ingreso con passkey --------

    private static async Task<object?> BeginWebAuthnAssertionAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new BeginWebAuthnAssertionCommand(), ct);

    private static async Task<object?> VerifyWebAuthnAssertionAsync(
        [FromBody] WebAuthnVerifyBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new VerifyWebAuthnAssertionCommand(
            RetoId: body.RetoId,
            RespuestaJson: body.RespuestaJson,
            IpAddress: GetIp(http),
            UserAgent: GetUserAgent(http)), ct);

    /// <param name="RespuestaJson">
    /// Lo que devolvió <c>navigator.credentials.get()</c>, serializado tal cual.
    /// </param>
    public sealed record WebAuthnVerifyBody(string RetoId, string RespuestaJson);

    // -------- Recuperación del segundo factor por correo --------

    private static async Task<object?> RequestMfaRecoveryAsync(
        HttpContext http, ISender sender, CancellationToken ct) =>
        await sender.Send(new RequestMfaRecoveryCommand(
            IpAddress: GetIp(http), UserAgent: GetUserAgent(http)), ct);

    private static async Task<object?> ConfirmMfaRecoveryAsync(
        [FromBody] ConfirmarRecuperacionBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ConfirmMfaRecoveryCommand(
            Token: body.Token,
            Password: body.Password,
            IpAddress: GetIp(http),
            UserAgent: GetUserAgent(http)), ct);

    /// <param name="Password">
    /// Sí, otra vez. Entre pedir la recuperación y confirmarla pasan horas o días,
    /// y en ese tiempo el enlace puede acabar en otras manos.
    /// </param>
    public sealed record ConfirmarRecuperacionBody(string Token, string Password);

    private static async Task<object?> CancelMfaRecoveryAsync(
        [FromBody] CancelarRecuperacionBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new CancelMfaRecoveryCommand(
            Token: body.Token,
            IpAddress: GetIp(http),
            UserAgent: GetUserAgent(http)), ct);

    /// <summary>
    /// Sólo el token. Cancelar no pide contraseña a propósito: tiene que ser más
    /// fácil que ejecutar, porque quien recibe el aviso sin haberlo pedido está
    /// viendo un ataque y sólo va a pararlo si pararlo es trivial.
    /// </summary>
    public sealed record CancelarRecuperacionBody(string Token);

    // -------- Refresh --------

    private static async Task<object?> RefreshAsync(
        [FromBody] RefreshBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new RefreshTokenCommand(
            RefreshToken: body.RefreshToken,
            IpAddress: GetIp(http),
            UserAgent: GetUserAgent(http)), ct);

    public sealed record RefreshBody(string RefreshToken);

    // -------- Logout --------

    private static async Task<object?> LogoutAsync(
        [FromBody] LogoutBody? body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new LogoutCommand(body?.RefreshToken), ct);

    public sealed record LogoutBody(string? RefreshToken);

    // -------- Me --------

    private static async Task<object?> MeAsync(
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new GetMeQuery(), ct);

    private static async Task<object?> SessionPolicyAsync(
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new IngenIA365ERP.Application.Identity.Auth.SessionPolicy.GetSessionPolicyQuery(), ct);

    // -------- Helpers --------

    private static string? GetIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString();

    private static string? GetUserAgent(HttpContext http) =>
        http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
