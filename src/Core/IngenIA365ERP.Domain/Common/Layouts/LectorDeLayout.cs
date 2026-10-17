using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Domain.Common.Layouts;

/// <summary>
/// Lee un layout desde su JSON (adaptadores-y-archivos.md §7.2) y revisa su definición (<see cref="LayoutDeArchivo.ValidarDefinicion"/>):
/// un JSON mal formado, una clave desconocida en un registro o en un campo, o una definición inválida es una
/// <see cref="FormatException"/> con el nombre del archivo. Las claves del nivel superior que no son del motor (la clase del archivo, la
/// central, quién lo cotejó, las notas) se ignoran aquí: las lee quien administra el catálogo. (nuevo)
/// </summary>
public static class LectorDeLayout
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static LayoutDeArchivo Leer(string json, string nombre)
    {
        LayoutJson? doc;
        try { doc = JsonSerializer.Deserialize<LayoutJson>(json, Opciones); }
        catch (JsonException e) { throw new FormatException($"El layout {nombre} no es un JSON válido: {e.Message}", e); }
        if (doc is null) throw new FormatException($"El layout {nombre} está vacío.");
        if (doc.ValidFrom is null) throw new FormatException($"El layout {nombre} no tiene «validFrom».");
        if (doc.FileKind is null) throw new FormatException($"El layout {nombre} no dice su «fileKind» (FixedWidth o Delimited).");

        var layout = new LayoutDeArchivo(doc.Code ?? string.Empty, doc.Version ?? string.Empty, doc.Source ?? string.Empty, doc.ValidFrom.Value, doc.ValidTo,
            doc.FileKind.Value, doc.Delimiter, doc.Encoding ?? "utf-8", doc.LineEnding ?? "CRLF", doc.NormalizeText,
            (doc.Records ?? []).Select(r => new RegistroDeLayout(r.Code ?? string.Empty, r.Repeat, r.Length,
                (r.Fields ?? []).Select(f => Campo(f, nombre)).ToList())).ToList());

        var errores = layout.ValidarDefinicion();
        if (errores.Count > 0) throw new FormatException($"El layout {nombre} no es válido: {string.Join(" ", errores)}");
        return layout;
    }

    private static CampoDeLayout Campo(FieldJson f, string nombre)
    {
        if (f.Format is null) throw new FormatException($"El layout {nombre}: el campo {f.Number} «{f.Name}» no dice su «format».");
        if (f.Pad is { Length: not 1 }) throw new FormatException($"El layout {nombre}: el campo {f.Number} «{f.Name}» tiene un «pad» de más de un carácter.");
        return new CampoDeLayout(f.Number, f.Name ?? string.Empty, f.Order, f.Start, f.Length, f.Kind ?? "A", f.Format.Value, f.Required, f.Path, f.Constant,
            f.AllowedValues ?? [], f.Pad?[0], f.Align, f.Decimals, f.Pattern, f.Verified);
    }

    private sealed class LayoutJson
    {
        public string? Code { get; set; }
        public string? Version { get; set; }
        public string? Source { get; set; }
        public DateOnly? ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
        public TipoDeArchivoDeLayout? FileKind { get; set; }
        public string? Delimiter { get; set; }
        public string? Encoding { get; set; }
        public string? LineEnding { get; set; }
        public bool NormalizeText { get; set; }
        public List<RecordJson>? Records { get; set; }

        /// <summary>Las claves que no son del motor (<c>kind</c>, <c>bureauCode</c>, <c>notes</c>, <c>verifiedBy</c>…).</summary>
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class RecordJson
    {
        public string? Code { get; set; }
        public string? Repeat { get; set; }
        public int? Length { get; set; }
        public List<FieldJson>? Fields { get; set; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class FieldJson
    {
        public int Number { get; set; }
        public string? Name { get; set; }
        public int Order { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public string? Kind { get; set; }
        public FormatoDeCampo? Format { get; set; }
        public bool Required { get; set; }
        public string? Path { get; set; }
        public string? Constant { get; set; }
        public List<string>? AllowedValues { get; set; }
        public string? Pad { get; set; }
        public AlineacionDeCampo? Align { get; set; }
        public int? Decimals { get; set; }
        public string? Pattern { get; set; }
        public bool Verified { get; set; }
    }
}
