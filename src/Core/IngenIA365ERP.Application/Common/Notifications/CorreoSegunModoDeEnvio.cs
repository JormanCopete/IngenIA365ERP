using System.Net;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Correo;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Notifications;

/// <summary>
/// El modo de prueba del correo (pedido del dueño del 2026-11-17), en un solo sitio: envuelve al remitente de la plataforma y, si hay
/// cooperativa en curso, lee sus parámetros <c>CORREO</c> (<see cref="ParametrosDeCorreo"/>) antes de dejar salir cada mensaje.
/// <list type="bullet">
/// <item><b>Enviar</b> (el defecto): sale como siempre.</item>
/// <item><b>Suspender</b>: no sale. Queda una fila en <c>COR_HeldEmails</c>: pendiente con el mensaje entero (para reenviarlo después) si
/// <c>Correo.SuspendidosQuedanPendientes</c> está en Sí; si no, descartado, sólo con a quién iba y el asunto.</item>
/// <item><b>Redirigir</b>: sale a cada dirección del buzón de prueba, con «[PRUEBA]» en el asunto y una franja que dice a quién iba.
/// Sin buzón configurado se comporta como Suspender, nunca como Enviar.</item>
/// </list>
/// No se tocan los correos sin cooperativa en curso (entrar, recuperar la clave, el segundo factor: todavía no hay ninguna elegida) ni los
/// marcados <see cref="EmailMessage.SiempreAlDestinatario"/> (invitaciones y avisos de identidad dentro de la cooperativa): sin ellos
/// nadie podría entrar. Si los parámetros no se pueden leer, el correo sale como siempre y queda el error en el registro. (nuevo)
/// </summary>
public sealed class CorreoSegunModoDeEnvio(IEmailSender remitente, IServiceProvider servicios, ILogger<CorreoSegunModoDeEnvio> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var modo = message.SiempreAlDestinatario ? null : await ModoAsync(ct);
        if (modo is null || modo.Envio == ParametrosDeCorreo.Enviar)
        {
            await remitente.SendAsync(message, ct);
            return;
        }

        var db = servicios.GetRequiredService<IApplicationDbContext>();
        var buzones = ParametrosDeCorreo.Buzones(modo.Buzon);
        if (modo.Envio == ParametrosDeCorreo.Redirigir && buzones.Count > 0)
        {
            foreach (var buzon in buzones)
                await remitente.SendAsync(Desviado(message, buzon), ct);
            db.HeldEmails.Add(new HeldEmail
            {
                Mode = HeldEmailMode.Redirected,
                Status = HeldEmailStatus.Redirected,
                OriginalTo = Recortar(message.To, 320),
                RedirectedTo = Recortar(string.Join(", ", buzones), 1000),
                Subject = Recortar(message.Subject, 500),
                FromOverride = message.FromOverride,
                AttachmentCount = message.Attachments?.Count ?? 0,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[Correo.Redirigido] El correo «{Asunto}» para {Destinatario} fue al buzón de prueba ({Buzones}).",
                message.Subject, message.To, string.Join(", ", buzones));
            return;
        }

        if (modo.Envio == ParametrosDeCorreo.Redirigir)
        {
            logger.LogWarning("[Correo.SinBuzonDePrueba] El envío está en Redirigir pero {Clave} está vacío: el correo «{Asunto}» para {Destinatario} se suspende.",
                ParametrosDeCorreo.BuzonDePrueba, message.Subject, message.To);
        }

        var retenido = new HeldEmail
        {
            Mode = HeldEmailMode.Suspended,
            Status = modo.QuedanPendientes ? HeldEmailStatus.Pending : HeldEmailStatus.Discarded,
            OriginalTo = Recortar(message.To, 320),
            Subject = Recortar(message.Subject, 500),
            FromOverride = message.FromOverride,
            AttachmentCount = message.Attachments?.Count ?? 0,
        };
        if (modo.QuedanPendientes)
        {
            retenido.BodyHtml = message.BodyHtml;
            retenido.BodyText = message.BodyText;
            foreach (var adjunto in message.Attachments ?? [])
                retenido.Attachments.Add(new HeldEmailAttachment { FileName = Recortar(adjunto.FileName, 255), ContentType = Recortar(adjunto.ContentType, 150), Content = adjunto.Content });
        }
        db.HeldEmails.Add(retenido);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("[Correo.Suspendido] El correo «{Asunto}» para {Destinatario} no salió ({Estado}).",
            message.Subject, message.To, retenido.Status);
    }

    /// <summary>El modo vigente de la cooperativa en curso; nulo si no hay cooperativa o no se pudo leer (sale como siempre).</summary>
    private async Task<ModoDeCorreo?> ModoAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(servicios.GetService<ICurrentTenantService>()?.TenantId)) return null;
        try
        {
            var lector = servicios.GetRequiredService<ILectorDeParametros>();
            var hoy = servicios.GetRequiredService<IDateTimeService>().HoyLocal;
            var envio = await lector.LeerAsync(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.EnvioATerceros, hoy, ct: ct);
            if (envio.IsFailure)
            {
                logger.LogError("[Correo.ModoIlegible] No se pudo leer {Clave} ({Codigo}); el correo sale como siempre.",
                    ParametrosDeCorreo.EnvioATerceros, envio.Error.Code);
                return null;
            }
            var valor = envio.Value.Texto;
            if (valor == ParametrosDeCorreo.Enviar) return new ModoDeCorreo(valor, null, false);
            var buzon = await lector.LeerAsync(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.BuzonDePrueba, hoy, ct: ct);
            var pendientes = await lector.LeerAsync(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.SuspendidosQuedanPendientes, hoy, ct: ct);
            return new ModoDeCorreo(valor, buzon.IsSuccess ? buzon.Value.Texto : null, pendientes.IsSuccess && pendientes.Value.Valor is true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[Correo.ModoIlegible] No se pudo leer el modo de prueba del correo; el correo sale como siempre.");
            return null;
        }
    }

    /// <summary>El mismo mensaje hacia el buzón de prueba, con la marca en el asunto y la franja con el destinatario real.</summary>
    public static EmailMessage Desviado(EmailMessage original, string buzon)
    {
        var destino = WebUtility.HtmlEncode(original.To);
        var franja = "<div style=\"background:#fff4ce;border:1px solid #e0b000;padding:10px 14px;margin-bottom:16px;font-family:Arial,sans-serif;font-size:13px;color:#4a3b00\">" +
                     $"<strong>Correo de prueba.</strong> La empresa tiene el envío de correos en modo de prueba. En operación normal este correo habría llegado a <strong>{destino}</strong>." +
                     "</div>";
        return original with
        {
            To = buzon,
            Subject = $"[PRUEBA] {original.Subject}",
            BodyHtml = franja + original.BodyHtml,
            // Sin texto plano propio, el remitente lo deriva del HTML, que ya trae la franja.
            BodyText = original.BodyText is null ? null : $"CORREO DE PRUEBA. En operación normal este correo habría llegado a {original.To}.\n\n{original.BodyText}",
        };
    }

    private static string Recortar(string? texto, int largo) =>
        string.IsNullOrEmpty(texto) ? string.Empty : texto.Length <= largo ? texto : texto[..largo];

    private sealed record ModoDeCorreo(string Envio, string? Buzon, bool QuedanPendientes);
}
