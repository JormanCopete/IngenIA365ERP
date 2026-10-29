namespace IngenIA365ERP.Domain.Enums.Treasury;

/// <summary>
/// El estado del módulo de Tesorería en la cooperativa (feature 015, T0036; data-model §9.1, §20). Nace <see cref="NotActivated"/> con la
/// fila única de <c>TES_Setup</c> y pasa a <see cref="Active"/> sólo por la activación; antes, toda escritura de negocio responde
/// <c>Treasury.NotActivated</c>. No afecta a lo heredado (<c>TRS_</c>), que sigue como está hasta T2.
/// </summary>
public enum TreasuryModuleStatus
{
    NotActivated = 1,
    Active = 2,
}
