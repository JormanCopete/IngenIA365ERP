using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pricing;

/// <summary>
/// El tope de descuento de un rol (<c>INV_DiscountCaps</c>; feature 012, I3, T576; FR-054, T51; data-model §14): por línea y por
/// total, como fracción, con vigencia y motivo obligatorio (sus comandos son <c>IConMotivo</c>). Único <c>(RoleId, ValidFrom)</c>
/// entre vivos y sin cruces del mismo rol (<c>Inventory.DiscountCap.Overlaps</c>); uno nuevo cierra el anterior la víspera. El
/// tope de un usuario es el mayor de sus roles activos; sin fila, 0 (<c>TopeDeDescuento.Efectivo</c>).
/// </summary>
public class DiscountCap : AuditableEntity
{
    /// <summary><c>SEC_Roles</c>: rol de la cooperativa.</summary>
    public int RoleId { get; set; }

    /// <summary>Fracción (0,05 = 5 %).</summary>
    public decimal MaxLineRate { get; set; }

    /// <summary>Fracción.</summary>
    public decimal MaxDocumentRate { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string Reason { get; set; } = string.Empty;
}
