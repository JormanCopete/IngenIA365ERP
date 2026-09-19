using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Lee una semilla JSON incrustada en este ensamblado (feature 009, R9: los catálogos contables
/// —planes de cuentas, rubros NIIF, tipos de comprobante, tipos de documento cruce, formatos de
/// exógena— son datos, no código, y viven en <c>Seeding/Parametric/Data/*.json</c>). Los revisa
/// el contador leyendo el archivo; los versiona el repositorio. Convención en
/// <c>docs/manual/semillas-json.md</c>.
/// </summary>
public static class RecursoJson
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static T Leer<T>(string nombre)
    {
        var ensamblado = typeof(RecursoJson).Assembly;
        var recurso = ensamblado.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + nombre, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No existe la semilla incrustada {nombre}; revise el EmbeddedResource del proyecto Persistence.");
        using var flujo = ensamblado.GetManifestResourceStream(recurso)!;
        return JsonSerializer.Deserialize<T>(flujo, Opciones)
            ?? throw new InvalidOperationException($"La semilla {nombre} está vacía.");
    }

    /// <summary>Nombres de todas las semillas incrustadas, para las pruebas de coherencia.</summary>
    public static IReadOnlyList<string> Nombres() => typeof(RecursoJson).Assembly.GetManifestResourceNames()
        .Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        .Select(n => n[(n.LastIndexOf(".Data.", StringComparison.Ordinal) + 6)..])
        .ToList();
}
