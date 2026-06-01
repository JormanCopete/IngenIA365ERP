using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// Implementación de <see cref="IPasswordChangedNotifier"/> que arma el
/// <see cref="EmailMessage"/> a partir de la plantilla
/// <c>PasswordChangedNotification.html</c> y lo envía con
/// <see cref="IEmailSender"/>.
/// </summary>
internal sealed class PasswordChangedNotifier(
    IIdentityEmailTemplates templates,
    IEmailSender sender,
    ILogger<PasswordChangedNotifier> logger) : IPasswordChangedNotifier
{
    public async Task NotifyAsync(
        string toEmail,
        string recipientName,
        DateTime occurredAt,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        var model = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nombre"] = recipientName,
            ["FechaCambio"] = occurredAt.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture),
            ["IpAddress"] = ipAddress ?? "desconocida",
            ["UserAgent"] = userAgent ?? "desconocido",
        };

        var html = await templates.RenderAsync("PasswordChangedNotification", model, ct);

        var message = new EmailMessage(
            To: toEmail,
            Subject: "Tu contraseña fue cambiada — IngenIA365ERP",
            BodyHtml: html);

        await sender.SendAsync(message, ct);

        logger.LogInformation(
            "PasswordChangedNotification enviada a {Email} (ocurrió {When}).",
            toEmail, occurredAt);
    }
}
