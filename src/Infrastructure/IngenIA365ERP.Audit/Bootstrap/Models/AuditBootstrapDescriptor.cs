using System.Text.Json.Serialization;

namespace IngenIA365ERP.Audit.Bootstrap.Models;

/// <summary>
/// POCO que mapea <c>database/migration/15_Audit_Mongodb_Bootstrap.json</c>.
/// Es el contrato declarativo del bootstrap de la BD de auditoría.
///
/// <para>
/// Procesado por <see cref="AuditBootstrapRunner"/>; las operaciones se
/// aplican vía <c>MongoDB.Driver</c> (no vía mongosh), por eso el JSON
/// directamente NO es ejecutable con <c>mongosh "..." file.json</c>.
/// </para>
/// </summary>
public sealed class AuditBootstrapDescriptor
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("$id")]
    public string? Id { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("database")]
    public string Database { get; set; } = string.Empty;

    [JsonPropertyName("collections")]
    public List<CollectionSpec> Collections { get; set; } = new();

    [JsonPropertyName("roles")]
    public List<RoleSpec> Roles { get; set; } = new();

    [JsonPropertyName("users")]
    public List<UserSpec> Users { get; set; } = new();

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();
}
