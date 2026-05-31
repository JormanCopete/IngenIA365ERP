using System.Text.Json.Serialization;

namespace IngenIA365ERP.Audit.Bootstrap.Models;

public sealed class UserSpec
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Fuente del password. Formatos soportados:
    /// <list type="bullet">
    ///   <item><c>env:VAR_NAME</c> — lee de variable de entorno.</item>
    /// </list>
    /// </summary>
    [JsonPropertyName("passwordSource")]
    public string PasswordSource { get; set; } = string.Empty;

    [JsonPropertyName("roles")]
    public List<RoleRef> Roles { get; set; } = new();
}
