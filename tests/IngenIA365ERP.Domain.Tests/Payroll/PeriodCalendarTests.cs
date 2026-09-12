using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Xunit;

namespace IngenIA365ERP.Domain.Tests.Payroll;

public class PeriodCalendarTests
{
    [Theory]
    [InlineData(PayrollPeriodicity.Monthly, "2026-03-01", 1)]
    [InlineData(PayrollPeriodicity.Biweekly, "2026-03-01", 1)]
    [InlineData(PayrollPeriodicity.Biweekly, "2026-03-16", 2)]
    [InlineData(PayrollPeriodicity.TenDay, "2026-03-11", 2)]
    [InlineData(PayrollPeriodicity.TenDay, "2026-03-21", 3)]
    [InlineData(PayrollPeriodicity.Weekly, "2026-03-01", 1)]
    [InlineData(PayrollPeriodicity.Weekly, "2026-03-08", 2)]
    [InlineData(PayrollPeriodicity.Weekly, "2026-03-29", 5)]
    public void Propone_el_subperiodo_por_la_fecha_de_inicio(PayrollPeriodicity p, string inicio, int esperado)
    {
        var propuesta = PeriodCalendar.Proponer(p, DateTime.Parse(inicio));
        propuesta.SubPeriodNumber.Should().Be((byte)esperado);
        propuesta.Month.Should().Be((byte)DateTime.Parse(inicio).Month);
    }

    [Theory]
    [InlineData(PayrollPeriodicity.Monthly, 1, null, true)]
    [InlineData(PayrollPeriodicity.Biweekly, 1, null, false)]
    [InlineData(PayrollPeriodicity.Biweekly, 2, null, true)]
    [InlineData(PayrollPeriodicity.TenDay, 3, null, true)]
    [InlineData(PayrollPeriodicity.Weekly, 4, (byte)4, true)]
    [InlineData(PayrollPeriodicity.Weekly, 4, (byte)5, false)]
    [InlineData(PayrollPeriodicity.Weekly, 5, null, true)]
    public void Sabe_cual_es_el_ultimo_del_mes(PayrollPeriodicity p, int sub, byte? mayorSemana, bool esperado) =>
        PeriodCalendar.EsUltimoDelMes(p, (byte)sub, mayorSemana).Should().Be(esperado);

    [Theory]
    [InlineData(PayrollPeriodicity.Monthly, "2026-02-01", "2026-02-28", true)]
    [InlineData(PayrollPeriodicity.Monthly, "2026-03-01", "2026-03-31", true)]
    [InlineData(PayrollPeriodicity.Monthly, "2026-03-01", "2026-03-20", false)]
    [InlineData(PayrollPeriodicity.Biweekly, "2026-03-01", "2026-03-15", true)]
    [InlineData(PayrollPeriodicity.Biweekly, "2026-03-16", "2026-03-31", true)]
    [InlineData(PayrollPeriodicity.Biweekly, "2026-02-16", "2026-02-28", true)]
    [InlineData(PayrollPeriodicity.Biweekly, "2026-03-01", "2026-03-10", false)]
    [InlineData(PayrollPeriodicity.TenDay, "2026-03-21", "2026-03-31", true)]
    [InlineData(PayrollPeriodicity.TenDay, "2026-02-21", "2026-02-28", true)]
    [InlineData(PayrollPeriodicity.TenDay, "2026-03-01", "2026-03-12", false)]
    [InlineData(PayrollPeriodicity.Weekly, "2026-03-02", "2026-03-08", true)]
    [InlineData(PayrollPeriodicity.Weekly, "2026-03-02", "2026-03-09", false)]
    public void Valida_la_duracion_con_las_excepciones_del_calendario(PayrollPeriodicity p, string ini, string fin, bool ok) =>
        PeriodCalendar.DuracionValida(p, DateTime.Parse(ini), DateTime.Parse(fin)).Should().Be(ok);
}
