using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Un conector externo HTTP/JSON definido por el maestro (<c>ADM_Connectors</c>; feature 020, R15; FR-035). La definición vive en sus
/// versiones, que son hechos: cambiarla publica una versión nueva (FR-039). Inactivo = ninguna cooperativa lo puede usar.
/// </summary>
public class PlatformConnector : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>El número de la versión vigente (la última publicada); nulo hasta publicar la primera.</summary>
    public int? CurrentVersionNumber { get; set; }

    public ICollection<PlatformConnectorVersion> Versions { get; init; } = [];
}

/// <summary>
/// Una versión publicada de un conector (<c>ADM_ConnectorVersions</c>; R15): URL base y definición (autenticación, operación, campos de
/// credencial, mapa de respuestas, intentos y tiempo de espera) validada contra <c>DefinicionDeConector</c>. No lleva credenciales.
/// No se modifica ni se borra.
/// </summary>
public class PlatformConnectorVersion : AuditableEntity, IHechoInmutable
{
    public int ConnectorId { get; init; }
    public PlatformConnector? Connector { get; init; }
    public int Number { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string DefinitionJson { get; init; } = string.Empty;
    public string DefinitionSha256 { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
    public string PublishedBy { get; init; } = string.Empty;
    public string? Note { get; init; }
}

/// <summary>
/// La habilitación de un conector para una cooperativa (<c>ADM_TenantConnectors</c>; R15; FR-041), con motivo. Una cooperativa sólo ve
/// y usa los conectores que tiene habilitados; la réplica a su base la escribe <c>IProvisionadorDeConectores</c>.
/// </summary>
public class TenantConnector : AuditableEntity
{
    /// <summary>El <c>PublicId</c> de la cooperativa.</summary>
    public Guid TenantId { get; set; }
    public int ConnectorId { get; set; }
    public PlatformConnector? Connector { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
