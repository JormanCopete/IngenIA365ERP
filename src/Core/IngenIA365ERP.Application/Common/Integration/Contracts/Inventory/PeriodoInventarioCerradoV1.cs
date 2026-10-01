using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// <c>PeriodoInventarioCerrado</c> v1 (contracts/mensajes.md §7.2): informativo. Lo emite
/// <c>CloseInventoryPeriodCommand</c> con origen <c>Operation</c> y <c>originEventKey</c>
/// <c>Close:{closingVersion}</c>, porque un mes puede cerrarse, reabrirse y volver a cerrarse.
/// </summary>
public sealed record PeriodoInventarioCerradoV1
{
    public const string Type = "PeriodoInventarioCerrado";

    public int Year { get; init; }

    public int Month { get; init; }

    /// <summary>Versión del valorizado fijado (<c>INV_PeriodClosingBalances</c>).</summary>
    public int ClosingVersion { get; init; }

    public IReadOnlyList<PeriodValuationLineV1> Valuation { get; init; } = [];

    /// <summary>Remisiones sin facturar aceptadas (Supuesto 12); vacía si no hay.</summary>
    public IReadOnlyList<UnbilledShipmentV1> UnbilledShipments { get; init; } = [];

    /// <summary>Mensajes con fecha en el mes que quedaban sin procesar al cerrar (FR-047 avisa, no bloquea).</summary>
    public AcknowledgedPendingV1 AcknowledgedPending { get; init; } = new();
}

/// <summary>Valorizado fijado al cierre por grupo y bodega (§7.2).</summary>
public sealed record PeriodValuationLineV1
{
    public string AccountingGroupCode { get; init; } = string.Empty;

    public string WarehouseCode { get; init; } = string.Empty;

    public WarehouseBehavior WarehouseBehavior { get; init; }

    public Guid BranchPublicId { get; init; }

    public decimal QuantityBase { get; init; }

    public decimal Value { get; init; }
}

/// <summary>Una remisión sin facturar aceptada al cierre (§7.2), para causar el ingreso por facturar.</summary>
public sealed record UnbilledShipmentV1
{
    public DocumentRefV1 Document { get; init; } = new();

    public Guid? PersonPublicId { get; init; }

    public decimal AmountToInvoice { get; init; }
}

/// <summary>Conteo de lo que quedaba sin procesar al cerrar (§7.2).</summary>
public sealed record AcknowledgedPendingV1
{
    public int Pending { get; init; }

    public int InBatch { get; init; }

    public int Rejected { get; init; }
}
