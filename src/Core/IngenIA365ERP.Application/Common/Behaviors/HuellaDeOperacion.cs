using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// La huella de una operación idempotente (feature 012, T13, T055): SHA-256 en hexadecimal minúsculo de
/// <c>{operación}\n{cuerpo canónico}</c>. El cuerpo es el comando serializado con las opciones web, con las
/// claves de todo objeto <b>ordenadas</b> (ordinal) —así la huella no cambia con el orden de las
/// propiedades— y sin <c>operationKey</c>, que es la clave y no el contenido. Los arreglos conservan su
/// orden: dos líneas en otro orden son otro documento. Como el comando lleva los parámetros de la ruta como
/// propiedades, la huella cubre «ruta + cuerpo» (contracts/api.md §2.3).
/// </summary>
public static class HuellaDeOperacion
{
    private const string PropiedadDeLaClave = "operationKey";

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static string Calcular(string operacion, object cuerpo)
    {
        var texto = $"{operacion}\n{Canonico(cuerpo)}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }

    /// <summary>El JSON canónico del cuerpo: claves ordenadas, sin <c>operationKey</c>, sin espacios.</summary>
    public static string Canonico(object cuerpo)
    {
        var nodo = JsonSerializer.SerializeToNode(cuerpo, cuerpo.GetType(), Opciones);
        if (nodo is JsonObject raiz)
        {
            var clave = raiz.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, PropiedadDeLaClave, StringComparison.OrdinalIgnoreCase));
            if (clave is not null) raiz.Remove(clave);
        }
        return Ordenar(nodo)?.ToJsonString() ?? "null";
    }

    private static JsonNode? Ordenar(JsonNode? nodo) => nodo switch
    {
        JsonObject objeto => new JsonObject(objeto
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Ordenar(p.Value)))),
        JsonArray arreglo => new JsonArray(arreglo.Select(Ordenar).ToArray()),
        null => null,
        _ => JsonNode.Parse(nodo.ToJsonString()),
    };
}
