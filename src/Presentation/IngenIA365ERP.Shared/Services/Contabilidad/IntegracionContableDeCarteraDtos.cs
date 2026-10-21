namespace IngenIA365ERP.Shared.Services.Contabilidad;

// Feature 013, US7 (T0663; contracts/api.md §23, contracts/contabilidad.md §3.7, §10, §15, §21): los DTO espejo del lado contable de la
// integración con Cartera. Los enums llegan como número (DeliveryStatus, RegulatoryIndicatorKind, ActorKind, MotivoSinComprobante); el
// lote es el IntegrationBatchDto de la plataforma (LoteDeIntegracionDto de la 012) y las etiquetas las ponen TextosDeContabilidadDeCartera
// y TextosDeInventario. (nuevo)

// ================================================================================================================== matriz --

public sealed record ReferenciaDeReglaDeCarteraDto(Guid PublicId, string Code);

/// <summary>Las dimensiones de una regla guardada (§3.1): las que no fija valen «cualquiera».</summary>
public sealed record DimensionesDeReglaDeCarteraDto(
    string? CreditLineCode,
    string? ModalityCode,
    string? SubsegmentCode,
    string? RiskCategory,
    string? ConceptCode,
    bool? IsEmployee,
    bool? InGracePeriod,
    bool? OnBehalfOfAssociate,
    bool? Restructured,
    string? CollectorClass,
    string? ReasonCode,
    ReferenciaDeReglaDeCarteraDto? Branch,
    ReferenciaDeReglaDeCarteraDto? CostCenter,
    string? LineNature = null,
    string? DepositClass = null,
    string? IssueTermBand = null,
    string? ContributionClass = null,
    string? AccountFlag = null);

/// <summary>La cuenta de una regla y si hoy sigue siendo elegible para Cartera.</summary>
public sealed record CuentaDeReglaDeCarteraDto(Guid PublicId, string Code, string Name, bool IsEligible);

/// <summary><c>LendingPostingRuleDto</c> (§23.1). El hecho es «*» salvo en los roles de resultado.</summary>
public sealed record ReglaDeCarteraDto(
    Guid RulePublicId,
    string Operation,
    string Role,
    DimensionesDeReglaDeCarteraDto Dimensions,
    CuentaDeReglaDeCarteraDto Account,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Notes,
    string DimensionKey,
    int SpecificityWeight,
    bool IsCurrent,
    string? CreatedByName);

/// <summary>Un hecho de negocio del catálogo: sus roles al débito y al crédito, su modo y su tipo de comprobante por defecto.</summary>
public sealed record HechoDelCatalogoDeCarteraDto(
    string Code,
    string ShortCode,
    bool IsInformational,
    IReadOnlyList<string> DebitRoles,
    IReadOnlyList<string> CreditRoles,
    string DefaultMode,
    string? DefaultVoucherType,
    string Delivery);

/// <summary>
/// Un rol de cuenta: naturaleza (<c>Debit</c>/<c>Credit</c>), familia (<c>Saldo</c>, <c>Orden</c>, <c>PasivoConElDeudor</c>, <c>Pasivo</c>,
/// <c>Puente</c>, <c>Resultado</c>, <c>Diferido</c>, <c>Contingente</c>), las dimensiones exigidas y las admitidas por su nombre de la API
/// (<c>creditLineCode</c>…), los conceptos y motivos que admite y si admite fijar el hecho.
/// </summary>
public sealed record RolDelCatalogoDeCarteraDto(
    string Code,
    string Nature,
    string Family,
    IReadOnlyList<string> RequiredDimensions,
    IReadOnlyList<string> AllowedDimensions,
    IReadOnlyList<string>? Concepts,
    IReadOnlyList<string>? Reasons,
    bool AllowsOperation);

public sealed record LineaDelCatalogoDeCarteraDto(string Code, string Name, string ModalityCode, string? SubsegmentCode, bool Active,
    string LineNature = "Placement", string? DepositClass = null);

public sealed record ModalidadDelCatalogoDeCarteraDto(string Code, string Name, IReadOnlyList<string> SubsegmentCodes);

public sealed record DimensionesDelCatalogoDeCarteraDto(
    IReadOnlyList<LineaDelCatalogoDeCarteraDto> CreditLines,
    IReadOnlyList<ModalidadDelCatalogoDeCarteraDto> Modalities,
    IReadOnlyList<string> Concepts,
    IReadOnlyList<string> CollectorClasses,
    IReadOnlyList<string> RiskCategories,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string>? LineNatures = null,
    IReadOnlyList<string>? DepositClasses = null,
    IReadOnlyList<string>? IssueTermBands = null,
    IReadOnlyList<string>? ContributionClasses = null,
    IReadOnlyList<string>? AccountFlags = null);

/// <summary><c>GET /rules/catalog</c>: hechos y roles fijos y los valores de cada dimensión.</summary>
public sealed record CatalogoDeReglasDeCarteraDto(
    IReadOnlyList<HechoDelCatalogoDeCarteraDto> Operations,
    IReadOnlyList<RolDelCatalogoDeCarteraDto> Roles,
    DimensionesDelCatalogoDeCarteraDto Dimensions);

public sealed record AvisoDeReglaDeCarteraDto(string Code, string Message);

/// <summary>Lo que responde guardar una regla o una versión: su <c>PublicId</c> y los avisos (§3.6).</summary>
public sealed record ReglaGuardadaDeCarteraDto(Guid RulePublicId, IReadOnlyList<AvisoDeReglaDeCarteraDto> Warnings);

/// <summary>Las dimensiones que fija una regla nueva o que se consultan en «¿a qué cuenta va?» (sucursal y centro por <c>PublicId</c>).</summary>
public sealed record DimensionesDeReglaDeCarteraRequest(
    string? CreditLineCode = null,
    string? ModalityCode = null,
    string? SubsegmentCode = null,
    string? RiskCategory = null,
    string? ConceptCode = null,
    bool? IsEmployee = null,
    bool? InGracePeriod = null,
    bool? OnBehalfOfAssociate = null,
    bool? Restructured = null,
    string? CollectorClass = null,
    string? ReasonCode = null,
    Guid? BranchPublicId = null,
    Guid? CostCenterPublicId = null,
    string? LineNature = null,
    string? DepositClass = null,
    string? IssueTermBand = null,
    string? ContributionClass = null,
    string? AccountFlag = null);

/// <summary>Una regla nueva; <paramref name="Operation"/> nulo o «*» salvo en los roles de resultado.</summary>
public sealed record CrearReglaDeCarteraRequest(
    string? Operation,
    string Role,
    DimensionesDeReglaDeCarteraRequest Dimensions,
    Guid AccountPublicId,
    DateOnly ValidFrom,
    string? Notes,
    string Reason,
    DateOnly? ValidTo = null);

public sealed record VersionDeReglaDeCarteraRequest(Guid AccountPublicId, DateOnly ValidFrom, string? Notes, string Reason, Guid? CostCenterPublicId = null);

public sealed record DesactivarReglaDeCarteraRequest(DateOnly ValidTo, string Reason);

/// <summary>«¿A qué cuenta va esta combinación?» (§3.7): hecho, rol, dimensiones y fecha.</summary>
public sealed record ResolverReglaDeCarteraRequest(string Operation, string Role, DimensionesDeReglaDeCarteraRequest Dimensions, DateOnly? Date);

public sealed record CandidataDeReglaDeCarteraDto(Guid RulePublicId, string DimensionKey, int SpecificityWeight, bool Matched);

/// <summary>La regla ganadora con su cuenta y las demás candidatas del rol; sin regla, <c>MissingCode</c> y <c>MissingMessage</c> lo dicen.</summary>
public sealed record ResolucionDeReglaDeCarteraDto(
    ReglaDeCarteraDto? Rule,
    CuentaDeReglaDeCarteraDto? Account,
    IReadOnlyList<CandidataDeReglaDeCarteraDto> Candidates,
    string? MissingCode,
    string? MissingMessage);

/// <summary>Los filtros de la lista de reglas (§23.1).</summary>
public sealed record FiltroDeReglasDeCartera(
    string? Operacion = null,
    string? Rol = null,
    DateOnly? ALaFecha = null,
    string? Linea = null,
    string? Modalidad = null,
    string? Subsegmento = null,
    string? Categoria = null,
    string? Concepto = null,
    string? ClaseDeRecaudador = null,
    string? Cuenta = null,
    bool SoloVigentes = false,
    int Pagina = 1,
    int TamanoDePagina = 50);

// ============================================================================================ tipos de comprobante --

public sealed record TipoDelMapeoDeCarteraDto(Guid PublicId, string Code, string Name);

public sealed record CruceDelMapeoDeCarteraDto(Guid PublicId, string Code);

/// <summary><c>LendingVoucherMappingDto</c> (§23.2). <c>ReviewPending</c>: semilla que la contadora todavía no validó.</summary>
public sealed record MapeoDeComprobanteDeCarteraDto(
    Guid MappingPublicId,
    string Operation,
    string CollectorClass,
    TipoDelMapeoDeCarteraDto VoucherType,
    CruceDelMapeoDeCarteraDto? CrossDocumentType,
    bool IsSeeded,
    bool ReviewPending);

/// <summary>Agrega o reemplaza la fila de (hecho, clase de recaudador); guardarla es la conformidad de la contadora.</summary>
public sealed record MapeoDeComprobanteDeCarteraRequest(
    string Operation,
    string? CollectorClass,
    Guid VoucherTypePublicId,
    Guid? CrossDocumentTypePublicId,
    string Reason);

public sealed record MapeoFijadoDeCarteraDto(Guid MappingPublicId);

// ===================================================================================================== completitud --

/// <summary>Una combinación en uso sin regla vigente: el hecho, el rol, sus dimensiones y las líneas de crédito que la usan.</summary>
public sealed record ReglaFaltanteDeCarteraDto(
    string Operation,
    string Role,
    DimensionesDeReglaDeCarteraRequest Dimensions,
    IReadOnlyList<string> CreditLines,
    DateOnly? LastUsedAt,
    string? Nature = null)
{
    /// <summary>Feature 014 (T144, T149): la cuenta CUIF sugerida de una combinación de captaciones (informativa, pendiente de la contadora).</summary>
    public string? SuggestedAccount { get; init; }
}

public sealed record RecaudadorSinPuenteDto(string CollectorClass, IReadOnlyList<string> Collectors);

public sealed record ReglaDeCarteraNoElegibleDto(Guid RulePublicId, string Operation, string Role, string AccountCode, string Reason);

public sealed record HechoSinMapeoDto(string Operation, string? CollectorClass, string Code, string Message);

public sealed record AvisoDeCompletitudDeCarteraDto(string Code, string Message, string? AccountCode, Guid? RulePublicId);

public sealed record ResumenDeCompletitudDeCarteraDto(int Total, IReadOnlyDictionary<string, int> PorTipo);

/// <summary>La completitud de la matriz de Cartera a una fecha (§23.3): cuatro listas, el resumen y los avisos que no impiden nada.</summary>
public sealed record CompletitudDeCarteraDto(
    DateOnly Date,
    IReadOnlyList<ReglaFaltanteDeCarteraDto> MissingRules,
    IReadOnlyList<RecaudadorSinPuenteDto> CollectorsWithoutBridge,
    IReadOnlyList<ReglaDeCarteraNoElegibleDto> IneligibleRules,
    IReadOnlyList<HechoSinMapeoDto> UnmappedOperations,
    ResumenDeCompletitudDeCarteraDto Summary,
    IReadOnlyList<AvisoDeCompletitudDeCarteraDto> Warnings);

// ============================================================================================================ lotes --

/// <summary>Un hecho de un lote: su tipo, su origen, su estado de entrega (<c>DeliveryStatus</c>) y los créditos que reúne.</summary>
public sealed record HechoDelLoteDeCarteraDto(
    Guid MessagePublicId,
    string Type,
    Guid OriginPublicId,
    string? OriginNumber,
    DateOnly OperationDate,
    string? Branch,
    int Status,
    IReadOnlyList<string> Loans);

public sealed record ComprobanteDelLoteDeCarteraDto(
    Guid AccountingDocumentPublicId,
    string VoucherTypeCode,
    long? Number,
    DateOnly OperationDate,
    bool Summarized,
    int FactsCount,
    int LoansCount);

public sealed record RechazoDelLoteDeCarteraDto(Guid MessagePublicId, string Type, string? OriginNumber, string? Code, string? Message, QuienCorrigeDto? WhoFixes);

public sealed record DetalleDeLoteDeCarteraDto(
    LoteDeIntegracionDto Batch,
    IReadOnlyList<HechoDelLoteDeCarteraDto> Facts,
    IReadOnlyList<ComprobanteDelLoteDeCarteraDto> Vouchers,
    IReadOnlyList<RechazoDelLoteDeCarteraDto> Rejected);

public sealed record TotalesDelLoteEnDestinoDto(decimal Debit, decimal Credit);

/// <summary><c>GET /batches/{id}</c>: el detalle y los débitos y créditos de los comprobantes que dejaron sus recibos.</summary>
public sealed record LoteDeCarteraConTotalesDto(DetalleDeLoteDeCarteraDto Detail, TotalesDelLoteEnDestinoDto Totals);

public sealed record LineaPropuestaDeCarteraDto(string AccountCode, string AccountName, string? Branch, decimal Debit, decimal Credit, string? ThirdParty,
    string? CrossDocument);

/// <summary>Un comprobante propuesto: el grupo resumido (nulo si es por hecho), el tipo, la fecha contable, sus hechos y sus líneas.</summary>
public sealed record ComprobantePropuestoDeCarteraDto(
    string? GroupKey,
    string VoucherTypeCode,
    DateOnly Date,
    IReadOnlyList<Guid> Facts,
    IReadOnlyList<LineaPropuestaDeCarteraDto> Lines,
    decimal Debit,
    decimal Credit);

/// <summary>Un hallazgo: el hecho, el rol, la combinación, la cuenta, la regla y quién lo corrige (§13.3).</summary>
public sealed record HallazgoDeCarteraDto(
    string FactType,
    string? Role,
    IReadOnlyDictionary<string, string>? Combination,
    string? AccountCode,
    string Rule,
    string Message,
    QuienCorrigeDto? WhoFixes);

public sealed record HechoExcluidoDeCarteraDto(Guid OriginPublicId, IReadOnlyList<Guid> Facts, string Type, string? Number, IReadOnlyList<HallazgoDeCarteraDto> Errors);

/// <summary>La vista previa de un lote de Cartera (§10.5): el corte, los comprobantes propuestos y lo excluido.</summary>
public sealed record VistaPreviaDeLoteDeCarteraDto(
    Guid? CutoffMessagePublicId,
    IReadOnlyList<ComprobantePropuestoDeCarteraDto> Vouchers,
    IReadOnlyList<HechoExcluidoDeCarteraDto> Excluded);

/// <summary>El alcance de la vista previa: rango de fechas contables, horario (<c>CAR.…</c>) y sucursal. Los tipos de hecho no filtran.</summary>
public sealed record VistaPreviaDeLoteDeCarteraRequest(DateOnly From, DateOnly To, string? ScheduleKey, Guid? BranchPublicId);

/// <summary>La orden del lote manual: procesa exactamente lo previsualizado hasta <paramref name="CutoffMessagePublicId"/>.</summary>
public sealed record OrdenDeLoteDeCarteraRequest(
    Guid CutoffMessagePublicId,
    DateOnly From,
    DateOnly To,
    string? ScheduleKey,
    Guid? BranchPublicId,
    string Reason);

/// <summary>Los filtros de la lista de lotes; <c>Estado</c> y <c>Disparador</c> son los números de <c>BatchStatus</c> y <c>BatchTrigger</c>.</summary>
public sealed record FiltroDeLotesDeCartera(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? Estado = null,
    int? Disparador = null,
    int Pagina = 1,
    int TamanoDePagina = 20);

// ========================================================================================================== recibos --

/// <summary><c>LendingPostingDto</c> (§21.1): el recibo de un hecho y su comprobante. <c>WithoutVoucher</c>: 1 informativo, 2 valor cero, 3 no incorporado.</summary>
public sealed record ReciboDeCarteraDto(
    Guid MessagePublicId,
    string Type,
    int Version,
    Guid SourcePublicId,
    string? SourceDocumentClass,
    string? SourceDocumentNumber,
    string? LoanNumber,
    Guid? RelatedMessagePublicId,
    DateOnly OperationDate,
    Guid? AccountingDocumentPublicId,
    string? VoucherTypeCode,
    long? VoucherNumber,
    int? WithoutVoucher,
    Guid? BatchPublicId,
    int OriginActorKind,
    string OriginUserName,
    string ActorName,
    DateTime ProcessedAt);

// ====================================================================================================== indicadores --

/// <summary>Un indicador registrado. <c>Kind</c>: <c>RegulatoryIndicatorKind</c> (1 patrimonio técnico, 2 relación de solvencia).</summary>
public sealed record IndicadorRegulatorioDto(
    Guid RegulatoryIndicatorPublicId,
    int Kind,
    DateOnly CutoffDate,
    decimal Value,
    string Unit,
    string Source,
    Guid? SupportAttachmentPublicId,
    Guid? SupersedesPublicId,
    Guid? SupersededByPublicId,
    string? Notes,
    DateTime RegisteredAt,
    string RegisteredBy);

/// <summary>Un indicador con su vigencia (§23.6): <c>ExpiresOn</c> = corte + <c>Contabilidad.IndicadoresVigenciaMeses</c>.</summary>
public sealed record IndicadorConVigenciaDto(IndicadorRegulatorioDto Indicator, bool Current, DateOnly? ExpiresOn);

public sealed record IndicadoresConVigenciaDto(IReadOnlyList<IndicadorConVigenciaDto> Items, int? ValidityMonths);

/// <summary>
/// Registra un indicador: <c>Kind</c> por nombre (<c>TechnicalCapital</c>, <c>SolvencyRatio</c>); <c>PublicId</c> es el que la pantalla reservó
/// para subir el soporte; <c>SupersedesPublicId</c>, el del mismo corte que reemplaza.
/// </summary>
public sealed record RegistrarIndicadorRegulatorioRequest(
    string Kind,
    DateOnly CutoffDate,
    decimal Value,
    string Source,
    Guid? SupportAttachmentPublicId,
    Guid? SupersedesPublicId,
    string? Notes,
    Guid? PublicId);

public sealed record IndicadorRegistradoDto(Guid RegulatoryIndicatorPublicId);

// ================================================================================================= cierre del mes --

/// <summary>El <c>data</c> de <c>Accounting.Period.LendingPending</c> (api.md §24): los hechos de Cartera del mes que Contabilidad aún no recibe.</summary>
public sealed record PendientesDeCarteraDelMesDto(int Pending, int InBatch, int Rejected, DateOnly? OldestOperationDate, IReadOnlyList<string> Types);

/// <summary>El <c>data</c> de <c>Accounting.Period.LendingLoadWillExpire</c>: la carga de Cartera que el cierre dejaría sin activar.</summary>
public sealed record CargaDeCarteraQueVenceDto(DateOnly CutoffDate, Guid LoadPublicId);

public sealed record UltimoCierreDeCarteraDto(string Month, DateOnly Date);

/// <summary>El <c>data</c> de <c>Accounting.Period.LendingNotClosed</c>: Cartera activa desde una fecha sin cerrar el mes.</summary>
public sealed record CarteraSinCerrarDto(string Month, DateOnly ActivationDate, UltimoCierreDeCarteraDto? LastLendingClose);
