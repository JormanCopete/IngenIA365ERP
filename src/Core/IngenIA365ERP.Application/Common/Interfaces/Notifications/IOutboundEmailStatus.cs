namespace IngenIA365ERP.Application.Common.Interfaces.Notifications;

/// <summary>
/// Si el ambiente tiene correo saliente de verdad. Un comando que va a enviar decenas de
/// correos lo consulta antes de intentar nada, para informar en vez de dejar decenas de
/// fallos registrados (feature 005, <c>Payroll.EmailNotConfigured</c>). La implementación
/// vive en Storage, junto a la configuración SMTP.
/// </summary>
public interface IOutboundEmailStatus
{
    bool IsConfigured { get; }

    /// <summary>Descripción para el mensaje al usuario (sin credenciales): host y remitente, o por qué no está configurado.</summary>
    string Description { get; }
}
