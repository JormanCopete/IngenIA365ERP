namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// Clase de una solicitud del titular de los datos (feature 013, T69, S10; data-model parte 3 §4.9, §9.2). Se guarda como
/// int en <c>COR_DataSubjectRequests.Kind</c>. <see cref="Query"/> corre con el plazo de consulta; las demás, con el de
/// reclamo (<c>PlazosDelTitular</c>).
/// </summary>
public enum DataSubjectRequestKind
{
    Query = 1,
    Update = 2,
    Rectification = 3,
    Suppression = 4,
    Revocation = 5,
}
