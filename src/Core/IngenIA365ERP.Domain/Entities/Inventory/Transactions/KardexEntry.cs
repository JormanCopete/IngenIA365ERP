using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Transactions;

/// <summary>
/// Un hecho del kardex (<c>INV_KardexEntries</c>; feature 012, T249; FR-001 a FR-004, FR-042 a FR-046; data-model §3.1). Todo
/// movimiento nace de una línea de un documento confirmado y queda aquí para siempre: es un <b>hecho</b>
/// (<see cref="IHechoInmutable"/>, sólo inserción, lo hace cumplir <c>ApplicationDbContext.SaveChangesAsync</c>) con un solo
/// escritor, <c>RegistroDeKardex</c> (<c>NadieEscribeElKardexFueraDelRegistro</c>). Anular, retroactivos, diferencias de
/// precio, negativos regularizados y residuos <b>agregan</b> líneas; nada se corrige en su sitio (FR-002).
///
/// <para>
/// <c>bigint</c> (<see cref="AuditableEntityLong"/>): ~9 millones de filas al año a la escala de referencia. El orden del
/// kardex es <c>(OperationDate, Id)</c>; nunca <see cref="RegisteredAt"/> ni <c>PublicId</c>. Invariantes:
/// <c>QuantityBase = 0 ⇔ Kind = CostAdjustment</c>; el signo concuerda con <see cref="Kind"/>; en <c>Entry</c>/<c>Exit</c>,
/// <c>TotalCost = round(QuantityBase × UnitCost)</c>. El saldo acumulado no se guarda. Sin diferencias de auditoría: el
/// documento es la referencia de lo que el kardex registra (T36).
/// </para>
/// </summary>
[SinDiffDeAuditoria]
public class KardexEntry : AuditableEntityLong, IHechoInmutable
{
    public int DocumentId { get; init; }

    /// <summary>Una línea produce una o varias entradas (el despacho: salida del origen y entrada al tránsito).</summary>
    public int DocumentLineId { get; init; }

    public int ProductId { get; init; }

    public int WarehouseId { get; init; }

    public int LocationId { get; init; }

    /// <summary>Sin FK hasta I6 (data-model §3.0); siempre nulo en I1.</summary>
    public int? LotId { get; init; }

    /// <summary>Sin FK hasta I6 (data-model §3.0); siempre nulo en I1.</summary>
    public int? SerialId { get; init; }

    /// <summary>La del documento; en un ajuste por retroactivo, la de la salida afectada.</summary>
    public DateOnly OperationDate { get; init; }

    /// <summary>Instante UTC real de la escritura. No ordena el kardex.</summary>
    public DateTime RegisteredAt { get; init; }

    public KardexEntryKind Kind { get; init; }

    public KardexReason Reason { get; init; }

    /// <summary>Con signo: + entrada, − salida, 0 en <c>CostAdjustment</c>.</summary>
    public decimal QuantityBase { get; init; }

    /// <summary>≥ 0: el costo con que se registró.</summary>
    public decimal UnitCost { get; init; }

    /// <summary>Con signo; en <c>CostAdjustment</c> es la diferencia.</summary>
    public decimal TotalCost { get; init; }

    /// <summary>Ámbito de costo: 0 = cooperativa, o el Id de la bodega. Sin FK (0 es centinela).</summary>
    public int CostScopeWarehouseId { get; init; }

    /// <summary>Sellado al escribir.</summary>
    public CostMethod CostMethod { get; init; }

    /// <summary>En la línea de un <c>Voiding</c>: la entrada que revierte, a su mismo costo.</summary>
    public long? ReversesEntryId { get; init; }

    public KardexEntry? ReversesEntry { get; init; }

    /// <summary>En <c>CostAdjustment</c>: la entrada cuyo costo corrige.</summary>
    public long? AffectsEntryId { get; init; }

    public KardexEntry? AffectsEntry { get; init; }
}
