using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Storage.Configuration;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// <see cref="IOutboundEmailStatus"/> sobre <see cref="SmtpSettings"/>: hay correo saliente si
/// la sección declara un host. No prueba la conexión —eso lo hace cada envío, con sus
/// reintentos—; sólo evita intentar sin configuración. Nunca expone usuario ni contraseña.
/// </summary>
public sealed class OutboundEmailStatus(IOptions<SmtpSettings> options) : IOutboundEmailStatus
{
    private SmtpSettings Settings => options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Settings.Host) && Settings.Port > 0 && !string.IsNullOrWhiteSpace(Settings.FromAddress);

    public string Description => IsConfigured
        ? $"{Settings.Host}:{Settings.Port}, remitente {Settings.FromAddress}"
          + (Settings.Respaldo is { } r && r.EstaConfigurado ? $"; respaldo {r.Host}:{r.Port}, remitente {r.FromAddress}" : "")
        : "la sección Smtp no declara host, puerto o remitente";
}
