using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Connectors;

namespace IngenIA365ERP.Domain.Entities.Core.Connectors;

/// <summary>
/// La copia, en la base de la cooperativa, de un conector que el maestro le habilitó (<c>COR_Connectors</c>; feature 020, R15). La
/// escribe sólo <c>IProvisionadorDeConectores</c>: el consumidor de envíos lee de aquí y nunca de la base administrativa
/// (Principio IV). <see cref="IsEnabled"/> refleja la habilitación del maestro.
/// </summary>
public class Connector : AuditableEntity
{
    /// <summary>El <c>PublicId</c> del conector en la plataforma (<c>ADM_Connectors</c>).</summary>
    public Guid PlatformPublicId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int? CurrentVersionNumber { get; set; }

    public ICollection<ConnectorVersion> Versions { get; init; } = [];
}

/// <summary>Una versión replicada de un conector (<c>COR_ConnectorVersions</c>; R15). Hecho: no cambia.</summary>
public class ConnectorVersion : AuditableEntity, IHechoInmutable
{
    public int ConnectorId { get; init; }
    public Connector? Connector { get; init; }
    public int Number { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string DefinitionJson { get; init; } = string.Empty;
    public string DefinitionSha256 { get; init; } = string.Empty;
}

/// <summary>
/// Las credenciales de un conector en la cooperativa (<c>COR_ConnectorCredentials</c>; feature 020, R16; FR-042, FR-057). La carga útil
/// va cifrada con DataProtection y <b>nunca</b> sale: ninguna respuesta, exportación, auditoría ni registro la incluye. Es la única
/// excepción, explícita y nominal, a la regla de credenciales externas fuera de la base (decisión del dueño). La descifra sólo
/// <c>AlmacenDeCredencialesDeConector</c>.
/// </summary>
public class ConnectorCredential : AuditableEntity
{
    public int ConnectorId { get; set; }
    public Connector? Connector { get; set; }

    /// <summary>Los campos de credencial, cifrados. Nunca se muestra ni se audita.</summary>
    [NoAuditar]
    public string ProtectedPayload { get; set; } = string.Empty;

    public DateTime SetAt { get; set; }
    public string SetBy { get; set; } = string.Empty;
    public DateTime? LastVerifiedAt { get; set; }
    public bool? LastVerificationOk { get; set; }
    public string? LastVerificationMessage { get; set; }
}

/// <summary>
/// Un envío a un sistema externo (<c>COR_ConnectorRequests</c>; feature 020, R18; FR-049): el que muestra el tablero. Nace con la
/// <c>SolicitudDeConector</c> y lleva la versión con que salió, el identificador externo y el estado. El conector no conoce a quien
/// pidió el envío: <see cref="OriginRef"/> lo dice como texto (<c>TRN:{cargo}</c>).
/// </summary>
public class ConnectorRequest : AuditableEntity
{
    /// <summary>El <c>IntegrationMessage</c> de la solicitud; único.</summary>
    public Guid MessagePublicId { get; set; }
    public int ConnectorId { get; set; }
    public Connector? Connector { get; set; }
    public int VersionNumber { get; set; }
    public string OriginRef { get; set; } = string.Empty;
    public Guid ExternalId { get; set; }
    public ConnectorRequestStatus Status { get; set; } = ConnectorRequestStatus.Pending;
    public int Attempts { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastResponseExcerpt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Warning { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionReason { get; set; }

    public ICollection<ConnectorCall> Calls { get; init; } = [];
}

/// <summary>
/// Un intento de llamada de un envío (<c>COR_ConnectorCalls</c>; R18; FR-052): inicio de sesión u operación, con código, extracto de
/// la respuesta, duración y el cuerpo enviado <b>saneado</b> (sin variables sensibles ni cabeceras de autorización). Hecho: no cambia.
/// </summary>
public class ConnectorCall : AuditableEntity, IHechoInmutable
{
    public int RequestId { get; init; }
    public ConnectorRequest? Request { get; init; }
    public int AttemptNumber { get; init; }
    public DateTime StartedAt { get; init; }
    public int DurationMs { get; init; }
    public ConnectorCallKind Kind { get; init; }
    public int StatusCode { get; init; }
    public ConnectorOutcome Outcome { get; init; }
    public string? ResponseExcerpt { get; init; }
    public string? RequestBodySanitized { get; init; }
    public string? Url { get; init; }
}
