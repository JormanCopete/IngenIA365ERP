using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.Settlements.Settlement;

/// <summary>
/// Un descuento de la definitiva (FR-018a; contracts/api.md §3.4 <c>GET /{runId}/deductions</c>):
/// la obligación, su desglose leído de Cartera al proponer, lo propuesto, lo aplicado, el motivo si
/// se bajó y el saldo que quedará (o quedó) en Cartera. <c>Kind</c> es <see cref="SettlementDeductionKind"/>
/// (<c>CooperativeLoan</c> = 1, <c>ThirdPartyLibranza</c> = 2, <c>Other</c> = 3).
/// </summary>
public sealed record SettlementDeductionItemDto(
    Guid ObligationPublicId,
    SettlementDeductionKind Kind,
    string Description,
    decimal? CapitalBalance,
    decimal? InterestBalance,
    decimal? DefaultBalance,
    int? PendingInstallments,
    int? CausedNotDeducted,
    decimal Proposed,
    decimal Applied,
    string? Reason,
    string? AdjustedBy,
    DateTime? AdjustedAt,
    decimal? RemainingAfter,
    SettlementDeductionStatus Status,
    Guid? CarteraTransactionPublicId)
{
    /// <summary>Feature 013 (T0168): el número visible de la obligación de Cartera (nulo en libranzas y descuentos heredados).</summary>
    public string? ObligationNumber { get; init; }

    /// <summary>Feature 013 (R105): el comprobante de reverso de Cartera, cuando la definitiva se reversó.</summary>
    public Guid? CarteraReversalPublicId { get; init; }

    /// <summary>Feature 013 (R105): verdadero si <c>RemainingAfter</c> es el saldo exacto que dio Cartera (aplicado o simulado al ajustar).</summary>
    public bool RemainingIsExact { get; init; }
}

/// <summary>La propuesta de descuentos con el neto antes y después.</summary>
public sealed record SettlementDeductionsDto(
    Guid RunPublicId,
    decimal Net,
    IReadOnlyList<SettlementDeductionItemDto> Items,
    decimal TotalProposed,
    decimal TotalApplied,
    decimal NetAfterDeductions,
    bool DeductionOverNet);

/// <summary>
/// Un recaudo que la aprobación dejó en Cartera por una obligación (contracts/api.md §3.4 <c>approve</c>/<c>reverse</c>). Feature
/// 013: <c>ObligationPublicId</c> es, como desde la 010, el <c>PublicId</c> del descuento (<c>PAY_SettlementDeductions</c>);
/// <c>PaymentPublicId</c> el comprobante de aplicación de Cartera y <c>ReversalPublicId</c> el de reverso, al reversar.
/// </summary>
public sealed record PortfolioPaymentDto(Guid ObligationPublicId, Guid? PaymentPublicId, decimal Applied, decimal? Remaining, string Description, Guid? ReversalPublicId = null);

/// <summary>Resultado de aprobar la definitiva: el comprobante, el neto y los recaudos aplicados en Cartera.</summary>
public sealed record SettlementApprovedWithPortfolioDto(
    Guid RunPublicId,
    Guid TerminationPublicId,
    Guid DocumentPublicId,
    string Number,
    decimal Net,
    DateOnly PostingDate,
    bool ApprovedWithoutSegregation,
    IReadOnlyList<PortfolioPaymentDto> PortfolioPayments,
    Guid? SettlementDocumentAttachmentPublicId);

/// <summary>
/// Resultado de reversar la definitiva: el espejo, la ficha reabierta y los recaudos de Cartera, que desde la feature 013
/// (R105) se reversan en la misma transacción por el contrato de obligaciones (cada uno con su comprobante de reverso).
/// </summary>
public sealed record SettlementReversedWithPortfolioDto(
    Guid RunPublicId,
    Guid TerminationPublicId,
    Guid ReversalDocumentPublicId,
    string ReversalNumber,
    IReadOnlyList<PortfolioPaymentDto> PortfolioPayments,
    string Message);
