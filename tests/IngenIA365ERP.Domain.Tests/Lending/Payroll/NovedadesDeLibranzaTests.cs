using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;

namespace IngenIA365ERP.Domain.Tests.Lending.Payroll;

/// <summary>
/// Feature 013, K2, T0803 (T78, FR-111, FR-113): el motor <see cref="NovedadesDeLibranza"/> coincide con los casos dorados de
/// <c>Casos/*.json</c>, calculados con la hoja <c>Casos/novedades.py</c> (decimal, sin el C#). Sin archivo base todas son altas; una cuota
/// distinta es un cambio con lo anterior; una obligación que ya no está (cancelada o trasladada) es una cancelación en cero; igual, sin
/// cambio. Con <c>IncludesPastDue</c> lo vencido se suma al total; sin él no viaja. Una línea por obligación, en orden de la fecha de la
/// libranza (prelación cronológica), y los totales por novedad.
/// </summary>
public class NovedadesDeLibranzaTests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Lending", "Payroll", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_novedades()
    {
        Directory.EnumerateFiles(Directorio, "*.json").Select(Path.GetFileNameWithoutExtension).Should().BeEquivalentTo(
            ["n01-sin-archivo-base", "n02-cambios-y-cancelaciones", "n03-prelacion-cronologica"]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_la_hoja(string archivo)
    {
        var caso = JsonSerializer.Deserialize<CasoJson>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var obligaciones = caso.Obligaciones.Select(o => new ExigibleDeLibranza(o.Credito, o.Vinculo, o.Persona, o.FechaLibranza, o.Exigible, o.Vencido));
        var archivoBase = caso.Base?.Select(b => new LineaDelArchivoBase(b.Credito, b.Vinculo, b.Persona, b.FechaLibranza, b.Total, b.Novedad));

        var r = NovedadesDeLibranza.Calcular(obligaciones, archivoBase, caso.IncluyeVencido);

        using var _ = new AssertionScope(caso.Nombre);
        r.Lineas.Select(l => (l.LineNumber, l.LoanId, l.Novedad, l.Corriente, l.Vencido, l.Total, l.Anterior)).Should().Equal(
            caso.Esperado.Lineas.Select(l => (l.Linea, l.Credito, l.Novedad, l.Corriente, l.Vencido, l.Total, l.Anterior)),
            "líneas en orden de la prelación, con su novedad y sus montos");
        var t = caso.Esperado.Totales;
        r.Totales.Should().Be(new TotalesDeNovedades(t.Nuevas, t.Cambios, t.Cancelaciones, t.SinCambio, t.MontoNuevas, t.MontoCambios, t.MontoSinCambio));
        r.Totales.Lineas.Should().Be(t.Lineas);
        r.Totales.Total.Should().Be(t.Total);
        r.Lineas.Select(l => l.LoanId).Should().OnlyHaveUniqueItems("una línea por obligación (UK PayrollDeductionFileId, LoanId)");
    }

    [Fact]
    public void La_cancelacion_conserva_el_vinculo_la_persona_y_la_fecha_del_base()
    {
        var r = NovedadesDeLibranza.Calcular([], [new LineaDelArchivoBase(7, 70, 700, new DateOnly(2025, 1, 2), 10_000m, PayrollNoveltyKind.New)
        {
            EmployeeIdAtPaymaster = "E-7",
        }], incluyeVencido: true);

        r.Lineas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            LineNumber = 1, LoanId = 7, LoanPayrollLinkId = 70, PersonId = 700, LibranzaDate = new DateOnly(2025, 1, 2),
            Novedad = PayrollNoveltyKind.Cancellation, Total = 0m, Anterior = (decimal?)10_000m, EmployeeIdAtPaymaster = "E-7",
        });
    }

    [Fact]
    public void Una_obligacion_repetida_o_un_monto_negativo_no_se_aceptan()
    {
        var o = new ExigibleDeLibranza(1, 1, 1, new DateOnly(2026, 1, 1), 10m, 0m);
        ((Action)(() => NovedadesDeLibranza.Calcular([o, o], null, false))).Should().Throw<ArgumentException>().WithMessage("*una línea por obligación*");
        ((Action)(() => NovedadesDeLibranza.Calcular([o with { Vencido = -1m }], null, true))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Las_lineas_llevan_la_cuota_del_periodo_que_reciben()
    {
        var o = new ExigibleDeLibranza(5, 50, 500, new DateOnly(2026, 1, 1), 1_000m, 0m)
        {
            RepaymentInstallmentId = 9_000_000_001L, InstallmentNumber = 3, DueDate = new DateOnly(2026, 4, 5), EmployeeIdAtPaymaster = "X1",
        };
        var linea = NovedadesDeLibranza.Calcular([o], null, false).Lineas.Single();
        (linea.RepaymentInstallmentId, linea.InstallmentNumber, linea.DueDate, linea.EmployeeIdAtPaymaster)
            .Should().Be((9_000_000_001L, 3, new DateOnly(2026, 4, 5), "X1"));
    }

    private sealed class CasoJson
    {
        public string Nombre { get; set; } = string.Empty;
        public bool IncluyeVencido { get; set; }
        public List<BaseJson>? Base { get; set; }
        public List<ObligacionJson> Obligaciones { get; set; } = [];
        public EsperadoJson Esperado { get; set; } = new();
    }

    private sealed class BaseJson
    {
        public int Credito { get; set; }
        public int Vinculo { get; set; }
        public int Persona { get; set; }
        public DateOnly FechaLibranza { get; set; }
        public decimal Total { get; set; }
        public PayrollNoveltyKind Novedad { get; set; }
    }

    private sealed class ObligacionJson
    {
        public int Credito { get; set; }
        public int Vinculo { get; set; }
        public int Persona { get; set; }
        public DateOnly FechaLibranza { get; set; }
        public decimal Exigible { get; set; }
        public decimal Vencido { get; set; }
    }

    private sealed class EsperadoJson
    {
        public List<LineaJson> Lineas { get; set; } = [];
        public TotalesJson Totales { get; set; } = new();
    }

    private sealed class LineaJson
    {
        public int Linea { get; set; }
        public int Credito { get; set; }
        public PayrollNoveltyKind Novedad { get; set; }
        public decimal Corriente { get; set; }
        public decimal Vencido { get; set; }
        public decimal Total { get; set; }
        public decimal? Anterior { get; set; }
    }

    private sealed class TotalesJson
    {
        public int Nuevas { get; set; }
        public int Cambios { get; set; }
        public int Cancelaciones { get; set; }
        public int SinCambio { get; set; }
        public decimal MontoNuevas { get; set; }
        public decimal MontoCambios { get; set; }
        public decimal MontoSinCambio { get; set; }
        public int Lineas { get; set; }
        public decimal Total { get; set; }
    }
}
