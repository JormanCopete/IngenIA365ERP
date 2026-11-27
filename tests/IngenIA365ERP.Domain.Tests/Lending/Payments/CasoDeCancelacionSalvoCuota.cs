using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Payments;

/// <summary>
/// Un caso dorado de la cancelación salvo la cuota causada (feature 028, R4, T061; FR-019, D13-A): un archivo JSON en <c>Lending/Payments/Casos/</c>, con
/// comentarios y la cuenta a mano en el encabezado, que reusa las condiciones, las series, los reconocimientos y las operaciones de
/// <see cref="CasoDoradoDeCartera"/> (la misma reconstrucción) y trae, por escenario, lo que se espera: la distribución del pago que deja la cuota
/// (<c>esperado</c>), lo que cuesta sin aplicarlo (<c>valor</c>), el pago posterior que la cierra (<c>cierre</c>) y los saldos a varias fechas
/// (<c>verificaciones</c>). (nuevo)
/// </summary>
public sealed class CasoDeCancelacionSalvoCuota
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Nombre { get; set; } = string.Empty;
    public CasoDoradoDeCartera.CondicionesJson Condiciones { get; set; } = new();
    public CasoDoradoDeCartera.SeriesJson Series { get; set; } = new();
    public List<CasoDoradoDeCartera.ReconocimientoJson> Reconocimientos { get; set; } = [];
    public List<EscenarioJson> Escenarios { get; set; } = [];

    public static string Directorio => Path.Combine(AppContext.BaseDirectory, "Lending", "Payments", "Casos");

    public static IEnumerable<string> Archivos() => Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDeCancelacionSalvoCuota Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDeCancelacionSalvoCuota>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    public static CasoDeCancelacionSalvoCuota CargarPorNombre(string archivo) => Cargar(Path.Combine(Directorio, archivo));

    public EscenarioJson Escenario(string nombre) => Escenarios.Single(e => e.Nombre == nombre);

    /// <summary>La entrada de la reconstrucción con las operaciones del escenario y <paramref name="r"/> como R (la de los casos dorados).</summary>
    public EntradaDeReconstruccion Entrada(IReadOnlyList<CasoDoradoDeCartera.OperacionJson> operaciones, DateTime r) =>
        new CasoDoradoDeCartera { Nombre = Nombre, Condiciones = Condiciones, Series = Series, Reconocimientos = Reconocimientos }
            .Entrada(Condiciones, operaciones, r);

    public sealed class EscenarioJson
    {
        public string Nombre { get; set; } = string.Empty;
        public List<CasoDoradoDeCartera.OperacionJson> Operaciones { get; set; } = [];

        /// <summary>El Id de la operación con <c>PayoffExceptCurrentInstallment</c>.</summary>
        public string Pago { get; set; } = string.Empty;
        public EsperadoJson Esperado { get; set; } = new();
        public ValorJson? Valor { get; set; }
        public CierreJson? Cierre { get; set; }
        public List<VerificacionJson> Verificaciones { get; set; } = [];

        public CasoDoradoDeCartera.OperacionJson Operacion(string id) => Operaciones.Single(o => o.Id == id);
    }

    public sealed class EsperadoJson
    {
        public int? Cuota { get; set; }
        public int? CuotaDejada { get; set; }
        public List<LineaJson> Lineas { get; set; } = [];
        public List<LineaJson> InteresFijado { get; set; } = [];
        public List<LineaJson> InteresNoCobrado { get; set; } = [];
        public List<LineaJson> AnticipoUsado { get; set; } = [];
        public decimal Anticipo { get; set; }
        public decimal Excedente { get; set; }
        public decimal Aplicado { get; set; }
        public bool Cancela { get; set; }
        public List<int>? CuotasDelPlanNuevo { get; set; }
        public List<string> Avisos { get; set; } = [];
    }

    public sealed class ValorJson
    {
        public int? Cuota { get; set; }
        public decimal ParaCancelar { get; set; }
        public decimal QuedaEnLaCuota { get; set; }
    }

    public sealed class CierreJson
    {
        public string Pago { get; set; } = string.Empty;
        public List<LineaJson> Lineas { get; set; } = [];
        public decimal Excedente { get; set; }
        public bool Cancela { get; set; }
    }

    public sealed class VerificacionJson
    {
        public DateOnly Fecha { get; set; }
        public DateTime InformacionHasta { get; set; }
        public int CuotasDelPlan { get; set; }
        public decimal ValorParaCancelar { get; set; }
        public List<LineaJson> PorCuota { get; set; } = [];
        public List<ReconocidoJson>? Reconocidos { get; set; }
    }

    public sealed class LineaJson
    {
        public int? Cuota { get; set; }
        public LoanConcept Concepto { get; set; } = LoanConcept.Advance;
        public decimal Valor { get; set; }
    }

    public sealed class ReconocidoJson
    {
        public int Cuota { get; set; }
        public LoanConcept Concepto { get; set; }
        public DateOnly Desde { get; set; }
        public decimal Reconocido { get; set; }
        public decimal Pendiente { get; set; }
    }
}
