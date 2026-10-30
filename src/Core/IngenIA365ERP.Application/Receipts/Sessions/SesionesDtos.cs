using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Application.Receipts.Sessions;

// Feature 015, US2 (T0092–T0095; contracts/api.md §3): lo que cruzan las rutas de la sesión de caja, el arqueo, la bóveda y el cierre del día. Toda
// referencia va por PublicId (Principio VI). (nuevo)

/// <summary>Billetes o monedas de una denominación del catálogo de la 012.</summary>
public sealed record DenominacionContadaDto(Guid DenominationPublicId, int Quantity);

/// <summary>Un medio sellado en la sesión.</summary>
public sealed record MedioDeSesionDto(Guid PaymentMeansPublicId, string Code, string Name, PaymentMeansClass Class, CashCountMethod CountMethod, decimal Tolerance);

/// <summary>La sesión de caja.</summary>
public sealed record CashSessionDto(
    Guid PublicId,
    Guid CashBoxPublicId,
    string CashBoxCode,
    Guid CollectionPointPublicId,
    string CollectionPointCode,
    Guid CashierUserPublicId,
    string CashierName,
    DateOnly BusinessDate,
    CashSessionState Status,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningBase,
    bool IsForgotten,
    bool BlindCount,
    bool DoubleCustody,
    decimal? CashWarnCap,
    decimal? CashBlockCap,
    IReadOnlyList<MedioDeSesionDto> Means);

/// <summary>Lo esperado de un medio: base, recibos, anulaciones, entradas y salidas.</summary>
public sealed record EsperadoDeMedioDto(Guid PaymentMeansPublicId, string Code, decimal Base, decimal Receipts, decimal Voided, decimal In, decimal Out, decimal Expected);

/// <summary>Lo esperado de la sesión; con arqueo ciego y sin lo contado registrado, <see cref="Hidden"/> y sin valores.</summary>
public sealed record SessionExpectedDto(Guid SessionPublicId, bool Hidden, IReadOnlyList<EsperadoDeMedioDto> Means, decimal? Total, bool CashWarnCapReached);

/// <summary>Un movimiento de caja.</summary>
public sealed record CashMovementDto(
    Guid PublicId,
    string Number,
    CashMovementKindR Kind,
    decimal Amount,
    Guid? FromMeansPublicId,
    Guid? ToMeansPublicId,
    Guid? CounterpartSessionPublicId,
    Guid? VaultPublicId,
    string Reason,
    DateTime MovedAt);

/// <summary>Una línea del arqueo por medio.</summary>
public sealed record CashCountLineDto(
    Guid PublicId,
    Guid PaymentMeansPublicId,
    string MeansCode,
    CashCountMethod Method,
    decimal Expected,
    decimal Counted,
    decimal Difference,
    decimal Tolerance,
    bool WithinTolerance,
    CountTreatment Treatment,
    string? Reason,
    bool PendingApproval,
    DateTime? ApprovedAt);

/// <summary>El arqueo de una sesión o de una bóveda, con el estado en que dejó la sesión.</summary>
public sealed record CashCountDto(
    Guid PublicId,
    CashCountKind Kind,
    DateTime CountedAt,
    decimal TotalExpected,
    decimal TotalCounted,
    decimal TotalDifference,
    decimal AmountToApprove,
    CashSessionState? SessionStatus,
    IReadOnlyList<CashCountLineDto> Lines);

/// <summary>Lo contado de un medio: total, denominaciones, lotes de datáfono o referencias, y el motivo de la diferencia.</summary>
public sealed record ConteoDeMedioDto(
    Guid PaymentMeansPublicId,
    decimal? Total = null,
    IReadOnlyList<DenominacionContadaDto>? Denominations = null,
    IReadOnlyList<LoteContadoDto>? TerminalBatches = null,
    IReadOnlyList<ReferenciaContadaDto>? References = null,
    string? Reason = null);

public sealed record LoteContadoDto(Guid CardTerminalPublicId, string BatchNumber, decimal Amount);

public sealed record ReferenciaContadaDto(string Reference, decimal Amount, bool Confirmed);

/// <summary>El ajuste de una sesión cerrada.</summary>
public sealed record CashSessionAdjustmentDto(Guid PublicId, string Number, SessionAdjustmentKind Kind, Guid PaymentMeansPublicId, decimal Amount, string Reason,
    DateTime AdjustedAt);

/// <summary>El cierre del día de un punto.</summary>
public sealed record DayCloseDto(
    Guid PublicId,
    Guid CollectionPointPublicId,
    DateOnly BusinessDate,
    int SessionCount,
    int ReceiptCount,
    int VoidingCount,
    decimal Expected,
    decimal Counted,
    decimal Difference,
    DateTime ClosedAt,
    string VerificationCode,
    bool ByDelegate,
    IReadOnlyList<DayCloseLineDto> Lines);

public sealed record DayCloseLineDto(Guid PaymentMeansPublicId, string MeansCode, decimal Expected, decimal Counted, decimal Difference);

/// <summary>Un movimiento de bóveda.</summary>
public sealed record VaultMovementDto(Guid PublicId, string Number, VaultMovementKind Kind, decimal Amount, string? BagSeal, string? Reason, DateTime MovedAt);

/// <summary>El saldo de la bóveda por denominación.</summary>
public sealed record VaultBalanceLineDto(Guid DenominationPublicId, decimal Value, int Quantity, decimal Amount);

/// <summary>La bóveda con su saldo y sus últimos movimientos.</summary>
public sealed record VaultDetailDto(Guid PublicId, Guid BranchPublicId, string BranchName, VaultMode Mode, ReceiptsCatalogStatus Status, decimal Balance,
    IReadOnlyList<VaultBalanceLineDto> Denominations, IReadOnlyList<VaultMovementDto> Movements);

/// <summary>El cuadre diario de una oficina.</summary>
public sealed record DailySquareDto(
    Guid PublicId,
    Guid BranchPublicId,
    string BranchName,
    DateOnly BusinessDate,
    decimal CashInBoxes,
    decimal CashInVault,
    decimal CashInTransit,
    decimal? LedgerBalance,
    decimal EmittedPendingBatch,
    decimal Difference,
    DailySquareStatus Status,
    bool VaultBalanceMatches,
    string? Explanation);

/// <summary>Un faltante a cargo del cajero.</summary>
public sealed record CashierReceivableDto(Guid PublicId, Guid CashierPersonPublicId, decimal Amount, decimal Balance, bool HasPayrollAuthorization,
    CashierReceivableState Status);

/// <summary>Un sobrante.</summary>
public sealed record CashSurplusDto(Guid PublicId, decimal Amount, DateOnly? ToIncomeFrom, CashSurplusState Status);
