using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// Las <b>únicas</b> opciones con que se serializa un mensaje de integración (feature 012, T8, T075;
/// contracts/mensajes.md §3). Es otro contrato que el JSON de la API, a propósito: un mensaje puede esperar
/// meses en la bandeja y no puede depender de la numeración de un enum ni de cómo la API decida escribir mañana.
///
/// <list type="bullet">
///   <item>propiedades en camelCase y en el orden en que las declara el record;</item>
///   <item>enums como <b>texto</b> (<c>"Business"</c>, <c>"PosEquivalentDocument"</c>), nunca como número;</item>
///   <item>los nulos se <b>escriben</b>: el esquema de cada versión es estable;</item>
///   <item><c>decimal</c> exacto, con la escala con que se construyó (<c>100000.00</c>, <c>20.0000</c>, <c>0.05</c>);
///         nunca <c>double</c>. Toda tarifa es fracción;</item>
///   <item><c>DateOnly</c> como <c>"2026-11-14"</c>; los instantes (<c>DateTimeOffset</c>) en UTC con <c>Z</c> y
///         sin ceros de más en la fracción (<c>"2026-11-15T01:12:09.418Z"</c>, <c>"2026-11-15T01:11:40Z"</c>);</item>
///   <item>Guid en formato <c>D</c> en minúsculas;</item>
///   <item>texto sin escapar fuera de lo que JSON exige: «Crédito» se guarda como «Crédito», no como
///         <c>é</c>. <c>PayloadJson</c> es texto de la base, no se incrusta en HTML, y así sus bytes son
///         los que un humano lee en la bandeja.</item>
/// </list>
///
/// <para>
/// <c>PayloadSha256</c> se calcula sobre los bytes que producen estas opciones (<see cref="Serializar"/>). Cambiar
/// cualquiera de ellas cambia los bytes de lo que se emita desde entonces: es un cambio de contrato y lo delata
/// <c>ContratosDeMensajesTests</c>, que compara byte a byte contra los archivos de <c>Casos/</c>.
/// </para>
/// </summary>
public static class OpcionesDeMensajes
{
    /// <summary>Las opciones, de sólo lectura.</summary>
    public static JsonSerializerOptions Opciones { get; } = Crear();

    /// <summary>Los bytes UTF-8 exactos del contenido, tal como se guardan en <c>PayloadJson</c>.</summary>
    public static byte[] Serializar(object contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        return JsonSerializer.SerializeToUtf8Bytes(contenido, contenido.GetType(), Opciones);
    }

    private static JsonSerializerOptions Crear()
    {
        var opciones = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.Strict,
            // Sin escapar tildes ni «+»: ver el resumen. Los caracteres de control y las comillas sí se escapan.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        opciones.Converters.Add(new JsonStringEnumConverter());
        opciones.Converters.Add(new InstanteUtc());
        opciones.MakeReadOnly(populateMissingResolver: true);
        return opciones;
    }

    /// <summary>Un instante siempre en UTC con <c>Z</c>; al leer admite cualquier desfase y lo pasa a UTC.</summary>
    private sealed class InstanteUtc : JsonConverter<DateTimeOffset>
    {
        private const string Formato = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime.ToString(Formato, CultureInfo.InvariantCulture));
    }
}
