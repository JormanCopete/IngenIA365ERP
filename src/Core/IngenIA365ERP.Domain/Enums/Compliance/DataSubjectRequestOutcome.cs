namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// Desenlace de una solicitud del titular al responderla (feature 013, T69, S10; data-model parte 3 §9.2). Se guarda como
/// int en <c>COR_DataSubjectRequests.Outcome</c>; una revocación que no procede por un deber legal es <see cref="Denied"/>.
/// </summary>
public enum DataSubjectRequestOutcome
{
    Granted = 1,
    PartiallyGranted = 2,
    Denied = 3,
}
