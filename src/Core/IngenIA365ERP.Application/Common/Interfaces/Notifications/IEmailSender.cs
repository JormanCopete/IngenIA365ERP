namespace IngenIA365ERP.Application.Common.Interfaces.Notifications;

/// <summary>
/// Envío de correo desde la aplicación. La implementación por defecto es
/// <c>SmtpEmailSender</c> (Storage, T042) con reintentos exponenciales
/// (1 s / 4 s / 16 s + jitter). En US6 (T119) los fallos persistidos
/// alimentan el dashboard de diagnóstico.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed record EmailMessage(
    string To,
    string Subject,
    string BodyHtml,
    string? BodyText = null,
    string? FromOverride = null);
