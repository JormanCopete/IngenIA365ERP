using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

// 2026-11-25 (pedido del dueño; feature 026): el contrato HTTP de los cargos masivos (/api/lending/bulk-charges; specs/026-cargos-masivos/contracts/api.md).
// Los enums entran por nombre o número y salen como número (EnumPorNombreONumero). (nuevos)

/// <summary>Los filtros sobre los asociados activos (vacíos = todos).</summary>
public sealed record BulkChargeFiltersInput(
    IReadOnlyList<Guid>? BranchPublicIds = null,
    IReadOnlyList<string>? DeductionTypes = null,
    IReadOnlyList<string>? DeductionPeriods = null,
    IReadOnlyList<Guid>? EmployerCompanyPublicIds = null);

/// <summary>Los datos de una corrida (crear o cambiar el borrador). Nulos = los de la versión de la línea.</summary>
public sealed record BulkChargeRunInput(
    Guid CreditLinePublicId,
    int Period,
    DateOnly ChargeDate,
    DateOnly FirstDueDate,
    decimal? Amount = null,
    int? Installments = null,
    RepaymentPeriodicity? Periodicity = null,
    BulkChargeChannelMode ChannelMode = BulkChargeChannelMode.Fixed,
    RepaymentChannel? RepaymentChannel = null,
    AccrualDiscountCycle? PaymentCycle = null,
    BulkChargeCounterpart CounterpartMode = BulkChargeCounterpart.Debtor,
    Guid? CounterpartPersonPublicId = null,
    BulkChargeSource Source = BulkChargeSource.Filters,
    BulkChargeFiltersInput? Filters = null);

/// <summary>Una línea de crédito que admite cargos masivos, con sus valores por defecto.</summary>
public sealed record BulkChargeLineOptionDto(
    Guid CreditLinePublicId,
    string Code,
    string Name,
    decimal MinAmount,
    decimal MaxAmount,
    decimal? DefaultAmount,
    int DefaultInstallments,
    int? MaxInstallments,
    RepaymentPeriodicity DefaultPeriodicity,
    IReadOnlyList<RepaymentPeriodicity> Periodicities,
    RepaymentChannel DefaultRepaymentChannel,
    IReadOnlyList<RepaymentChannel> RepaymentChannels,
    bool ReportsToBureaus,
    bool EntersCollection);

public sealed record BulkChargeOptionsDto(IReadOnlyList<BulkChargeLineOptionDto> Lines, bool SegregationRequired);

/// <summary>Cuántos quedaron fuera por cada motivo.</summary>
public sealed record BulkChargeReasonCountDto(string Reason, int Count);

/// <summary>La evaluación contable de la vista previa: el resultado y los mensajes para quien confirma.</summary>
public sealed record BulkChargeAccountingDto(string Outcome, bool Blocked, IReadOnlyList<string> Messages, string VoucherType);

public sealed record BulkChargeRunDto(
    Guid PublicId,
    string Number,
    BulkChargeRunStatus Status,
    Guid CreditLinePublicId,
    string CreditLineCode,
    string CreditLineName,
    int Period,
    DateOnly ChargeDate,
    DateOnly FirstDueDate,
    decimal Amount,
    int Installments,
    RepaymentPeriodicity Periodicity,
    BulkChargeChannelMode ChannelMode,
    RepaymentChannel? RepaymentChannel,
    AccrualDiscountCycle? PaymentCycle,
    BulkChargeCounterpart CounterpartMode,
    Guid? CounterpartPersonPublicId,
    string? CounterpartPersonName,
    BulkChargeSource Source,
    BulkChargeFiltersInput Filters,
    int FileRows,
    string? PreviewSha256,
    DateTime? PreviewedAt,
    int TotalLines,
    int PendingCount,
    int CreatedCount,
    int SkippedCount,
    int FailedCount,
    int VoidedCount,
    decimal TotalAmount,
    IReadOnlyList<BulkChargeReasonCountDto> SkippedByReason,
    BulkChargeAccountingDto? Accounting,
    bool CanConfirm,
    string? PreparedByName,
    string? ConfirmedByName,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt,
    Guid? ProcessRunPublicId,
    string? ProcessRunStatus,
    long? BatchNumber,
    string? VoidReason,
    DateTime? VoidedAt,
    DateTime CreatedAt);

public sealed record BulkChargeRunSummaryDto(
    Guid PublicId,
    string Number,
    BulkChargeRunStatus Status,
    string CreditLineCode,
    string CreditLineName,
    int Period,
    DateOnly ChargeDate,
    int CreatedCount,
    int SkippedCount,
    int FailedCount,
    int VoidedCount,
    int PendingCount,
    decimal TotalAmount,
    DateTime CreatedAt);

public sealed record BulkChargeLineDto(
    Guid PublicId,
    Guid PersonPublicId,
    string PersonDocument,
    string PersonName,
    string? BranchName,
    decimal Amount,
    int Installments,
    DateOnly FirstDueDate,
    RepaymentChannel RepaymentChannel,
    BulkChargeLineStatus Status,
    string? Reason,
    string? ReasonDetail,
    string? Warning,
    Guid? LoanPublicId,
    string? LoanNumber,
    int? SourceRow,
    DateTime? ProcessedAt);

public sealed record BulkChargeLinesPageDto(IReadOnlyList<BulkChargeLineDto> Items, int Total, int Page, int PageSize);

/// <summary>Una fila del plano ya resuelta (persona y valores propios), guardada en el borrador (<c>FiltersJson.rows</c>).</summary>
public sealed record FilaDeCargoMasivo(
    int Row,
    int PersonId,
    decimal? Amount = null,
    int? Installments = null,
    DateOnly? FirstDueDate = null,
    RepaymentChannel? RepaymentChannel = null);
