using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Security;

/// <summary>
/// Monto máximo de un permiso para un rol (<c>SEC_PermissionAmountLimits</c>; feature 012, T34, T081; data-model
/// §21; contracts/api.md §1.3). Con vigencia sin cruces y motivo; <see cref="MaxAmount"/> nulo = sin límite desde
/// <see cref="ValidFrom"/>. El límite efectivo de un usuario es el mayor de sus roles activos que conceden el permiso,
/// y un rol que lo concede sin fila no tiene límite (<c>ILimitesPorPermiso</c>, C4). Único escritor:
/// <c>SetPermissionAmountLimitCommandHandler</c>. La tabla llega con la migración <c>PlataformaParaInventario</c>.
/// </summary>
public class PermissionAmountLimit : AuditableEntity
{
    public int RoleId { get; set; }

    public Role? Role { get; set; }

    /// <summary>Código <c>Recurso.Acción</c> del catálogo (máx. 100); sólo los limitables.</summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>18,2; nulo = sin límite.</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary><c>COP</c> (char(3)).</summary>
    public string Currency { get; set; } = "COP";

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Motivo obligatorio (máx. 300).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Vigente a la fecha (inclusive en los dos extremos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
