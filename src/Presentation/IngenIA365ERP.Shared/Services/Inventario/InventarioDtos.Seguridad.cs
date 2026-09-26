namespace IngenIA365ERP.Shared.Services.Inventario;

// Feature 012, T429 (US12): los espejos de contracts/api.md §7 (parámetros), §15 (aprobaciones, políticas y montos), §16
// (alertas, tipos y alcance) y §31 (vendedores). Los enums viajan como número (ParameterScopeKind, AlertChannels,
// ApprovalDecisionKind, ApprovalMethod); los estados derivados (Status, State, Severity, Type) son texto de presentación.

// ------------------------------------------------------------------------------------------------- §7 parámetros --

/// <summary>Una entidad de un ámbito (bodega, tipo de documento…) por su PublicId, código y nombre.</summary>
public sealed record AmbitoDeParametroDto(Guid PublicId, string Code, string Name);

public sealed record ValorVigenteDeParametroDto(
    string Value,
    string Source,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? Reason,
    string? LegalSource,
    string? ChangedBy,
    DateTime? ChangedAt);

public sealed record ExcepcionDeParametroDto(int ScopeKind, AmbitoDeParametroDto? Scope, string Value, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record ParametroProgramadoDto(int ScopeKind, AmbitoDeParametroDto? Scope, string Value, DateOnly ValidFrom);

/// <summary><c>ParameterDto</c> (§7): la clave, su valor vigente, las excepciones por ámbito y lo programado.</summary>
public sealed record ParametroDto(
    string Module,
    string Key,
    string Description,
    string Type,
    IReadOnlyList<string>? AllowedValues,
    string DefaultValue,
    IReadOnlyList<int> AllowedScopes,
    bool SealedOnConfirm,
    string? RequiredPermission,
    bool RequiresLegalSource,
    string AvailableFrom,
    ValorVigenteDeParametroDto Current,
    IReadOnlyList<ExcepcionDeParametroDto> Overrides,
    IReadOnlyList<ParametroProgramadoDto> Scheduled);

/// <summary>Una vigencia del historial de una clave (§7), la más reciente primero.</summary>
public sealed record VigenciaDeParametroDto(
    Guid VersionPublicId,
    int ScopeKind,
    AmbitoDeParametroDto? Scope,
    string Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? LegalSource,
    string? CreatedBy,
    DateTime CreatedAt);

/// <summary>El cuerpo de <c>POST /parameters/{module}/{key}/versions</c> (§7). <c>ScopeKind</c> 0 = general.</summary>
public sealed record NuevaVigenciaDeParametroRequest(
    int ScopeKind,
    Guid? ScopePublicId,
    string? Chain,
    string Value,
    DateOnly ValidFrom,
    string Reason,
    string? LegalSource,
    bool? ConfirmFiscalWithoutPosting);

public sealed record VigenciaCreadaDto(IReadOnlyList<Guid> VersionPublicIds, DateOnly? PreviousClosedOn, IReadOnlyList<AmbitoDeParametroDto>? AffectedDocumentTypes);

/// <summary>Un tipo de documento fiscal de <c>data.fiscalDocumentTypes</c> (<c>Inventory.PostingMode.FiscalRequiresConfirmation</c>).</summary>
public sealed record TipoFiscalDto(Guid PublicId, string Code, string Name, string? Class);

// ------------------------------------------------------------------------------- §15 aprobaciones, políticas, montos --

public sealed record NivelDeAprobacionDto(int Order, decimal Threshold, string PermissionCode);

public sealed record TipoDeDocumentoDePoliticaDto(Guid PublicId, string Code, string Name, string? Class);

public sealed record PoliticaDeAprobacionDto(
    Guid PublicId,
    string Module,
    string Subject,
    TipoDeDocumentoDePoliticaDto? DocumentType,
    int Version,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    IReadOnlyList<NivelDeAprobacionDto> Levels,
    string? CreatedBy,
    DateTime CreatedAt);

public sealed record GuardarPoliticaDeAprobacionRequest(
    string Subject,
    Guid? DocumentTypePublicId,
    DateOnly ValidFrom,
    string Reason,
    IReadOnlyList<NivelDeAprobacionDto> Levels);

public sealed record RolDeMontoDto(Guid PublicId, string Name);

/// <summary>Un rol de <c>GET /api/admin/roles</c>, lo que la pantalla de montos necesita.</summary>
public sealed record RolDeCooperativaDto(Guid PublicId, string Code, string Name, bool IsActive);

public sealed record MontoMaximoDto(
    Guid PublicId,
    RolDeMontoDto Role,
    string PermissionCode,
    decimal? MaxAmount,
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? CreatedBy,
    DateTime CreatedAt);

/// <summary>El cuerpo de <c>POST /amount-limits</c> (§15.3); <c>MaxAmount</c> nulo = sin límite.</summary>
public sealed record FijarMontoMaximoRequest(Guid RolePublicId, string PermissionCode, decimal? MaxAmount, DateOnly ValidFrom, string Reason);

public sealed record TipoDeOrigenDeAprobacionDto(string Code, string Name);

public sealed record OrigenDeAprobacionDto(
    Guid PublicId,
    string? Class,
    TipoDeOrigenDeAprobacionDto? DocumentType,
    string? DisplayNumber,
    DateOnly OperationDate,
    Guid? Warehouse,
    Guid? PointOfSale,
    string Summary,
    string? Route);

public sealed record NivelDeSolicitudDto(int Order, decimal Threshold, string PermissionCode, string State, string? DecidedBy, DateTime? DecidedAt, string? Method);

public sealed record DecisionDeAprobacionDto(int Level, string Decision, string DecidedBy, DateTime DecidedAt, string? Reason, string Method);

/// <summary><c>ApprovalRequestDto</c> (§15.2); <c>Decisions</c> sólo en el detalle.</summary>
public sealed record SolicitudDeAprobacionDto(
    Guid PublicId,
    string Module,
    string Subject,
    string SourceType,
    OrigenDeAprobacionDto Source,
    decimal Amount,
    string Currency,
    string Status,
    int CurrentLevel,
    IReadOnlyList<NivelDeSolicitudDto> Levels,
    string? CreatedBy,
    string? RequestedBy,
    DateTime RequestedAt,
    string ContentSha256,
    bool CanDecide,
    string? ExcludedReason,
    IReadOnlyList<DecisionDeAprobacionDto>? Decisions);

/// <summary>El cuerpo de <c>decide</c> (§15.2). <c>Decision</c>: 1 aprobar, 2 rechazar; <c>Method</c>: 1 sesión propia.</summary>
public sealed record DecidirAprobacionRequest(int Decision, string? Reason, int Method, string? ExpectedContentSha256);

public sealed record EstadoDeFuenteDto(Guid PublicId, string? Class, string? Status, string? DisplayNumber);

public sealed record ResultadoDeDecisionDto(Guid RequestPublicId, string Status, int CurrentLevel, EstadoDeFuenteDto Source);

// ------------------------------------------------------------------------------------- §16 alertas, tipos, alcance --

public sealed record EntidadDeAlertaDto(string? Type, Guid PublicId, string? Label, string? Route);

public sealed record AlertaDto(
    Guid PublicId,
    string TypeCode,
    string Module,
    string Severity,
    string Subject,
    string Body,
    EntidadDeAlertaDto? Entity,
    string Status,
    DateTime RaisedAt,
    string RaisedBy,
    int OccurrenceCount,
    DateTime LastOccurredAt,
    DateTime? AttendedAt,
    string? AttendedBy,
    string? AttendNote,
    bool WithoutRecipient);

public sealed record TipoDeAlertaDto(
    string TypeCode,
    string Module,
    string Description,
    string Severity,
    IReadOnlyList<string> RecipientPermissions,
    IReadOnlyList<string> Channels,
    IReadOnlyDictionary<string, decimal>? Thresholds,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Reason,
    bool IsEnabled,
    int ActiveRecipients,
    bool WithoutRecipient,
    string AvailableFrom);

/// <summary>El cuerpo de una versión de tipo (§16.2). <c>Channels</c>: 1 aplicación (siempre), 2 correo.</summary>
public sealed record VersionDeTipoDeAlertaRequest(
    IReadOnlyList<string> RecipientPermissions,
    IReadOnlyList<int> Channels,
    IReadOnlyDictionary<string, decimal>? Thresholds,
    DateOnly ValidFrom,
    string Reason,
    bool IsEnabled);

public sealed record FiltrosDeAlertas(string? Status = null, string? TypeCode = null, string? Severity = null, DateOnly? From = null, DateOnly? To = null,
    int Page = 1, int PageSize = 50);

public sealed record UsuarioDeAlcanceDto(Guid PublicId, string Name, string? Email);

public sealed record BodegaDeAlcanceDto(Guid WarehousePublicId, string Code, string Name, bool IsDefault);

public sealed record PuntoDeAlcanceDto(Guid PointOfSalePublicId, string Code, string Name, bool IsDefault);

/// <summary>El alcance comercial de un usuario (§16.3).</summary>
public sealed record AlcanceComercialDto(
    UsuarioDeAlcanceDto User,
    bool HasAllWarehouses,
    bool HasAllPointsOfSale,
    IReadOnlyList<BodegaDeAlcanceDto> Warehouses,
    IReadOnlyList<PuntoDeAlcanceDto> PointsOfSale);

public sealed record BodegaPedidaDto(Guid WarehousePublicId, bool IsDefault);

/// <summary>El cuerpo del <c>PUT</c> (§16.3); sin <c>PointsOfSale</c> no toca los puntos (llegan con I3).</summary>
public sealed record FijarAlcanceRequest(IReadOnlyList<BodegaPedidaDto> Warehouses);

// ---------------------------------------------------------------------------------------------- §31 vendedores --

public sealed record PersonaDelVendedorDto(Guid PersonPublicId, string Name, string IdNumber);

public sealed record VendedorDto(Guid SalespersonPublicId, PersonaDelVendedorDto Person, int? SalespersonType, bool AppliesCommission, bool IsActive);

public sealed record CrearVendedorRequest(Guid PersonPublicId, int? SalespersonType, bool? AppliesCommission, string? Reason);

public sealed record VendedorCreadoDto(Guid SalespersonPublicId, bool Restored);

public sealed record ActualizarVendedorRequest(int? SalespersonType, bool AppliesCommission, string? Reason);

// ---------------------------------------------------------------------------------- textos de presentación --

/// <summary>Los textos en español de lo que la API devuelve como código (US12). (nuevo)</summary>
public static class TextosDeSeguridad
{
    /// <summary>Por qué quien consulta no puede decidir (<c>excludedReason</c>, §15.2).</summary>
    public static string Exclusion(string? motivo) => motivo switch
    {
        null or "" => string.Empty,
        "Creator" => "Usted creó el documento: quien crea no aprueba.",
        "Requester" => "Usted pidió la aprobación: quien la pide no la decide.",
        "Participant" => "Usted participó en el documento (lo abrió o lo contó).",
        "PreviousLevel" => "Usted ya aprobó otro nivel de esta solicitud.",
        "MissingPermission" => "No tiene el permiso de este nivel.",
        "OutOfScope" => "El documento es de una bodega fuera de su alcance.",
        _ => motivo,
    };

    public static string EstadoDeSolicitud(string estado) => estado switch
    {
        "Pending" => "Pendiente",
        "Approved" => "Aprobada",
        "Rejected" => "Rechazada",
        "Cancelled" => "Retirada",
        _ => estado,
    };

    public static string EstadoDeNivel(string estado) => estado switch
    {
        "Waiting" => "En espera",
        "Pending" => "Pendiente",
        "Approved" => "Aprobado",
        "Rejected" => "Rechazado",
        _ => estado,
    };

    public static string Severidad(string severidad) => severidad switch
    {
        "Info" => "Información",
        "Warning" => "Advertencia",
        "Critical" => "Crítica",
        _ => severidad,
    };

    public static string EstadoDeAlerta(string estado) => estado switch
    {
        "Pending" => "Pendiente",
        "Attended" => "Atendida",
        _ => estado,
    };

    public static string Ambito(int scopeKind) => scopeKind switch
    {
        0 => "General",
        1 => "Bodega",
        2 => "Tipo de documento",
        3 => "Punto de venta",
        4 => "Caja",
        5 => "Clase de tercero",
        _ => scopeKind.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Un hallazgo de la verificación de integridad de la auditoría (§29).</summary>
    public static string HallazgoDeIntegridad(string kind) => kind switch
    {
        "Altered" => "Alterado: el evento no coincide con su sello.",
        "Deleted" => "Eliminado: falta el evento de esa posición.",
        "Interleaved" => "Intercalado: otro evento ocupa esa posición.",
        "AnchorInvalid" => "Ancla inválida: la firma del ancla no corresponde a la cadena.",
        "PurgedByRetention" => "Depurado por retención: venció su plazo de diez años.",
        _ => kind,
    };

    /// <summary>Los sujetos de una política de aprobación (data-model, <c>ApprovalSubjects</c>).</summary>
    public static IReadOnlyList<(string Codigo, string Nombre)> Sujetos { get; } =
    [
        ("DocumentConfirmation", "Confirmar un documento"),
        ("DiscountOverCap", "Descuento sobre el tope"),
        ("ProvisionalCredit", "Crédito provisional"),
        ("TransferDiscrepancy", "Diferencia de traslado"),
        ("PurchaseMatchException", "Excepción del cruce de compras"),
    ];
}
