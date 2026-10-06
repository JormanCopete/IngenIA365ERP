namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// De dónde salió una autorización de tratamiento de datos (feature 013, T69, S10; data-model parte 3 §9.2). Se guarda
/// como <b>texto</b> en <c>CMP_HabeasDataConsents.Origin</c>.
/// </summary>
public enum DataAuthorizationOrigin
{
    PersonRegistration = 1,
    CreditApplication = 2,
    Migration = 3,
    TitularRequest = 4,
    Import = 5,
}
