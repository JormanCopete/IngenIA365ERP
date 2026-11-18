using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;

namespace IngenIA365ERP.Domain.Entities.Inventory.Periods;

/// <summary>
/// El costo que un documento dejó para el cierre del mes (<c>INV_DeferredCosts</c>; 2026-11-18, pedido del dueño): con
/// <c>Contabilidad.CostoDeVenta = AlCierreDelPeriodo</c> la venta pasa en línea sin su costo, y el contenido de costo que habría
/// emitido —<c>CostoDeVentaReconocido</c>, la <c>DevolucionRegistrada</c> de cliente, el <c>AjusteDeCostoReconocido</c> de un documento
/// diferido, o su espejo en una anulación— queda aquí tal cual, en JSON. El cierre del mes los suma en un solo
/// <c>CostoDelPeriodoReconocido</c> y apunta <see cref="PeriodCostPostingId"/> al envío; la reapertura lo vuelve a dejar nulo.
/// </summary>
[SinDiffDeAuditoria]
public class DeferredCost : AuditableEntity
{
    /// <summary>El documento cuya confirmación lo dejó (la venta, la compra que la ajustó, la anulación).</summary>
    public int SourceDocumentId { get; init; }

    public InventoryDocument? SourceDocument { get; init; }

    /// <summary>El documento diferido cuyo costo es (la venta misma, la afectada por el ajuste, la anulada).</summary>
    public int CostDocumentId { get; init; }

    public InventoryDocument? CostDocument { get; init; }

    /// <summary>Año y mes de inventario al que pertenece: los de la fecha de operación del documento que lo dejó.</summary>
    public short Year { get; init; }

    public byte Month { get; init; }

    public DateOnly OperationDate { get; init; }

    /// <summary>Tipo y versión del contenido del catálogo de mensajes.</summary>
    public string ContentType { get; init; } = string.Empty;

    public int ContentVersion { get; init; } = 1;

    public string ContentJson { get; init; } = string.Empty;

    /// <summary>Sucursal y centro de costo del sobre que habría llevado el contenido.</summary>
    public Guid BranchPublicId { get; init; }

    public Guid? CostCenterPublicId { get; init; }

    /// <summary>El envío del cierre que lo incluyó; nulo mientras espera el cierre (o tras una reapertura).</summary>
    public int? PeriodCostPostingId { get; set; }

    public PeriodCostPosting? PeriodCostPosting { get; set; }
}

/// <summary>
/// Un envío del costo del mes a Contabilidad (<c>INV_PeriodCostPostings</c>): lo crea el cierre del período con los costos diferidos
/// del mes, y la reapertura con su reversión. Es el origen del mensaje <c>CostoDelPeriodoReconocido</c>.
/// </summary>
[SinDiffDeAuditoria]
public class PeriodCostPosting : AuditableEntity
{
    public int PeriodId { get; init; }

    public InventoryPeriod? Period { get; init; }

    /// <summary>La versión de cierre que lo creó (en la reversión, la del cierre que reversa).</summary>
    public int CloseVersion { get; init; }

    public bool IsReversal { get; init; }

    /// <summary>En la reversión: el envío que reversa.</summary>
    public int? ReversesId { get; init; }

    public DateOnly OperationDate { get; init; }

    public int EntryCount { get; init; }

    public Guid? MessagePublicId { get; set; }
}
