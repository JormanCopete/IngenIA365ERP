using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Payroll.Withholding;

namespace IngenIA365ERP.Domain.Tests.Payroll.Withholding;

/// <summary>
/// SC-006 de la feature 010: el porcentaje fijo del procedimiento 2 coincide con el hecho a
/// mano (ET art. 386; research R8) y la contadora lo reconstruye mes a mes desde la explicación.
/// </summary>
public class CasosDoradosDeRetencionTests
{
    public static IEnumerable<object[]> Casos() => CasoDoradoRetencion.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_porcentaje_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = CasoDoradoRetencion.Cargar(Path.Combine(CasoDoradoRetencion.DirectorioDeCasos, archivo));
        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
        caso.Derivacion.Should().NotBeNullOrWhiteSpace("todo caso dorado lleva su derivación a mano");

        var r = FixedRateCalculator.Calculate(caso.ConstruirEntrada());
        var e = caso.Esperado;
        var porque = string.Join("\n", r.Steps.Select(s => $"  {s.Label}{(s.Value is { } v ? $" = {v:N2}" : string.Empty)}"));

        r.MonthsConsidered.Should().Be(e.MesesConsiderados, porque);
        r.Divisor.Should().Be(e.Divisor, porque);
        Math.Round(r.AverageMonthlyBase, 2).Should().Be(e.BasePromedio, porque);
        Math.Round(r.AverageInUvt, 4).Should().Be(e.PromedioUvt, porque);
        Math.Round(r.TheoreticalWithholding, 2).Should().Be(e.RetencionTeorica, porque);
        r.RatePercent.Should().Be(e.Porcentaje, porque);
        r.Sequence.Should().Be(caso.Secuencia);
        r.Steps.Should().Contain(s => s.Label.StartsWith("Divisor"), "la explicación dice de dónde salió el divisor");
        r.Steps.Count(s => s.Label.Contains(": ingreso gravable")).Should().Be(e.MesesConsiderados, "un paso por mes");
    }
}
