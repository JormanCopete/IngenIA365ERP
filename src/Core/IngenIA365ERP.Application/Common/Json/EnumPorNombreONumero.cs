using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Common.Json;

/// <summary>
/// Lee un enum tanto por nombre («Earning», sin distinguir mayúsculas; «A, B» en los de
/// banderas) como por número, y lo <b>escribe como número</b>, que es lo que los DTOs de
/// Shared esperan hoy (<c>int Status</c>, <c>int Channels</c>…): un
/// <see cref="JsonStringEnumConverter"/> a secas habría cambiado todas las respuestas.
///
/// <para>
/// Sin esto System.Text.Json sólo aceptaba números. Las pantallas mandan el nombre
/// —«nature»: «Earning», «periodicity»: «Monthly»— y el binding del cuerpo respondía un
/// 400 vacío antes de llegar al handler: sin sobre de error, sin log, y la persona veía
/// «Error HTTP 400». Así estaba crear un concepto de nómina en QA el 2026-09-18.
/// </para>
/// </summary>
public sealed class EnumPorNombreONumero : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(Convertidor<>).MakeGenericType(typeToConvert));

    private sealed class Convertidor<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    var texto = reader.GetString();
                    if (Enum.TryParse<T>(texto, ignoreCase: true, out var porNombre)) return porNombre;
                    throw new JsonException($"«{texto}» no es un valor de {typeof(T).Name}. Admite: {string.Join(", ", Enum.GetNames<T>())}.");
                case JsonTokenType.Number:
                    return (T)Enum.ToObject(typeof(T), reader.GetInt64());
                default:
                    throw new JsonException($"Se esperaba el nombre o el número de un {typeof(T).Name}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
    }
}
