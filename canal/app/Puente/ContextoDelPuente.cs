using System.Text.Json.Serialization;

namespace IngenIA365Canal.App.Puente;

/// <summary>
/// Los tipos que cruzan el puente, para la serialización sin reflexión (la app se recorta). camelCase: la PWA igual normaliza la primera letra,
/// pero así el contrato se lee como en contracts/pasarela-y-app.md.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InfoDeLaPlataforma))]
[JsonSerializable(typeof(BiometriaDelDispositivo))]
[JsonSerializable(typeof(ResultadoDeLaPasarela))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ContextoDelPuente : JsonSerializerContext;
