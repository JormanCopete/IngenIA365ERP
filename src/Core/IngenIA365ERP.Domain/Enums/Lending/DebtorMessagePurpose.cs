namespace IngenIA365ERP.Domain.Enums.Lending;

/// <summary>
/// Propósito de un mensaje al deudor (feature 013, T74, T76, F-04; data-model §9.1). Se guarda como int en
/// <c>LND_DebtorMessages</c> y como <b>texto</b> en <c>COR_NotificationTemplates.Purpose</c>; cada propósito tiene su lista
/// blanca de variables de plantilla. Lo crea la fase Fundacional A (T0045).
/// </summary>
public enum DebtorMessagePurpose
{
    Collection = 1,
    PriorNotice = 2,
    Informative = 3,
    DocumentDelivery = 4,
    CollectionFeeNotice = 5,
}
