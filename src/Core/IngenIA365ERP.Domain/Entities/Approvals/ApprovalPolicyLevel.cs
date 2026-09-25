using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Approvals;

/// <summary>
/// Un nivel de una versión de política (<c>COR_ApprovalPolicyLevels</c>; feature 012, T33, T081; data-model §21):
/// orden 1..n, umbral en pesos que no decrece con el orden y el permiso que lo decide (cualquier código del
/// catálogo, C6). Único <c>(PolicyId, Order)</c>. Se crea con su versión y no se edita: cambiar un nivel es registrar
/// otra versión.
/// </summary>
public class ApprovalPolicyLevel : AuditableEntity
{
    public int PolicyId { get; set; }

    public ApprovalPolicy? Policy { get; set; }

    /// <summary>1..n (tinyint).</summary>
    public byte Order { get; set; }

    /// <summary>Monto desde el que se exige este nivel (18,2).</summary>
    public decimal Threshold { get; set; }

    /// <summary>El permiso que decide este nivel (máx. 100).</summary>
    public string PermissionCode { get; set; } = string.Empty;
}
