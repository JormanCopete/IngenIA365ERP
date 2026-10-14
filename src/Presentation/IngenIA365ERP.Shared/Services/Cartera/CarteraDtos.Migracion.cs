using IngenIA365ERP.Shared.Services.Contabilidad;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US8 (T0758; contracts/api.md §19.1–§19.3): la carga de migración por partes, su cuadre contra Contabilidad, la conciliación de
// ventas, la aceptación de diferencias con el acta y la activación. Espejo de los DTOs de la API: los estados y clases llegan como número (los
// textos están en TextosDeMigracion). (nuevo)

// ------------------------------------------------------------------------------------------------------------- cargas --

/// <summary>Una tanda (un archivo) de una parte; <see cref="Status"/> 1 = cuenta, 2 = la reemplazó otra.</summary>
public sealed record TandaDeMigracionDto(string Part, int Rows, string? Sha256, DateTime? ImportedAt, int Batch, string? FileName, int Status);

public sealed record DiferenciasDeLaCargaDto(int Pending, int Accepted);

/// <summary>Lo registrado a mano antes de activar que prevalece sobre la carga.</summary>
public sealed record LoPrevalecidoDto(Guid PersonPublicId, string Concept, string Detail);

/// <summary>Una carga de migración; <see cref="Status"/>: 1 borrador, 2 aplicada, 3 reemplazada, 4 activada.</summary>
public sealed record CargaDeMigracionDto(
    Guid PublicId,
    int Number,
    DateOnly CutoffDate,
    int Status,
    string? Description,
    IReadOnlyList<TandaDeMigracionDto> Parts,
    DateTime? AppliedAt,
    string? AppliedBy,
    DateOnly? ActivationDeadline,
    bool IsBalanced,
    bool SalesReconciled,
    int LoanCount,
    DiferenciasDeLaCargaDto Differences,
    DateTime? ActivatedAt,
    IReadOnlyList<LoPrevalecidoDto>? ManualPrevails);

public sealed record CrearCargaRequest(DateOnly CutoffDate, string? Notes);

public sealed record ResumenDelCuadreDto(int CheckVersion, bool IsBalanced, int Differences);

public sealed record ResumenDeLaConciliacionDto(bool SalesReconciled, int Differences, int Migrated, int CancelledBeforeCutoff, int NotMigratedWithBalance,
    int AcceptedOutsideLending);

/// <summary>Un error (o aviso) entre partes de <c>Lending.Migration.CrossPartErrors</c>.</summary>
public sealed record ErrorEntrePartesDto(string Part, int? Batch, int Row, string Column, string Code, string Message);

/// <summary>Lo que responde aplicar la carga.</summary>
public sealed record CargaAplicadaDto(
    Guid PublicId,
    int Status,
    DateOnly CutoffDate,
    DateOnly ActivationDeadline,
    ResumenDelCuadreDto BalanceCheck,
    ResumenDeLaConciliacionDto SalesReconciliation,
    Guid? ReplacedLoadPublicId,
    Guid AppliedMessagePublicId,
    IReadOnlyDictionary<string, int>? Counts,
    IReadOnlyList<LoPrevalecidoDto>? ManualPrevails,
    IReadOnlyList<ErrorEntrePartesDto>? Warnings,
    int RevalidatedLimits);

// ------------------------------------------------------------------------------------------------- cuadre y ventas --

public sealed record CuentaDelCuadreDto(string Code, string Name);

/// <summary>Una fila del cuadre: concepto, modalidad, sub-segmento, categoría, conjunto de cuentas, Cartera, Contabilidad y diferencia.</summary>
public sealed record FilaDeCuadreDto(
    string DimensionKey,
    string Family,
    string? Concept,
    string? Modality,
    string? Subsegment,
    string? Category,
    IReadOnlyList<CuentaDelCuadreDto> Accounts,
    decimal Lending,
    decimal Accounting,
    decimal Difference,
    bool Unresolved);

/// <summary>Una diferencia; <see cref="Kind"/>: 1 cuadre, 2 venta no migrada, 3 venta fuera de Cartera.</summary>
public sealed record DiferenciaDeMigracionDto(
    Guid PublicId,
    int Kind,
    string DimensionKey,
    string? Concept,
    string? Modality,
    string? Subsegment,
    string? Category,
    string? Accounts,
    string? InvoiceNumber,
    decimal Lending,
    decimal Accounting,
    decimal Difference,
    DateTime? AcceptedAt,
    string? AcceptanceReason);

public sealed record CuadreDeMigracionDto(
    Guid LoadPublicId,
    DateOnly CutoffDate,
    int CheckVersion,
    bool IsBalanced,
    IReadOnlyList<FilaDeCuadreDto> Rows,
    IReadOnlyList<DiferenciaDeMigracionDto> Differences);

/// <summary>Una venta de la conciliación; <see cref="Classification"/>: 1 migrada, 2 cancelada antes del corte, 3 no migrada con saldo, 4 fuera de Cartera.</summary>
public sealed record VentaDeMigracionDto(
    Guid MessagePublicId,
    string InvoiceNumber,
    DateOnly SaleDate,
    decimal Amount,
    int Classification,
    bool MarkedFromList,
    string? MarkReason,
    int AdjustmentsUpToCutoff,
    int AdjustmentsAfterCutoff,
    Guid? DifferencePublicId,
    bool Accepted);

public sealed record MarcaDeVentaRequest(Guid MessagePublicId, int Classification, string Reason);

public sealed record AceptarDiferenciasRequest(IReadOnlyList<Guid> DifferencePublicIds, Guid SessionItemPublicId, string Reason);

// ---------------------------------------------------------------------------------------------------------- activación --

/// <summary>Un requisito; <see cref="Status"/>: <c>Met</c>, <c>Missing</c>, <c>NotApplicable</c> o <c>Waived</c> (falta, pero fuera de producción no bloquea).</summary>
public sealed record RequisitoDeActivacionDto(string Code, bool Met, string Detail, QuienCorrigeDto? WhoFixes, string Status);

/// <summary>Un aviso que no bloquea.</summary>
public sealed record AvisoDeActivacionDto(string Code, string Detail, QuienCorrigeDto? WhoFixes);

public sealed record CargaDeLaActivacionDto(Guid PublicId, int Number, DateOnly CutoffDate, int Status);

/// <summary><c>ActivationReadinessDto</c>: el estado, la carga, la fecha que corresponde (corte + 1), el plazo, los requisitos y los avisos.</summary>
public sealed record EstadoDeActivacionDto(
    string Status,
    DateOnly? ActivationDate,
    Guid? ActivationPublicId,
    DateTime? ActivatedAt,
    CargaDeLaActivacionDto? CurrentLoad,
    DateOnly? ExpectedActivationDate,
    DateOnly? ActivationDeadline,
    bool IsProductionEnvironment,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    IReadOnlyList<AvisoDeActivacionDto>? Warnings,
    bool ObligatoryModelUnavailable);

/// <summary>Lo que responde la revisión previa de los cobros provisionales: el adjunto que se pasa a la activación.</summary>
public sealed record RevisionDeCobrosProvisionalesDto(
    Guid StagedAttachmentPublicId,
    string Sha256,
    int Rows,
    decimal Total,
    IReadOnlyList<ErrorDeImportacionCarteraDto> Errors,
    IReadOnlyList<ErrorDeImportacionCarteraDto> Warnings);

/// <summary>Un error o aviso de fila de los cobros provisionales.</summary>
public sealed record ErrorDeImportacionCarteraDto(int Row, string Column, string Code, string Message);

public sealed record ActivarCarteraRequest(
    Guid MigrationLoadPublicId,
    DateOnly ActivationDate,
    Guid? ProvisionalCollectionsAttachmentPublicId,
    Guid SessionItemPublicId,
    bool AcknowledgeWarnings,
    string Reason,
    bool PayrollWarningAcknowledged);

public sealed record MensajeEmitidoDto(string Type, Guid MessagePublicId);

/// <summary>Lo que nació con la activación.</summary>
public sealed record CarteraActivadaDto(
    Guid ActivationPublicId,
    DateOnly ActivationDate,
    int Loans,
    int OpeningTransactions,
    Guid? BaseClosePublicId,
    int CreditLimits,
    int RevalidatedLimits,
    int NonDeductionsMarked,
    int ProvisionalCollectionsStored,
    IReadOnlyList<MensajeEmitidoDto> Messages);
