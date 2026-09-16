namespace IngenIA365ERP.Application.Accounting.Setup;

// Contratos de configuración y catálogos (feature 009, contracts/api.md §2). Espejo en
// Shared/Services/Contabilidad/ContabilidadDtos.cs: un cambio aquí es un cambio allá.

public sealed record ConfiguracionContableDto(
    bool Initialized,
    string? CatalogCode,
    string? CatalogName,
    byte MovementLevel,
    byte Level5Length,
    byte Level6Length,
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

public sealed record InicializacionDto(int Accounts, int Periods);

public sealed record CatalogoContableDto(string Code, string Name, string Version, string Source, int EntryCount, DateTime? ValidatedAt, string? ValidatedBy);

public sealed record EntradaDeCatalogoDto(string Code, string Name, byte Level, string Nature, string NiifItemCode, string? ParentCode);

public sealed record ImportacionDeCatalogoDto(string Code, int EntryCount);

/// <summary>Una fila con error en una importación; nada se guarda a medias (FR-005).</summary>
public sealed record ErrorDeFila(int Row, string Column, string Code, string Message);

public sealed record CuentaNuevaDeCatalogoDto(string Code, string Name, byte Level, string? ParentCode, bool AlreadyInCompany);
