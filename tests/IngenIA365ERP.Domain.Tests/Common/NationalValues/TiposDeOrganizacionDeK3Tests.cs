using FluentAssertions;
using IngenIA365ERP.Domain.Common.NationalValues;

namespace IngenIA365ERP.Domain.Tests.Common.NationalValues;

/// <summary>
/// Feature 013, K3 (US14, T0910, T0912; FR-095, FR-102): los datos opcionales del documento <c>TiposDeOrganizacion</c> que consume el castigo y la
/// evaluación de cartera —si el tipo debe reportar los castigos al supervisor y en cuántos días, y la periodicidad de la evaluación—, sin cambiar la versión
/// de esquema: sin ellos el documento se lee igual que antes. (nuevo)
/// </summary>
public class TiposDeOrganizacionDeK3Tests
{
    private static string Documento(string extra) => $$"""
        {
          "employeeFundCategories": [],
          "organizationTypes": [
            { "code": "COOP_AHORRO_CREDITO", "name": "Cooperativa", "isEmployeeFund": false, "expectedLoss": "Admitted"{{extra}} }
          ]
        }
        """;

    [Fact]
    public void Sin_los_datos_de_K3_el_tipo_no_exige_reporte_ni_periodicidad()
    {
        var r = TiposDeOrganizacion.Leer(Documento(string.Empty), 1);

        r.Errores.Should().BeEmpty();
        var tipo = r.Documento!.Buscar("COOP_AHORRO_CREDITO")!;
        (tipo.WriteOffReport, tipo.PortfolioEvaluation).Should().Be((null, null));
    }

    [Fact]
    public void Lee_el_reporte_de_castigos_y_la_periodicidad_de_la_evaluacion()
    {
        var r = TiposDeOrganizacion.Leer(Documento("""
            , "writeOffReport": { "required": true, "dueDays": 30 }, "portfolioEvaluation": { "cutoffMonths": [6, 12], "effectMonthsAfterCutoff": 1 }
            """), 1);

        r.Errores.Should().BeEmpty();
        var tipo = r.Documento!.Buscar("COOP_AHORRO_CREDITO")!;
        tipo.WriteOffReport.Should().Be(new ReporteDeCastigos(true, 30));
        tipo.PortfolioEvaluation!.CutoffMonths.Should().Equal(6, 12);
        tipo.PortfolioEvaluation.Admite(new DateOnly(2026, 12, 31), 2027, 1).Should().BeTrue();
        tipo.PortfolioEvaluation.Admite(new DateOnly(2026, 12, 31), 2027, 2).Should().BeFalse("el efecto es un mes después del corte");
        tipo.PortfolioEvaluation.Admite(new DateOnly(2026, 9, 30), 2026, 10).Should().BeFalse("septiembre no es mes de corte");
    }

    [Theory]
    [InlineData("""{ "required": true }""", "dueDays")]
    [InlineData("""{ "required": true, "dueDays": 0 }""", "dueDays")]
    [InlineData("""[]""", "writeOffReport")]
    public void Un_reporte_obligatorio_sin_plazo_valido_es_error(string reporte, string campo)
    {
        var r = TiposDeOrganizacion.Leer(Documento($", \"writeOffReport\": {reporte}"), 1);

        r.Documento.Should().BeNull();
        r.Errores.Should().Contain(e => e.Ruta.Contains(campo));
    }

    [Theory]
    [InlineData("""{ "cutoffMonths": [13], "effectMonthsAfterCutoff": 1 }""", "cutoffMonths")]
    [InlineData("""{ "cutoffMonths": [], "effectMonthsAfterCutoff": 1 }""", "cutoffMonths")]
    [InlineData("""{ "cutoffMonths": [12], "effectMonthsAfterCutoff": -1 }""", "effectMonthsAfterCutoff")]
    public void Una_periodicidad_invalida_es_error(string evaluacion, string campo)
    {
        var r = TiposDeOrganizacion.Leer(Documento($", \"portfolioEvaluation\": {evaluacion}"), 1);

        r.Documento.Should().BeNull();
        r.Errores.Should().Contain(e => e.Ruta.Contains(campo));
    }
}
