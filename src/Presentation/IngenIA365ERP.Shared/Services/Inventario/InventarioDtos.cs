namespace IngenIA365ERP.Shared.Services.Inventario;

// DTO espejo de la API de Inventario (feature 012, T183; contracts/api.md §2.9, §8, §9, §27 y contracts/plantillas.md
// §0.5). `Shared` no referencia `Application`, así que se duplican aquí con los mismos nombres de propiedad: el JSON es el
// contrato. Los enums salen de la API como número y aquí son `int` con sus etiquetas en `TextosDeInventario`; los que
// viajan por nombre (el modo y la acción de una importación, que llevan su propio convertidor) son `string`. Cada
// historia suma sus DTO en su propio archivo (`InventarioDtos.Seguridad.cs`…). (nuevo)

// ------------------------------------------------------------------------------------------ referencias --

/// <summary>Una entidad nombrada por su código (<c>ReferenciaDto</c>).</summary>
public sealed record ReferenciaDeInventarioDto(Guid PublicId, string Code, string Name);

/// <summary>Un usuario, nunca su Id interno (<c>UsuarioDto</c>).</summary>
public sealed record UsuarioDeInventarioDto(Guid? UserPublicId, string Name);

/// <summary>La contraparte (<c>ContraparteDto</c>).</summary>
public sealed record ContraparteDeInventarioDto(Guid PersonPublicId, string Name, string DocumentNumber);

/// <summary>Otro documento por su número visible (<c>DocumentoReferidoDto</c>).</summary>
public sealed record DocumentoReferidoDeInventarioDto(Guid PublicId, string? DisplayNumber);

/// <summary>Un aviso que no detiene nada (<c>AvisoDto</c>, §2.8).</summary>
public sealed record AvisoDeInventarioDto(string Code, string Message, System.Text.Json.JsonElement? Data);

/// <summary>Una página (<c>PagedResult</c>).</summary>
public sealed record PaginaDeInventarioDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

/// <summary>El cuerpo de las acciones que sólo piden motivo (descartar, inactivar, reactivar).</summary>
public sealed record MotivoDeInventarioRequest(string Reason);

// -------------------------------------------------------------------------------------- tipos de documento --

/// <summary>Una clase fija del sistema (<c>DocumentClassDto</c>, §8).</summary>
public sealed record ClaseDeDocumentoDto
{
    /// <summary><c>DocumentClass</c> (1..34).</summary>
    public int Class { get; init; }
    /// <summary><c>DocumentClassGroup</c>; nulo en las que no son de ningún grupo (la anulación).</summary>
    public int? Group { get; init; }
    public string EffectDescription { get; init; } = "";
    public bool IsFiscal { get; init; }
    /// <summary><c>FiscalDirection</c>: recibido o emitido.</summary>
    public int? FiscalDirection { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = [];
    /// <summary><c>PostingChain</c>.</summary>
    public int Chain { get; init; }
    /// <summary><c>NumberedBy</c>: consecutivo propio o resolución DIAN.</summary>
    public int NumberedBy { get; init; }
    public string AvailableFrom { get; init; } = "";
    /// <summary>Disponible en este despliegue.</summary>
    public bool Operable { get; init; }
}

/// <summary>Los campos que el tipo exige (<c>requiredFields</c>).</summary>
public sealed record CamposObligatoriosDelTipoDto(bool Counterparty, bool CostCenter, bool Reason, bool ExternalReference);

/// <summary>Un consecutivo del tipo, vigente o del historial.</summary>
public sealed record ConsecutivoDelTipoDto(Guid PublicId, string Prefix, long NextValue, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Un nivel de la política de aprobación vigente del tipo.</summary>
public sealed record NivelDePoliticaDelTipoDto(int Order, decimal Threshold, string PermissionCode);

/// <summary>La política de aprobación vigente (de sólo lectura aquí; se administra en §15.1).</summary>
public sealed record PoliticaDelTipoDto(Guid PublicId, int Version, IReadOnlyList<NivelDePoliticaDelTipoDto> Levels);

/// <summary>El modo de paso vigente (de sólo lectura aquí; se administra en §7). <c>Mode</c> es <c>PostingMode</c>.</summary>
public sealed record ModoDePasoDelTipoDto(int Mode, bool ModeInherited, int Chain, string Granularity, string BatchTrigger, string? BatchTime);

/// <summary><c>DocumentTypeDto</c> (§8); <see cref="Sequences"/> sólo en el detalle.</summary>
public sealed record TipoDeDocumentoDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public int Class { get; init; }
    public int? Group { get; init; }
    public bool IsFiscal { get; init; }
    public int NumberedBy { get; init; }
    public string Prefix { get; init; } = "";
    public CamposObligatoriosDelTipoDto RequiredFields { get; init; } = new(false, false, false, false);
    public IReadOnlyList<ReferenciaDeInventarioDto> Warehouses { get; init; } = [];
    public ReferenciaDeInventarioDto? SalesChannel { get; init; }
    public bool IsTaxableWithdrawal { get; init; }
    public bool VatNonDeductible { get; init; }
    public bool AllowsFutureDate { get; init; }
    public ConsecutivoDelTipoDto? CurrentSequence { get; init; }
    public PoliticaDelTipoDto? ApprovalPolicy { get; init; }
    public ModoDePasoDelTipoDto? Posting { get; init; }
    public bool IsSeeded { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<ConsecutivoDelTipoDto>? Sequences { get; init; }
}

/// <summary>El alta de un tipo (§8): la clase viaja por nombre (<c>PositiveAdjustment</c>) o número.</summary>
public sealed record CrearTipoDeDocumentoRequest(
    string Code,
    string Name,
    int Class,
    bool RequiresCounterparty,
    bool RequiresCostCenter,
    bool RequiresReason,
    bool RequiresExternalReference,
    IReadOnlyList<Guid>? WarehousePublicIds,
    Guid? SalesChannelPublicId,
    bool? IsTaxableWithdrawal,
    bool? VatNonDeductible,
    bool? AllowsFutureDate,
    string? Prefix,
    long? FirstNumber,
    DateOnly? ValidFrom);

/// <summary>La edición (§8): sin código, clase, prefijo, primer número ni vigencia.</summary>
public sealed record EditarTipoDeDocumentoRequest(
    string Name,
    bool RequiresCounterparty,
    bool RequiresCostCenter,
    bool RequiresReason,
    bool RequiresExternalReference,
    IReadOnlyList<Guid>? WarehousePublicIds,
    Guid? SalesChannelPublicId,
    bool? IsTaxableWithdrawal,
    bool? VatNonDeductible,
    bool? AllowsFutureDate);

/// <summary>Un consecutivo nuevo o un cambio de prefijo o de número (§8): cierra el anterior la víspera.</summary>
public sealed record ConsecutivoRequest(string? Prefix, long NextValue, DateOnly ValidFrom, string Reason);

// -------------------------------------------------------------------------------------------- documentos --

/// <summary>Los filtros de la lista genérica (§9.2).</summary>
public sealed record FiltroDeDocumentosDeInventario
{
    public int? Group { get; init; }
    public int? Class { get; init; }
    public Guid? DocumentTypePublicId { get; init; }
    public int? Status { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? WarehousePublicId { get; init; }
    public Guid? CounterpartyPersonPublicId { get; init; }
    public string? Number { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary><c>DocumentSummaryDto</c> (§9.2). <see cref="CostValue"/> sólo con <c>Inventory.Costs.Read</c>.</summary>
public sealed record ResumenDeDocumentoDto
{
    public Guid PublicId { get; init; }
    public int Class { get; init; }
    public int Group { get; init; }
    public ReferenciaDeInventarioDto DocumentType { get; init; } = new(Guid.Empty, "", "");
    public string Prefix { get; init; } = "";
    public long? Number { get; init; }
    public string? DisplayNumber { get; init; }
    /// <summary><c>DocumentStatus</c>: 0 borrador, 1 en aprobación, 2 confirmado, 3 anulado, 4 descartado.</summary>
    public int Status { get; init; }
    public DateOnly OperationDate { get; init; }
    public ReferenciaDeInventarioDto? Warehouse { get; init; }
    public ReferenciaDeInventarioDto? DestinationWarehouse { get; init; }
    public ContraparteDeInventarioDto? Counterparty { get; init; }
    public decimal? Total { get; init; }
    public decimal? CostValue { get; init; }
    public UsuarioDeInventarioDto CreatedBy { get; init; } = new(null, "");
    public DateTime CreatedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public UsuarioDeInventarioDto? ConfirmedBy { get; init; }
    public int? PostingMode { get; init; }
    public DocumentoReferidoDeInventarioDto? Voids { get; init; }
    public DocumentoReferidoDeInventarioDto? VoidedBy { get; init; }
}

/// <summary>Una línea del documento (§9.2).</summary>
public sealed record LineaDeDocumentoDto
{
    public Guid LinePublicId { get; init; }
    public int LineNumber { get; init; }
    public ReferenciaDeInventarioDto Product { get; init; } = new(Guid.Empty, "", "");
    public UnidadDeLineaDto Unit { get; init; } = new(Guid.Empty, "");
    public decimal Quantity { get; init; }
    public decimal Factor { get; init; }
    public decimal QuantityBase { get; init; }
    public decimal RoundingQuantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? DiscountAmount { get; init; }
    public decimal? LineSubtotal { get; init; }
    public decimal? UnitCost { get; init; }
    public decimal? TotalCost { get; init; }
    public ReferenciaDeInventarioDto? Location { get; init; }
    public ReferenciaDeInventarioDto? ToLocation { get; init; }
    public string? Lot { get; init; }
    public string? Serial { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public string? Notes { get; init; }
}

/// <summary>La unidad de una línea.</summary>
public sealed record UnidadDeLineaDto(Guid PublicId, string Code);

/// <summary>Los totales del documento (T26).</summary>
public sealed record TotalesDeDocumentoDto(decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal WithholdingTotal, decimal Total, decimal AmountDue, decimal? CostTotal);

/// <summary>
/// <c>InventoryDocumentDto</c> (§9.2): la cabecera, las líneas, los totales y lo que el servidor permite hacer
/// (<see cref="AllowedActions"/>: la pantalla no lo deduce). Los satélites que cada historia muestre (vínculos, foto
/// tributaria, aprobación, mensajes, adjuntos) los suma su DTO cuando los pinte.
/// </summary>
public sealed record DocumentoDeInventarioDto
{
    public Guid PublicId { get; init; }
    public int Class { get; init; }
    public int Group { get; init; }
    public ReferenciaDeInventarioDto DocumentType { get; init; } = new(Guid.Empty, "", "");
    public string Prefix { get; init; } = "";
    public long? Number { get; init; }
    public string? DisplayNumber { get; init; }
    public int Status { get; init; }
    public DateOnly OperationDate { get; init; }
    public UsuarioDeInventarioDto CreatedBy { get; init; } = new(null, "");
    public DateTime CreatedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public UsuarioDeInventarioDto? ConfirmedBy { get; init; }
    public ReferenciaDeInventarioDto? Warehouse { get; init; }
    public ReferenciaDeInventarioDto? DestinationWarehouse { get; init; }
    public ReferenciaDeInventarioDto? TransitWarehouse { get; init; }
    public ReferenciaDeInventarioDto Branch { get; init; } = new(Guid.Empty, "", "");
    public ReferenciaDeInventarioDto? CostCenter { get; init; }
    public ContraparteDeInventarioDto? Counterparty { get; init; }
    public string? ExternalReference { get; init; }
    public string? Reason { get; init; }
    public ReferenciaDeInventarioDto? AdjustmentCause { get; init; }
    public string? Notes { get; init; }
    public string Currency { get; init; } = "COP";
    public decimal ExchangeRate { get; init; } = 1m;
    public int? PostingMode { get; init; }
    public DocumentoReferidoDeInventarioDto? Voids { get; init; }
    public DocumentoReferidoDeInventarioDto? VoidedBy { get; init; }
    public byte[] RowVersion { get; init; } = [];
    public IReadOnlyList<LineaDeDocumentoDto> Lines { get; init; } = [];
    public TotalesDeDocumentoDto? Totals { get; init; }
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public IReadOnlyList<AvisoDeInventarioDto> Warnings { get; init; } = [];
}

/// <summary>Una línea del borrador (<c>SaveInventoryDraftLine</c>): con <see cref="LinePublicId"/> se conserva; sin él, es nueva.</summary>
public sealed record LineaDeBorradorRequest(
    Guid? LinePublicId,
    Guid ProductPublicId,
    Guid UnitPublicId,
    decimal Quantity,
    decimal? UnitPrice = null,
    decimal? DiscountPercent = null,
    decimal? DiscountAmount = null,
    decimal? UnitCost = null,
    Guid? LocationPublicId = null,
    Guid? ToLocationPublicId = null,
    Guid? SourceLinePublicId = null,
    string? LotCode = null,
    string? SerialNumber = null,
    DateOnly? ExpiryDate = null,
    string? Notes = null);

/// <summary>El cuerpo de <c>POST /</c> y <c>PUT /{id}</c> del ciclo común (<c>SaveInventoryDraftRequest</c>, §9.3).</summary>
public sealed record BorradorDeInventarioRequest(
    Guid DocumentTypePublicId,
    DateOnly? OperationDate,
    Guid? WarehousePublicId,
    Guid? DestinationWarehousePublicId,
    Guid? CostCenterPublicId,
    Guid? CounterpartyPersonPublicId,
    string? ExternalReference,
    string? Reason,
    Guid? AdjustmentCausePublicId,
    string? Notes,
    string? Currency,
    decimal? ExchangeRate,
    byte[]? RowVersion,
    IReadOnlyList<LineaDeBorradorRequest> Lines);

/// <summary>Un nivel de la aprobación que pidió la confirmación.</summary>
public sealed record NivelPedidoDeInventarioDto(int Order, decimal Threshold, string PermissionCode, string Status);

/// <summary>La aprobación que pidió la confirmación: <c>Reason</c> es <c>Policy</c> o <c>AmountLimit</c>.</summary>
public sealed record AprobacionPedidaDeInventarioDto(Guid RequestPublicId, string Reason, IReadOnlyList<NivelPedidoDeInventarioDto> Levels);

/// <summary><c>ConfirmationResultDto</c> (§9.3): <see cref="Status"/> 2 confirmado o 1 en aprobación.</summary>
public sealed record ResultadoDeConfirmacionDto
{
    public Guid PublicId { get; init; }
    public int Status { get; init; }
    public long? Number { get; init; }
    public string? DisplayNumber { get; init; }
    public DateOnly OperationDate { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public int? PostingMode { get; init; }
    public AprobacionPedidaDeInventarioDto? Approval { get; init; }
    public IReadOnlyList<AvisoDeInventarioDto> Warnings { get; init; } = [];
}

/// <summary><c>VoidResultDto</c> (§9.5). <see cref="CostAdjustments"/>: la diferencia de costo por producto (US2), sólo con <c>Inventory.Costs.Read</c>.</summary>
public sealed record ResultadoDeAnulacionDto(Guid VoidingDocumentPublicId, string? DisplayNumber, int Status, DateOnly OperationDate,
    IReadOnlyList<AjusteDeCostoDeAnulacionDto>? CostAdjustments = null);

/// <summary>El cuerpo de anular (§9.5): motivo y, si el tipo lo admite, otra fecha que hoy.</summary>
public sealed record AnulacionRequest(string Reason, DateOnly? OperationDate);

/// <summary>El cuerpo de confirmar (§9.3): la versión leída del borrador.</summary>
public sealed record ConfirmacionRequest(byte[]? RowVersion);

// ------------------------------------------------------------------------------------------- plantillas --

/// <summary>Una de las dieciséis plantillas de la parametrización (<c>ImportTemplateDto</c>, §3.9).</summary>
public sealed record PlantillaDeParametrizacionDto
{
    public int Number { get; init; }
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Sheets { get; init; } = [];
    public string BaseRoute { get; init; } = "";
    public string Command { get; init; } = "";
    public string ImportsFrom { get; init; } = "";
    public string DownloadsFrom { get; init; } = "";
    public bool CanDownload { get; init; }
    public bool CanImport { get; init; }
    public string DownloadPermission { get; init; } = "";
    public string ImportPermission { get; init; } = "";
    public string? AdditionalPermissions { get; init; }
    public string? Note { get; init; }
}

/// <summary>Conteos de una hoja de la importación.</summary>
public sealed record ResumenDeHojaDto(string Sheet, int Rows, int Created, int Updated, int Unchanged);

/// <summary>Un campo que cambia; <see cref="Before"/> es nulo al crear.</summary>
public sealed record CampoCambiadoDto(string Column, string? Before, string? After);

/// <summary>Una fila que crea o actualiza. <see cref="Action"/>: <c>Create</c>, <c>Update</c> o <c>Unchanged</c>.</summary>
public sealed record CambioDeFilaDto(string? Sheet, int Row, string Key, string Action, IReadOnlyList<CampoCambiadoDto> Fields);

/// <summary>Un error o aviso de importación: hoja (nula en plantillas de una hoja), fila de Excel (0 = no es de una fila) y columna.</summary>
public sealed record ErrorDeImportacionDto(int Row, string Column, string Code, string Message, string? Sheet);

/// <summary><c>ImportResultDto</c> (contracts/plantillas.md §0.5): lo que responde revisar y aplicar.</summary>
public sealed record ResultadoDeImportacionDto
{
    public string Template { get; init; } = "";
    /// <summary><c>Review</c> o <c>Apply</c>.</summary>
    public string Mode { get; init; } = "";
    public bool Valid { get; init; }
    public bool Applied { get; init; }
    public string FileName { get; init; } = "";
    public string FileSha256 { get; init; } = "";
    public bool RequiresReason { get; init; }
    public IReadOnlyList<ResumenDeHojaDto> Sheets { get; init; } = [];
    public IReadOnlyList<CambioDeFilaDto> Changes { get; init; } = [];
    public bool ChangesTruncated { get; init; }
    public IReadOnlyList<ErrorDeImportacionDto> Warnings { get; init; } = [];
    public IReadOnlyList<ErrorDeImportacionDto> Errors { get; init; } = [];
    public int TotalErrors { get; init; }

    /// <summary>
    /// Lo propio de cada plantilla (§8, §14, §15): la lee la pantalla de esa plantilla con <see cref="ExtraComo{T}"/> (US4, T320:
    /// <c>byWarehouse</c> del saldo inicial, <c>byDateWarehouseGroup</c> de las cifras de SOLIDO).
    /// </summary>
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? Extra { get; init; }

    /// <summary><see cref="Extra"/>[<paramref name="clave"/>] leído como <typeparamref name="T"/>; nulo si no vino.</summary>
    public T? ExtraComo<T>(string clave) =>
        Extra is not null && Extra.TryGetValue(clave, out var valor) && valor.ValueKind is not (System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined)
            ? System.Text.Json.JsonSerializer.Deserialize<T>(valor, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            : default;
}

// --------------------------------------------------------------------------------------------- informes --

/// <summary>Una vista publicada del centro de informes (<c>VistaDeInformeDeInventario</c>, §27).</summary>
public sealed record VistaDeInformeDto
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string FileName { get; init; } = "";
    /// <summary>Los filtros comunes que usa (<c>from</c>, <c>to</c>, <c>asOf</c>, <c>warehouse</c>…).</summary>
    public IReadOnlyList<string> Filters { get; init; } = [];
    public IReadOnlyList<string>? OwnFilters { get; init; }
    public bool PersonalData { get; init; }
    public string? PersonalDataWhen { get; init; }
    public string? RequiredPermission { get; init; }
}
