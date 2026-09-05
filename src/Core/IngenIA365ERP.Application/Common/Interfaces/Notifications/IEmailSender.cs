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
    string? FromOverride = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);

/// <summary>
/// Adjunto de un correo. Nació para el comprobante de pago de nómina (feature 005):
/// el empleado no tiene sesión en el sistema, así que el PDF tiene que viajar en
/// el correo. Opcional y al final para que ningún llamador existente cambie.
/// </summary>
public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);
