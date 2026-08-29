using System.Globalization;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Identity.Auth.Recuperacion;

/// <summary>
/// Manda el aviso de recuperación, con los dos enlaces. Copia el camino del correo
/// de «olvidé mi contraseña» —plantilla + <c>IEmailSender</c>— porque las dos cosas
/// ocurren en el mismo estado: sin cooperativa elegida.
/// </summary>
internal sealed class MfaRecoveryEmailDispatcher(
    IIdentityEmailTemplates templates,
    IEmailSender sender,
    IOptions<IdentityEmailOptions> options,
    ILogger<MfaRecoveryEmailDispatcher> logger) : IMfaRecoveryEmailDispatcher
{
    private readonly IdentityEmailOptions _options = options.Value;

    public async Task DespacharAsync(AvisoDeRecuperacionMfa aviso, CancellationToken ct)
    {
        var modelo = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EnlaceDeCancelacion"] = Enlace("cancelar", aviso.TokenDeCancelacion),
            ["EnlaceDeConfirmacion"] = Enlace("confirmar", aviso.TokenDeConfirmacion),
            ["EjecutableDesde"] = Momento(aviso.EjecutableDesde),
            ["ExpiraEn"] = Momento(aviso.ExpiraEn),
            ["IpSolicitante"] = aviso.IpSolicitante ?? "desconocida",
        };

        var html = await templates.RenderAsync("MfaRecoveryEmail", modelo, ct);

        await sender.SendAsync(
            new EmailMessage(
                To: aviso.Correo,
                // El asunto tiene que leerse entero en la lista del buzón, sin
                // abrirlo: es la única línea que ve alguien que no esperaba este
                // correo, y de que la lea depende que cancele a tiempo.
                Subject: "Alguien pidió retirar tu segundo factor — IngenIA365ERP",
                BodyHtml: html),
            ct);

        logger.LogInformation(
            "Aviso de recuperación de segundo factor enviado a {Correo}; ejecutable desde {Cuando}.",
            aviso.Correo, aviso.EjecutableDesde);
    }

    private string Enlace(string accion, string tokenPlano)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/security/mfa-recovery/{accion}?token={Uri.EscapeDataString(tokenPlano)}";
    }

    private static string Momento(DateTime utc) =>
        utc.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);
}
