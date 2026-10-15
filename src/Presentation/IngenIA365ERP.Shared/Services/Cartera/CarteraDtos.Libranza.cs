namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, K2, US10 (T0851): los espejos de contracts/api.md §13 (pagadurías y convenios, archivos de descuento, no aplicados y traslado) y de
// las dos rutas de K2 del recaudador de prueba (§20). Los enums de entrada viajan por nombre (la API los acepta por nombre o número) y los de salida
// como texto; la pantalla los traduce con TextosDeLibranza. (nuevo)

// ------------------------------------------------------------------------------------------------ §13.1 convenios --

/// <summary>La pagaduría: persona jurídica del maestro, su razón social y su NIT.</summary>
public sealed record PagaduriaDto(Guid PersonPublicId, string LegalName, string TaxId);

/// <summary>Lo que rige hoy del convenio.</summary>
public sealed record VersionVigenteDeConvenioDto(Guid VersionPublicId, DateOnly ValidFrom, int CutoffDay, int? SendDay, int PayDay, int? TransferWindowDays);

/// <summary>Un convenio en la lista.</summary>
public sealed record ConvenioResumenDto(Guid PublicId, string Code, string Name, PagaduriaDto Payer, bool IsOwnPayroll, VersionVigenteDeConvenioDto? CurrentVersion,
    int ActiveLoans, bool IsActive, string Status);

/// <summary>Un código de respuesta de la pagaduría y su traducción.</summary>
public sealed record CodigoDeRespuestaDto(string Code, string Description, string Outcome, string? Reason);

/// <summary>Una versión del convenio.</summary>
public sealed record VersionDeConvenioDto(
    Guid PublicId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Periodicity,
    int CutoffDay,
    int? SendDay,
    int PayDay,
    int SendLeadDays,
    Guid OutboundFormatPublicId,
    string OutboundFormatCode,
    Guid ResultFormatPublicId,
    string ResultFormatCode,
    decimal? DeductionCap,
    bool IncludesPastDue,
    IReadOnlyList<string> NoveltyRules,
    int? TransferWindowDays,
    string NotAppliedAction,
    string AlternateRepaymentChannel,
    string? Notes,
    IReadOnlyList<CodigoDeRespuestaDto> ResultCodes);

/// <summary>Un contacto vigente: nombre, papel y contacto institucional (sin documento, C84).</summary>
public sealed record ContactoDeConvenioDto(Guid PublicId, Guid PersonPublicId, string Name, string Role, bool IsPrimary, string? Email, string? Phone,
    DateOnly ValidFrom);

/// <summary>El convenio completo.</summary>
public sealed record ConvenioDto(
    Guid PublicId,
    string Code,
    string Name,
    PagaduriaDto Payer,
    bool IsOwnPayroll,
    string Status,
    bool IsActive,
    VersionVigenteDeConvenioDto? CurrentVersion,
    int ActiveLoans,
    IReadOnlyList<VersionDeConvenioDto> Versions,
    IReadOnlyList<ContactoDeConvenioDto> Contacts);

/// <summary>Un código de respuesta al registrar una versión (<c>outcome</c> Deducted, PartiallyDeducted o NotDeducted; <c>reason</c> el motivo).</summary>
public sealed record CodigoDeRespuestaRequest(string Code, string Description, string Outcome, string? Reason = null);

/// <summary>La versión de un convenio (§13.1). <c>deductionCap</c> es fracción (0,4 = 40 %).</summary>
public sealed record VersionDeConvenioRequest
{
    public DateOnly ValidFrom { get; init; }
    public int CutoffDay { get; init; } = 20;
    public int PayDay { get; init; } = 30;
    public Guid OutboundFormatPublicId { get; init; }
    public Guid ResultFormatPublicId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int? SendDay { get; init; }
    public int SendLeadDays { get; init; }
    public decimal? DeductionCap { get; init; }
    public bool IncludesPastDue { get; init; }
    public IReadOnlyList<string>? NoveltyRules { get; init; }
    public int? TransferWindowDays { get; init; }
    public string? NotAppliedAction { get; init; }
    public string? AlternateRepaymentChannel { get; init; }
    public string? Periodicity { get; init; }
    public IReadOnlyList<CodigoDeRespuestaRequest>? ResultCodes { get; init; }
    public string? Notes { get; init; }
}

/// <summary>El alta de una pagaduría con su convenio y su primera versión.</summary>
public sealed record ConvenioRequest(string Code, string Name, Guid PayerPersonPublicId, bool IsOwnPayroll, VersionDeConvenioRequest FirstVersion);

/// <summary>Un contacto: una persona del maestro con su papel.</summary>
public sealed record ContactoDeConvenioRequest(Guid PersonPublicId, string? Role = null, bool IsPrimary = false);

public sealed record ContactosDeConvenioRequest(IReadOnlyList<ContactoDeConvenioRequest> Contacts);

/// <summary>Lo descontado pendiente de giro de un archivo del convenio.</summary>
public sealed record PendienteDeGiroDto(int Year, int Month, int SubPeriod, Guid FilePublicId, string FileNumber, int Lines, decimal Amount,
    DateOnly? FirstDeductionDate, DateOnly? TransferDueDate, int PaymasterArrearsDays, int? ThresholdDays, bool ThresholdExceeded);

// ----------------------------------------------------------------------------------------------- §13.2 archivos --

/// <summary>Un archivo de descuentos.</summary>
public sealed record ArchivoDeDescuentosDto(
    Guid PublicId,
    string Number,
    Guid AgreementPublicId,
    string AgreementCode,
    int Year,
    int Month,
    int SubPeriod,
    int Version,
    string Status,
    int Lines,
    decimal Total,
    int NewCount,
    int ChangeCount,
    int CancellationCount,
    int UnchangedCount,
    string Sha256,
    string FileName,
    DateTime GeneratedAt,
    DateTime? SentAt,
    string? PaymasterReference,
    string? VoidReason,
    Guid? SupersededByPublicId);

/// <summary>Una línea del archivo.</summary>
public sealed record LineaDeDescuentoDto(
    Guid PublicId,
    int LineNumber,
    string? EmployeeId,
    Guid LoanPublicId,
    string LoanNumber,
    string Debtor,
    int? InstallmentNumber,
    DateOnly? DueDate,
    decimal CurrentAmount,
    decimal PastDueAmount,
    decimal Total,
    string Novelty,
    decimal? PreviousAmount,
    DateOnly LibranzaDate);

/// <summary>El archivo con una página de sus líneas.</summary>
public sealed record ArchivoDeDescuentosDetalleDto(ArchivoDeDescuentosDto File, IReadOnlyList<LineaDeDescuentoDto> Lines, int TotalLines, int Page, int PageSize);

/// <summary>Lo que saldría: las líneas, el texto del archivo y los totales.</summary>
public sealed record VistaPreviaDeDescuentosDto(string FileName, DateOnly SendBy, int Version, IReadOnlyList<LineaDeDescuentoDto> Lines, IReadOnlyList<string> Text,
    decimal Total, int NewCount, int ChangeCount, int CancellationCount, int UnchangedCount);

/// <summary>El pedido de vista previa o de generación: el convenio, el período y el motivo de regenerar.</summary>
public sealed record ArchivoDeDescuentosRequest(Guid AgreementPublicId, int Year, int Month, int? SubPeriod = null, string? Reason = null);

// ------------------------------------------------------------------------------------- §13.3 no aplicados y traslado --

/// <summary>Un no aplicado con su último seguimiento.</summary>
public sealed record NoAplicadoDeLibranzaDto(
    Guid PublicId,
    Guid LoanPublicId,
    string LoanNumber,
    string Debtor,
    int InstallmentNumber,
    DateOnly DueDate,
    string? Collector,
    string Reason,
    string? ReasonText,
    string Origin,
    decimal ExpectedAmount,
    decimal NotDeductedAmount,
    string? LastAction,
    DateTime? NotifiedAt,
    DateTime Since,
    bool Resolved);

/// <summary>La resolución: <c>MovedToCashPayment</c>, <c>SentToCollection</c> o <c>Closed</c>, con motivo.</summary>
public sealed record ResolucionDeNoAplicadoRequest(string Action, string Reason);

/// <summary>El traslado: el convenio nuevo (nulo = la forma alterna), la identificación, la certificación y el motivo.</summary>
public sealed record TrasladoDeLibranzaRequest(Guid? NewAgreementPublicId, string? EmployeeId, Guid? CertificationAttachmentPublicId, DateOnly? CertificationDate,
    decimal? CertifiedNetIncome, string Reason);

/// <summary>Lo que dejó el traslado.</summary>
public sealed record TrasladoDeLibranzaDto(Guid LoanPublicId, string LoanNumber, Guid PreviousLinkPublicId, Guid NewLinkPublicId, string RepaymentChannel,
    Guid? AgreementPublicId, string? AgreementCode, DateOnly ValidFrom, DateOnly? LibranzaDate, bool SubsegmentChanged, string? Subsegment,
    IReadOnlyList<string> Warnings);

// ------------------------------------------------------------------------------------------- §20 recaudador de prueba --

/// <summary>Lo que emitió el recaudador de prueba sobre un archivo de descuentos.</summary>
public sealed record MensajeDeLibranzaDePruebaDto(Guid MessagePublicId, Guid FilePublicId, string Type, int Lines);

public sealed record ConfirmacionDeEnvioDePruebaRequest(string Channel = "Portal", string? Reference = null);

// ------------------------------------------------------------------------- lectura de prueba de un formato (T0832) --

/// <summary>Una fila leída con el formato: su línea, el texto y los valores por origen.</summary>
public sealed record FilaLeidaDto(int LineNumber, string Text, IReadOnlyDictionary<string, string?> Values);

/// <summary>Un error de lectura con su línea y campo.</summary>
public sealed record ErrorDeLecturaDto(int LineNumber, string? Field, string Message);

/// <summary>Lo que un formato leyó de un archivo de prueba (<c>POST /api/core/bank-file-formats/{id}/read-preview</c>).</summary>
public sealed record LecturaDeFormatoDto(IReadOnlyList<FilaLeidaDto> Rows, IReadOnlyList<ErrorDeLecturaDto> Errors, int DetailLineCount);
