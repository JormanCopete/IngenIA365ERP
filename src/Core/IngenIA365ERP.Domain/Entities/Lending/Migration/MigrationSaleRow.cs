using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationSales</c> (feature 013, K1; data-model §29.3, parte <c>sales</c>; FR-143): una venta a crédito cancelada antes del
/// corte (saldo al corte en cero). Hecho sin diff de auditoría. Al activar nace la obligación en cero de la venta.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationSaleRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public int PersonId { get; init; }

    public DateOnly SaleDate { get; init; }

    public decimal OriginalAmount { get; init; }

    public decimal ExternalCollections { get; init; }

    public decimal AdjustmentsAmount { get; init; }

    /// <summary>Siempre 0: la parte sólo admite ventas canceladas antes del corte.</summary>
    public decimal BalanceAtCutoff { get; init; }

    /// <summary>Línea de la obligación en cero; nula = la de la carga o la de origen comercial por defecto.</summary>
    public string? CreditLineCode { get; init; }

    public string CancellationReason { get; init; } = string.Empty;
}
