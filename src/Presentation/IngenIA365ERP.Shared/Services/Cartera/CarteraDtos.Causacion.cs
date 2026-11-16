namespace IngenIA365ERP.Shared.Services.Cartera;

// 2026-11-16 (pedido del dueño; feature 024, fase 3): lo que la pantalla del proceso de causación lee y manda a /api/lending/accrual-processes. Los enums
// llegan como número (los nombres y textos están en TextosDeCausacion); al mandar, la API los recibe por nombre o por número. (nuevos)

/// <summary>Lo que se pide al armar o recalcular un proceso.</summary>
public sealed record ProcesoDeCausacionRequest(
    int Periodicity,
    byte? SubPeriod,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<int> Concepts,
    Guid? CreditLinePublicId = null,
    Guid? BranchPublicId = null,
    Guid? PaymasterPersonPublicId = null);

/// <summary><c>{ changes? }</c> del recálculo.</summary>
public sealed record RecalculoDeCausacionRequest(ProcesoDeCausacionRequest? Changes);

/// <summary><c>{ reason, expectedContentSha256 }</c> de la puesta en firme.</summary>
public sealed record FirmaDeCausacionRequest(string Reason, string ExpectedContentSha256);

public sealed record RefDeFiltroDeCausacionDto(Guid PublicId, string? Code, string Name);

public sealed record FiltrosDeCausacionDto(RefDeFiltroDeCausacionDto? CreditLine, RefDeFiltroDeCausacionDto? Branch, RefDeFiltroDeCausacionDto? Paymaster);

public sealed record RefDeProcesoDeCausacionDto(Guid PublicId, string Number);

public sealed record AvisoDeCausacionDto(string Code, string Message, bool Blocking);

public sealed record TotalDeCausacionDto(string Key, string Label, int Facts, int Loans, decimal IncludedAmount, decimal ExcludedAmount);

public sealed record HechoDeCausacionDto(
    Guid? MessagePublicId,
    string FactType,
    int? Concept,
    DateOnly CausedDate,
    DateOnly AccountingDate,
    string? Branch,
    int LoanCount,
    decimal Amount,
    int Status,
    int? Reason,
    bool Blocking,
    string? Detail);

public sealed record LineaDeCausacionDto(
    Guid LoanPublicId,
    string LoanNumber,
    string? Branch,
    string? CreditLineCode,
    string? FactType,
    int? Concept,
    decimal Amount,
    decimal? PreviousAmount,
    int Status,
    int? Reason);

public sealed record AprobacionDeCausacionDto(
    Guid RequestPublicId,
    string Status,
    string ContentSha256,
    string PermissionCode,
    string? RequestedBy,
    DateTime? RequestedAt,
    string? ApprovedBy,
    int? Method);

public sealed record ComprobanteDeCausacionDto(string VoucherTypeCode, string? Number, int Facts);

public sealed record RechazoDeCausacionDto(Guid MessagePublicId, string FactType, string? Code, string? Message);

public sealed record LoteDeCausacionDto(
    Guid BatchPublicId,
    long Number,
    int Status,
    int Facts,
    int Processed,
    int Rejected,
    int Pending,
    IReadOnlyList<ComprobanteDeCausacionDto> Vouchers,
    IReadOnlyList<RechazoDeCausacionDto> Rejections);

/// <summary>El proceso con su borrador, su aprobación y su lote.</summary>
public sealed record ProcesoDeCausacionDto(
    Guid PublicId,
    string Number,
    int Kind,
    int Status,
    int Periodicity,
    byte? SubPeriod,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<int> Concepts,
    FiltrosDeCausacionDto Filters,
    int CalculationCount,
    DateTime? CalculatedAt,
    string? CalculatedBy,
    string? ContentSha256,
    int LoanCount,
    int IncludedFactCount,
    int ExcludedFactCount,
    decimal IncludedAmount,
    decimal ExcludedAmount,
    bool HasBlockingIssues,
    bool CanConfirm,
    IReadOnlyList<AvisoDeCausacionDto> Issues,
    IReadOnlyList<TotalDeCausacionDto> TotalsByConcept,
    IReadOnlyList<TotalDeCausacionDto> TotalsByBranch,
    IReadOnlyList<HechoDeCausacionDto> Facts,
    string? Reason,
    AprobacionDeCausacionDto? Approval,
    LoteDeCausacionDto? Batch,
    RefDeProcesoDeCausacionDto? ReprocessOf,
    RefDeProcesoDeCausacionDto? ReprocessedBy,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    string? VoidReason);

/// <summary>Un proceso en la lista.</summary>
public sealed record ProcesoDeCausacionEnListaDto(
    Guid PublicId,
    string Number,
    int Kind,
    int Status,
    int Periodicity,
    byte? SubPeriod,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<int> Concepts,
    string? Branch,
    string? CreditLine,
    decimal IncludedAmount,
    int LoanCount,
    bool HasBlockingIssues,
    long? BatchNumber,
    string? ReprocessOfNumber,
    DateTime CreatedAt);

/// <summary>Un concepto que se puede elegir, con su modo de hoy y lo que espera.</summary>
public sealed record OpcionDeConceptoDeCausacionDto(
    int Concept,
    string FactType,
    string Label,
    bool Manual,
    int PendingFacts,
    decimal PendingAmount,
    DateOnly? OldestPendingDate,
    DateOnly? NewestPendingDate);

public sealed record OpcionesDeCausacionDto(
    IReadOnlyList<OpcionDeConceptoDeCausacionDto> Concepts,
    IReadOnlyList<int> Periodicities,
    DateOnly Today,
    IReadOnlyList<RefDeFiltroDeCausacionDto> Branches,
    IReadOnlyList<RefDeFiltroDeCausacionDto> CreditLines,
    IReadOnlyList<RefDeFiltroDeCausacionDto> Paymasters,
    bool AllBranches,
    bool AllCreditLines);
