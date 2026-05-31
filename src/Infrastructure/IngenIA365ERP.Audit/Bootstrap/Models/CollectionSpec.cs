using System.Text.Json.Serialization;

namespace IngenIA365ERP.Audit.Bootstrap.Models;

public sealed class CollectionSpec
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createIfMissing")]
    public bool CreateIfMissing { get; set; } = true;

    [JsonPropertyName("indexes")]
    public List<IndexSpec> Indexes { get; set; } = new();
}
