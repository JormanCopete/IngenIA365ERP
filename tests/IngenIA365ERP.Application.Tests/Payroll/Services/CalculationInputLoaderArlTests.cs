using FluentAssertions;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Application.Tests.Payroll.Services;

/// <summary>
/// La clase de riesgo ARL que llega al motor es el <c>Code</c> de la fila de
/// <c>PAY_WorkRiskRates</c> que la ficha referencia, no su Id. Hasta el 2026-09-11 el
/// cargador tomaba el Id como clase: con las filas sembradas después de cualquier otra
/// (Ids 2..6) un empleado de clase I salía como clase II, y en QA, sin filas, todos
/// quedaban «sin clase» aunque la ficha dijera 1.
/// </summary>
public class CalculationInputLoaderArlTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task La_clase_es_el_Code_de_la_tarifa_no_su_Id(int clase)
    {
        var d = new NominaTestData();
        var carlos = d.Empleado("Carlos", 2_500_000m, new DateTime(2025, 1, 1), claseArl: clase);
        carlos.WorkRiskRateId.Should().NotBe(clase, "la prueba sólo vale si Id y clase difieren");

        var batch = await d.Loader.LoadAsync(d.Marzo, CancellationToken.None);

        var cargado = batch.Employees.Single(e => e.Employee.Id == carlos.Id);
        cargado.Input.Affiliations.WorkRiskClass.Should().Be(clase);
    }

    [Fact]
    public async Task Sin_tarifa_en_la_ficha_la_clase_es_nula_y_el_motor_bloquea()
    {
        var d = new NominaTestData();
        var sinClase = d.Empleado("Diana", 2_500_000m, new DateTime(2025, 1, 1), claseArl: 0);

        var batch = await d.Loader.LoadAsync(d.Marzo, CancellationToken.None);

        var cargado = batch.Employees.Single(e => e.Employee.Id == sinClase.Id);
        cargado.Input.Affiliations.WorkRiskClass.Should().BeNull();

        var resultado = new PayrollCalculationEngine().Calculate(batch.InputFor(cargado));
        resultado.Flags.Should().HaveFlag(RunEmployeeFlag.MissingAffiliation);
        resultado.Refusals.Should().ContainMatch("*Sin clase de riesgo ARL*");
    }

    [Fact]
    public async Task Una_tarifa_que_apunta_a_una_fila_inexistente_no_inventa_clase()
    {
        var d = new NominaTestData();
        var huerfano = d.Empleado("Elena", 2_500_000m, new DateTime(2025, 1, 1));
        huerfano.WorkRiskRateId = 9_999;
        await d.Db.SaveChangesAsync();

        var batch = await d.Loader.LoadAsync(d.Marzo, CancellationToken.None);

        batch.Employees.Single(e => e.Employee.Id == huerfano.Id).Input.Affiliations.WorkRiskClass.Should().BeNull();
    }

    [Fact]
    public void La_semilla_trae_las_cinco_clases_con_el_porcentaje_del_parametro_legal()
    {
        var clases = WorkRiskClassesSeeder.Catalogo();
        var parametros = PayrollLegalParametersSeeder.Catalogo().ToDictionary(p => p.Code, p => p.Value);

        clases.Select(c => c.Code).Should().Equal(1, 2, 3, 4, 5);
        foreach (var c in clases)
            c.Rate.Should().Be(parametros[LegalParameterCodes.WorkRiskPct(c.Code)], "una sola fuente para el porcentaje");
    }
}
