using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Transactions;

namespace IngenIA365ERP.Domain.Entities.Inventory.Projections;

/// <summary>
/// Una capa PEPS (<c>INV_CostLayers</c>; feature 012, I5, T827; FR-043; data-model §3.5): <b>proyección</b> del kardex,
/// reconstruible (FR-002), que nace con la entrada que la creó —en el cambio de método, con la línea <c>MethodChange</c> que
/// lleva la existencia al promedio— y se va consumiendo. <c>UK (EntryKardexEntryId)</c>; el orden PEPS es
/// <c>(ProductId, ScopeWarehouseId, OperationDate, EntryKardexEntryId)</c>. Invariante: <c>0 ≤ RemainingQuantity =
/// OriginalQuantity − Σ</c> de sus consumos. La escriben sólo <c>RegistroDeKardex</c> y <c>RebuildInventoryProjectionsCommand</c>,
/// y la protege la fila exclusiva de <c>INV_CostStates</c> del ámbito. <c>bigint</c> como el kardex: una capa por entrada. Sin
/// diferencias de auditoría. (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class CostLayer : AuditableEntityLong
{
    public int ProductId { get; init; }

    /// <summary>0 = cooperativa; o el Id de la bodega. Sin FK (0 es centinela), como en <c>INV_CostStates</c>.</summary>
    public int ScopeWarehouseId { get; init; }

    /// <summary>La línea del kardex que creó la capa (la entrada; en el cambio de método, la línea <c>MethodChange</c>).</summary>
    public long EntryKardexEntryId { get; init; }

    /// <summary>
    /// La línea que la creó, cuando todavía no tiene Id (se escribe en la misma unidad de trabajo que la capa, T836): el registro la
    /// nombra por navegación y EF pone <see cref="EntryKardexEntryId"/> al guardar. No agrega columnas.
    /// </summary>
    public KardexEntry? EntryKardexEntry { get; init; }

    public DateOnly OperationDate { get; init; }

    public decimal OriginalQuantity { get; init; }

    /// <summary>Lo que queda: sólo lo mueven <see cref="Consumir"/> y la reconstrucción (<see cref="Reconstruir"/>).</summary>
    public decimal RemainingQuantity { get; private set; }

    /// <summary>
    /// El costo unitario de la capa. Nace con el de su entrada y sólo lo cambia <see cref="Revaluar"/>: la diferencia de precio o los
    /// costos adicionales que suman a lo que queda de ella con PEPS (T843, D5).
    /// </summary>
    public decimal UnitCost { get; private set; }

    /// <summary>La capa que propone <c>MotorDeCosteo</c>, ya con el Id de la línea del kardex que la creó.</summary>
    public static CostLayer Desde(
        int productId, int scopeWarehouseId, long entryKardexEntryId, DateOnly operationDate,
        decimal originalQuantity, decimal remainingQuantity, decimal unitCost)
    {
        if (originalQuantity <= 0m) throw new ArgumentOutOfRangeException(nameof(originalQuantity), "Una capa nace con cantidad positiva.");
        var capa = new CostLayer
        {
            ProductId = productId,
            ScopeWarehouseId = scopeWarehouseId,
            EntryKardexEntryId = entryKardexEntryId,
            OperationDate = operationDate,
            OriginalQuantity = originalQuantity,
            UnitCost = unitCost,
        };
        capa.Reconstruir(remainingQuantity);
        return capa;
    }

    /// <summary>La capa de una entrada escrita en esta misma unidad de trabajo (sin Id todavía): se enlaza por navegación (T836).</summary>
    public static CostLayer Desde(
        int productId, int scopeWarehouseId, KardexEntry entrada, DateOnly operationDate,
        decimal originalQuantity, decimal remainingQuantity, decimal unitCost)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (originalQuantity <= 0m) throw new ArgumentOutOfRangeException(nameof(originalQuantity), "Una capa nace con cantidad positiva.");
        var capa = new CostLayer
        {
            ProductId = productId,
            ScopeWarehouseId = scopeWarehouseId,
            EntryKardexEntryId = entrada.Id,
            EntryKardexEntry = entrada.Id == 0 ? entrada : null,
            OperationDate = operationDate,
            OriginalQuantity = originalQuantity,
            UnitCost = unitCost,
        };
        capa.Reconstruir(remainingQuantity);
        return capa;
    }

    /// <summary>El costo unitario nuevo de lo que queda (T843): lo calcula <c>Peps.AjusteSobreEntrada</c>. Nunca negativo.</summary>
    public void Revaluar(decimal costoUnitario)
    {
        if (costoUnitario < 0m) throw new ArgumentOutOfRangeException(nameof(costoUnitario), "El costo de una capa no es negativo.");
        UnitCost = costoUnitario;
    }

    /// <summary>Un consumo de la capa; con cantidad negativa, la anulación que la devuelve. Nunca negativa ni más que la original.</summary>
    public void Consumir(decimal cantidad) => Reconstruir(RemainingQuantity - cantidad);

    /// <summary>Fija lo que queda (la reconstrucción desde el kardex y los consumos).</summary>
    public void Reconstruir(decimal restante)
    {
        if (restante < 0m || restante > OriginalQuantity)
            throw new InvalidOperationException($"El restante de una capa va de 0 a {OriginalQuantity}; quedaría en {restante}.");
        RemainingQuantity = restante;
    }
}
