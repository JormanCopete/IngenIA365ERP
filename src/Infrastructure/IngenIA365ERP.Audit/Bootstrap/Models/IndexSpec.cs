using System.Text.Json.Serialization;

namespace IngenIA365ERP.Audit.Bootstrap.Models;

public sealed class IndexSpec
{
    /// <summary>Pares campo→dirección (1 ascendente, -1 descendente).</summary>
    [JsonPropertyName("keys")]
    public Dictionary<string, int> Keys { get; set; } = new();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Si está presente, índice TTL — MongoDB expira documentos
    /// cuya fecha en el campo de la key es más vieja que este intervalo.</summary>
    [JsonPropertyName("expireAfterSeconds")]
    public int? ExpireAfterSeconds { get; set; }

    [JsonPropertyName("unique")]
    public bool Unique { get; set; }
}
