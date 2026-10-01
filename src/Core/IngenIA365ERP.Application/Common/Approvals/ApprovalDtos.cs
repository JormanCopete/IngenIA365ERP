namespace IngenIA365ERP.Application.Common.Approvals;

// Las formas de contracts/api.md §15 (feature 012, T33, T34). Todo identificador es PublicId; un usuario sale por su
// nombre, nunca por SEC_Users.Id (Principio VI).

/// <summary>Un nivel de una política o de una solicitud (§15.1).</summary>
public sealed record NivelDto(int Order, decimal Threshold, string PermissionCode);

/// <summary>El tipo de documento de una política, descrito por el módulo. (nuevo)</summary>
public sealed record TipoDeDocumentoDeAprobacionDto(Guid PublicId, string Code, string Name, string? Class);

/// <summary><c>ApprovalPolicyDto</c> (§15.1).</summary>
public sealed record ApprovalPolicyDto(
    Guid PublicId,
    string Module,
    string Subject,
    TipoDeDocumentoDeAprobacionDto? DocumentType,
    int Version,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    IReadOnlyList<NivelDto> Levels,
    string? CreatedBy,
    DateTime CreatedAt);

/// <summary>Un rol por su PublicId y nombre. (nuevo)</summary>
public sealed record RolDto(Guid PublicId, string Name);

/// <summary>Un monto máximo de <c>/amount-limits</c> (§15.3). (nuevo)</summary>
public sealed record PermissionAmountLimitDto(
    Guid PublicId,
    RolDto Role,
    string PermissionCode,
    decimal? MaxAmount,
    string Currency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? CreatedBy,
    DateTime CreatedAt);

/// <summary>El tipo de documento de lo que se aprueba (<c>source.documentType</c>, §15.2). (nuevo)</summary>
public sealed record TipoDeOrigenDto(string Code, string Name);

/// <summary>
/// <c>source</c> de <c>ApprovalRequestDto</c> (§15.2): lo describe la fuente (<see cref="IFuenteDeAprobacion"/>); sin
/// fuente registrada, el motor lo arma con lo que la solicitud guarda. <c>Class</c> es la clase del documento como la
/// nombra el módulo; <c>Route</c>, la pantalla del documento. (nuevo)
/// </summary>
public sealed record OrigenDeAprobacionDto(
    Guid PublicId,
    string? Class,
    TipoDeOrigenDto? DocumentType,
    string? DisplayNumber,
    DateOnly OperationDate,
    Guid? Warehouse,
    Guid? PointOfSale,
    string Summary,
    string? Route);

/// <summary>Un nivel de una solicitud con su estado (<c>Waiting</c>, <c>Pending</c>, <c>Approved</c>, <c>Rejected</c>). (nuevo)</summary>
public sealed record NivelDeSolicitudDto(
    int Order,
    decimal Threshold,
    string PermissionCode,
    string State,
    string? DecidedBy,
    DateTime? DecidedAt,
    string? Method);

/// <summary>Una decisión del detalle (§15.2, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record DecisionDto(int Level, string Decision, string DecidedBy, DateTime DecidedAt, string? Reason, string Method);

/// <summary><c>ApprovalRequestDto</c> (§15.2); <see cref="Decisions"/> sólo en el detalle.</summary>
public sealed record ApprovalRequestDto(
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
    IReadOnlyList<DecisionDto>? Decisions);

/// <summary>El estado de la fuente tras decidir (<c>DecisionResultDto.source</c>). (nuevo)</summary>
public sealed record EstadoDeFuenteDto(Guid PublicId, string? Class, string? Status, string? DisplayNumber);

/// <summary><c>DecisionResultDto</c> (§15.2).</summary>
public sealed record DecisionResultDto(Guid RequestPublicId, string Status, int CurrentLevel, EstadoDeFuenteDto Source);

/// <summary>La respuesta de <c>presence-challenge</c> (§15.2). (nuevo)</summary>
public sealed record DesafioDePresenciaDto(Guid ChallengePublicId, DateTime ExpiresAt, IReadOnlyList<string> Methods, string? PublicKeyOptions);
