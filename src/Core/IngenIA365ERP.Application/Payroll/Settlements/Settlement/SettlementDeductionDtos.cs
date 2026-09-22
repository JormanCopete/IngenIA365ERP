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
    Guid? CarteraTransactionPublicId);

/// <summary>La propuesta de descuentos con el neto antes y después.</summary>
public sealed record SettlementDeductionsDto(
    Guid RunPublicId,
    decimal Net,
    IReadOnlyList<SettlementDeductionItemDto> Items,
    decimal TotalProposed,
    decimal TotalApplied,
    decimal NetAfterDeductions,
    bool DeductionOverNet);

/// <summary>Un recaudo que la aprobación dejó en Cartera por una obligación (contracts/api.md §3.4 <c>approve</c>/<c>reverse</c>).</summary>
public sealed record PortfolioPaymentDto(Guid ObligationPublicId, Guid? PaymentPublicId, decimal Applied, decimal? Remaining, string Description);

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
/// Resultado de reversar la definitiva: el espejo, la ficha reabierta y los recaudos de Cartera que
/// <b>no</b> se reversan solos —Cartera los reversa por su propio flujo— para que quien reversa sepa
/// cuáles son.
/// </summary>
public sealed record SettlementReversedWithPortfolioDto(
    Guid RunPublicId,
    Guid TerminationPublicId,
    Guid ReversalDocumentPublicId,
    string ReversalNumber,
    IReadOnlyList<PortfolioPaymentDto> PortfolioPayments,
    string Message);
