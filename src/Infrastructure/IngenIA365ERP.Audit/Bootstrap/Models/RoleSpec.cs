using System.Text.Json.Serialization;

namespace IngenIA365ERP.Audit.Bootstrap.Models;

public sealed class RoleSpec
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("privileges")]
    public List<PrivilegeSpec> Privileges { get; set; } = new();

    [JsonPropertyName("roles")]
    public List<RoleRef> Roles { get; set; } = new();

    /// <summary>Texto libre — documenta el porqué; no se aplica.</summary>
    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }
}

public sealed class PrivilegeSpec
{
    [JsonPropertyName("resource")]
    public ResourceSpec Resource { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<string> Actions { get; set; } = new();
}

public sealed class ResourceSpec
{
    [JsonPropertyName("db")]
    public string Db { get; set; } = string.Empty;

    [JsonPropertyName("collection")]
    public string Collection { get; set; } = string.Empty;
}

public sealed class RoleRef
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("db")]
    public string Db { get; set; } = string.Empty;
}
