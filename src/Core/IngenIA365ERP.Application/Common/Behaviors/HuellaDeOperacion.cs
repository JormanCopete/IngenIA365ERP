using System.Globalization;
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
/// propiedades, la huella cubre «ruta + cuerpo» (contracts/api.md §2.3). Los números se escriben <b>sin la escala</b>
/// (<c>2.0000</c> es <c>2</c>): una entidad en memoria y la misma releída de la base (con la escala de su columna) tienen
/// que dar la misma huella; si no, la aprobación de un descuento o de un crédito nunca coincidía con lo guardado (2026-09-27).
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
        JsonValue valor when valor.GetValueKind() == JsonValueKind.Number => SinEscala(valor),
        null => null,
        _ => JsonNode.Parse(nodo.ToJsonString()),
    };

    /// <summary>El número sin ceros de escala (<c>10000.000000</c> → <c>10000</c>); lo que no cabe en un decimal queda como vino.</summary>
    private static JsonNode? SinEscala(JsonValue valor)
    {
        var texto = valor.ToJsonString();
        return decimal.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero)
            ? JsonValue.Create(numero / 1.000000000000000000000000000000000m)
            : JsonNode.Parse(texto);
    }
}
