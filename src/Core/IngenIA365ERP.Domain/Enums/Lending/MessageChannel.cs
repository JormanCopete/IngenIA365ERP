namespace IngenIA365ERP.Domain.Enums.Lending;

/// <summary>
/// Canal de un mensaje al deudor (feature 013, T74, T76, F-04; data-model §9.1). Se guarda como int en
/// <c>LND_DebtorMessages</c> y como <b>texto</b> en <c>COR_NotificationTemplates.Channel</c>. Lo crea la fase
/// Fundacional A (T0045) porque el puerto de mensajería y las plantillas lo usan desde ahí.
/// </summary>
public enum MessageChannel
{
    Email = 1,
    Sms = 2,
    WhatsApp = 3,
    Letter = 4,
}
