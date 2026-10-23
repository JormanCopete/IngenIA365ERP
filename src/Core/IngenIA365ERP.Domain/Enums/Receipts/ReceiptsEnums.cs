namespace IngenIA365ERP.Domain.Enums.Receipts;

/// <summary>
/// El estado del módulo de Recaudo en la cooperativa (feature 015, T0036; data-model §1.1, §20). Nace <see cref="NotActivated"/> con la fila
/// única de <c>RCD_Setup</c> y pasa a <see cref="Active"/> sólo por la activación (<c>ActivateReceiptsCommand</c>, fase 19; la mínima del
/// bloque 2, TR42). Antes de activar, toda escritura de negocio responde <c>Receipts.NotActivated</c>; la configuración y la puesta en
/// marcha sí se admiten (FR-150, FR-159).
/// </summary>
public enum ReceiptsModuleStatus
{
    NotActivated = 1,
    Active = 2,
}
