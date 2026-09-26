using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.GoLive;

/// <summary>
/// La activación de una bodega (<c>INV_WarehouseActivations</c>; feature 012, T304; FR-090, SC-018; data-model §6.4): se activa
/// una sola vez (<c>UK (WarehouseId)</c> filtrado) y la fila guarda la comparación entera con Contabilidad a la fecha de corte
/// —por grupo contable y conjunto de cuentas, con las cifras de SOLIDO de las bodegas no activas que comparten cuentas—, la
/// diferencia, si cuadró y, si no, quién la aceptó y por qué. La escribe sólo <c>ActivateWarehouseCommand</c>; sin cuadre ni
/// aceptación no hay fila (el intento queda en la auditoría). Antes de I2 no hay consulta de saldos: fuera de producción la
/// activación se ensaya aceptando la diferencia «sin comparación contable» y <see cref="ComparisonJson"/> va sin conjuntos.
/// </summary>
public class WarehouseActivation : AuditableEntity
{
    public const int LargoDelMotivo = 500;

    public int WarehouseId { get; set; }

    /// <summary>Copia de <c>INV_Warehouses.CutoffDate</c> al activar.</summary>
    public DateOnly CutoffDate { get; set; }

    /// <summary>La comparación entera tal como se calculó al activar (<c>ActivationPreviewDto</c> sin los bloqueos).</summary>
    public string ComparisonJson { get; set; } = "{}";

    public decimal TotalDifference { get; set; }

    /// <summary>Diferencia cero en todo conjunto.</summary>
    public bool IsBalanced { get; set; }

    /// <summary><c>SEC_Users.Id</c> de quien aceptó la diferencia (<c>Inventory.Warehouses.AcceptActivationDifference</c>).</summary>
    public int? DifferenceAcceptedByUserId { get; set; }

    public string? AcceptanceReason { get; set; }

    public DateTime ActivatedAt { get; set; }

    /// <summary><c>SEC_Users.Id</c> de quien activó.</summary>
    public int ActivatedByUserId { get; set; }
}
