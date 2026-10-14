using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationSales</c> (feature 013, K1; data-model §29.3, parte <c>sales</c>; plantillas.md §5.3.9; FR-143): una venta a crédito de
/// Comercial hasta el corte, por factura, con su resultado: <c>Migrated</c> (saldo mayor que cero y un crédito de <c>loans</c> con esa factura)
/// o <c>CancelledBeforeCutoff</c> (saldo cero). Hecho sin diff de auditoría. Al activar, la cancelada nace como obligación en cero.
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

    /// <summary>= <see cref="OriginalAmount"/> − <see cref="ExternalCollections"/> + <see cref="AdjustmentsAmount"/>; ≥ 0.</summary>
    public decimal BalanceAtCutoff { get; init; }

    /// <summary><c>Migrated</c> o <c>CancelledBeforeCutoff</c> (<c>resultado</c>).</summary>
    public SaleReconciliationOutcome Outcome { get; init; }

    /// <summary>El crédito de <c>loans</c> que la conserva, con <c>Migrated</c> (<c>numeroDeCredito</c>).</summary>
    public string? SourceLoanNumber { get; init; }

    /// <summary>Línea de la obligación en cero; nula = la de la carga o la de origen comercial por defecto.</summary>
    public string? CreditLineCode { get; init; }

    /// <summary>Referencia de los cobros recibidos por fuera, sumados por factura (<c>referenciaDeLosCobros</c>).</summary>
    public string? CollectionsReference { get; init; }
}
