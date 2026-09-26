namespace IngenIA365ERP.Shared.Services.Contabilidad;

// Feature 012, US7 (T532; contracts/api.md §26): los DTO espejo del lado contable de la integración con Inventario. Los enums
// llegan como número (BatchStatus, BatchTrigger, PostingGranularity, ActorKind, WarehouseBehavior); las etiquetas las pone
// TextosDeInventario. (nuevo)

// ================================================================================================================== matriz --

public sealed record ReferenciaDeReglaDeInventarioDto(Guid PublicId, string Code);

public sealed record DimensionesDeReglaDeInventarioDto(
    string? AccountingGroupCode,
    string? WarehouseCode,
    string? PointOfSaleCode,
    string? PaymentMeansCode,
    string? TaxRateCode,
    decimal? TaxRate,
    string? ReasonCode,
    ReferenciaDeReglaDeInventarioDto? Branch,
    ReferenciaDeReglaDeInventarioDto? CostCenter);

public sealed record CuentaDeReglaDeInventarioDto(Guid PublicId, string Code, string Name, bool IsEligible);

/// <summary><c>InventoryPostingRuleDto</c> (§26.1).</summary>
public sealed record ReglaDeInventarioDto(
    Guid RulePublicId,
    string Operation,
    string Role,
    DimensionesDeReglaDeInventarioDto Dimensions,
    CuentaDeReglaDeInventarioDto Account,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Notes,
    string DimensionKey,
    short Specificity,
    bool IsCurrent,
    string? CreatedByName);

public sealed record OperacionDelCatalogoDeInventarioDto(string Code, string Message, IReadOnlyList<string> DebitRoles, IReadOnlyList<string> CreditRoles,
    IReadOnlyList<string> RequiredRoles);

public sealed record RolDelCatalogoDeInventarioDto(string Code, IReadOnlyList<string> RequiredDimensions, IReadOnlyList<string> AllowedDimensions,
    IReadOnlyList<string> FixedReasons);

public sealed record CodigoDeDimensionDeInventarioDto(string Code, string Name);

/// <summary><c>Behavior</c>: <c>WarehouseBehavior</c> (1 operativa, 2 tránsito).</summary>
public sealed record BodegaDelCatalogoDeInventarioDto(string Code, string Name, string? BranchCode, int Behavior);

public sealed record TarifaDelCatalogoDeInventarioDto(string Code, string Name, string Kind, decimal? Rate, decimal? AmountPerUnit, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record DimensionesDelCatalogoDeInventarioDto(
    IReadOnlyList<CodigoDeDimensionDeInventarioDto> AccountingGroups,
    IReadOnlyList<BodegaDelCatalogoDeInventarioDto> Warehouses,
    IReadOnlyList<CodigoDeDimensionDeInventarioDto> PointsOfSale,
    IReadOnlyList<CodigoDeDimensionDeInventarioDto> PaymentMeans,
    IReadOnlyList<TarifaDelCatalogoDeInventarioDto> TaxRates,
    IReadOnlyList<CodigoDeDimensionDeInventarioDto> Reasons);

/// <summary><c>GET /rules/catalog</c>: operaciones y roles fijos, y los valores de cada dimensión.</summary>
public sealed record CatalogoDeReglasDeInventarioDto(
    IReadOnlyList<OperacionDelCatalogoDeInventarioDto> Operations,
    IReadOnlyList<RolDelCatalogoDeInventarioDto> Roles,
    DimensionesDelCatalogoDeInventarioDto Dimensions);

public sealed record AvisoDeReglaDeInventarioDto(string Code, string Message);

/// <summary>Lo que responde guardar una regla o una versión: su <c>PublicId</c> y los avisos (C8).</summary>
public sealed record ReglaGuardadaDeInventarioDto(Guid RulePublicId, IReadOnlyList<AvisoDeReglaDeInventarioDto> Warnings);

/// <summary>Las dimensiones que fija una regla nueva (sucursal y centro por <c>PublicId</c>).</summary>
public sealed record DimensionesDeReglaDeInventarioRequest(
    string? AccountingGroupCode = null,
    string? WarehouseCode = null,
    string? PointOfSaleCode = null,
    string? PaymentMeansCode = null,
    string? TaxRateCode = null,
    decimal? TaxRate = null,
    string? ReasonCode = null,
    Guid? BranchPublicId = null,
    Guid? CostCenterPublicId = null);

public sealed record CrearReglaDeInventarioRequest(
    string Operation,
    string Role,
    DimensionesDeReglaDeInventarioRequest Dimensions,
    Guid AccountPublicId,
    DateOnly ValidFrom,
    string? Notes,
    string Reason,
    DateOnly? ValidTo = null);

public sealed record VersionDeReglaDeInventarioRequest(Guid AccountPublicId, DateOnly ValidFrom, string? Notes, string Reason);

public sealed record DesactivarReglaDeInventarioRequest(DateOnly ValidTo, string Reason);

/// <summary>Los filtros de la lista de reglas (§26.1).</summary>
public sealed record FiltroDeReglasDeInventario(
    string? Operacion = null,
    string? Rol = null,
    DateOnly? ALaFecha = null,
    string? GrupoContable = null,
    string? Bodega = null,
    string? PuntoDeVenta = null,
    string? MedioDePago = null,
    string? Tarifa = null,
    string? Causa = null,
    string? Cuenta = null,
    bool SoloVigentes = false,
    int Pagina = 1,
    int TamanoDePagina = 50);

// ============================================================================================ tipos de comprobante --

public sealed record TipoDelMapeoDeInventarioDto(Guid PublicId, string Code, string Name);

public sealed record CruceDelMapeoDeInventarioDto(Guid PublicId, string Code);

/// <summary><c>InventoryVoucherMappingDto</c> (§26.2).</summary>
public sealed record MapeoDeComprobanteDeInventarioDto(
    Guid MappingPublicId,
    string Operation,
    string? InventoryDocumentTypeCode,
    TipoDelMapeoDeInventarioDto VoucherType,
    CruceDelMapeoDeInventarioDto? CrossDocumentType,
    bool IsSeeded);

public sealed record MapeoDeComprobanteDeInventarioRequest(
    string Operation,
    string? InventoryDocumentTypeCode,
    Guid VoucherTypePublicId,
    Guid? CrossDocumentTypePublicId,
    string Reason);

public sealed record MapeoFijadoDeInventarioDto(Guid MappingPublicId);

// ===================================================================================================== completitud --

public sealed record ReglaFaltanteDeInventarioDto(
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

public sealed record MedioSinCuentaDeInventarioDto(string PaymentMeansCode, string Name, string? PointOfSaleCode);

public sealed record ReglaNoElegibleDeInventarioDto(Guid RulePublicId, string Operation, string Role, string Account, string Reason);

public sealed record TarifaDistintaDeInventarioDto(string TaxRateCode, decimal CatalogRate, string Account, decimal? AccountRate, DateOnly From, DateOnly? To);

public sealed record OperacionSinMapeoDeInventarioDto(string Operation, string? InventoryDocumentTypeCode);

public sealed record ResumenDeCompletitudDeInventarioDto(int Total, IReadOnlyDictionary<string, int> ByKind);

public sealed record AvisoDeCompletitudDeInventarioDto(string Kind, string Message, string? Account, Guid? RulePublicId);

/// <summary>La completitud de la matriz a una fecha (§26.3): cinco listas, el resumen y los avisos que no impiden nada.</summary>
public sealed record CompletitudDeInventarioDto(
    DateOnly Date,
    IReadOnlyList<ReglaFaltanteDeInventarioDto> MissingRules,
    IReadOnlyList<MedioSinCuentaDeInventarioDto> PaymentMeansWithoutAccount,
    IReadOnlyList<ReglaNoElegibleDeInventarioDto> IneligibleRules,
    IReadOnlyList<TarifaDistintaDeInventarioDto> TaxRateMismatches,
    IReadOnlyList<OperacionSinMapeoDeInventarioDto> UnmappedOperations,
    ResumenDeCompletitudDeInventarioDto Summary,
    IReadOnlyList<AvisoDeCompletitudDeInventarioDto>? Warnings = null);

// ============================================================================================================ lotes --

/// <summary><c>Kind</c>: <c>ActorKind</c> (1 persona, 2 proceso).</summary>
public sealed record SolicitanteDelLoteDeIntegracionDto(int Kind, string? Name);

public sealed record PeriodoDelLoteDeIntegracionDto(int Year, int Month);

public sealed record TotalesDelLoteDeIntegracionDto(int Messages, int Documents, int Vouchers, int Rejected, decimal Debit, decimal Credit);

/// <summary><c>IntegrationBatchDto</c> (§26.4). <c>Trigger</c>, <c>Status</c> y <c>Granularity</c> llegan como número.</summary>
public sealed record LoteDeIntegracionDto(
    Guid BatchPublicId,
    long Number,
    string Destination,
    int Trigger,
    string? ScheduleKey,
    DateTime? ScheduledFor,
    Guid? CashSessionPublicId,
    PeriodoDelLoteDeIntegracionDto? Period,
    DateOnly? DateFrom,
    DateOnly? DateTo,
    int? Granularity,
    int Status,
    SolicitanteDelLoteDeIntegracionDto RequestedBy,
    DateTime RequestedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    TotalesDelLoteDeIntegracionDto Totals,
    bool Late);

public sealed record QuienCorrigeDto(string Module, string? Page, string? Permission);

public sealed record DocumentoDelLoteDeIntegracionDto(
    Guid DocumentPublicId,
    string? Class,
    string? DocumentTypeCode,
    string? Number,
    DateOnly OperationDate,
    string? Branch,
    decimal Total);

public sealed record ComprobanteDelLoteDeIntegracionDto(
    Guid AccountingDocumentPublicId,
    string VoucherTypeCode,
    long? Number,
    DateOnly OperationDate,
    string? Branch,
    string? CostCenter,
    int Granularity,
    int DocumentsCount);

public sealed record RechazoDelLoteDeIntegracionDto(Guid MessagePublicId, string? DocumentNumber, string? Code, string? Message, QuienCorrigeDto? WhoFixes);

/// <summary>El detalle de un lote: el lote, sus documentos, sus comprobantes y sus rechazos con quién los corrige.</summary>
public sealed record DetalleDeLoteDeIntegracionDto(
    LoteDeIntegracionDto Batch,
    IReadOnlyList<DocumentoDelLoteDeIntegracionDto> Documents,
    IReadOnlyList<ComprobanteDelLoteDeIntegracionDto> Vouchers,
    IReadOnlyList<RechazoDelLoteDeIntegracionDto> Rejected);

public sealed record CuentaPropuestaDeInventarioDto(string Code, string Name);

public sealed record LineaPropuestaDeInventarioDto(
    CuentaPropuestaDeInventarioDto Account,
    decimal Debit,
    decimal Credit,
    string? ThirdParty,
    string? CrossDocument,
    decimal? TaxBase);

public sealed record TotalesPropuestosDeInventarioDto(decimal Debit, decimal Credit);

public sealed record ComprobantePropuestoDeInventarioDto(
    string VoucherTypeCode,
    DateOnly OperationDate,
    string? Branch,
    string? CostCenter,
    int Granularity,
    int DocumentsCount,
    IReadOnlyList<LineaPropuestaDeInventarioDto> Lines,
    TotalesPropuestosDeInventarioDto Totals);

public sealed record ErrorDeExclusionDeInventarioDto(int? LineNumber, string? Account, string Rule, QuienCorrigeDto? WhoFixes);

public sealed record DocumentoExcluidoDeInventarioDto(Guid DocumentPublicId, string? Number, IReadOnlyList<ErrorDeExclusionDeInventarioDto> Errors);

/// <summary><c>InventoryBatchPreviewDto</c> (§26.4): lo que haría el lote, sin numerar ni guardar, con su corte.</summary>
public sealed record VistaPreviaDeLoteDeIntegracionDto(
    Guid? CutoffMessagePublicId,
    IReadOnlyList<DocumentoDelLoteDeIntegracionDto> Documents,
    IReadOnlyList<ComprobantePropuestoDeInventarioDto> Vouchers,
    IReadOnlyList<DocumentoExcluidoDeInventarioDto> Excluded);

public sealed record VistaPreviaDeLoteDeIntegracionRequest(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string>? DocumentTypeCodes,
    string? ScheduleKey,
    Guid? BranchPublicId);

/// <summary>La orden del lote manual: procesa exactamente lo previsualizado hasta <paramref name="CutoffMessagePublicId"/>.</summary>
public sealed record OrdenDeLoteDeIntegracionRequest(
    Guid CutoffMessagePublicId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string>? DocumentTypeCodes,
    string? ScheduleKey,
    Guid? BranchPublicId,
    string Reason);

/// <summary>El 202 de ordenar un lote, reprocesar o enviar después: el lote que corre en segundo plano.</summary>
public sealed record LoteOrdenadoDeIntegracionDto(Guid BatchPublicId, long Number, int? Status);

/// <summary>Los filtros de la lista de lotes; <c>Estado</c> y <c>Disparador</c> son los números de <c>BatchStatus</c> y <c>BatchTrigger</c>.</summary>
public sealed record FiltroDeLotesDeIntegracion(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? Estado = null,
    int? Disparador = null,
    string? Destino = null,
    int Pagina = 1,
    int TamanoDePagina = 20);

// ========================================================================================================== recibos --

public sealed record ComprobanteDelReciboDeInventarioDto(string TypeCode, long? Number);

public sealed record ActorDelReciboDeInventarioDto(int Kind, string Name);

/// <summary><c>InventoryPostingDto</c> (§26.5): el vínculo documento ↔ comprobante. <c>WithoutVoucher</c>: 1 informativo, 2 valor cero.</summary>
public sealed record ReciboDeContabilizacionDto(
    Guid MessagePublicId,
    string Type,
    int Version,
    Guid SourceDocumentPublicId,
    Guid? RelatedDocumentPublicId,
    DateOnly OperationDate,
    Guid? AccountingDocumentPublicId,
    ComprobanteDelReciboDeInventarioDto? Voucher,
    int? WithoutVoucher,
    Guid? BatchPublicId,
    string OriginUserName,
    ActorDelReciboDeInventarioDto Actor,
    DateTime PostedAt);
