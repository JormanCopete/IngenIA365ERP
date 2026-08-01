using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Identity.Profile.RequestPasswordReset;
using IngenIA365ERP.Application.Identity.Profile.ResetPassword;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T079j — Endpoints públicos del flujo "olvidé mi contraseña". Ambos son
/// anónimos: el primero acepta el email (siempre 202 — defensa anti-enumeración),
/// el segundo consume el token recibido por correo + aplica la nueva contraseña.
/// </summary>
public sealed class AuthRecoveryModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/password")
            .WithTags("Identity / Auth Recovery")
            .AddEndpointFilter<ErrorEnvelopeFilter>();

        group.MapPost("/forgot", ForgotAsync)
            .AllowAnonymous()
            .WithName("AuthRecovery_Forgot");

        group.MapPost("/reset", ResetAsync)
            .AllowAnonymous()
            .WithName("AuthRecovery_Reset");
    }

    private static async Task<object?> ForgotAsync(
        [FromBody] ForgotBody body,
        HttpContext http,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new RequestPasswordResetCommand(
            Email: body.Email,
            IpAddress: http.Connection.RemoteIpAddress?.ToString()), ct);

    public sealed record ForgotBody(string Email);

    private static async Task<object?> ResetAsync(
        [FromBody] ResetBody body,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new ResetPasswordCommand(body.Token, body.NewPassword), ct);

    public sealed record ResetBody(string Token, string NewPassword);
}
