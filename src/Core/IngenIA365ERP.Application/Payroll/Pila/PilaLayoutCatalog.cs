using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Payroll.Pila;

namespace IngenIA365ERP.Application.Payroll.Pila;

/// <summary>
/// Los layouts de la PILA embebidos en el ensamblado (<c>Payroll/Pila/Layouts/*.json</c>;
/// feature 010, US5; research R9). Una versión nueva del anexo es un JSON nuevo con
/// <c>validFrom</c>; el vigente para un período es el que lo cubre por fecha. Un layout
/// cuyos campos no están todos cotejados sigue siendo el vigente, pero generar con él deja la
/// alerta <c>Pila.LayoutSinCotejar</c> (D-43): el cotejo con el anexo y con una planilla pagada
/// es un paso del dueño (T094), y hasta entonces el archivo se prueba en el validador del operador.
/// </summary>
public static class PilaLayoutCatalog
{
    private static readonly Lazy<IReadOnlyList<PilaLayout>> Layouts = new(Cargar);

    public static IReadOnlyList<PilaLayout> All => Layouts.Value;

    /// <summary>El layout vigente al primer día del período, o nulo si ninguno lo cubre.</summary>
    public static PilaLayout? ForPeriod(DateOnly firstDay) =>
        All.Where(l => l.IsValidAt(firstDay)).OrderByDescending(l => l.ValidFrom).FirstOrDefault();

    public static PilaLayout? ByCode(string code) => All.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Código con el que se guarda la versión usada: <c>AT2-v30</c>.</summary>
    public static string VersionLabel(PilaLayout layout) => layout.Code;

    private static IReadOnlyList<PilaLayout> Cargar()
    {
        var asm = Assembly.GetExecutingAssembly();
        var nombres = asm.GetManifestResourceNames().Where(n => n.Contains(".Payroll.Pila.Layouts.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        var lista = new List<PilaLayout>();
        foreach (var nombre in nombres)
        {
            using var stream = asm.GetManifestResourceStream(nombre)!;
            var doc = JsonSerializer.Deserialize<LayoutJson>(stream, Json) ?? throw new InvalidOperationException($"El layout embebido {nombre} no se pudo leer.");
            var layout = doc.ToLayout();
            var errores = layout.Validate();
            if (errores.Count > 0)
                throw new InvalidOperationException($"El layout embebido {layout.Code} no es válido: {string.Join(" ", errores)}");
            lista.Add(layout);
        }
        return lista;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private sealed class LayoutJson
    {
        public string Code { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateOnly ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
        public string Encoding { get; set; } = "us-ascii";
        public string LineEnding { get; set; } = "CRLF";
        public List<string> Notes { get; set; } = [];
        public List<RecordJson> Records { get; set; } = [];

        public PilaLayout ToLayout() => new(Code, Version, Source, ValidFrom, ValidTo, Encoding, LineEnding,
            Records.Select(r => new PilaRecordLayout(r.Type, r.Length, r.Fields.Select(f =>
                new PilaFieldLayout(f.Number, f.Name, f.Start, f.Length, f.Kind, f.Required, f.Source, f.Path, f.Constant, f.Format, f.Rules, f.Verified)).ToList())).ToList());
    }

    private sealed class RecordJson
    {
        public int Type { get; set; }
        public int Length { get; set; }
        public List<FieldJson> Fields { get; set; } = [];
    }

    private sealed class FieldJson
    {
        public int Number { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Start { get; set; }
        public int Length { get; set; }
        public string Kind { get; set; } = "A";
        public bool Required { get; set; }
        public string Source { get; set; } = "Blank";
        public string? Path { get; set; }
        public string? Constant { get; set; }
        public string Format { get; set; } = "Text";
        public List<string> Rules { get; set; } = [];
        public bool Verified { get; set; }

        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }
}
