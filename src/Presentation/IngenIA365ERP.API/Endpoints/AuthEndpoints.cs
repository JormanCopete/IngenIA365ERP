using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.ChangePassword;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Application.Security.Auth.EnrollMfa;
using IngenIA365ERP.Application.Security.Auth.Login;
using IngenIA365ERP.Application.Security.Auth.Logout;
using IngenIA365ERP.Application.Security.Auth.LogoutAll;
using IngenIA365ERP.Application.Security.Auth.MfaReset;
using IngenIA365ERP.Application.Security.Auth.RefreshToken;
using IngenIA365ERP.Application.Security.Auth.RegenerateBackupCodes;
using IngenIA365ERP.Application.Security.Auth.VerifyMfa;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// Módulo Carter que materializa los 11 endpoints definidos en
/// <c>specs/001-cimientos-tecnicos/contracts/auth.md</c>. Cada endpoint
/// se limita a reenviar al <see cref="ISender"/> de MediatR; toda la lógica
/// vive en los handlers de <c>IngenIA365ERP.Application.Security.Auth.*</c>.
/// La envolvente de error la aplica <see cref="ErrorEnvelopeFilter"/>.
/// </summary>
public class AuthEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        // === Anónimos ===
        // US2 (T071) — las 4 rutas /login, /mfa/verify, /refresh, /logout las
        // sustituye CentralAuthModule con el flujo de identidad central
        // (sin tenant en el request, tagged union de challenges). Estas rutas
        // legacy quedan comentadas; cuando se cierre Phase 4b y el master
        // admin pueda loguearse por el flujo central completo, AuthEndpoints
        // entero se elimina.
        // group.MapPost("/login", LoginAsync).AllowAnonymous().WithName("Auth_Login");
        // group.MapPost("/mfa/verify", VerifyMfaAsync).AllowAnonymous().WithName("Auth_VerifyMfa");
        // group.MapPost("/refresh", RefreshAsync).AllowAnonymous().WithName("Auth_Refresh");

        // === Dev-only: login en un solo paso (combina login + mfa/verify) ===
        // Útil para iteración local cuando MFA no está inscrito en el admin sembrado.
        // No se mapea en Staging/Production.
        var env = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
        {
            group.MapPost("/dev/login", DevLoginAsync).AllowAnonymous().WithName("Auth_DevLogin");
        }

        // === Autenticados ===
        // US2 (T071) — /logout también sustituido. Ver comentario de arriba.
        // group.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("Auth_Logout");
        group.MapPost("/logout-all", LogoutAllAsync).RequireAuthorization().WithName("Auth_LogoutAll");

        group.MapPost("/mfa/enroll/start", EnrollMfaStartAsync).RequireAuthorization().WithName("Auth_EnrollMfaStart");
        group.MapPost("/mfa/enroll/confirm", EnrollMfaConfirmAsync).RequireAuthorization().WithName("Auth_EnrollMfaConfirm");
        group.MapPost("/mfa/backup-codes/regenerate", RegenerateBackupCodesAsync).RequireAuthorization().WithName("Auth_RegenerateBackupCodes");

        group.MapPost("/mfa/reset/request", RequestMfaResetAsync).RequireAuthorization().WithName("Auth_RequestMfaReset");
        group.MapPost("/mfa/reset/{requestPublicId:guid}/approve", ApproveMfaResetAsync).RequireAuthorization().WithName("Auth_ApproveMfaReset");
        group.MapGet("/mfa/reset/requests", ListMfaResetRequestsAsync).RequireAuthorization().WithName("Auth_ListMfaResetRequests");

        group.MapPost("/password/change", ChangePasswordAsync).RequireAuthorization().WithName("Auth_ChangePassword");
    }

    // === Handlers ===

    private static async Task<object?> LoginAsync(
        [FromBody] LoginRequestBody body,
        ISender sender,
        HttpContext http,
        IIpAddressAccessor ip,
        CancellationToken ct)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        // Acepta indistintamente `username`/`email` y `tenantSubdomainOrNit`/`tenantId`.
        // El handler busca por Username o Email (ver LoginCommandHandler).
        var loginId = !string.IsNullOrWhiteSpace(body.Username) ? body.Username : body.Email;
        var tenant = !string.IsNullOrWhiteSpace(body.TenantSubdomainOrNit)
            ? body.TenantSubdomainOrNit : body.TenantId;
        return await sender.Send(new LoginCommand(
            tenant, loginId ?? string.Empty, body.Password,
            ip.IpAddress, ua), ct);
    }

    private static async Task<object?> VerifyMfaAsync(
        [FromBody] VerifyMfaRequestBody body,
        ISender sender,
        HttpContext http,
        IIpAddressAccessor ip,
        CancellationToken ct)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        return await sender.Send(new VerifyMfaCommand(
            body.MfaChallengeToken, body.TotpCode, body.UseBackupCode,
            body.BackupCode, body.BranchPublicId, ip.IpAddress, ua), ct);
    }

    private static async Task<object?> RefreshAsync(
        [FromBody] RefreshRequestBody body,
        ISender sender,
        HttpContext http,
        IIpAddressAccessor ip,
        CancellationToken ct)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        return await sender.Send(new RefreshTokenCommand(
            body.RefreshToken, ip.IpAddress, ua), ct);
    }

    private static async Task<object?> LogoutAsync(
        [FromBody] LogoutRequestBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new LogoutCommand(body.RefreshToken), ct);

    private static async Task<object?> LogoutAllAsync(
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new LogoutAllCommand(), ct);

    private static async Task<object?> EnrollMfaStartAsync(
        [FromBody] EnrollMfaStartRequestBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new EnrollMfaStartCommand(body.Password), ct);

    private static async Task<object?> EnrollMfaConfirmAsync(
        [FromBody] EnrollMfaConfirmRequestBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new EnrollMfaConfirmCommand(body.EnrollmentToken, body.TotpCode), ct);

    private static async Task<object?> RegenerateBackupCodesAsync(
        [FromBody] RegenerateBackupCodesRequestBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new RegenerateBackupCodesCommand(body.TotpCode), ct);

    private static async Task<object?> RequestMfaResetAsync(
        [FromBody] RequestMfaResetRequestBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new RequestMfaResetCommand(
            body.TargetUserPublicId, body.Reason, body.EvidenceAttachmentPublicId), ct);

    private static async Task<object?> ApproveMfaResetAsync(
        Guid requestPublicId,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ApproveMfaResetCommand(requestPublicId), ct);

    private static async Task<object?> ListMfaResetRequestsAsync(
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ISender sender,
        CancellationToken ct)
    {
        IngenIA365ERP.Domain.Entities.Security.MfaResetStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<IngenIA365ERP.Domain.Entities.Security.MfaResetStatus>(status, ignoreCase: true, out var s))
        {
            parsed = s;
        }
        return await sender.Send(new ListMfaResetRequestsQuery(
            parsed, page ?? 1, pageSize ?? 20), ct);
    }

    private static async Task<object?> ChangePasswordAsync(
        [FromBody] ChangePasswordRequestBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), ct);

    /// <summary>
    /// Dev-only: hace login + mfa/verify en una sola llamada, retornando
    /// access + refresh directamente. Solo funciona si el admin sembrado
    /// tiene <c>IsMfaEnabled=false</c> (caso típico en local). Acepta el
    /// mismo body que <c>/login</c> (incluidos los alias <c>email</c>/<c>tenantId</c>).
    /// </summary>
    private static async Task<object?> DevLoginAsync(
        [FromBody] LoginRequestBody body,
        ISender sender,
        HttpContext http,
        IIpAddressAccessor ip,
        CancellationToken ct)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        var loginId = !string.IsNullOrWhiteSpace(body.Username) ? body.Username : body.Email;
        var tenant = !string.IsNullOrWhiteSpace(body.TenantSubdomainOrNit)
            ? body.TenantSubdomainOrNit : body.TenantId;

        // 1) Login → challenge token.
        var loginResult = await sender.Send(new LoginCommand(
            tenant, loginId ?? string.Empty, body.Password, ip.IpAddress, ua), ct);

        if (loginResult.IsFailure)
        {
            return loginResult; // ErrorEnvelopeFilter mapea a HTTP apropiado.
        }

        var challenge = loginResult.Value.MfaChallengeToken;

        // 2) Verify MFA inmediatamente con un código dummy.
        //    Si el usuario tiene IsMfaEnabled=false, el handler ignora el TOTP.
        //    Si está enrolado, devolverá Auth.InvalidMfaCode y el cliente debe
        //    usar el flujo regular (no /dev/login).
        var verifyResult = await sender.Send(new VerifyMfaCommand(
            MfaChallengeToken: challenge,
            TotpCode: "000000",
            UseBackupCode: false,
            BackupCode: null,
            BranchPublicId: null,
            IpAddress: ip.IpAddress,
            UserAgent: ua), ct);

        if (verifyResult.IsFailure && verifyResult.Error.Code == "Auth.InvalidMfaCode")
        {
            return Result.Failure<AuthTokensResult>(
                "Auth.DevLoginRequiresMfa",
                "Este usuario tiene MFA inscrito — /dev/login no aplica. " +
                "Usa el flujo regular: /login → /mfa/verify con tu código TOTP.");
        }

        return verifyResult;
    }
}

// === Bodies (PublicIds en payload — FR Principio VI) ===
/// <summary>
/// Body de /api/auth/login. Acepta el contrato canónico <c>username</c> +
/// <c>tenantSubdomainOrNit</c> y, por conveniencia operacional, los alias
/// <c>email</c> + <c>tenantId</c> que usan algunos clientes legacy. El
/// handler de dominio busca el usuario por <c>Username</c> o <c>Email</c>
/// indistintamente.
/// </summary>
public record LoginRequestBody(
    string? TenantSubdomainOrNit,
    string? Username,
    string Password,
    string? Email = null,
    string? TenantId = null);
public record VerifyMfaRequestBody(string MfaChallengeToken, string? TotpCode, bool UseBackupCode, string? BackupCode, Guid? BranchPublicId);
public record RefreshRequestBody(string RefreshToken);
public record LogoutRequestBody(string RefreshToken);
public record EnrollMfaStartRequestBody(string Password);
public record EnrollMfaConfirmRequestBody(string EnrollmentToken, string TotpCode);
public record RegenerateBackupCodesRequestBody(string TotpCode);
public record RequestMfaResetRequestBody(Guid TargetUserPublicId, string Reason, Guid? EvidenceAttachmentPublicId);
public record ChangePasswordRequestBody(string CurrentPassword, string NewPassword);
