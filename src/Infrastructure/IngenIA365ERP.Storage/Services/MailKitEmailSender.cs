using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Storage.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// Envío SMTP con MailKit y reintentos exponenciales 1 s / 4 s / 16 s
/// con jitter (FR-040). Tras agotar reintentos, propaga la excepción
/// para que el orquestador (T119, US6) persista el fallo y reintente
/// más tarde desde el worker.
/// </summary>
internal sealed class MailKitEmailSender(
    IOptions<SmtpSettings> settings,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    private readonly SmtpSettings _settings = settings.Value;
    private readonly Random _jitter = new();

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mime = new MimeMessage();
        var from = string.IsNullOrWhiteSpace(message.FromOverride)
            ? new MailboxAddress(_settings.FromName, _settings.FromAddress)
            : MailboxAddress.Parse(message.FromOverride);
        mime.From.Add(from);
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        var body = new BodyBuilder
        {
            HtmlBody = message.BodyHtml,
            TextBody = message.BodyText ?? ToPlainText(message.BodyHtml)
        };
        mime.Body = body.ToMessageBody();

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(
                    _settings.Host,
                    _settings.Port,
                    _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                    ct);

                if (!string.IsNullOrEmpty(_settings.Username))
                    await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

                await client.SendAsync(mime, ct);
                await client.DisconnectAsync(true, ct);
                logger.LogInformation(
                    "Correo entregado a {Recipient} (intento {Attempt})",
                    message.To, attempt);
                return;
            }
            catch (Exception ex) when (attempt <= _settings.MaxRetries)
            {
                var backoffSeconds = _settings.InitialBackoffSeconds * Math.Pow(4, attempt - 1);
                var jitterMs = _jitter.Next(0, 500);
                var delay = TimeSpan.FromSeconds(backoffSeconds) + TimeSpan.FromMilliseconds(jitterMs);
                logger.LogWarning(
                    ex,
                    "Fallo al enviar correo a {Recipient} (intento {Attempt}/{Max}); reintento en {Delay}",
                    message.To, attempt, _settings.MaxRetries + 1, delay);
                await Task.Delay(delay, ct);
            }
        }
    }

    private static string ToPlainText(string html)
    {
        // Heurística mínima: usa el TextConverter de MimeKit si está disponible.
        // No es un convertidor HTML completo — suficiente para "plantillas planas".
        var doc = new TextPart(TextFormat.Html) { Text = html };
        return doc.Text;
    }
}
