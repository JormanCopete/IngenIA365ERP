using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Cash;

// ------------------------------------------------------------------------------------------------ entradas --

/// <summary>Una denominación contada (<c>denominations: [{ cashDenominationPublicId, quantity }]</c>, §21.1–§21.3). (nuevo)</summary>
public sealed record DenominationCountInput(Guid CashDenominationPublicId, int Quantity);

/// <summary>El lote de cierre de un datáfono (<c>terminalBatches</c>, §21.2). (nuevo)</summary>
public sealed record TerminalBatchInput(Guid CardTerminalPublicId, string BatchNumber, decimal Total, int Count);

/// <summary>La marca de una referencia cotejada (<c>referenceChecks</c>, §21.2). (nuevo)</summary>
public sealed record ReferenceCheckInput(Guid DocumentPaymentPublicId, bool Checked);

/// <summary>Lo contado de un medio al cerrar (<c>counts[]</c>, §21.2); qué admite depende de su <c>CashCountMethod</c>. (nuevo)</summary>
public sealed record CashCountInput(
    Guid PaymentMeansPublicId,
    decimal? CountedTotal = null,
    IReadOnlyList<DenominationCountInput>? Denominations = null,
    IReadOnlyList<TerminalBatchInput>? TerminalBatches = null,
    IReadOnlyList<ReferenceCheckInput>? ReferenceChecks = null,
    string? Reason = null);

/// <summary>El retiro de cierre (<c>closingWithdrawal: { destination: Safe | Deposit }</c>, §21.2). (nuevo)</summary>
public sealed record ClosingWithdrawalInput(CashMovementDestination Destination);

// ------------------------------------------------------------------------------------------------ sesiones --

/// <summary>El cajero de la sesión (§21.1): su usuario, su nombre y, si la tiene, su persona. (nuevo)</summary>
public sealed record CashSessionCashierDto(Guid? UserPublicId, string Name, Guid? PersonPublicId);

/// <summary>Un documento de la sesión con su estado (diferencia, ingreso de base, retiro de cierre). (nuevo)</summary>
public sealed record CashDocumentRefDto(Guid DocumentPublicId, string? Number, DocumentStatus Status, Guid? ApprovalRequestPublicId = null);

/// <summary><c>CashSessionDto</c> (contracts/api.md §21.1). (nuevo)</summary>
public sealed record CashSessionDto(
    Guid CashSessionPublicId,
    CashSessionPointOfSaleDto PointOfSale,
    ReferenciaDto CashRegister,
    CashSessionCashierDto Cashier,
    DateOnly OperatingDate,
    DateTime OpenedAt,
    decimal OpeningBase,
    string BaseMode,
    CashSessionStatus Status,
    DateTime? ClosedAt,
    string? ClosedByName,
    int SalesCount,
    decimal SalesTotal,
    CashDocumentRefDto? DifferenceDocument);

/// <summary>El resultado de abrir (§21.1): la sesión, el ingreso de base si lo hubo y los avisos. (nuevo)</summary>
public sealed record OpenCashSessionResultDto(CashSessionDto Session, CashDocumentRefDto? BaseIncomeDocument, IReadOnlyList<AvisoDto> Warnings);

/// <summary>Una línea del arqueo tal como quedó (<c>count</c> del detalle y <c>lines</c> del cierre, §21.2). (nuevo)</summary>
public sealed record CashCountLineDto(
    string PaymentMeansCode,
    decimal Expected,
    decimal Counted,
    decimal Difference,
    decimal Tolerance,
    bool WithinTolerance,
    CashDifferenceTreatment? Treatment);

/// <summary>El detalle de una sesión (<c>GET /cash-sessions/{id}</c>, §21.1). (nuevo)</summary>
public sealed record CashSessionDetailDto(
    CashSessionDto Session,
    IReadOnlyList<CashMovementDto> Movements,
    int SuspendedDrafts,
    int OpenDrafts,
    int PendingDeliveries,
    IReadOnlyList<CashCountLineDto>? Count);

// ------------------------------------------------------------------------------------------------ esperado --

/// <summary>Un medio en el esperado (§21.2 <c>lines[].paymentMeans</c>). (nuevo)</summary>
public sealed record CashExpectedMeansDto(Guid PublicId, string Code, string Name, PaymentMeansClass Class, CashCountMethod CountMethod);

/// <summary>El esperado de un datáfono (§21.2 <c>terminals[]</c>). (nuevo)</summary>
public sealed record CashExpectedTerminalDto(Guid CardTerminalPublicId, string Code, string? AcquirerCode, decimal? Expected, int PaymentsCount);

/// <summary>Un pago para cotejar por referencia (§21.2 <c>references[]</c>). (nuevo)</summary>
public sealed record CashExpectedReferenceDto(Guid DocumentPaymentPublicId, string? DocumentNumber, string? Reference, decimal Amount);

/// <summary>
/// Una línea de <c>CashSessionExpectedDto</c> (§21.2). En arqueo ciego sin <c>ViewAll</c> las cifras salen nulas: el cajero cuenta
/// sin verlas; la tolerancia y las referencias que tiene que marcar sí se ven. (nuevo)
/// </summary>
public sealed record CashExpectedLineDto(
    CashExpectedMeansDto PaymentMeans,
    decimal? OpeningBase,
    decimal? Sales,
    decimal? Refunds,
    decimal? MovementsIn,
    decimal? MovementsOut,
    decimal? ReclassificationsIn,
    decimal? ReclassificationsOut,
    decimal? Expected,
    decimal Tolerance,
    int PaymentsCount,
    IReadOnlyList<CashExpectedTerminalDto>? Terminals,
    IReadOnlyList<CashExpectedReferenceDto>? References);

/// <summary><c>CashSessionExpectedDto</c> (§21.2). (nuevo)</summary>
public sealed record CashSessionExpectedDto(bool Blind, IReadOnlyList<CashExpectedLineDto> Lines, decimal? Total);

// ------------------------------------------------------------------------------------------------ cierre --

/// <summary>La orden del lote del cierre del turno (§21.2 <c>batch</c>). (nuevo)</summary>
public sealed record CashSessionBatchDto(Guid BatchPublicId, long Number, string Trigger);

/// <summary>La respuesta del cierre y del reconteo (§21.2). (nuevo)</summary>
public sealed record CloseCashSessionResultDto(
    Guid CashSessionPublicId,
    CashSessionStatus Status,
    IReadOnlyList<CashCountLineDto> Lines,
    CashDocumentRefDto? DifferenceDocument,
    CashDocumentRefDto? ClosingWithdrawalDocument,
    CashSessionBatchDto? Batch);

// ------------------------------------------------------------------------------------------------ movimientos --

/// <summary><c>CashMovementDto</c> (contracts/api.md §21.3). (nuevo)</summary>
public sealed record CashMovementDto(
    Guid DocumentPublicId,
    string? Number,
    DocumentStatus Status,
    Guid CashSessionPublicId,
    CashMovementKind Kind,
    string SourcePaymentMeansCode,
    string? TargetPaymentMeansCode,
    CashMovementDestination? Destination,
    string? DestinationCashRegisterCode,
    decimal Amount,
    string Reason,
    string? CreatedByName,
    string? ApprovedByName);

// ------------------------------------------------------------------------------------------------ cierre del día --

/// <summary><c>DayCloseDto</c> (§21.4). (nuevo)</summary>
public sealed record DayCloseDto(
    Guid DayClosePublicId,
    ReferenciaDto PointOfSale,
    DateOnly OperatingDate,
    short Version,
    DayCloseStatus Status,
    DateTime ClosedAt,
    string? ClosedByName,
    DateTime? ReopenedAt,
    string? ReopenReason,
    decimal Total);

/// <summary>Una línea por medio del cierre del día (§21.4 <c>lines</c>). (nuevo)</summary>
public sealed record DayCloseLineDto(string PaymentMeansCode, decimal Expected, decimal Counted, decimal Difference, int PaymentsCount);

/// <summary>El detalle de tarjetas por adquirente y datáfono (§21.4 <c>cards</c>). (nuevo)</summary>
public sealed record DayCloseCardDto(string PaymentMeansCode, string? AcquirerCode, string? CardTerminalCode, IReadOnlyList<string> BatchNumbers,
    decimal Expected, decimal Counted, int Count);

/// <summary>Una sesión consolidada en el cierre del día (§21.4 <c>sessions</c>). (nuevo)</summary>
public sealed record DayCloseSessionDto(Guid CashSessionPublicId, string CashRegisterCode, string CashierName, DateTime OpenedAt, DateTime? ClosedAt);

/// <summary>El detalle del cierre del día (<c>GET /day-closes/{id}</c>, §21.4). (nuevo)</summary>
public sealed record DayCloseDetailDto(
    DayCloseDto DayClose,
    IReadOnlyList<DayCloseLineDto> Lines,
    IReadOnlyList<DayCloseCardDto> Cards,
    IReadOnlyList<DayCloseSessionDto> Sessions);
