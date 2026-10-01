using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Grupo contable (<c>INV_AccountingGroups</c>; feature 012, T199; FR-027, FR-073; data-model §1.4): la clasificación que
/// Inventario envía en cada mensaje y que la matriz de Contabilidad traduce a cuentas. <see cref="Code"/> es inmutable (la
/// matriz lo usa como <c>AccountingGroupCode</c>, T27). Inventario no guarda cuentas.
/// </summary>
public class AccountingGroup : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Qué agrupa, para la contadora.</summary>
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
