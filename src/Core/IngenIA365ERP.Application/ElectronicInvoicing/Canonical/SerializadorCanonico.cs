using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

/// <summary>
/// El JSON canónico de <see cref="DocumentoElectronicoCanonico"/> y su SHA-256 (feature 012, I4, T701; contracts/dian.md §4):
/// claves en camelCase y <b>ordenadas</b> (ordinal, en todos los niveles: no depende del orden en que la reflexión entregue
/// las propiedades), decimales con su escala fija y punto decimal sea cual sea la cultura del proceso, fechas ISO con el
/// desfase local (-05:00), enumeraciones por nombre, sin espacios y con los nulos escritos. Dos construcciones del mismo
/// documento dan los mismos bytes y el mismo hash: eso permite subir el artefacto después del commit y comprobarlo (§4.1).
/// (nuevo)
/// </summary>
public static class SerializadorCanonico
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new JsonStringEnumConverter(),
            new FechaYHoraLocal(),
            new FechaIso(),
        },
    };

    /// <summary>El JSON canónico, en UTF-8 sin BOM.</summary>
    public static string Serializar(DocumentoElectronicoCanonico documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        return Ordenado(JsonSerializer.SerializeToNode(documento, Opciones));
    }

    /// <summary>El JSON canónico de cualquier forma del canónico (p. ej. <c>EventoRadianCanonico</c>).</summary>
    public static string Serializar<T>(T valor) => Ordenado(JsonSerializer.SerializeToNode(valor, Opciones));

    /// <summary>SHA-256 del JSON canónico, en hexadecimal minúsculo de 64 caracteres (<c>CanonicalSha256</c>).</summary>
    public static string Sha256(string jsonCanonico) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(jsonCanonico)));

    /// <summary>Lee un JSON canónico guardado (el adjunto de la versión).</summary>
    public static DocumentoElectronicoCanonico? Leer(string jsonCanonico) =>
        JsonSerializer.Deserialize<DocumentoElectronicoCanonico>(jsonCanonico, Opciones);

    private static string Ordenado(JsonNode? nodo)
    {
        using var flujo = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(flujo, new JsonWriterOptions { Encoder = Opciones.Encoder, Indented = false }))
            Escribir(escritor, nodo);
        return Encoding.UTF8.GetString(flujo.ToArray());
    }

    private static void Escribir(Utf8JsonWriter escritor, JsonNode? nodo)
    {
        switch (nodo)
        {
            case null:
                escritor.WriteNullValue();
                break;
            case JsonObject objeto:
                escritor.WriteStartObject();
                foreach (var (clave, valor) in objeto.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    escritor.WritePropertyName(clave);
                    Escribir(escritor, valor);
                }
                escritor.WriteEndObject();
                break;
            case JsonArray arreglo:
                escritor.WriteStartArray();
                foreach (var elemento in arreglo) Escribir(escritor, elemento);
                escritor.WriteEndArray();
                break;
            default:
                // Un valor conserva su texto tal cual lo escribió su convertidor (la escala de los decimales incluida).
                nodo.WriteTo(escritor);
                break;
        }
    }

    /// <summary>Fecha y hora con el desfase local de la plataforma (-05:00), sin fracciones de segundo.</summary>
    private sealed class FechaYHoraLocal : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToOffset(IDateTimeService.DesfaseColombia).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture));
    }

    private sealed class FechaIso : JsonConverter<DateOnly>
    {
        public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateOnly.ParseExact(reader.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Escribe un <see cref="decimal"/> (o <see cref="Nullable{Decimal}"/>) con una escala fija y punto decimal, redondeando
/// «half away from zero» si trae más decimales (feature 012, I4, T701; T19). Las escalas del canónico son
/// <see cref="EscalaDeMonto"/> (18,2), <see cref="EscalaDeCantidad"/> (18,4), <see cref="EscalaDePrecio"/> (18,6) y
/// <see cref="EscalaDeTarifa"/> (9,6). (nuevo)
/// </summary>
public abstract class EscalaFija(int decimales) : JsonConverterFactory
{
    public int Decimales { get; } = decimales;

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(decimal) || typeToConvert == typeof(decimal?);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        typeToConvert == typeof(decimal) ? new Simple(Decimales) : new Anulable(Decimales);

    /// <summary>El texto del valor a la escala (lo usa también quien necesite comparar como el serializador).</summary>
    public static string Texto(decimal valor, int decimales) =>
        decimal.Round(valor, decimales, MidpointRounding.AwayFromZero).ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private sealed class Simple(int decimales) : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteRawValue(Texto(value, decimales), skipInputValidation: true);
    }

    private sealed class Anulable(int decimales) : JsonConverter<decimal?>
    {
        public override bool HandleNull => true;

        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? null : reader.GetDecimal();

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value is { } v) writer.WriteRawValue(Texto(v, decimales), skipInputValidation: true);
            else writer.WriteNullValue();
        }
    }
}

/// <summary>Montos: 2 decimales. (nuevo)</summary>
public sealed class EscalaDeMonto() : EscalaFija(2);

/// <summary>Cantidades: 4 decimales. (nuevo)</summary>
public sealed class EscalaDeCantidad() : EscalaFija(4);

/// <summary>Precio unitario: 6 decimales. (nuevo)</summary>
public sealed class EscalaDePrecio() : EscalaFija(6);

/// <summary>Tarifas como fracción y tasa de cambio: 6 decimales. (nuevo)</summary>
public sealed class EscalaDeTarifa() : EscalaFija(6);
