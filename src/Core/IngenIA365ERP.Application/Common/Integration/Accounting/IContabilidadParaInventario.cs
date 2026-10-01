using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Accounting;

/// <summary>
/// Lo único de Contabilidad que Inventario conoce (feature 012, T30, T31; FR-014; contracts/contabilidad.md §4.1, §7.2).
/// Declara <b>exactamente</b> estos cinco métodos (<c>InventarioNoConoceContabilidadNiCartera</c>). Lo implementa el
/// adaptador <c>ContabilidadParaInventario</c> de <c>Application/Accounting/Inventory</c> (T517), que delega en sus cuatro
/// consultas; mientras no esté registrado, la validación previa queda <c>NotApplicable</c> y la vista previa de un lote
/// responde <c>Integration.Destination.Unavailable</c>.
///
/// <para>
/// La interfaz y sus DTO nacen con la sección de mensajería de I2 (T496, T499) porque la vista previa del lote la usa; los
/// campos de <see cref="CompletitudDeLaMatrizDto"/> y <see cref="VistaPreviaDeLoteDto"/> son los de api.md §26.3 y §26.4.
/// </para>
/// </summary>
public interface IContabilidadParaInventario
{
    /// <summary>FR-074: ¿las líneas que generarían estos mensajes cumplen las reglas y está abierto el período?</summary>
    Task<Result<ResultadoDeContabilizacionDto>> EvaluarAsync(IReadOnlyList<MensajeContableDto> mensajes, CancellationToken ct);

    /// <summary>FR-081, FR-090: saldo contable de las cuentas mapeadas, por conjunto, a una fecha.</summary>
    Task<Result<IReadOnlyList<ConjuntoDeCuentasDto>>> SaldosDeCuentasMapeadasAsync(DateOnly corte, CancellationToken ct);

    /// <summary>FR-082: combinaciones sin regla, medios sin cuenta, cuentas no elegibles, tarifas distintas.</summary>
    Task<Result<CompletitudDeLaMatrizDto>> CompletitudAsync(DateOnly fecha, CancellationToken ct);

    /// <summary>FR-077: los comprobantes que generaría un lote con estos mensajes (en orden de emisión), sin numerar ni guardar.</summary>
    Task<Result<VistaPreviaDeLoteDto>> PrevisualizarLoteAsync(IReadOnlyList<Guid> messagePublicIds, CancellationToken ct);

    /// <summary>
    /// ¿Falta iniciar la contabilidad? Mientras falte, el modo de paso por defecto es «no pasa» (<c>ModoDePasoVigente</c>; decisión
    /// del dueño del 2026-09-26). Se pregunta en negativo para que «no sé» —un doble sin configurar— deje el comportamiento de siempre.
    /// </summary>
    Task<bool> SinIniciarAsync(CancellationToken ct);
}

/// <summary>El sobre y el contenido tal como se emitirían (§4.1): lo evaluado y lo emitido son el mismo contrato.</summary>
public sealed record MensajeContableDto(IntegrationEnvelopeV1 Envelope, object Payload);

public sealed record ResultadoDeContabilizacionDto(
    bool IsPostable,
    IReadOnlyList<HallazgoContableDto> Errors,
    IReadOnlyList<HallazgoContableDto> Warnings);

public sealed record HallazgoContableDto(
    string MessageType,
    IReadOnlyList<int> DocumentLines,
    string? AccountCode,
    string Rule,
    string Message,
    QuienCorrigeDto WhoFixes);

public sealed record QuienCorrigeDto(string Module, string? Page, string? Permission);

/// <summary>Un conjunto de cuentas de inventario y tránsito: componente conexo grupo ↔ cuenta (§7.2).</summary>
public sealed record ConjuntoDeCuentasDto(
    IReadOnlyList<string> AccountingGroupCodes,
    IReadOnlyList<ParGrupoBodegaDto> Pairs,
    IReadOnlyList<CuentaDelConjuntoDto> Accounts,
    decimal Balance);

/// <summary>Un par grupo contable × bodega del conjunto; <c>"*"</c> = todas las bodegas.</summary>
public sealed record ParGrupoBodegaDto(string AccountingGroupCode, string WarehouseCode);

/// <summary>Una cuenta del conjunto, con su rol (<c>Inventario</c> | <c>Transito</c>) y su saldo por sucursal.</summary>
public sealed record CuentaDelConjuntoDto(
    string AccountCode,
    string AccountName,
    string Role,
    IReadOnlyList<SaldoPorSucursalDto> BalanceByBranch);

public sealed record SaldoPorSucursalDto(Guid BranchPublicId, decimal Balance);

/// <summary>La completitud de la matriz a una fecha (api.md §26.3).</summary>
public sealed record CompletitudDeLaMatrizDto(
    DateOnly Date,
    IReadOnlyList<ReglaFaltanteDto> MissingRules,
    IReadOnlyList<MedioSinCuentaDto> PaymentMeansWithoutAccount,
    IReadOnlyList<ReglaNoElegibleDto> IneligibleRules,
    IReadOnlyList<TarifaDistintaDto> TaxRateMismatches,
    IReadOnlyList<OperacionSinMapeoDto> UnmappedOperations,
    ResumenDeCompletitudDto Summary,
    IReadOnlyList<AvisoDeCompletitudDto>? Warnings = null);

/// <summary>
/// Un aviso de la completitud (contracts/contabilidad.md §7.1, «avisos»): no impide nada y no suma al resumen. <see cref="Kind"/>
/// es <c>UnitTaxAccountRequiresBase</c> (impuesto por unidad con una cuenta que exige base) o
/// <c>GoodsNotInvoicedRequiresCrossDocument</c> (mercancía por facturar con una cuenta que exige cruce). (nuevo, T516)
/// </summary>
public sealed record AvisoDeCompletitudDto(string Kind, string Message, string? Account, Guid? RulePublicId);

public sealed record ReglaFaltanteDto(
    string Operation,
    string Role,
    string? AccountingGroupCode,
    string? WarehouseCode,
    string? PointOfSaleCode,
    string? PaymentMeansCode,
    string? TaxRateCode,
    string? ReasonCode,
    IReadOnlyList<string> UsedByDocumentTypes,
    DateOnly? LastUsedAt);

public sealed record MedioSinCuentaDto(string PaymentMeansCode, string Name, string? PointOfSaleCode);

public sealed record ReglaNoElegibleDto(Guid RulePublicId, string Operation, string Role, string Account, string Reason);

public sealed record TarifaDistintaDto(string TaxRateCode, decimal CatalogRate, string Account, decimal? AccountRate, DateOnly From, DateOnly? To);

public sealed record OperacionSinMapeoDto(string Operation, string? InventoryDocumentTypeCode);

public sealed record ResumenDeCompletitudDto(int Total, IReadOnlyDictionary<string, int> ByKind);

/// <summary>La vista previa de un lote (api.md §26.4, <c>InventoryBatchPreviewDto</c>): no numera, no guarda.</summary>
public sealed record VistaPreviaDeLoteDto(
    Guid? CutoffMessagePublicId,
    IReadOnlyList<DocumentoDeLoteDto> Documents,
    IReadOnlyList<ComprobantePropuestoDto> Vouchers,
    IReadOnlyList<DocumentoExcluidoDto> Excluded);

public sealed record DocumentoDeLoteDto(
    Guid DocumentPublicId,
    string? Class,
    string? DocumentTypeCode,
    string? Number,
    DateOnly OperationDate,
    string? Branch,
    decimal Total);

public sealed record ComprobantePropuestoDto(
    string VoucherTypeCode,
    DateOnly OperationDate,
    string? Branch,
    string? CostCenter,
    PostingGranularity Granularity,
    int DocumentsCount,
    IReadOnlyList<LineaPropuestaDto> Lines,
    TotalesPropuestosDto Totals);

public sealed record LineaPropuestaDto(
    CuentaPropuestaDto Account,
    decimal Debit,
    decimal Credit,
    string? ThirdParty,
    string? CrossDocument,
    decimal? TaxBase);

public sealed record CuentaPropuestaDto(string Code, string Name);

public sealed record TotalesPropuestosDto(decimal Debit, decimal Credit);

public sealed record DocumentoExcluidoDto(Guid DocumentPublicId, string? Number, IReadOnlyList<ErrorDeExclusionDto> Errors);

public sealed record ErrorDeExclusionDto(int? LineNumber, string? Account, string Rule, QuienCorrigeDto? WhoFixes);
