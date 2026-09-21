using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Application.Payroll.Pila;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Pila;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Domain.Tests.Payroll.Pila;

/// <summary>
/// Un caso dorado de la PILA (feature 010, US5): el mes, el aportante, las políticas, los
/// cotizantes con lo que la ficha y las corridas dicen de ellos, y lo esperado calculado a
/// mano (líneas, días, IBC y aportes por subsistema, totales). Los parámetros son la semilla
/// 2026 vigente al primer día del mes, salvo lo que el caso sobrescriba. El archivo
/// <c>.esperado.txt</c> hermano se compara byte a byte.
/// </summary>
public sealed class CasoDoradoPila
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Payroll", "Pila", "Casos");

    public static IEnumerable<string> Archivos() => Directory.GetFiles(DirectorioDeCasos, "*.json").OrderBy(f => f);

    public static CasoDoradoPila Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoPila>(File.ReadAllText(ruta), Opciones) ?? throw new InvalidOperationException($"{ruta} está vacío.");

    public string Nombre { get; set; } = string.Empty;
    public string Derivacion { get; set; } = string.Empty;
    public short Year { get; set; }
    public byte Month { get; set; }
    public EmpresaJson Empresa { get; set; } = new();
    public PoliticasJson Politicas { get; set; } = new();
    public Dictionary<string, decimal> Parametros { get; set; } = [];
    public List<CotizanteJson> Cotizantes { get; set; } = [];
    public EsperadoJson Esperado { get; set; } = new();

    /// <summary>La tabla del fondo de solidaridad que regía antes de la vigente al mes del caso (para el régimen de transición), o nula.</summary>
    /// <summary>La semilla completa: el catálogo 2026 más las revisiones con vigencia futura, cerrando la anterior de cada código como hace el seeder.</summary>
    public static List<PayrollLegalParameter> SemillaCompleta()
    {
        var lista = PayrollLegalParametersSeeder.Catalogo().ToList();
        foreach (var revision in PayrollLegalParametersSeeder.Revisiones().OrderBy(r => r.ValidFrom))
        {
            var ultima = lista.Where(p => p.Code.Equals(revision.Code, StringComparison.OrdinalIgnoreCase) && p.ValidFrom < revision.ValidFrom).OrderByDescending(p => p.ValidFrom).FirstOrDefault();
            if (ultima is not null && ultima.ValidTo is null) ultima.ValidTo = revision.ValidFrom.AddDays(-1);
            lista.Add(revision);
        }
        return lista;
    }

    public PayrollLegalParameter? TablaFspDeTransicion()
    {
        var inicio = new DateTime(Year, Month, 1);
        var tablas = SemillaCompleta().Where(p => p.Code == LegalParameterCodes.SolidarityFundTable).OrderBy(p => p.ValidFrom).ToList();
        var vigente = tablas.LastOrDefault(p => p.ValidFrom <= inicio);
        return vigente is null ? null : tablas.LastOrDefault(p => p.ValidFrom < vigente.ValidFrom);
    }

    public PilaInput ConstruirEntrada()
    {
        var inicio = new DateTime(Year, Month, 1);
        var catalogo = SemillaCompleta().Where(p => p.ValidFrom <= inicio && (p.ValidTo == null || p.ValidTo >= inicio)).ToList();
        foreach (var (codigo, valor) in Parametros)
        {
            catalogo.RemoveAll(p => p.Code.Equals(codigo, StringComparison.OrdinalIgnoreCase));
            catalogo.Add(new PayrollLegalParameter { Code = codigo, Name = codigo, Kind = LegalParameterKind.Amount, Value = valor, ValidFrom = inicio });
        }
        var layout = PilaLayoutCatalog.ForPeriod(DateOnly.FromDateTime(inicio)) ?? throw new InvalidOperationException("Sin layout vigente para el caso.");
        var employer = new PilaEmployer(Empresa.Nombre, Empresa.Nit, Empresa.Dv, "1", Empresa.Clase, "U", null, null, Empresa.ArlPilaCode, Empresa.CodigoOperador, "E", Empresa.Dane, Empresa.ActividadEconomica);
        var cotizantes = Cotizantes.Select((c, i) => new PilaContributor
        {
            EmployeeId = i + 1, EmployeePublicId = Guid.Parse($"00000000-0000-0000-0000-{i + 1:000000000000}"),
            DocumentType = c.TipoDocumento, Document = c.Documento,
            FirstLastName = c.PrimerApellido, SecondLastName = c.SegundoApellido, FirstName = c.PrimerNombre, OtherNames = c.SegundoNombre,
            Class = c.Clase, ApprenticeStage = c.Etapa, ContributorTypeOverride = c.TipoCotizante, ContributorSubTypeOverride = c.SubtipoCotizante,
            ForeignNotRequiredToContributePension = c.ExtranjeroSinPension, ColombianAbroad = false,
            MunicipalityDaneCode = c.Dane, EconomicActivityCode = c.ActividadEconomica, WorkCenterCode = c.CentroDeTrabajo,
            HighRiskPension = c.AltoRiesgo, TransitionRegime = c.RegimenTransicion,
            HireDate = c.Ingreso, TerminationDate = c.Retiro, BasicSalary = c.Salario, SalaryChangeDate = c.CambioDeSalario,
            HasHealthProvider = c.Eps is not null, HealthPilaCode = c.Eps, HasPensionProvider = c.Afp is not null, PensionPilaCode = c.Afp,
            HasWorkRiskProvider = c.Arl is not null, WorkRiskPilaCode = c.Arl, HasFamilyCompensationFund = c.Ccf is not null, FamilyCompensationPilaCode = c.Ccf,
            WorkRiskClass = c.ClaseRiesgo,
            ContributionEarnings = c.DevengosIbc ?? c.Salario, DaysWorked = c.DiasTrabajados ?? 30, VoluntaryPensionEmployee = c.AporteVoluntario,
            Novelties = c.Novedades.Select(n => new PilaNovelty(n.Tipo, n.Desde, n.Hasta, n.Autorizacion)).ToList(),
        }).ToList();
        return new PilaInput(Year, Month, employer, layout, new ParameterSet(catalogo, inicio), new PilaPolicies(Politicas.Exonerada114_1, Politicas.CotizaArlEnVacaciones), cotizantes);
    }

    public sealed class EmpresaJson
    {
        public string Nombre { get; set; } = "COOPERATIVA DE PRUEBA";
        public string Nit { get; set; } = "900123456";
        public string Dv { get; set; } = "7";
        public string Clase { get; set; } = "B";
        public string ArlPilaCode { get; set; } = "14-23";
        public string CodigoOperador { get; set; } = "24";
        public string Dane { get; set; } = "76001";
        public string ActividadEconomica { get; set; } = "1649501";
    }

    public sealed class PoliticasJson
    {
        public bool Exonerada114_1 { get; set; } = true;
        public bool CotizaArlEnVacaciones { get; set; }
    }

    public sealed class CotizanteJson
    {
        public string TipoDocumento { get; set; } = "CC";
        public string Documento { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;
        public string? SegundoApellido { get; set; }
        public string PrimerNombre { get; set; } = string.Empty;
        public string? SegundoNombre { get; set; }
        public EmployeeClass Clase { get; set; } = EmployeeClass.Standard;
        public ApprenticeStage? Etapa { get; set; }
        public string? TipoCotizante { get; set; }
        public string? SubtipoCotizante { get; set; }
        public bool ExtranjeroSinPension { get; set; }
        public string? Dane { get; set; }
        public string? ActividadEconomica { get; set; }
        public string? CentroDeTrabajo { get; set; }
        public bool AltoRiesgo { get; set; }
        public PensionTransitionRegime RegimenTransicion { get; set; } = PensionTransitionRegime.Unknown;
        public DateTime Ingreso { get; set; }
        public DateTime? Retiro { get; set; }
        public decimal Salario { get; set; }
        public DateTime? CambioDeSalario { get; set; }
        public string? Eps { get; set; } = "EPS010";
        public string? Afp { get; set; } = "230301";
        public string? Arl { get; set; } = "14-23";
        public string? Ccf { get; set; } = "CCF24";
        public int? ClaseRiesgo { get; set; } = 1;
        public decimal? DevengosIbc { get; set; }
        public int? DiasTrabajados { get; set; }
        public decimal AporteVoluntario { get; set; }
        public List<NovedadJson> Novedades { get; set; } = [];
    }

    public sealed class NovedadJson
    {
        public PilaNoveltyKind Tipo { get; set; }
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public string? Autorizacion { get; set; }
    }

    public sealed class EsperadoJson
    {
        public int Cotizantes { get; set; }
        public int Lineas { get; set; }
        public decimal TotalIbcCcf { get; set; }
        public decimal TotalAportes { get; set; }
        public List<string> Bloqueantes { get; set; } = [];
        public List<string> Alertas { get; set; } = [];
        public List<LineaEsperadaJson> LineasEsperadas { get; set; } = [];
    }

    public sealed class LineaEsperadaJson
    {
        public int N { get; set; }
        public string Documento { get; set; } = string.Empty;
        public string? Novedades { get; set; }
        public string? Tipo { get; set; }
        public int Dias { get; set; }
        public decimal Ibc { get; set; }
        public decimal Pension { get; set; }
        public decimal Fsp { get; set; }
        public decimal Salud { get; set; }
        public decimal Arl { get; set; }
        public decimal Ccf { get; set; }
        public decimal Sena { get; set; }
        public decimal Icbf { get; set; }
        public bool Exonerado { get; set; }
        public int? Horas { get; set; }
    }
}
