using System.Globalization;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Invitations.Services;

/// <summary>
/// T057 — Empaqueta una <see cref="Invitation"/> en un <see cref="EmailMessage"/>
/// usando la plantilla <c>InvitationEmail.html</c> y lo despacha vía
/// <see cref="IEmailSender"/>. Mantiene la composición del subject + el
/// armado del enlace fuera del handler que emite la invitación, para que
/// éste solo decida el "qué" y no el "cómo".
///
/// <para>
/// El dispatcher recibe el <b>token plano</b> (no el hash que vive en BD)
/// porque el destinatario lo necesita en el enlace. El plano se genera una
/// sola vez en el handler emisor y nunca se persiste.
/// </para>
/// </summary>
public interface IInvitationEmailDispatcher
{
    Task DispatchAsync(InvitationEmailRequest request, CancellationToken ct);
}

/// <summary>
/// Carga útil para enviar una invitación. <paramref name="PlainTokenBase64Url"/>
/// es el token de un solo uso que aparece en el enlace y NO se persiste —
/// el handler emisor lo destruye después de invocar al dispatcher.
/// </summary>
public sealed record InvitationEmailRequest(
    Invitation Invitation,
    Tenant Tenant,
    string InviterDisplayName,
    string PlainTokenBase64Url,
    string? RecipientDisplayName = null);

internal sealed class InvitationEmailDispatcher(
    IIdentityEmailTemplates templates,
    IEmailSender sender,
    IOptions<IdentityEmailOptions> options,
    ILogger<InvitationEmailDispatcher> logger) : IInvitationEmailDispatcher
{
    private readonly IdentityEmailOptions _options = options.Value;

    public async Task DispatchAsync(InvitationEmailRequest request, CancellationToken ct)
    {
        var link = BuildAcceptUrl(request.PlainTokenBase64Url);
        var expires = request.Invitation.ExpiresAt
            .ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);

        var model = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nombre"] = request.RecipientDisplayName
                ?? ExtractLocalPart(request.Invitation.Email),
            ["Empresa"] = request.Tenant.Name,
            ["Emisor"] = request.InviterDisplayName,
            ["Enlace"] = link,
            ["Expira"] = expires,
        };

        var html = await templates.RenderAsync("InvitationEmail", model, ct);

        var message = new EmailMessage(
            To: request.Invitation.Email,
            Subject: $"Invitación a {request.Tenant.Name} — IngenIA365ERP",
            BodyHtml: html);

        await sender.SendAsync(message, ct);

        logger.LogInformation(
            "Invitación enviada a {Email} para tenant '{Tenant}' (expira {Expires}).",
            request.Invitation.Email, request.Tenant.Name, expires);
    }

    private string BuildAcceptUrl(string plainToken)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/auth/accept-invitation?token={Uri.EscapeDataString(plainToken)}";
    }

    private static string ExtractLocalPart(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? email : email[..at];
    }
}
