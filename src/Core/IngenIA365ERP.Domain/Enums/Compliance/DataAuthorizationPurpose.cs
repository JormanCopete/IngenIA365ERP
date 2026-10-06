namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// Finalidad de una autorización de tratamiento de datos (feature 013, T69, S10; data-model parte 3 §9.2). Se guarda como
/// <b>texto</b> (el nombre) en <c>CMP_HabeasDataConsents.Purpose</c>, <c>CMP_HabeasDataPolicyVersions.Purpose</c> y
/// <c>COR_DataSubjectRequests.Purpose</c>; las filas anteriores a la 013 quedan en <see cref="GeneralTreatment"/>.
/// </summary>
public enum DataAuthorizationPurpose
{
    GeneralTreatment = 1,
    CreditProcessing = 2,
    CreditBureauQuery = 3,
    CreditBureauReport = 4,
    ContactChannel = 5,
}
