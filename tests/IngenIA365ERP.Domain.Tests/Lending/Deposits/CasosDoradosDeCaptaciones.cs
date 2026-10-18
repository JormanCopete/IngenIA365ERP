using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Un caso dorado de captaciones (feature 014, T004; research R12; FR-026, SC-002, SC-019): un archivo JSON en <c>Casos/</c> con
/// comentarios, claves en español y enums por nombre —el formato de los casos de crédito (<see cref="Lending.CasoDoradoDeCartera"/>)—,
/// calculado con la hoja <c>Casos/calcular.py</c> (decimal de 40 dígitos, nunca Excel ni el código que se prueba) para que la contadora
/// lo pueda leer y firmar.
///
/// <para>
/// El esquema nace con lo que pide el libro de cuentas (fase fundacional) y la causación de US4 de captaciones (R05): las
/// <c>condiciones</c> de la versión de línea (clase, convención de días, equivalencia, método de saldo, tasa o tramos, redondeo de la
/// liquidación), las <c>series</c> (UVT, componente inflacionario, festivos), las <c>operaciones</c> con fecha valor y de registro, y las
/// <c>consultas</c> a (D, R) con su <c>esperado</c> (saldos por concepto, interés causado día a día, liquidación). Las historias que
/// siguen (retención, GMF, CDAT, contractual, revalorización, fondo de liquidez) amplían el mismo esquema sin romperlo, como hizo la 013.
/// </para>
/// </summary>
public sealed class CasoDoradoDeCaptaciones
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
    public CondicionesJson Condiciones { get; set; } = new();
    public SeriesJson Series { get; set; } = new();
    public List<OperacionJson> Operaciones { get; set; } = [];
    public List<ConsultaJson> Consultas { get; set; } = [];

    /// <summary>Lo esperado de todo el caso cuando no depende de una consulta (el plan de un CDAT, una revalorización…).</summary>
    public JsonElement? Esperado { get; set; }

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Lending", "Deposits", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.Exists(DirectorioDeCasos)
            ? Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal)
            : [];

    public static CasoDoradoDeCaptaciones Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeCaptaciones>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    /// <summary>Las condiciones selladas de la versión de línea (o de la cuenta, en CDAT y contractual).</summary>
    public sealed class CondicionesJson
    {
        /// <summary><c>Deposit</c> o <c>Contribution</c> (nombre de <c>LineNature</c>).</summary>
        public string Naturaleza { get; set; } = "Deposit";

        /// <summary><c>Demand</c>, <c>Contractual</c>, <c>Permanent</c> o <c>TermDeposit</c> (nombre de <c>DepositClass</c>); nulo en aportes.</summary>
        public string? Clase { get; set; }

        /// <summary>Convención de días de la 013 (<c>Thirty360</c>, <c>Actual365</c>, <c>Actual360</c>).</summary>
        public string Convencion { get; set; } = "Actual365";

        /// <summary><c>Compound</c> (defecto, R05) o <c>Linear</c>.</summary>
        public string Equivalencia { get; set; } = "Compound";

        /// <summary><c>DailyBalance</c> (defecto, D31), <c>AverageBalance</c> o <c>MinimumBalance</c>.</summary>
        public string MetodoDeSaldo { get; set; } = "DailyBalance";

        /// <summary>Tasa efectiva anual como fracción, si la línea no tiene tramos.</summary>
        public decimal? TasaEA { get; set; }

        /// <summary><c>WholeBalance</c> o <c>Stepped</c> (nombre de <c>RateTierMode</c>).</summary>
        public string? ModoDeTramos { get; set; }

        public List<TramoJson> Tramos { get; set; } = [];

        /// <summary>Saldo mínimo para causar: un día con saldo menor causa cero (R05).</summary>
        public decimal SaldoMinimoParaInteres { get; set; }

        /// <summary>Unidad de redondeo de la liquidación (1 = al peso, R05).</summary>
        public decimal UnidadDeRedondeo { get; set; } = 1m;

        public DateOnly FechaDeApertura { get; set; }

        /// <summary>CDAT y contractual: plazo en días, periodicidad de intereses y momento (<c>Arrears</c>, <c>Advance</c>).</summary>
        public int? PlazoDias { get; set; }
        public string? PeriodicidadDeIntereses { get; set; }
        public string? MomentoDeIntereses { get; set; }
        public decimal? Monto { get; set; }
    }

    /// <summary>Un tramo de tasa por saldo (y, en CDAT, por plazo y monto).</summary>
    public sealed class TramoJson
    {
        public decimal Desde { get; set; }
        public decimal? Hasta { get; set; }
        public decimal Ea { get; set; }
        public int? PlazoDesde { get; set; }
        public int? PlazoHasta { get; set; }
    }

    /// <summary>Series con vigencia que el caso certifica (valores nacionales y parámetros que el motor recibe por parámetro).</summary>
    public sealed class SeriesJson
    {
        public List<ValorConFechaJson> Uvt { get; set; } = [];
        public List<ValorConFechaJson> ComponenteInflacionario { get; set; } = [];
        public List<DateOnly> Festivos { get; set; } = [];

        /// <summary>Parámetros de Cartera con su valor a la fecha (clave <c>Grupo.Nombre</c> → valor como texto).</summary>
        public Dictionary<string, string> Parametros { get; set; } = [];
    }

    public sealed class ValorConFechaJson
    {
        public DateOnly Desde { get; set; }
        public decimal Valor { get; set; }
        public DateTime? RegistradoEn { get; set; }
    }

    /// <summary>Una operación de cuenta: su tipo (nombre de <c>MemberAccountTransactionKind</c>), fechas y monto.</summary>
    public sealed class OperacionJson
    {
        public string Id { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaValor { get; set; }
        public DateTime RegistradoEn { get; set; }
        public decimal Monto { get; set; }

        /// <summary>El id de la operación que reversa, si es un reverso.</summary>
        public string? Reversa { get; set; }
    }

    /// <summary>Una consulta a (D, R) con su modo (<c>Saldos</c>, <c>Causacion</c>, <c>Liquidacion</c>…) y lo esperado.</summary>
    public sealed class ConsultaJson
    {
        public string Modo { get; set; } = "Saldos";
        public DateOnly Fecha { get; set; }
        public DateTime InformacionHasta { get; set; }
        public DateOnly? Desde { get; set; }
        public JsonElement? Esperado { get; set; }
    }
}

/// <summary>
/// Feature 014, T004: el lector de casos dorados de captaciones carga todo archivo de <c>Casos/</c> y la hoja que los recalcula está al
/// lado. Los casos llegan con sus motores (US4 de captaciones en adelante, mínimo 40, SC-002); esta prueba fija el formato desde ya.
/// </summary>
public class CasosDoradosDeCaptacionesTests
{
    [Fact]
    public void Cada_caso_se_lee_con_nombre_condiciones_y_lo_esperado()
    {
        foreach (var archivo in CasoDoradoDeCaptaciones.Archivos())
        {
            var caso = CasoDoradoDeCaptaciones.Cargar(archivo);
            caso.Nombre.Should().NotBeNullOrWhiteSpace($"{Path.GetFileName(archivo)} trae nombre");
            (caso.Esperado is not null || caso.Consultas.Any(c => c.Esperado is not null))
                .Should().BeTrue($"{Path.GetFileName(archivo)} trae lo esperado (calculado con calcular.py)");
        }
    }

    [Fact]
    public void La_hoja_que_los_recalcula_vive_junto_a_los_casos()
    {
        var hoja = Path.Combine(RaizDelRepositorio(), "tests", "IngenIA365ERP.Domain.Tests", "Lending", "Deposits", "Casos", "calcular.py");
        File.Exists(hoja).Should().BeTrue("los casos dorados de captaciones se calculan con Casos/calcular.py (R12, T26 de la 013)");
    }

    private static string RaizDelRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IngenIA365ERP.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
