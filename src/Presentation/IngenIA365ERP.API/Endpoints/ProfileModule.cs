using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Profile.BeginMfaEnrollment;
using IngenIA365ERP.Application.Identity.Profile.ChangePassword;
using IngenIA365ERP.Application.Identity.Profile.ConfirmMfaEnrollment;
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
        await sender.Send(new ConfirmMfaEnrollmentCommand(body.Code), ct);

    public sealed record ConfirmMfaBody(string Code);

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
