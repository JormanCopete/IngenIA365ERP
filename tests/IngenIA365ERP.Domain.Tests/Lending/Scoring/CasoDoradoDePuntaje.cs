using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Lending.Scoring;

namespace IngenIA365ERP.Domain.Tests.Lending.Scoring;

/// <summary>
/// Un caso dorado del puntaje de crédito (feature 013, T0364; FR-037; quickstart §1.4): un archivo JSON en <c>Casos/</c>, calculado a
/// mano (la hoja <c>Casos/puntaje.py</c>, decimal de 40 dígitos), que el jefe de cartera puede leer. Trae el modelo de una versión de
/// línea —variables con su fuente, peso (fracción) y rangos con puntos (desde incluido, hasta excluido; el rango «sin dato» se marca con
/// <c>sinDato</c>)—, el puntaje mínimo y escenarios: los valores de cada fuente (<c>null</c> = sin dato) y lo que DEBE salir —el puntaje,
/// si quedó bajo el mínimo, los puntos y el aporte de cada variable y frases de la explicación—. Molde de <c>CasoDoradoDeAtribuciones</c>.
/// </summary>
public sealed class CasoDoradoDePuntaje
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public ModeloJson Modelo { get; set; } = new();
    public List<EscenarioJson> Escenarios { get; set; } = [];

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Lending", "Scoring", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDePuntaje Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDePuntaje>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    /// <summary>El modelo como lo guarda la versión de la línea.</summary>
    public ModeloDePuntaje ModeloDelMotor() => new(
        Modelo.NivelDeExcepcion,
        Modelo.Variables.Select(v => new VariableDePuntaje(v.Codigo, v.Nombre, v.Fuente, v.Peso,
            v.Rangos.Select(r => new RangoDePuntaje(r.Desde, r.Hasta, r.Puntos, r.SinDato)).ToList())).ToList());

    public sealed class ModeloJson
    {
        public string? NivelDeExcepcion { get; set; }
        public decimal? Minimo { get; set; }
        public List<VariableJson> Variables { get; set; } = [];
    }

    public sealed class VariableJson
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Fuente { get; set; } = string.Empty;
        public decimal Peso { get; set; }
        public List<RangoJson> Rangos { get; set; } = [];
    }

    public sealed class RangoJson
    {
        public decimal? Desde { get; set; }
        public decimal? Hasta { get; set; }
        public decimal Puntos { get; set; }
        public bool SinDato { get; set; }
    }

    public sealed class EscenarioJson
    {
        public string Nombre { get; set; } = string.Empty;

        /// <summary>El valor de cada fuente; <c>null</c> o ausente = sin dato.</summary>
        public Dictionary<string, decimal?> Valores { get; set; } = [];

        public EsperadoJson Esperado { get; set; } = new();
    }

    public sealed class EsperadoJson
    {
        public decimal Puntaje { get; set; }
        public bool BajoElMinimo { get; set; }
        public List<AporteJson> Variables { get; set; } = [];
        public List<string> ExplicacionContiene { get; set; } = [];
    }

    public sealed class AporteJson
    {
        public string Codigo { get; set; } = string.Empty;
        public decimal Puntos { get; set; }
        public decimal Aporte { get; set; }
        public bool SinDato { get; set; }
        public bool FueraDeRango { get; set; }
    }
}
