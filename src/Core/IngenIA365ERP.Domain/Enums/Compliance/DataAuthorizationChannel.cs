namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// Canal de contacto autorizado o revocado (finalidad <see cref="DataAuthorizationPurpose.ContactChannel"/>; feature 013,
/// T69, S10; data-model parte 3 §9.2). Se guarda como <b>texto</b> en <c>CMP_HabeasDataConsents.ContactChannel</c> y
/// <c>COR_DataSubjectRequests.ContactChannel</c>.
/// </summary>
public enum DataAuthorizationChannel
{
    Sms = 1,
    WhatsApp = 2,
    Email = 3,
    Call = 4,
    Visit = 5,
}
