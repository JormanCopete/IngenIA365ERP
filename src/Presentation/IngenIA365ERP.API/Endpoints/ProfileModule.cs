using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Profile.BeginMfaEnrollment;
using IngenIA365ERP.Application.Identity.Profile.ChangePassword;
using IngenIA365ERP.Application.Identity.Profile.ConfirmMfaEnrollment;
using IngenIA365ERP.Application.Identity.Profile.Credenciales;
using IngenIA365ERP.Application.Identity.Profile.WebAuthn;
using IngenIA365ERP.Application.Identity.Profile.DisableMfa;
using IngenIA365ERP.Application.Identity.Profile.Preferencias;
using IngenIA365ERP.Application.Identity.Profile.RegenerateRecoveryCodes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T079i — Endpoints del perfil del usuario autenticado: enrollment de MFA
/// (voluntario o forzado), desactivación de MFA, cambio de contraseña.
/// Todas las rutas requieren JWT central; el handler verifica el
/// <c>purpose</c> esperado (full o mfa-enroll).
/// </summary>
public sealed class ProfileModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile")
            .WithTags("Identity / Profile")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        // MFA — enrollment voluntario o forzado.
        group.MapPost("/mfa/enroll", BeginMfaEnrollmentAsync)
            .WithName("Profile_BeginMfaEnrollment");

        group.MapPost("/mfa/confirm", ConfirmMfaEnrollmentAsync)
            .WithName("Profile_ConfirmMfaEnrollment");

        group.MapPost("/mfa/disable", DisableMfaAsync)
            .WithName("Profile_DisableMfa");

        group.MapPost("/mfa/recovery-codes/regenerate", RegenerateRecoveryCodesAsync)
            .WithName("Profile_RegenerateRecoveryCodes");

        // Credenciales de segundo factor. Una persona puede tener varias —el
        // teléfono, el escritorio, uno viejo de respaldo— y necesita verlas para
        // saber cuál retirar.
        //
        // Ninguna lleva RequirePermission, por lo mismo que las preferencias: no
        // son una facultad sobre terceros sino la gestión de lo propio, y el
        // handler saca la identidad del token, nunca de la petición. Un PublicId
        // ajeno adivinado responde «no existe».
        group.MapGet("/mfa/credentials", ListMfaCredentialsAsync)
            .WithName("Profile_ListMfaCredentials");

        group.MapPatch("/mfa/credentials/{credencialPublicId:guid}", RenameMfaCredentialAsync)
            .WithName("Profile_RenameMfaCredential");

        group.MapDelete("/mfa/credentials/{credencialPublicId:guid}", RevokeMfaCredentialAsync)
            .WithName("Profile_RevokeMfaCredential");

        // Passkeys. Son dos viajes porque WebAuthn es reto/respuesta: el servidor
        // emite un reto, el navegador lo firma con la llave, y el servidor
        // comprueba la firma contra el reto que emitió. El reto se queda aquí, y
        // eso es lo que impide reproducir una respuesta capturada.
        group.MapPost("/mfa/webauthn/begin", BeginWebAuthnEnrollmentAsync)
            .WithName("Profile_BeginWebAuthnEnrollment");

        group.MapPost("/mfa/webauthn/confirm", ConfirmWebAuthnEnrollmentAsync)
            .WithName("Profile_ConfirmWebAuthnEnrollment");

        // Password change (purpose=full).
        group.MapPost("/password", ChangePasswordAsync)
            .WithName("Profile_ChangePassword");

        // Preferencias de interfaz. No llevan RequirePermission: cada usuario
        // sólo puede leer y escribir las suyas, y el handler saca la identidad
        // del token, nunca de la petición. Exigir un permiso además obligaría a
        // concederlo a los 8 roles para algo que no es una facultad sino un
        // ajuste personal.
        group.MapGet("/preferencias", GetPreferenciasAsync)
            .WithName("Profile_GetPreferencias");

        group.MapPut("/preferencias", SavePreferenciasAsync)
            .WithName("Profile_SavePreferencias");
    }

    // ---------- Handlers ----------

    private static async Task<object?> BeginMfaEnrollmentAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new BeginMfaEnrollmentCommand(), ct);

    private static async Task<object?> ConfirmMfaEnrollmentAsync(
        [FromBody] ConfirmMfaBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new ConfirmMfaEnrollmentCommand(body.Code, body.Label), ct);

    /// <param name="Label">
    /// Cómo quiere llamar la persona a este dispositivo. Opcional: quien sólo
    /// tiene uno no necesita bautizarlo.
    /// </param>
    public sealed record ConfirmMfaBody(string Code, string? Label = null);

    private static async Task<object?> ListMfaCredentialsAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new ListMfaCredentialsQuery(), ct);

    private static async Task<object?> RenameMfaCredentialAsync(
        Guid credencialPublicId,
        [FromBody] RenameMfaCredentialBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new RenameMfaCredentialCommand(credencialPublicId, body.Label), ct);

    public sealed record RenameMfaCredentialBody(string? Label);

    private static async Task<object?> RevokeMfaCredentialAsync(
        Guid credencialPublicId, ISender sender, CancellationToken ct) =>
        await sender.Send(new RevokeMfaCredentialCommand(credencialPublicId), ct);

    private static async Task<object?> BeginWebAuthnEnrollmentAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new BeginWebAuthnEnrollmentCommand(), ct);

    private static async Task<object?> ConfirmWebAuthnEnrollmentAsync(
        [FromBody] ConfirmWebAuthnBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new ConfirmWebAuthnEnrollmentCommand(
            body.RetoId, body.RespuestaJson, body.Label), ct);

    /// <param name="RespuestaJson">
    /// Lo que devolvió <c>navigator.credentials.create()</c>, serializado tal cual.
    /// Viaja como texto y no como objeto tipado porque la librería que sabe
    /// interpretarlo vive en Infrastructure, y no puede asomar por aquí.
    /// </param>
    public sealed record ConfirmWebAuthnBody(string RetoId, string RespuestaJson, string? Label = null);

    private static async Task<object?> DisableMfaAsync(
        [FromBody] DisableMfaBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new DisableMfaCommand(body.CurrentPassword), ct);

    public sealed record DisableMfaBody(string CurrentPassword);

    private static async Task<object?> RegenerateRecoveryCodesAsync(
        [FromBody] RegenerateRecoveryCodesBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new RegenerateRecoveryCodesCommand(
            CurrentPassword: body.CurrentPassword,
            TotpCode: body.TotpCode), ct);

    public sealed record RegenerateRecoveryCodesBody(string? CurrentPassword, string? TotpCode);

    private static async Task<object?> ChangePasswordAsync(
        [FromBody] ChangePasswordBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ChangePasswordCommand(
            CurrentPassword: body.CurrentPassword,
            NewPassword: body.NewPassword,
            IpAddress: http.Connection.RemoteIpAddress?.ToString(),
            UserAgent: http.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null), ct);

    public sealed record ChangePasswordBody(string CurrentPassword, string NewPassword);

    private static async Task<object?> GetPreferenciasAsync(
        ISender sender, CancellationToken ct) =>
        await sender.Send(new GetMyPreferencesQuery(), ct);

    private static async Task<object?> SavePreferenciasAsync(
        [FromBody] SavePreferenciasBody body, ISender sender, CancellationToken ct) =>
        await sender.Send(new SaveMyPreferencesCommand(
            body.Preferencias ?? new Dictionary<string, string?>()), ct);

    public sealed record SavePreferenciasBody(Dictionary<string, string?>? Preferencias);
}
