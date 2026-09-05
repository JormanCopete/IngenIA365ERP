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
///
/// <para>
/// T042 (Feature 002): renombrado desde <c>MailKitEmailSender</c> para
/// alinear el nombre con la abstracción (SMTP) en lugar del proveedor
/// concreto (MailKit). El comportamiento es idéntico.
/// </para>
/// </summary>
internal sealed class SmtpEmailSender(
    IOptions<SmtpSettings> settings,
    ILogger<SmtpEmailSender> logger) : IEmailSender
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
        // Adjuntos (feature 005: el comprobante de pago viaja como PDF). Opcionales;
        // los correos que ya existian no traen ninguno y siguen igual.
        if (message.Attachments is { Count: > 0 })
        {
            foreach (var adjunto in message.Attachments)
            {
                body.Attachments.Add(adjunto.FileName, adjunto.Content, ContentType.Parse(adjunto.ContentType));
            }
        }
        mime.Body = body.ToMessageBody();

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var client = new SmtpClient();

                // Sólo si se configuró una huella. Sin ella, MailKit valida
                // contra las autoridades del sistema, que es lo que debe pasar.
                if (!string.IsNullOrWhiteSpace(_settings.HuellaCertificadoAceptada))
                    client.ServerCertificateValidationCallback = ValidarCertificadoFijado;

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

    /// <summary>
    /// Acepta el certificado si valida normalmente O si su huella SHA-256
    /// coincide EXACTAMENTE con la configurada. Cualquier otro se rechaza.
    /// </summary>
    /// <remarks>
    /// La diferencia con desactivar la validación es toda: aquello acepta
    /// cualquier certificado, y entonces quien se interponga en la red puede
    /// presentar el suyo, descifrar el tráfico y quedarse con los enlaces de
    /// invitación y de restablecimiento que viajan en esos correos. Esto acepta
    /// uno solo. Si el servidor cambia de certificado, el envío falla y alguien
    /// tiene que mirar por qué — que es justo lo que se quiere.
    /// </remarks>
    internal bool ValidarCertificadoFijado(
        object sender,
        System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain,
        System.Net.Security.SslPolicyErrors errors)
    {
        if (errors == System.Net.Security.SslPolicyErrors.None)
            return true;

        if (certificate is null)
            return false;

        // Sin huella configurada no hay nada contra que comparar. Se rechaza en
        // vez de dejar pasar: ante la duda, no conectar. (Con la configuracion
        // normal este metodo ni siquiera se engancha, pero un default seguro
        // vale mas que confiar en que nadie lo llame por otro camino.)
        if (string.IsNullOrWhiteSpace(_settings.HuellaCertificadoAceptada))
            return false;

        var huella = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(certificate.GetRawCertData()));

        var esperada = _settings.HuellaCertificadoAceptada
            .Replace(":", string.Empty)
            .Replace(" ", string.Empty);

        var coincide = string.Equals(huella, esperada, StringComparison.OrdinalIgnoreCase);

        if (coincide)
        {
            logger.LogWarning(
                "El servidor {Host} presenta un certificado que no valida ({Errores}), " +
                "pero coincide con la huella fijada. Es un parche: corresponde instalar " +
                "un certificado válido en ese servidor.",
                _settings.Host, errors);
        }
        else
        {
            logger.LogError(
                "Certificado de {Host} rechazado. Errores: {Errores}. " +
                "Huella recibida {Recibida}, esperada {Esperada}. " +
                "Si el servidor cambió de certificado a propósito, hay que actualizar " +
                "Smtp:HuellaCertificadoAceptada; si no, alguien se está interponiendo.",
                _settings.Host, errors, huella, esperada);
        }

        return coincide;
    }

    private static string ToPlainText(string html)
    {
        // Heurística mínima: usa el TextConverter de MimeKit si está disponible.
        // No es un convertidor HTML completo — suficiente para "plantillas planas".
        var doc = new TextPart(TextFormat.Html) { Text = html };
        return doc.Text;
    }
}
