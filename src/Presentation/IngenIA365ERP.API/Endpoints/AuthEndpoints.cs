using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Application.Security.Auth.LogoutAll;
using IngenIA365ERP.Application.Security.Auth.MfaReset;
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

        // El login de un solo paso de desarrollo se retiro: existia para
        // saltarse el segundo factor ("util cuando MFA no esta inscrito"), y el
        // segundo factor es obligatorio desde que tambien lo es para el
        // administrador maestro. Un atajo que apaga la defensa que se acaba de
        // poner no es un atajo, es la puerta de atras.

        // === Autenticados ===
        // US2 (T071) — /logout también sustituido. Ver comentario de arriba.
        // group.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("Auth_Logout");
        group.MapPost("/logout-all", LogoutAllAsync).RequireAuthorization().WithName("Auth_LogoutAll");


        // Las rutas de inscripcion de MFA, regeneracion de codigos y cambio de
        // contrasena se retiraron: eran DUPLICADOS de Fase 0 sobre SEC_Users,
        // sin un solo llamador, y ademas enganosos — leen el claim uid, que el
        // emisor central no pone, asi que con una sesion de hoy respondian
        // "no autenticado" pasaran las credenciales que pasaran. Lo vivo esta
        // en /api/profile/*, contra ADM_CentralUsers.

        // Estas tres rutas exigian solo estar autenticado. Mientras el reset no
        // restablecia nada daba igual quien aprobara; ahora que si restablece,
        // sin permiso bastarian DOS cuentas cualesquiera de la cooperativa para
        // dejar sin segundo factor a quien fuera. Los permisos ya existian en el
        // catalogo (DomainPermissionCatalogSeeder) desde Fase 0 y el contrato
        // los documentaba; lo unico que faltaba era exigirlos aqui.
        //
        // El listado pide Approve y no Request: es la cola del aprobador, y
        // enumera quien esta bloqueado fuera de su cuenta.
        group.MapPost("/mfa/reset/request", RequestMfaResetAsync)
            .RequireAuthorization().RequirePermission("Security.MfaReset.Request")
            .WithName("Auth_RequestMfaReset");
        group.MapPost("/mfa/reset/{requestPublicId:guid}/approve", ApproveMfaResetAsync)
            .RequireAuthorization().RequirePermission("Security.MfaReset.Approve")
            .WithName("Auth_ApproveMfaReset");
        group.MapGet("/mfa/reset/requests", ListMfaResetRequestsAsync)
            .RequireAuthorization().RequirePermission("Security.MfaReset.Approve")
            .WithName("Auth_ListMfaResetRequests");

    }

    // === Handlers ===

    private static async Task<object?> LogoutAllAsync(
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new LogoutAllCommand(), ct);

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
