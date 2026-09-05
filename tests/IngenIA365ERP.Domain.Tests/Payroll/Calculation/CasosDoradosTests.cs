using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Tests.Payroll.Calculation;

/// <summary>
/// SC-001: la liquidación del motor coincide al peso con la hecha a mano. Cada caso es
/// un JSON en <c>Casos/</c>; añadir un caso es añadir un archivo. Cuando un caso
/// falla, el mensaje dice qué línea, cuánto esperaba y cuánto salió, y adjunta la
/// explicación del motor para esa línea.
/// </summary>
public class CasosDoradosTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDorado.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_al_peso_con_la_liquidacion_manual(string archivo)
    {
        var caso = CasoDorado.Cargar(Path.Combine(CasoDorado.DirectorioDeCasos, archivo));
        var resultado = new PayrollCalculationEngine().Calculate(caso.ConstruirEntrada());

        using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");

        resultado.Refusals.Should().BeEmpty("el motor no debería negarse en un caso dorado");

        foreach (var (codigo, esperado) in caso.Esperado.Lineas)
        {
            var lineas = resultado.Lines.Where(l => l.Code.Equals(codigo, StringComparison.OrdinalIgnoreCase)).ToList();
            lineas.Should().NotBeEmpty($"falta la línea {codigo}; omitidas: {string.Join(" | ", resultado.Skips)}");
            lineas.Sum(l => l.Amount).Should().Be(esperado,
                $"línea {codigo}. Explicación: {Explicar(lineas)}");
        }

        foreach (var linea in resultado.Lines.Where(l => !caso.Esperado.Lineas.ContainsKey(l.Code)))
            linea.Amount.Should().Be(0m, $"la línea {linea.Code} no está en lo esperado y no debería tener valor. Explicación: {Explicar([linea])}");

        var t = resultado.Totals;
        var e = caso.Esperado.Totales;
        t.Earnings.Should().Be(e.Devengos, "total devengado");
        t.Deductions.Should().Be(e.Deducciones, "total deducido");
        t.EmployerContributions.Should().Be(e.AportesEmpleador, "aportes del empleador");
        t.Provisions.Should().Be(e.Provisiones, "provisiones");
        t.Net.Should().Be(e.Neto, "neto a pagar");
        t.RoundingAdjustment.Should().Be(e.AjusteRedondeo, "ajuste por redondeo");
        (t.Earnings - t.Deductions).Should().Be(t.Net, "la suma de líneas redondeadas debe coincidir con el neto (FR-017)");

        resultado.Flags.Should().Be(caso.Esperado.Banderas);
        if (caso.Esperado.DiasPagados is { } dias)
            resultado.Tranches.Sum(x => x.PaidDays).Should().Be(dias, "días pagados");
        if (caso.Esperado.DiasAusencia is { } ausencia)
            resultado.AbsenceDays.Should().Be(ausencia, "días de ausencia");

        resultado.Lines.Should().OnlyContain(l => l.Explanation.Steps.Count > 0 && l.Explanation.Summary.Length > 0,
            "toda línea lleva explicación (FR-013)");
    }

    [Fact]
    public void Hay_casos_dorados()
    {
        CasoDorado.Archivos().Should().HaveCountGreaterThanOrEqualTo(5);
    }

    private static string Explicar(IEnumerable<CalculationLine> lineas) =>
        string.Join(" || ", lineas.Select(l =>
            $"[{l.Explanation.Form}] {l.Explanation.Summary}: " +
            string.Join("; ", l.Explanation.Steps.Select(s => s.Value is { } v ? $"{s.Label} = {v}" : $"{s.Label}: {s.Text}"))));
}
