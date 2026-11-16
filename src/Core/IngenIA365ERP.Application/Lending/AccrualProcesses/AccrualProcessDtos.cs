using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

// 2026-11-16 (pedido del dueño; feature 024, fase 3): lo que devuelve /api/lending/accrual-processes. Sin Id interno ni datos personales del deudor: el
// crédito va por su número. (nuevos)

/// <summary>Un catálogo nombrado (línea, oficina, pagaduría) del filtro.</summary>
public sealed record RefDeFiltroDto(Guid PublicId, string? Code, string Name);

/// <summary>Los filtros opcionales del proceso.</summary>
public sealed record AccrualProcessFiltersDto(RefDeFiltroDto? CreditLine, RefDeFiltroDto? Branch, RefDeFiltroDto? Paymaster);

/// <summary>Un proceso que se nombra desde otro (el que se reprocesa, el que lo reemplazó).</summary>
public sealed record RefDeProcesoDto(Guid PublicId, string Number);

/// <summary>Un bloqueo o un aviso del cálculo: el código, lo que lee la persona y si impide ponerlo en firme.</summary>
public sealed record AccrualProcessIssueDto(string Code, string Message, bool Blocking);

/// <summary>Un total del borrador, por concepto o por sucursal: hechos, créditos y valor de lo que entra y lo que queda fuera.</summary>
public sealed record AccrualProcessTotalDto(string Key, string Label, int Facts, int Loans, decimal IncludedAmount, decimal ExcludedAmount);

/// <summary>Un hecho del borrador (lo causado de un día, una sucursal y un concepto), con su estado y, si queda fuera, el motivo.</summary>
public sealed record AccrualProcessFactDto(
    Guid? MessagePublicId,
    string FactType,
    AccrualProcessConcepts? Concept,
    DateOnly CausedDate,
    DateOnly AccountingDate,
    string? Branch,
    int LoanCount,
    decimal Amount,
    AccrualProcessItemStatus Status,
    AccrualExclusionReason? Reason,
    bool Blocking,
    string? Detail);

/// <summary>Lo de un crédito y un concepto en el rango (y, en un reproceso, lo que contabilizó el proceso reprocesado).</summary>
public sealed record AccrualProcessLineDto(
    Guid LoanPublicId,
    string LoanNumber,
    string? Branch,
    string? CreditLineCode,
    string? FactType,
    LoanConcept? Concept,
    decimal Amount,
    decimal? PreviousAmount,
    AccrualProcessItemStatus Status,
    AccrualExclusionReason? Reason);

/// <summary>La solicitud de aprobación de la firma: su estado, la huella que confirma la decisión y quién la pidió y la dio.</summary>
public sealed record AccrualProcessApprovalDto(
    Guid RequestPublicId,
    string Status,
    string ContentSha256,
    string PermissionCode,
    string? RequestedBy,
    DateTime? RequestedAt,
    string? ApprovedBy,
    ApprovalMethod? Method);

/// <summary>Un comprobante que dejó el lote (lo responde Contabilidad en la entrega; Cartera no lee <c>ACC_</c>).</summary>
public sealed record AccrualProcessVoucherDto(string VoucherTypeCode, string? Number, int Facts);

/// <summary>Un hecho del lote que Contabilidad rechazó, con su código y su motivo.</summary>
public sealed record AccrualProcessRejectionDto(Guid MessagePublicId, string FactType, string? Code, string? Message);

/// <summary>El lote que ordenó la puesta en firme: su estado, cuántos hechos lleva y en qué van, y los comprobantes y rechazos.</summary>
public sealed record AccrualProcessBatchDto(
    Guid BatchPublicId,
    long Number,
    BatchStatus Status,
    int Facts,
    int Processed,
    int Rejected,
    int Pending,
    IReadOnlyList<AccrualProcessVoucherDto> Vouchers,
    IReadOnlyList<AccrualProcessRejectionDto> Rejections);

/// <summary>El proceso de causación con su borrador (totales, hechos, bloqueos y avisos), su aprobación y su lote.</summary>
public sealed record AccrualProcessDto(
    Guid PublicId,
    string Number,
    AccrualProcessKind Kind,
    AccrualProcessStatus Status,
    AccrualProcessPeriodicity Periodicity,
    byte? SubPeriod,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<AccrualProcessConcepts> Concepts,
    AccrualProcessFiltersDto Filters,
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
    IReadOnlyList<AccrualProcessIssueDto> Issues,
    IReadOnlyList<AccrualProcessTotalDto> TotalsByConcept,
    IReadOnlyList<AccrualProcessTotalDto> TotalsByBranch,
    IReadOnlyList<AccrualProcessFactDto> Facts,
    string? Reason,
    AccrualProcessApprovalDto? Approval,
    AccrualProcessBatchDto? Batch,
    RefDeProcesoDto? ReprocessOf,
    RefDeProcesoDto? ReprocessedBy,
    string? CreatedBy,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    string? VoidReason);

/// <summary>Un proceso en la lista.</summary>
public sealed record AccrualProcessListItemDto(
    Guid PublicId,
    string Number,
    AccrualProcessKind Kind,
    AccrualProcessStatus Status,
    AccrualProcessPeriodicity Periodicity,
    byte? SubPeriod,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<AccrualProcessConcepts> Concepts,
    string? Branch,
    string? CreditLine,
    decimal IncludedAmount,
    int LoanCount,
    bool HasBlockingIssues,
    long? BatchNumber,
    string? ReprocessOfNumber,
    DateTime CreatedAt);

/// <summary>Un concepto que se puede elegir: si hoy está en modo manual y lo que espera contabilizarse (en el alcance de quien consulta).</summary>
public sealed record AccrualConceptOptionDto(
    AccrualProcessConcepts Concept,
    string FactType,
    string Label,
    bool Manual,
    int PendingFacts,
    decimal PendingAmount,
    DateOnly? OldestPendingDate,
    DateOnly? NewestPendingDate);

/// <summary>
/// Lo que la pantalla necesita para armar un proceso: los conceptos con su modo y lo pendiente, las periodicidades, hoy y los catálogos de los filtros
/// (las oficinas y las líneas de colocación del alcance de quien consulta y las pagadurías con convenio).
/// </summary>
public sealed record AccrualProcessOptionsDto(
    IReadOnlyList<AccrualConceptOptionDto> Concepts,
    IReadOnlyList<AccrualProcessPeriodicity> Periodicities,
    DateOnly Today,
    IReadOnlyList<RefDeFiltroDto> Branches,
    IReadOnlyList<RefDeFiltroDto> CreditLines,
    IReadOnlyList<RefDeFiltroDto> Paymasters,
    bool AllBranches,
    bool AllCreditLines);
