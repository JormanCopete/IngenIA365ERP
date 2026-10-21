using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.Domain.Tests.Lending.Attributions;

/// <summary>
/// Un caso dorado del enrutamiento de atribuciones (feature 013, T0241; T63, R132; quickstart §1.4 y §2.7): un archivo JSON en
/// <c>Casos/</c>, calculado a mano, que el jefe de cartera puede leer. Trae la matriz como datos (niveles con su rango, clase,
/// permiso u órgano, techo de monto consolidado y marcas de excepción y de consejo; reglas con sus condiciones y su nivel mínimo)
/// y escenarios: el sujeto a enrutar y lo que DEBE salir —el nivel efectivo, cada razón con su nivel y los motivos no
/// exceptuables—. Molde de <c>CasoDoradoDeCosteo</c>.
/// </summary>
public sealed class CasoDoradoDeAtribuciones
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
    public MatrizJson Matriz { get; set; } = new();
    public List<EscenarioJson> Escenarios { get; set; } = [];

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Lending", "Attributions", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeAtribuciones Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeAtribuciones>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    public MatrizDeAtribuciones MatrizDelMotor() => new(
        Matriz.Niveles.Select(n => new NivelDeAtribucion(n.Codigo, n.Nombre, n.Rango, n.Clase, n.Techo, n.Excepcion, n.Consejo, n.Permiso, n.Organo))
            .ToList(),
        Matriz.Reglas.Select(r => new ReglaDeAtribucion(r.Orden, r.Sujeto, r.NivelMinimo)
        {
            Linea = r.Linea,
            Modalidad = r.Modalidad,
            MontoMinimo = r.MontoMinimo,
            MontoMaximo = r.MontoMaximo,
            PlazoMinimo = r.PlazoMinimo,
            PlazoMaximo = r.PlazoMaximo,
            ClaseDeGarantia = r.Garantia,
            PuntajeBajoElMinimo = r.PuntajeBajo,
            ConAlertas = r.Alertas,
            ExcepcionALaPolitica = r.ExcepcionALaPolitica,
            MoraMinima = r.MoraMinima,
            MoraMaxima = r.MoraMaxima,
            Concepto = r.Concepto,
        }).ToList());

    public sealed class MatrizJson
    {
        public List<NivelJson> Niveles { get; set; } = [];
        public List<ReglaJson> Reglas { get; set; } = [];
    }

    public sealed class NivelJson
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Rango { get; set; }
        public AttributionLevelKind Clase { get; set; }
        public string? Permiso { get; set; }
        public string? Organo { get; set; }
        public decimal? Techo { get; set; }
        public bool Excepcion { get; set; }
        public bool Consejo { get; set; }
    }

    public sealed class ReglaJson
    {
        public int Orden { get; set; }
        public string Sujeto { get; set; } = LendingDecisionSubjects.CreditApplication;
        public string? Linea { get; set; }
        public string? Modalidad { get; set; }
        public decimal? MontoMinimo { get; set; }
        public decimal? MontoMaximo { get; set; }
        public int? PlazoMinimo { get; set; }
        public int? PlazoMaximo { get; set; }
        public CollateralKind? Garantia { get; set; }
        public bool? PuntajeBajo { get; set; }
        public bool? Alertas { get; set; }
        public bool? ExcepcionALaPolitica { get; set; }
        public int? MoraMinima { get; set; }
        public int? MoraMaxima { get; set; }
        public LoanConcept? Concepto { get; set; }
        public string NivelMinimo { get; set; } = string.Empty;
    }

    public sealed class EscenarioJson
    {
        public string Nombre { get; set; } = string.Empty;
        public SujetoJson Sujeto { get; set; } = new();
        public EsperadoJson Esperado { get; set; } = new();
    }

    public sealed class SujetoJson
    {
        public string Sujeto { get; set; } = LendingDecisionSubjects.CreditApplication;
        public string? Linea { get; set; }
        public string? Modalidad { get; set; }
        public decimal MontoConsolidado { get; set; }
        public int? PlazoMeses { get; set; }
        public List<CollateralKind> Garantias { get; set; } = [];
        public bool PuntajeBajoElMinimo { get; set; }
        public bool Alertas { get; set; }
        public bool ExcepcionALaPolitica { get; set; }
        public int? DiasDeMora { get; set; }
        public LoanConcept? Concepto { get; set; }
        public bool HayVinculado { get; set; }
        public List<MotivoNoExceptuable> Hallazgos { get; set; } = [];

        public SujetoAEnrutar DelMotor() => new(Sujeto, MontoConsolidado)
        {
            Linea = Linea,
            Modalidad = Modalidad,
            PlazoMeses = PlazoMeses,
            ClasesDeGarantia = Garantias,
            PuntajeBajoElMinimo = PuntajeBajoElMinimo,
            ConAlertas = Alertas,
            ExcepcionALaPolitica = ExcepcionALaPolitica,
            DiasDeMora = DiasDeMora,
            Concepto = Concepto,
            HayVinculado = HayVinculado,
            Hallazgos = Hallazgos,
        };
    }

    public sealed class EsperadoJson
    {
        public string Nivel { get; set; } = string.Empty;
        public List<RazonJson> Razones { get; set; } = [];
        public List<MotivoNoExceptuable> NoExceptuables { get; set; } = [];
    }

    public sealed class RazonJson
    {
        public RazonDeEnrutamiento Clase { get; set; }
        public string Nivel { get; set; } = string.Empty;
    }
}
