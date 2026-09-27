using IngenIA365ERP.Shared.Services.Inventario;

namespace IngenIA365ERP.Shared.Services.Ventas;

// Espejos de la caja (feature 012, I3, T633; contracts/api.md §21): sesiones, esperado, arqueo por medio, movimientos y cierre del día.
// Enums como número al leer y por nombre al mandar (TextosDeVentas). (nuevos)

// --------------------------------------------------------------------------------------------- sesiones --

public sealed record PuntoDeLaSesionDto(Guid PointOfSalePublicId, string Code, string Name, bool PosEnabled);

public sealed record CajeroDto(Guid? UserPublicId, string Name, Guid? PersonPublicId);

public sealed record DocumentoDeCajaDto(Guid DocumentPublicId, string? Number, int Status, Guid? ApprovalRequestPublicId = null);

public sealed record SesionDeCajaDto(
    Guid CashSessionPublicId,
    PuntoDeLaSesionDto PointOfSale,
    ReferenciaDeInventarioDto CashRegister,
    CajeroDto Cashier,
    DateOnly OperatingDate,
    DateTime OpenedAt,
    decimal OpeningBase,
    string BaseMode,
    int Status,
    DateTime? ClosedAt,
    string? ClosedByName,
    int SalesCount,
    decimal SalesTotal,
    DocumentoDeCajaDto? DifferenceDocument);

public sealed record SesionAbiertaDto(SesionDeCajaDto Session, DocumentoDeCajaDto? BaseIncomeDocument, IReadOnlyList<AvisoDeInventarioDto> Warnings);

public sealed record LineaDeArqueoDto(string PaymentMeansCode, decimal Expected, decimal Counted, decimal Difference, decimal Tolerance, bool WithinTolerance,
    int? Treatment);

public sealed record DetalleDeSesionDto(
    SesionDeCajaDto Session,
    IReadOnlyList<MovimientoDeCajaDto> Movements,
    int SuspendedDrafts,
    int OpenDrafts,
    int PendingDeliveries,
    IReadOnlyList<LineaDeArqueoDto>? Count);

// ---------------------------------------------------------------------------------------------- esperado --

public sealed record MedioEsperadoDto(Guid PublicId, string Code, string Name, int Class, int CountMethod);

public sealed record DatafonoEsperadoDto(Guid CardTerminalPublicId, string Code, string? AcquirerCode, decimal? Expected, int PaymentsCount);

public sealed record ReferenciaEsperadaDto(Guid DocumentPaymentPublicId, string? DocumentNumber, string? Reference, decimal Amount);

public sealed record LineaEsperadaDto(
    MedioEsperadoDto PaymentMeans,
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
    IReadOnlyList<DatafonoEsperadoDto>? Terminals,
    IReadOnlyList<ReferenciaEsperadaDto>? References);

/// <summary>Con <see cref="Blind"/> (arqueo ciego) los esperados vienen nulos: la persona cuenta sin ver lo que debería haber.</summary>
public sealed record EsperadoDeSesionDto(bool Blind, IReadOnlyList<LineaEsperadaDto> Lines, decimal? Total);

// ------------------------------------------------------------------------------------------------ cierre --

public sealed record LoteDeSesionDto(Guid BatchPublicId, long Number, string Trigger);

public sealed record ResultadoDeCierreDto(
    Guid CashSessionPublicId,
    int Status,
    IReadOnlyList<LineaDeArqueoDto> Lines,
    DocumentoDeCajaDto? DifferenceDocument,
    DocumentoDeCajaDto? ClosingWithdrawalDocument,
    LoteDeSesionDto? Batch);

public sealed record ConteoDeDenominacionRequest(Guid CashDenominationPublicId, int Quantity);

public sealed record LoteDeDatafonoRequest(Guid CardTerminalPublicId, string BatchNumber, decimal Total, int Count);

public sealed record CotejoDeReferenciaRequest(Guid DocumentPaymentPublicId, bool Checked);

/// <summary>Lo contado de un medio: total, por denominaciones, por lote de datáfono o por referencias, con el motivo si hay diferencia.</summary>
public sealed record ConteoPorMedioRequest(
    Guid PaymentMeansPublicId,
    decimal? CountedTotal = null,
    IReadOnlyList<ConteoDeDenominacionRequest>? Denominations = null,
    IReadOnlyList<LoteDeDatafonoRequest>? TerminalBatches = null,
    IReadOnlyList<CotejoDeReferenciaRequest>? ReferenceChecks = null,
    string? Reason = null);

/// <summary>El retiro de cierre y su destino por nombre (<c>Safe</c>, <c>Register</c>, <c>Deposit</c>).</summary>
public sealed record RetiroDeCierreRequest(string Destination);

public sealed record AbrirSesionRequest(Guid CashRegisterPublicId, decimal? OpeningBase, IReadOnlyList<ConteoDeDenominacionRequest>? Denominations);

public sealed record CerrarSesionRequest(IReadOnlyList<ConteoPorMedioRequest> Counts, RetiroDeCierreRequest? ClosingWithdrawal = null);

// ------------------------------------------------------------------------------------------- movimientos --

public sealed record MovimientoDeCajaDto(
    Guid DocumentPublicId,
    string? Number,
    int Status,
    Guid CashSessionPublicId,
    int Kind,
    string SourcePaymentMeansCode,
    string? TargetPaymentMeansCode,
    int? Destination,
    string? DestinationCashRegisterCode,
    decimal Amount,
    string Reason,
    string? CreatedByName,
    string? ApprovedByName);

/// <summary>Un movimiento de caja (§21.3): clase y destino por nombre; la reclasificación dice el pago y el medio al que pasa.</summary>
public sealed record MovimientoDeCajaRequest(
    Guid CashSessionPublicId,
    string Kind,
    Guid SourcePaymentMeansPublicId,
    decimal Amount,
    string Reason,
    Guid? TargetPaymentMeansPublicId = null,
    string? Destination = null,
    Guid? DestinationCashRegisterPublicId = null,
    IReadOnlyList<ConteoDeDenominacionRequest>? Denominations = null,
    Guid? ReclassifiedPaymentPublicId = null,
    string? TargetReference = null,
    string? TargetAuthorizationCode = null,
    Guid? TargetCardTerminalPublicId = null,
    Guid? DepositBankPublicId = null,
    Guid? DocumentTypePublicId = null,
    DateOnly? OperationDate = null,
    byte[]? RowVersion = null);

// ---------------------------------------------------------------------------------------- cierre del día --

public sealed record CierreDelDiaDto(
    Guid DayClosePublicId,
    ReferenciaDeInventarioDto PointOfSale,
    DateOnly OperatingDate,
    short Version,
    int Status,
    DateTime ClosedAt,
    string? ClosedByName,
    DateTime? ReopenedAt,
    string? ReopenReason,
    decimal Total);

public sealed record LineaDelCierreDelDiaDto(string PaymentMeansCode, decimal Expected, decimal Counted, decimal Difference, int PaymentsCount);

public sealed record TarjetaDelCierreDelDiaDto(string PaymentMeansCode, string? AcquirerCode, string? CardTerminalCode, IReadOnlyList<string> BatchNumbers,
    decimal Expected, decimal Counted, int Count);

public sealed record SesionDelCierreDelDiaDto(Guid CashSessionPublicId, string CashRegisterCode, string CashierName, DateTime OpenedAt, DateTime? ClosedAt);

public sealed record DetalleDelCierreDelDiaDto(
    CierreDelDiaDto DayClose,
    IReadOnlyList<LineaDelCierreDelDiaDto> Lines,
    IReadOnlyList<TarjetaDelCierreDelDiaDto> Cards,
    IReadOnlyList<SesionDelCierreDelDiaDto> Sessions);

public sealed record CierreDelDiaRequest(Guid PointOfSalePublicId, DateOnly OperatingDate);
