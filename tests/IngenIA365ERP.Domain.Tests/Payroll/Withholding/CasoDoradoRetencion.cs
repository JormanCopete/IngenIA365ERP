using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Withholding;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Domain.Tests.Payroll.Withholding;

/// <summary>
/// Un caso dorado del porcentaje fijo del procedimiento 2 (feature 010, US7): los meses con
/// su ingreso gravable y aportes, las deducciones declaradas, la secuencia de la política, la
/// tabla (la legal de la semilla o una propia del plan) y lo esperado calculado a mano.
/// </summary>
public sealed class CasoDoradoRetencion
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Payroll", "Withholding", "Casos");
    public static IEnumerable<string> Archivos() => Directory.GetFiles(DirectorioDeCasos, "*.json").OrderBy(f => f);
    public static CasoDoradoRetencion Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoRetencion>(File.ReadAllText(ruta), Opciones) ?? throw new InvalidOperationException($"{ruta} está vacío.");

    public string Nombre { get; set; } = string.Empty;
    public string Derivacion { get; set; } = string.Empty;
    public short Year { get; set; }
    public byte Semester { get; set; }
    public DateTime MesDeCalculo { get; set; }
    public DepurationSequence Secuencia { get; set; }
    public ModoDeTopesAnuales Topes { get; set; } = ModoDeTopesAnuales.Mensualizado;
    public List<MesJson> Meses { get; set; } = [];
    public List<DeduccionJson> Deducciones { get; set; } = [];
    public TablaJson? TablaDelPlan { get; set; }
    public EsperadoJson Esperado { get; set; } = new();

    public FixedRateInput ConstruirEntrada()
    {
        var catalogo = PayrollLegalParametersSeeder.Catalogo().Where(p => p.ValidFrom <= MesDeCalculo && (p.ValidTo == null || p.ValidTo >= MesDeCalculo)).ToList();
        PayrollLegalParameter tabla;
        if (TablaDelPlan is { } t)
        {
            catalogo.RemoveAll(p => p.Code.Equals(LegalParameterCodes.WithholdingTableUvt, StringComparison.OrdinalIgnoreCase));
            tabla = new PayrollLegalParameter
            {
                Code = LegalParameterCodes.WithholdingTableUvt, Name = "Tabla del plan", Kind = LegalParameterKind.RangeTable, ValidFrom = MesDeCalculo,
                Source = "Parámetros de retención del plan", RangeUnitParameterCode = LegalParameterCodes.Uvt, RangeIsMarginal = true,
            };
            var orden = 0;
            foreach (var r in t.Tramos) tabla.Ranges.Add(new PayrollLegalParameterRange { FromValue = r.Desde, ToValue = r.Hasta, Rate = r.Tarifa, FixedValue = r.Fijo, Order = ++orden });
            catalogo.Add(tabla);
        }
        var set = new ParameterSet(catalogo, MesDeCalculo);
        tabla = set.Table(LegalParameterCodes.WithholdingTableUvt);
        var meses = Meses.Select(m => new FixedRateMonth(m.Year, m.Month, m.Bruto, m.Aportes, m.Especial,
            [new FixedRateSourceRun(Guid.Empty, 1, m.Especial ? "ServiceBonus" : "Ordinary", m.Bruto)])).ToList();
        var deducciones = Deducciones.Select(d => new TaxDeductionInput(d.Tipo, d.Mensual, d.Porcentaje)).ToList();
        return new FixedRateInput(Guid.Empty, Year, Semester, meses, deducciones, set, tabla, TablaDelPlan is not null, Secuencia, Topes);
    }

    public sealed class MesJson { public short Year { get; set; } public byte Month { get; set; } public decimal Bruto { get; set; } public decimal Aportes { get; set; } public bool Especial { get; set; } }
    public sealed class DeduccionJson { public TaxDeductionKind Tipo { get; set; } public decimal? Mensual { get; set; } public decimal? Porcentaje { get; set; } }
    public sealed class TablaJson { public List<TramoJson> Tramos { get; set; } = []; }
    public sealed class TramoJson { public decimal Desde { get; set; } public decimal? Hasta { get; set; } public decimal Tarifa { get; set; } public decimal Fijo { get; set; } }

    public sealed class EsperadoJson
    {
        public int MesesConsiderados { get; set; }
        public decimal Divisor { get; set; }
        public decimal BasePromedio { get; set; }
        public decimal PromedioUvt { get; set; }
        public decimal RetencionTeorica { get; set; }
        public decimal Porcentaje { get; set; }
    }
}
