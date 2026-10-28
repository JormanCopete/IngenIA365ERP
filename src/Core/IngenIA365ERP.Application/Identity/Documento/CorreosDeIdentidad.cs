using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Documento;

/// <summary>
/// Feature 017: los avisos de identidad (usuario cambiado, código y aviso del cambio de correo). Salen por
/// <see cref="IEmailSender"/> directo, como la bienvenida: lo que avisan ya ocurrió en la base central y no
/// hay una cooperativa en cuya bandeja escribir. Nunca fallan el comando: un aviso que no sale se anota.
/// </summary>
public sealed class CorreosDeIdentidad(
    IEmailSender correo,
    IIdentityEmailTemplates plantillas,
    ILogger<CorreosDeIdentidad> logger)
{
    public async Task<bool> EnviarAsync(
        string destino, string plantilla, string asunto, IReadOnlyDictionary<string, string?> modelo, CancellationToken ct)
    {
        try
        {
            var html = await plantillas.RenderAsync(
                plantilla, new Dictionary<string, string?>(modelo, StringComparer.OrdinalIgnoreCase), ct);
            await correo.SendAsync(new EmailMessage(destino, asunto, html), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No salió el aviso de identidad {Plantilla}.", plantilla);
            return false;
        }
    }
}
