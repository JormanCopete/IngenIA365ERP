using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Pila;

namespace IngenIA365ERP.Domain.Tests.Payroll.Pila;

/// <summary>Cada inconsistencia con su severidad, su campo y su ruta (FR-025; taxonomía de Aportes en Línea).</summary>
public class PilaValidatorTests
{
    private static CasoDoradoPila Caso() => CasoDoradoPila.Cargar(Path.Combine(CasoDoradoPila.DirectorioDeCasos, "01-diciembre-2026-once-empleados.json"));

    private static PilaInput Entrada(Action<CasoDoradoPila> ajusta)
    {
        var caso = Caso();
        ajusta(caso);
        return caso.ConstruirEntrada();
    }

    [Fact]
    public void Sin_eps_es_bloqueante_con_enlace_a_la_ficha()
    {
        var input = Entrada(c => c.Cotizantes[0].Eps = null);
        var issue = PilaValidator.Validate(input).Should().ContainSingle(i => i.Code == "Pila.SinEps").Subject;
        issue.Severity.Should().Be(PilaIssueSeverity.Blocking);
        issue.Field.Should().Be(33);
        issue.LinkRoute.Should().StartWith("/nomina/empleados/");
        issue.EmployeeName.Should().Contain("Arias");
    }

    [Fact]
    public void Catalogo_sin_codigo_pila_es_bloqueante_con_enlace_al_catalogo()
    {
        var input = Entrada(c => c.Cotizantes[0].Eps = "   ");
        // Eps con blancos: hay EPS pero sin código PILA.
        var cot = input.Contributors[0] with { HasHealthProvider = true, HealthPilaCode = null };
        var i2 = input with { Contributors = [cot] };
        var issue = PilaValidator.Validate(i2).Should().ContainSingle(i => i.Code == "Pila.SinCodigoPila" && i.Field == 33).Subject;
        issue.LinkRoute.Should().Be("/nomina/eps");
    }

    [Fact]
    public void Documento_mas_largo_que_la_resolucion_1529_es_bloqueante_desde_octubre_de_2026()
    {
        var input = Entrada(c => c.Cotizantes[0].Documento = "12345678901");
        PilaValidator.Validate(input).Should().Contain(i => i.Code == "Pila.DocumentoLargo" && i.Severity == PilaIssueSeverity.Blocking && i.Field == 4);
        var antes = Entrada(c => { c.Cotizantes[0].Documento = "12345678901"; c.Year = 2026; c.Month = 9; });
        PilaValidator.Validate(antes).Should().NotContain(i => i.Code == "Pila.DocumentoLargo", "la regla rige para pagos desde el 01-10-2026");
    }

    [Fact]
    public void Segundo_apellido_faltante_es_alerta_y_no_bloquea()
    {
        var input = Caso().ConstruirEntrada();
        var issues = PilaValidator.Validate(input);
        issues.Should().Contain(i => i.Code == "Pila.SegundoApellidoFaltante" && i.Severity == PilaIssueSeverity.Warning && i.Field == 12);
        issues.Should().NotContain(i => i.Severity == PilaIssueSeverity.Blocking);
    }

    [Fact]
    public void Sin_divipola_ni_actividad_en_ficha_ni_aportante_es_bloqueante()
    {
        var input = Entrada(c => { c.Empresa.Dane = ""; c.Empresa.ActividadEconomica = ""; });
        var issues = PilaValidator.Validate(input);
        issues.Should().Contain(i => i.Code == "Pila.SinDivipola" && i.Severity == PilaIssueSeverity.Blocking);
        issues.Should().Contain(i => i.Code == "Pila.SinActividadEconomica" && i.Severity == PilaIssueSeverity.Blocking);
    }

    [Fact]
    public void Aportante_sin_arl_ni_operador_es_bloqueante_con_enlace_a_los_ajustes()
    {
        var input = Entrada(c => { c.Empresa.ArlPilaCode = ""; c.Empresa.CodigoOperador = ""; });
        var issues = PilaValidator.Validate(input);
        issues.Should().Contain(i => i.Code == "Pila.AportanteSinArl" && i.LinkRoute == "/nomina/pila?ajustes=1");
        issues.Should().Contain(i => i.Code == "Pila.AportanteSinOperador" && i.Field == 22);
    }

    [Fact]
    public void Regimen_de_transicion_desconocido_es_alerta_solo_desde_abril_de_2027()
    {
        PilaValidator.Validate(Entrada(c => { c.Year = 2027; c.Month = 4; })).Should().Contain(i => i.Code == "Pila.RegimenTransicionDesconocido" && i.Severity == PilaIssueSeverity.Warning);
        PilaValidator.Validate(Caso().ConstruirEntrada()).Should().NotContain(i => i.Code == "Pila.RegimenTransicionDesconocido");
    }

    [Fact]
    public void El_aprendiz_en_etapa_lectiva_no_necesita_afp_ni_ccf()
    {
        var issues = PilaValidator.Validate(Caso().ConstruirEntrada());
        issues.Should().NotContain(i => i.Code == "Pila.SinAfp" || i.Code == "Pila.SinCcf");
    }

    [Fact]
    public void Los_dias_de_un_cotizante_sin_ingreso_ni_retiro_deben_sumar_30()
    {
        var input = Entrada(c => c.Cotizantes[2].Novedades[0].Hasta = new DateTime(2027, 1, 20)); // IGE que cruza el mes: se recorta al 31
        var r = PilaBuilder.Build(input);
        r.Issues.Should().NotContain(i => i.Code == "Pila.DiasNoSuman30", "las novedades se recortan a la ventana del mes");
        var castro = r.Lines.Where(l => l.Contributor.Document == "1003").ToList();
        castro.Sum(l => l.DaysPension).Should().Be(30);
    }
}
