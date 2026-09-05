using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Domain.Tests.Payroll.Calculation;

/// <summary>
/// Un caso dorado del motor (SC-001, D-16): un archivo JSON en <c>Casos/</c> que una
/// contadora puede leer y firmar. Describe el período, el empleado, sus novedades y la
/// política de la cooperativa, y lo que DEBE salir al peso. Los conceptos y los
/// parámetros legales son exactamente los de la semilla 2026
/// (<see cref="PayrollConceptDefinitionsSeeder"/>, <see cref="PayrollLegalParametersSeeder"/>),
/// salvo los valores que el caso sobreescriba en <c>parametros</c>.
/// </summary>
public sealed class CasoDorado
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
    public PeriodoJson Periodo { get; set; } = new();
    public EmpleadoJson Empleado { get; set; } = new();
    public List<NovedadJson> Novedades { get; set; } = [];
    public PoliticasJson Politicas { get; set; } = new();
    public Dictionary<string, decimal> Parametros { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public EsperadoJson Esperado { get; set; } = new();

    public static string DirectorioDeCasos =>
        Path.Combine(AppContext.BaseDirectory, "Payroll", "Calculation", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDorado Cargar(string path) =>
        JsonSerializer.Deserialize<CasoDorado>(File.ReadAllText(path), Opciones)
        ?? throw new InvalidOperationException($"El caso {path} está vacío.");

    public CalculationInput ConstruirEntrada()
    {
        var conceptos = PayrollConceptDefinitionsSeeder.Catalogo();
        var id = 0;
        foreach (var c in conceptos) c.Id = ++id;

        var parametros = PayrollLegalParametersSeeder.Catalogo();
        id = 0;
        foreach (var p in parametros)
        {
            p.Id = ++id;
            if (Parametros.TryGetValue(p.Code, out var valor)) p.Value = valor;
        }

        return new CalculationInput
        {
            Period = new PeriodInput(Periodo.Inicio, Periodo.Fin, Periodo.Periodicidad),
            Employee = new EmployeeInput
            {
                PublicId = Empleado.Id,
                DisplayName = Empleado.Nombre ?? Nombre,
                Class = Empleado.Clase,
                JoinDate = Empleado.Ingreso,
                TerminationDate = Empleado.Retiro,
                SalaryHistory = Empleado.Salarios.Select(s => new SalaryChangeInput(s.Desde, s.Mensual)).ToList(),
                Affiliations = new AffiliationsInput
                {
                    Health = Empleado.Afiliaciones.Salud,
                    Pension = Empleado.Afiliaciones.Pension,
                    WorkRiskClass = Empleado.Afiliaciones.Arl,
                    FamilyCompensation = Empleado.Afiliaciones.Caja,
                },
                WithholdingProcedure = Empleado.ProcedimientoRetencion,
                WithholdingRatePercent = Empleado.TasaRetencion,
                TaxDeductions = Empleado.DeduccionesTributarias
                    .Select(d => new TaxDeductionInput(d.Tipo, d.Mensual, d.Porcentaje)).ToList(),
            },
            Novelties = Novedades.Select((n, i) => new NoveltyInput
            {
                PublicId = n.Id ?? new Guid(i + 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                ConceptCode = n.Concepto,
                Quantity = n.Cantidad,
                Amount = n.Valor,
                StartDate = n.Desde,
                EndDate = n.Hasta,
                Origin = n.Origen,
                Description = n.Descripcion,
            }).ToList(),
            Concepts = conceptos,
            Parameters = parametros,
            Policies = new CalculationPolicies { Rounding = Politicas.Redondeo, ApplyEmployerExemption = Politicas.Exoneracion },
        };
    }

    public sealed class PeriodoJson
    {
        public DateTime Inicio { get; set; }
        public DateTime Fin { get; set; }
        public PayrollPeriodicity Periodicidad { get; set; } = PayrollPeriodicity.Monthly;
    }

    public sealed class EmpleadoJson
    {
        public Guid Id { get; set; } = new("11111111-1111-1111-1111-111111111111");
        public string? Nombre { get; set; }
        public EmployeeClass Clase { get; set; } = EmployeeClass.Standard;
        public DateTime Ingreso { get; set; }
        public DateTime? Retiro { get; set; }
        public List<SalarioJson> Salarios { get; set; } = [];
        public AfiliacionesJson Afiliaciones { get; set; } = new();
        public byte ProcedimientoRetencion { get; set; } = 1;
        public decimal? TasaRetencion { get; set; }
        public List<DeduccionTributariaJson> DeduccionesTributarias { get; set; } = [];
    }

    public sealed class SalarioJson
    {
        public DateTime Desde { get; set; }
        public decimal Mensual { get; set; }
    }

    public sealed class AfiliacionesJson
    {
        public bool Salud { get; set; } = true;
        public bool Pension { get; set; } = true;
        public int? Arl { get; set; } = 1;
        public bool Caja { get; set; } = true;
    }

    public sealed class DeduccionTributariaJson
    {
        public TaxDeductionKind Tipo { get; set; }
        public decimal? Mensual { get; set; }
        public decimal? Porcentaje { get; set; }
    }

    public sealed class NovedadJson
    {
        public Guid? Id { get; set; }
        public string Concepto { get; set; } = string.Empty;
        public decimal? Cantidad { get; set; }
        public decimal? Valor { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public NoveltyOrigin Origen { get; set; } = NoveltyOrigin.Manual;
        public string? Descripcion { get; set; }
    }

    public sealed class PoliticasJson
    {
        public PayrollRounding Redondeo { get; set; } = PayrollRounding.Peso;
        public bool Exoneracion { get; set; }
    }

    public sealed class EsperadoJson
    {
        /// <summary>Código → valor redondeado. Toda línea del motor que no esté aquí debe valer cero.</summary>
        public Dictionary<string, decimal> Lineas { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public TotalesJson Totales { get; set; } = new();
        public RunEmployeeFlag Banderas { get; set; } = RunEmployeeFlag.None;
        public int? DiasPagados { get; set; }
        public int? DiasAusencia { get; set; }
    }

    public sealed class TotalesJson
    {
        public decimal Devengos { get; set; }
        public decimal Deducciones { get; set; }
        public decimal AportesEmpleador { get; set; }
        public decimal Provisiones { get; set; }
        public decimal Neto { get; set; }
        public decimal AjusteRedondeo { get; set; }
    }
}
