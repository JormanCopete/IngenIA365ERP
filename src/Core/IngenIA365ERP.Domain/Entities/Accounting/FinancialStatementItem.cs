using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Rubro de un estado financiero NIIF (feature 009, FR-045, FR-047): estado de situación
/// financiera, estado de resultados integral, cambios en el patrimonio o flujo de efectivo.
/// Sembrado por grupo NIIF; ningún rubro está en código. Las cuentas del catálogo apuntan aquí
/// por <see cref="Code"/> y los estados se arman recorriendo rubros → cuentas → saldos.
/// </summary>
public class FinancialStatementItem : AuditableEntity
{
    public byte NiifGroup { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FinancialStatementKind Statement { get; set; }
    public string Section { get; set; } = string.Empty;
    public int Order { get; set; }

    /// <summary>+1 suma, −1 resta al total de su sección.</summary>
    public short Sign { get; set; }

    public string? ParentCode { get; set; }
}
