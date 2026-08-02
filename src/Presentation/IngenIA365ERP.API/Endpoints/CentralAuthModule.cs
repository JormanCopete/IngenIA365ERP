using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Auth.Login;
using IngenIA365ERP.Application.Identity.Auth.Logout;
using IngenIA365ERP.Application.Identity.Auth.Me;
using IngenIA365ERP.Application.Identity.Auth.MfaVerify;
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

        anon.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("CentralAuth_Logout");

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

    // -------- Helpers --------

    private static string? GetIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString();

    private static string? GetUserAgent(HttpContext http) =>
        http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
