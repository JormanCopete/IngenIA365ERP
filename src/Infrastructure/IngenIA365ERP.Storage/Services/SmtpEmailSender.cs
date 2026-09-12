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
///
/// <para>
/// <b>Respaldo</b> (2026-09-12): si la cuenta principal agota sus reintentos y hay
/// una sección <c>Smtp:Respaldo</c>, el mismo mensaje sale por ella —con SU
/// remitente— y se registra como error que la principal falló. Si las dos fallan,
/// la excepción que sube lleva los dos motivos. Nació el día que producción
/// quedó con la primera cooperativa sin poder invitar a nadie porque el buzón
/// autenticado no era el del remitente.
/// </para>
/// </summary>
internal sealed class SmtpEmailSender(
    IOptions<SmtpSettings> settings,
    ILogger<SmtpEmailSender> logger,
    Func<SmtpClient>? fabricaDeClientes = null) : IEmailSender
{
    private readonly SmtpSettings _settings = settings.Value;
    private readonly Func<SmtpClient> _nuevoCliente = fabricaDeClientes ?? (() => new SmtpClient());
    private readonly Random _jitter = new();

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var respaldo = _settings.Respaldo;
        if (respaldo is null || !respaldo.EstaConfigurado)
        {
            await EnviarPorAsync(_settings, "principal", message, ct);
            return;
        }

        try
        {
            await EnviarPorAsync(_settings, "principal", message, ct);
        }
        catch (Exception principal) when (principal is not OperationCanceledException)
        {
            logger.LogError(principal,
                "El relay principal ({Host}, remitente {From}) no pudo entregar el correo a {Recipient}; se intenta por el respaldo ({HostRespaldo}, remitente {FromRespaldo}).",
                _settings.Host, _settings.FromAddress, message.To, respaldo.Host, respaldo.FromAddress);
            try
            {
                await EnviarPorAsync(respaldo, "respaldo", message, ct);
            }
            catch (Exception secundario) when (secundario is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "Ni el relay principal ni el de respaldo pudieron entregar el correo. " +
                    $"Principal: {principal.GetBaseException().Message} Respaldo: {secundario.GetBaseException().Message}",
                    secundario);
            }
        }
    }

    private async Task EnviarPorAsync(SmtpSettings cuenta, string rol, EmailMessage message, CancellationToken ct)
    {
        var mime = new MimeMessage();
        var from = string.IsNullOrWhiteSpace(message.FromOverride)
            ? new MailboxAddress(cuenta.FromName, cuenta.FromAddress)
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
                using var client = _nuevoCliente();

                // Sólo si se configuró una huella. Sin ella, MailKit valida
                // contra las autoridades del sistema, que es lo que debe pasar.
                if (!string.IsNullOrWhiteSpace(cuenta.HuellaCertificadoAceptada))
                    client.ServerCertificateValidationCallback = (_, c, _, e) => ValidarCertificadoFijado(cuenta, c, e);

                await client.ConnectAsync(
                    cuenta.Host,
                    cuenta.Port,
                    cuenta.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                    ct);

                if (!string.IsNullOrEmpty(cuenta.Username))
                    await client.AuthenticateAsync(cuenta.Username, cuenta.Password, ct);

                await client.SendAsync(mime, ct);
                await client.DisconnectAsync(true, ct);
                logger.LogInformation(
                    "Correo entregado a {Recipient} por el relay {Rol} (intento {Attempt})",
                    message.To, rol, attempt);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt <= cuenta.MaxRetries)
            {
                var backoffSeconds = cuenta.InitialBackoffSeconds * Math.Pow(4, attempt - 1);
                var jitterMs = _jitter.Next(0, 500);
                var delay = TimeSpan.FromSeconds(backoffSeconds) + TimeSpan.FromMilliseconds(jitterMs);
                logger.LogWarning(
                    ex,
                    "Fallo al enviar correo a {Recipient} por el relay {Rol} (intento {Attempt}/{Max}); reintento en {Delay}",
                    message.To, rol, attempt, cuenta.MaxRetries + 1, delay);
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
        System.Net.Security.SslPolicyErrors errors) =>
        ValidarCertificadoFijado(_settings, certificate, errors);

    /// <summary>Cada cuenta (principal o respaldo) fija su propia huella: pueden ser servidores distintos.</summary>
    internal bool ValidarCertificadoFijado(
        SmtpSettings cuenta,
        System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
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
        if (string.IsNullOrWhiteSpace(cuenta.HuellaCertificadoAceptada))
            return false;

        var huella = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(certificate.GetRawCertData()));

        var esperada = cuenta.HuellaCertificadoAceptada
            .Replace(":", string.Empty)
            .Replace(" ", string.Empty);

        var coincide = string.Equals(huella, esperada, StringComparison.OrdinalIgnoreCase);

        if (coincide)
        {
            logger.LogWarning(
                "El servidor {Host} presenta un certificado que no valida ({Errores}), " +
                "pero coincide con la huella fijada. Es un parche: corresponde instalar " +
                "un certificado válido en ese servidor.",
                cuenta.Host, errors);
        }
        else
        {
            logger.LogError(
                "Certificado de {Host} rechazado. Errores: {Errores}. " +
                "Huella recibida {Recibida}, esperada {Esperada}. " +
                "Si el servidor cambió de certificado a propósito, hay que actualizar " +
                "Smtp:HuellaCertificadoAceptada; si no, alguien se está interponiendo.",
                cuenta.Host, errors, huella, esperada);
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
