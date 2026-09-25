namespace IngenIA365ERP.Domain.Enums.Parameters;

/// <summary>
/// El ámbito de una vigencia de parámetro (feature 012, T21, T067; decisiones-transversales §2.5; data-model §4.1).
/// <see cref="None"/> es el valor general; los demás son una excepción para una entidad concreta, cuyo Id va en
/// <c>COR_ParameterVersions.ScopeId</c> (sin FK: lo valida <c>AddParameterVersionCommand</c>). Se guarda como int;
/// un valor nunca se renumera.
/// </summary>
public enum ParameterScopeKind
{
    None = 0,
    Warehouse = 1,
    DocumentType = 2,
    PointOfSale = 3,
    CashRegister = 4,
    ThirdPartyKind = 5,
}
