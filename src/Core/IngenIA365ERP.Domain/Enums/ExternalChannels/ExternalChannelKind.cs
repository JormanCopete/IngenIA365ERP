namespace IngenIA365ERP.Domain.Enums.ExternalChannels;

/// <summary>
/// Clase de un canal externo configurado (feature 013, T72, F-02; data-model parte 3 §9.2). Se guarda como int en
/// <c>COR_ExternalChannelSettings.Kind</c>; un valor nunca se renumera.
/// </summary>
public enum ExternalChannelKind
{
    Sms = 1,
    WhatsApp = 2,
    SecuritiesDepository = 3,
    CreditBureau = 4,
    RestrictiveLists = 5,
}
