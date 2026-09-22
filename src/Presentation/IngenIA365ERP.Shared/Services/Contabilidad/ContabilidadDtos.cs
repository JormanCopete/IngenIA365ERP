namespace IngenIA365ERP.Shared.Services.Contabilidad;

// DTOs del módulo contable tal como los sirve la API (feature 009). `Shared` no referencia
// `Application`, así que se duplican aquí con los mismos nombres de propiedad; el JSON es el
// contrato (specs/009-contabilidad-niif/contracts/api.md). Un cambio aquí es un cambio allá.

// ------------------------------------------------------------------ configuración y catálogos --

/// <summary><c>GET /api/accounting/setup</c>.</summary>
public sealed record ConfiguracionContableDto(
    bool Initialized,
    string? CatalogCode,
    string? CatalogName,
    byte MovementLevel,
    byte NiifGroup,
    int FirstFiscalYear,
    Guid? ResultAccountPublicId,
    string? ResultAccountCode,
    Guid? MainBranchPublicId,
    string? MainBranchName,
    bool FourEyes,
    int ReconciliationDayTolerance,
    decimal TaxTolerance,
    bool Locked,
    string? LockReason,
    Guid? OpeningDocumentPublicId,
    DateTime? InitializedAt,
    string? InitializedBy);

public sealed record InicializarContabilidadRequest(
    string CatalogCode,
    byte MovementLevel,
    byte NiifGroup,
    int FirstFiscalYear,
    Guid MainBranchPublicId,
    bool FourEyes);

public sealed record InicializacionDto(int Accounts, int Periods);

public sealed record ActualizarConfiguracionRequest(
    string? CatalogCode,
    byte? MovementLevel,
    byte? NiifGroup,
    Guid? ResultAccountPublicId,
    Guid? MainBranchPublicId,
    bool FourEyes,
    int ReconciliationDayTolerance,
    decimal TaxTolerance);

public sealed record CatalogoContableDto(string Code, string Name, string Version, string Source, int EntryCount, DateTime? ValidatedAt, string? ValidatedBy);

public sealed record EntradaDeCatalogoDto(string Code, string Name, byte Level, string Nature, string NiifItemCode, string? ParentCode);

public sealed record ImportacionDeCatalogoDto(string Code, int EntryCount);

/// <summary>Una fila con error en una importación (catálogo o apertura): nada se guarda a medias.</summary>
public sealed record ErrorDeFilaDto(int Row, string Column, string Code, string Message);

public sealed record CuentaNuevaDeCatalogoDto(string Code, string Name, byte Level, string? ParentCode, bool AlreadyInCompany);

// -------------------------------------------------------------------------- plan de cuentas --

/// <summary>Nodo del árbol (<c>GET /accounts/tree</c>) y fila del buscador.</summary>
public sealed record CuentaNodoDto(
    Guid PublicId,
    string Code,
    string Name,
    byte Level,
    string Nature,
    bool IsMovement,
    bool IsActive,
    bool HasChildren,
    string Origin,
    Guid? ParentPublicId);

public sealed record CuentaBuscadaDto(
    Guid PublicId,
    string Code,
    string Name,
    string Nature,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresCostCenter,
    bool RequiresBranch,
    bool RequiresTaxBase)
{
    public string Texto => $"{Code} - {Name}";
}

public sealed record CuentaBancariaDto(Guid BankPublicId, string? BankName, string? AccountNumber);

public sealed record TarifaDto(DateOnly ValidFrom, decimal Rate);

public sealed record CuentaDeImpuestoDto(string Kind, string? ConceptCode, bool RequiresTaxBase, IReadOnlyList<TarifaDto> Rates);

/// <summary>Dónde está parametrizada la cuenta en otro módulo (para no eliminarla a ciegas).</summary>
public sealed record ReferenciaDeCuentaDto(string Module, string Where, string Detail);

/// <summary><c>GET /accounts/{id}</c>: ficha completa con reglas.</summary>
public sealed record CuentaDto(
    Guid PublicId,
    string Code,
    string Name,
    byte Level,
    string Nature,
    Guid? ParentPublicId,
    string? ParentCode,
    string NiifItemCode,
    string Origin,
    bool IsMovement,
    bool IsActive,
    DateOnly? FirstMovementAt,
    bool Locked,
    IReadOnlyList<string> EnabledModules,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresCostCenter,
    bool RequiresBranch,
    CuentaBancariaDto? Bank,
    CuentaDeImpuestoDto? Tax,
    IReadOnlyList<ReferenciaDeCuentaDto> References);

public sealed record CuentaBancariaRequest(Guid BankPublicId, string? AccountNumber);

public sealed record CuentaDeImpuestoRequest(string Kind, string? ConceptCode, bool RequiresTaxBase, IReadOnlyList<TarifaDto> Rates);

/// <summary><c>POST</c>/<c>PUT /accounts</c>: código, nombre, padre y reglas.</summary>
public sealed record CuentaRequest(
    string Code,
    string Name,
    Guid? ParentPublicId,
    IReadOnlyList<string> EnabledModules,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresCostCenter,
    bool RequiresBranch,
    CuentaBancariaRequest? Bank,
    CuentaDeImpuestoRequest? Tax);

public sealed record EventoDeCuentaDto(DateTime OccurredAt, string Action, string? User, string? Detail);

/// <summary>FR-017 y FR-088: parametrizaciones de otros módulos que ya no cumplen, y vínculos institucionales que faltan.</summary>
public sealed record ParametrizacionInvalidaDto(string Module, string Where, string Detail, string? AccountCode, string Problem);

// ------------------------------------------------------- tipos de comprobante y de cruce --

public sealed record TipoComprobanteDto(Guid PublicId, string Code, string Name, string Usage, string? ModuleCode, long NextNumber, bool IsActive, bool IsSeeded);

public sealed record TipoComprobanteRequest(string Code, string Name, string Usage, string? ModuleCode, bool IsActive);

public sealed record TipoCruceDto(Guid PublicId, string Code, string Name, bool IsActive, bool IsSeeded);

public sealed record TipoCruceRequest(string Code, string Name, bool IsActive);

// ----------------------------------------------------------------------------- períodos --

public sealed record PeriodoContableDto(
    Guid PublicId,
    int Year,
    byte Month,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateTime? ClosedAt,
    string? ClosedBy,
    DateTime? ReopenedAt,
    string? ReopenedBy,
    string? ReopenReason,
    int Drafts);

public sealed record EjercicioDto(
    Guid PublicId,
    int Year,
    string Status,
    DateTime? ClosedAt,
    string? ClosedBy,
    DateTime? ReopenedAt,
    string? ReopenedBy,
    string? ReopenReason,
    Guid? ClosingDocumentPublicId,
    IReadOnlyList<PeriodoContableDto> Periods);

public sealed record AbrirEjercicioRequest(int Year);

public sealed record MotivoRequest(string Reason);

/// <summary>Respuesta de cerrar o reabrir el ejercicio (E2, US6): el comprobante <c>CI</c> (o su reverso), nulo si no había resultados que cancelar.</summary>
public sealed record CierreDeEjercicioDto(int Year, Guid? ClosingDocumentPublicId, long? Number, int Lines, decimal Result);

// -------------------------------------------------------------------------- comprobantes --

public sealed record FiltroDeComprobantes(
    DateOnly? From = null,
    DateOnly? To = null,
    string? VoucherType = null,
    string? Status = null,
    string? Origin = null,
    long? Number = null,
    int Page = 1,
    int PageSize = 50);

public sealed record ComprobanteResumenDto(
    Guid PublicId,
    string VoucherTypeCode,
    long? Number,
    DateOnly Date,
    string Description,
    string Status,
    string Kind,
    string OriginModule,
    decimal TotalDebit,
    decimal TotalCredit,
    string RegisteredBy,
    string? PostedBy,
    DateTime? PostedAt,
    int Lines);

public sealed record LineaDeComprobanteDto(
    int LineNumber,
    Guid AccountPublicId,
    string AccountCode,
    string AccountName,
    Guid BranchPublicId,
    string BranchName,
    Guid? CostCenterPublicId,
    string? CostCenterName,
    Guid? PersonPublicId,
    string? PersonName,
    string? PersonTaxId,
    string? CrossDocumentType,
    string? CrossDocumentNumber,
    decimal Debit,
    decimal Credit,
    string? Detail,
    decimal? TaxBase);

public sealed record OrigenDeComprobanteDto(string Module, string ModuleName, string? SourceType, Guid? SourcePublicId, string? Link);

public sealed record ReversionDto(Guid? ReversesPublicId, string? ReversesNumber, Guid? ReversedByPublicId, string? ReversedByNumber, string? Reason);

public sealed record AdjuntoDto(Guid PublicId, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt, string? CreatedBy);

/// <summary><c>GET /documents/{id}</c>: cabecera, líneas, origen, reversión y soportes.</summary>
public sealed record ComprobanteDto(
    Guid PublicId,
    string VoucherTypeCode,
    string VoucherTypeName,
    long? Number,
    DateOnly Date,
    string Description,
    string Status,
    string Kind,
    decimal TotalDebit,
    decimal TotalCredit,
    string RegisteredBy,
    DateTime RegisteredAt,
    string? PostedBy,
    DateTime? PostedAt,
    OrigenDeComprobanteDto Origin,
    ReversionDto Reversal,
    IReadOnlyList<LineaDeComprobanteDto> Lines,
    IReadOnlyList<AdjuntoDto> Attachments,
    IReadOnlyList<ErrorDeLineaDto> Errors)
{
    /// <summary>«CG-12», o «borrador» mientras no tiene número.</summary>
    public string Referencia => Number is { } n ? $"{VoucherTypeCode}-{n}" : "borrador";
}

public sealed record LineaDeBorradorRequest(
    string AccountCode,
    Guid? BranchPublicId,
    Guid? CostCenterPublicId,
    Guid? PersonPublicId,
    string? CrossDocumentType,
    string? CrossDocumentNumber,
    decimal Debit,
    decimal Credit,
    string? Detail,
    decimal? TaxBase);

/// <summary><c>POST</c>/<c>PUT /documents/drafts</c> y <c>POST /documents/validate</c>.</summary>
public sealed record BorradorRequest(string VoucherTypeCode, DateOnly Date, string Description, IReadOnlyList<LineaDeBorradorRequest> Lines);

/// <summary>Una infracción por línea con el campo que la pantalla señala; <c>Severity</c> «Aviso» no bloquea.</summary>
public sealed record ErrorDeLineaDto(int LineNumber, string Field, string Code, string Message, string Severity)
{
    public bool Bloquea => !string.Equals(Severity, "Aviso", StringComparison.OrdinalIgnoreCase);
}

public sealed record BorradorGuardadoDto(Guid PublicId, IReadOnlyList<ErrorDeLineaDto> Errors);

public sealed record ValidacionDto(IReadOnlyList<ErrorDeLineaDto> Errors, decimal TotalDebit, decimal TotalCredit, decimal Difference);

public sealed record ContabilizadoDto(long Number);

public sealed record ReversionRequest(string Reason, DateOnly? Date);

public sealed record ReversadoDto(Guid ReversalPublicId, long Number);

// ------------------------------------------------------------------------------- apertura --

public sealed record AperturaImportadaDto(Guid DraftPublicId, int Lines, decimal TotalDebit, decimal TotalCredit, DateOnly Date, bool Replaced, IReadOnlyList<ErrorDeFilaDto> Errors);

/// <summary>Carga masiva de auxiliares (E2, 2026-09-22): cuántas se crearon, se actualizaron y ya estaban como el archivo pide.</summary>
public sealed record ImportacionDeCuentasDto(int Created, int Updated, int Unchanged, IReadOnlyList<ErrorDeFilaDto> Errors);

public sealed record AperturaResumenDto(Guid PublicId, long? Number, DateOnly Date, string Status, decimal TotalDebit, decimal TotalCredit, int Lines,
    string RegisteredBy, DateTime? PostedAt, string? PostedBy, Guid? ReversedByPublicId);

/// <summary><c>GET /api/accounting/opening</c> (E2, US13): la fecha que le toca a la apertura, la vigente, los borradores pendientes y las reversadas.</summary>
public sealed record EstadoDeAperturaDto(DateOnly ExpectedDate, DateOnly MaxDate, int FirstFiscalYear, AperturaResumenDto? Posted, IReadOnlyList<AperturaResumenDto> Drafts, IReadOnlyList<AperturaResumenDto> Reversed);
