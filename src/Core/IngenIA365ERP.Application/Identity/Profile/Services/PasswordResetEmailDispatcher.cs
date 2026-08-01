using System.Globalization;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Identity.Profile.Services;

/// <summary>
/// T079e — Despacha el correo del flujo "olvidé mi contraseña" usando la
/// plantilla <c>PasswordResetEmail.html</c>. Construye el enlace
/// <c>{BaseUrl}/auth/reset-password?token={token}</c> con el token plano
/// (que NO se persiste en BD).
/// </summary>
public interface IPasswordResetEmailDispatcher
{
    Task DispatchAsync(
        string toEmail,
        string recipientName,
        string plainTokenBase64Url,
        DateTime expiresAt,
        string? requesterIp,
        CancellationToken ct);
}

internal sealed class PasswordResetEmailDispatcher(
    IIdentityEmailTemplates templates,
    IEmailSender sender,
    IOptions<IdentityEmailOptions> options,
    ILogger<PasswordResetEmailDispatcher> logger) : IPasswordResetEmailDispatcher
{
    private readonly IdentityEmailOptions _options = options.Value;

    public async Task DispatchAsync(
        string toEmail,
        string recipientName,
        string plainTokenBase64Url,
        DateTime expiresAt,
        string? requesterIp,
        CancellationToken ct)
    {
        var link = BuildResetUrl(plainTokenBase64Url);

        var model = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nombre"] = recipientName,
            ["Enlace"] = link,
            ["Expira"] = expiresAt.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture),
            ["IpAddress"] = requesterIp ?? "desconocida",
        };

        var html = await templates.RenderAsync("PasswordResetEmail", model, ct);

        var message = new EmailMessage(
            To: toEmail,
            Subject: "Restablece tu contraseña — IngenIA365ERP",
            BodyHtml: html);

        await sender.SendAsync(message, ct);

        logger.LogInformation(
            "Password reset email enviado a {Email} (expira {Expires}).",
            toEmail, expiresAt);
    }

    private string BuildResetUrl(string plainToken)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/auth/reset-password?token={Uri.EscapeDataString(plainToken)}";
    }
}
