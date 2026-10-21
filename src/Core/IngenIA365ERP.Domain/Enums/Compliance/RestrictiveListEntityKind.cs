namespace IngenIA365ERP.Domain.Enums.Compliance;

/// <summary>
/// Clase de una entrada de lista restrictiva (feature 013, T70, F-18; data-model parte 3 §9.2). Se guarda como int en
/// <c>COR_RestrictiveListEntries.EntityKind</c>.
/// </summary>
public enum RestrictiveListEntityKind
{
    Person = 1,
    Organization = 2,
    Other = 3,
}
