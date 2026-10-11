using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Un caso dorado del motor financiero de Cartera (feature 013, T0330; quickstart §1.2, R45): un archivo JSON en <c>Casos/</c> con
/// comentarios, claves en español y enums por nombre (molde <c>CasoDoradoDeCosteo</c> y los casos de la fase 4), calculado con la
/// hoja <c>Casos/calcular.py</c> —decimal de 40 dígitos, nunca Excel ni el código que se prueba (T26)— para que la contadora lo pueda
/// leer y firmar.
///
/// <para>
/// El esquema es el completo de quickstart §1.2 y no cambia en US4 (C105: el cargador nace aquí y US4 no lo recrea):
/// <list type="bullet">
/// <item><c>condiciones</c>: lo sellado de la línea y el crédito (sistema, convención, equivalencia, redondeo, prelación, monto,
/// <c>plazoCuotas</c> = períodos del plazo —con gracia total incluye los de gracia—, periodicidad, tasa EA, fechas, día de pago,
/// modalidad, extraordinarias, gracia y seguros);</item>
/// <item><c>series</c>: usura por clave de modalidad con su vigencia y registro (R), festivos nacionales e índices;</item>
/// <item><c>operaciones</c> con fecha valor y de registro, y <c>consultas</c> a (D, R): las corre US4;</item>
/// <item><c>esperado</c> de esta fase (casos sin operaciones): tasas, plan cuota a cuota y veredicto de usura; con
/// <c>variantes</c>, lo de cada una en <c>esperado.variantes[nombre]</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class CasoDoradoDeCartera
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
    public List<VarianteJson> Variantes { get; set; } = [];
    public SeriesJson Series { get; set; } = new();
    public List<OperacionJson> Operaciones { get; set; } = [];
    public List<ConsultaJson> Consultas { get; set; } = [];
    public EsperadoJson? Esperado { get; set; }

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Lending", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeCartera Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeCartera>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    /// <summary>El plan principal y cada variante, con lo que esperan.</summary>
    public IEnumerable<(string Nombre, CondicionesJson Condiciones, EsperadoJson Esperado)> Escenarios()
    {
        var esperado = Esperado ?? throw new InvalidOperationException($"{Nombre}: no trae «esperado».");
        yield return (Nombre, Condiciones, esperado);
        foreach (var v in Variantes)
        {
            if (esperado.Variantes is null || !esperado.Variantes.TryGetValue(v.Nombre, out var delaVariante))
                throw new InvalidOperationException($"{Nombre}: la variante «{v.Nombre}» no trae lo esperado.");
            yield return ($"{Nombre} · {v.Nombre}", v.Condiciones, delaVariante);
        }
    }

    /// <summary>
    /// La usura que el caso certifica para la modalidad a la fecha de desembolso: la de la clave con el «desde» más reciente que no
    /// pase de esa fecha (y, si hay empate, la registrada después). Nula si no hay.
    /// </summary>
    public UsuraCertificada? UsuraDe(CondicionesJson c) =>
        c.Modalidad is not { } clave
            ? null
            : Series.Usura
                .Where(u => u.Clave == clave && u.Desde <= c.FechaDesembolso)
                .OrderByDescending(u => u.Desde).ThenByDescending(u => u.RegistradoEn)
                .Select(u => new UsuraCertificada(u.Ea, u.PublicId ?? Guid.Empty, u.Clave, u.Desde))
                .FirstOrDefault();

    public sealed class CondicionesJson
    {
        public AmortizationSystem Sistema { get; set; }
        public DayCountConvention Convencion { get; set; }
        public RateEquivalence Equivalencia { get; set; } = RateEquivalence.Compound;
        public RedondeoJson Redondeo { get; set; } = new();
        public AllocationOrder Prelacion { get; set; } = AllocationOrder.OldestInstallmentFirst;
        public decimal Monto { get; set; }
        public int PlazoCuotas { get; set; }
        public RepaymentPeriodicity Periodicidad { get; set; } = RepaymentPeriodicity.Monthly;
        public decimal TasaEA { get; set; }
        public DateOnly FechaDesembolso { get; set; }
        public DateOnly? PrimerVencimiento { get; set; }
        public int? DiaDePago { get; set; }
        public string? Modalidad { get; set; }
        public List<ExtraordinariaJson> Extraordinarias { get; set; } = [];
        public GraciaJson? Gracia { get; set; }
        public List<SeguroJson> Seguros { get; set; } = [];

        public EntradaDePlan Entrada() => new(Monto, FechaDesembolso, PlazoCuotas, Periodicidad, Sistema, TasaEA, Convencion, Equivalencia,
            new RedondeoDeCartera(Redondeo.Unidad, Redondeo.Modo, Redondeo.Momento))
        {
            PrimerVencimiento = PrimerVencimiento,
            DiaDePago = DiaDePago,
            Extraordinarias = Extraordinarias.Select(x => new CuotaExtraordinaria(x.Cuota, x.Valor)).ToList(),
            Gracia = Gracia?.Clase ?? GraceKind.None,
            PeriodosDeGracia = Gracia?.Periodos ?? 0,
            TratamientoDeGracia = Gracia?.Tratamiento,
            CuotasDeDistribucion = Gracia?.CuotasDeDistribucion,
            Seguros = Seguros.Select(s => new SeguroDelPlan(s.Codigo, s.Base, s.Tarifa, s.Periodicidad, s.Computa)).ToList(),
        };
    }

    public sealed class VarianteJson
    {
        public string Nombre { get; set; } = string.Empty;
        public CondicionesJson Condiciones { get; set; } = new();
    }

    public sealed class RedondeoJson
    {
        public decimal Unidad { get; set; } = RedondeoDeCartera.Centavo;
        public RoundingMode Modo { get; set; } = RoundingMode.AwayFromZero;
        public RoundingMoment Momento { get; set; } = RoundingMoment.PerConcept;
    }

    public sealed class ExtraordinariaJson
    {
        public int Cuota { get; set; }
        public decimal Valor { get; set; }
    }

    public sealed class GraciaJson
    {
        public GraceKind Clase { get; set; }
        public int Periodos { get; set; }
        public GraceInterestTreatment? Tratamiento { get; set; }
        public int? CuotasDeDistribucion { get; set; }
    }

    public sealed class SeguroJson
    {
        public string Codigo { get; set; } = string.Empty;
        public InsuranceBase Base { get; set; }
        public decimal Tarifa { get; set; }
        public RepaymentPeriodicity Periodicidad { get; set; } = RepaymentPeriodicity.Monthly;
        public bool Computa { get; set; }
    }

    public sealed class SeriesJson
    {
        public List<UsuraJson> Usura { get; set; } = [];
        public List<DateOnly> Festivos { get; set; } = [];
        public List<IndiceJson> Indices { get; set; } = [];
    }

    public sealed class UsuraJson
    {
        public string Clave { get; set; } = string.Empty;
        public DateOnly Desde { get; set; }
        public decimal Ea { get; set; }
        public DateTime RegistradoEn { get; set; }
        public Guid? PublicId { get; set; }
        /// <summary>El valor que esta fila corrige (T28): lo usa US4.</summary>
        public Guid? Corrige { get; set; }
    }

    public sealed class IndiceJson
    {
        public string Codigo { get; set; } = string.Empty;
        public string? Clave { get; set; }
        public DateOnly Desde { get; set; }
        public decimal Valor { get; set; }
        public int? PeriodosNominales { get; set; }
        public DateTime RegistradoEn { get; set; }
    }

    /// <summary>Una operación con fecha valor y de registro (US4/US5); en esta fase los casos no traen ninguna.</summary>
    public sealed class OperacionJson
    {
        public string Id { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string? Modalidad { get; set; }
        public JsonElement? Valor { get; set; }
        public DateOnly FechaValor { get; set; }
        public DateTime RegistradoEn { get; set; }
        public JsonElement? Esperado { get; set; }
    }

    /// <summary>Una consulta a la fecha D con la información hasta R (US4).</summary>
    public sealed class ConsultaJson
    {
        public DateOnly Fecha { get; set; }
        public DateTime InformacionHasta { get; set; }
        public JsonElement? Esperado { get; set; }
    }

    public sealed class EsperadoJson
    {
        public TasasJson Tasas { get; set; } = new();
        public PlanJson Plan { get; set; } = new();
        public UsuraEsperadaJson Usura { get; set; } = new();
        public Dictionary<string, EsperadoJson>? Variantes { get; set; }
    }

    public sealed class TasasJson
    {
        public decimal Diaria { get; set; }
        public decimal NominalDelPeriodo { get; set; }
        public decimal? CuotaNivelada { get; set; }
        /// <summary>Efectiva con cargos, a 6 decimales.</summary>
        public decimal Efectiva { get; set; }
        public decimal EfectivaSinCargos { get; set; }
    }

    public sealed class PlanJson
    {
        public decimal InteresDeGracia { get; set; }
        public decimal Residuo { get; set; }
        public TotalesJson Totales { get; set; } = new();
        public List<CuotaJson> Cuotas { get; set; } = [];
    }

    public sealed class TotalesJson
    {
        public decimal Capital { get; set; }
        public decimal Interes { get; set; }
        public decimal Seguro { get; set; }
    }

    public sealed class CuotaJson
    {
        public int Numero { get; set; }
        public DateOnly Vencimiento { get; set; }
        public int Dias { get; set; }
        public decimal Capital { get; set; }
        public decimal Interes { get; set; }
        public decimal Seguro { get; set; }
        public decimal Saldo { get; set; }
    }

    public sealed class UsuraEsperadaJson
    {
        public VeredictoDeUsura Veredicto { get; set; }
        public decimal? Usura { get; set; }
        /// <summary>Usura − efectiva con cargos, a 6 decimales.</summary>
        public decimal? Holgura { get; set; }
    }
}
