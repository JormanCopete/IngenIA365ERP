using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;

namespace IngenIA365ERP.Domain.Tests.Payroll.Settlements;

/// <summary>
/// SC-001 de la feature 010: la liquidación especial del motor coincide al peso con la
/// hecha a mano (research R14). Cada archivo de <c>Casos/</c> es uno o varios casos con su
/// derivación escrita; añadir un caso es añadir un archivo. Cuando falla, el mensaje dice
/// qué línea, cuánto esperaba y cuánto salió, y adjunta la explicación del motor.
/// </summary>
public class CasosDoradosDeLiquidacionTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoLiquidacion.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_motor_coincide_al_peso_con_la_liquidacion_manual(string archivo)
    {
        var casos = CasoDoradoLiquidacion.Cargar(Path.Combine(CasoDoradoLiquidacion.DirectorioDeCasos, archivo));
        casos.Should().NotBeEmpty();
        var motor = new SettlementCalculationEngine();

        foreach (var caso in casos)
        {
            using var _ = new AssertionScope($"{archivo} — {caso.Nombre}");
            caso.Derivacion.Should().NotBeNullOrWhiteSpace("todo caso dorado lleva su derivación a mano");

            var resultado = motor.Calculate(caso.ConstruirEntrada());
            var e = caso.Esperado;

            if (e.Rechazos.Count == 0)
                resultado.Refusals.Should().BeEmpty("el motor no debería negarse en un caso dorado");
            else
                foreach (var esperado in e.Rechazos)
                    resultado.Refusals.Should().Contain(r => r.Contains(esperado, StringComparison.OrdinalIgnoreCase), $"el motor debe negarse nombrando «{esperado}»");

            resultado.ExclusionReasonCode.Should().Be(e.Excluido, "exclusión del empleado");
            if (e.Excluido is not null) resultado.Lines.Should().BeEmpty("un empleado excluido no tiene líneas");

            foreach (var (codigo, esperado) in e.Lineas)
            {
                var lineas = resultado.Lines.Where(l => l.Code.Equals(codigo, StringComparison.OrdinalIgnoreCase)).ToList();
                lineas.Should().NotBeEmpty($"falta la línea {codigo}; omitidas: {string.Join(" | ", resultado.Skips.Select(s => $"{s.ConceptCode}:{s.ReasonCode}"))}");
                lineas.Sum(l => l.Amount).Should().Be(esperado, $"línea {codigo}. Explicación: {Explicar(lineas)}");
            }

            if (!e.SoloLineasListadas)
            {
                foreach (var linea in resultado.Lines.Where(l => !e.Lineas.ContainsKey(l.Code)))
                    linea.Amount.Should().Be(0m, $"la línea {linea.Code} no está en lo esperado y no debería tener valor. Explicación: {Explicar([linea])}");

                if (e.Totales is { } t)
                {
                    resultado.Totals.Earnings.Should().Be(t.Devengos, "total devengado");
                    resultado.Totals.Deductions.Should().Be(t.Deducciones, "total deducido");
                    resultado.Totals.Provisions.Should().Be(t.Provisiones, "provisiones (ajustes)");
                    resultado.Totals.Net.Should().Be(t.Neto, "neto a pagar");
                }
                (resultado.Totals.Earnings - resultado.Totals.Deductions).Should().Be(resultado.Totals.Net, "la suma de líneas redondeadas es el neto");
            }

            resultado.Flags.Should().Be(e.Banderas, "banderas");

            foreach (var omitido in e.Omitidos)
            {
                var partes = omitido.Split(':', 2);
                resultado.Skips.Should().Contain(s => s.ConceptCode.Equals(partes[0], StringComparison.OrdinalIgnoreCase)
                                                      && (partes.Length == 1 || s.ReasonCode.Equals(partes[1], StringComparison.OrdinalIgnoreCase)),
                    $"debe omitirse «{omitido}»; omitidas: {string.Join(" | ", resultado.Skips.Select(s => $"{s.ConceptCode}:{s.ReasonCode}"))}");
            }

            foreach (var aviso in e.Advertencias)
                resultado.Warnings.Should().Contain(w => w.Contains(aviso, StringComparison.OrdinalIgnoreCase), $"debe advertir «{aviso}»");

            foreach (var explicacion in e.Explicaciones)
            {
                var partes = explicacion.Split(':', 2);
                var linea = resultado.Lines.FirstOrDefault(l => l.Code.Equals(partes[0].Trim(), StringComparison.OrdinalIgnoreCase));
                linea.Should().NotBeNull($"la línea {partes[0]} debe existir para revisar su explicación");
                if (linea is null) continue;
                var texto = linea.Explanation.Summary + " " + string.Join(" ", linea.Explanation.Steps.Select(s => s.Label + " " + s.Text));
                texto.Should().Contain(partes[1].Trim(), $"la explicación de {partes[0]} debe decir «{partes[1].Trim()}». Explicación: {Explicar([linea])}");
            }

            if (e.Vacaciones is { } v)
            {
                if (v.Causados is { } causados) resultado.Vacations.Accrued.Should().Be(causados, "días hábiles causados");
                if (v.Pendientes is { } pendientes) resultado.Vacations.Pending.Should().Be(pendientes, "días hábiles pendientes");
            }

            resultado.Lines.Should().OnlyContain(l => l.Explanation.Steps.Count > 0 && l.Explanation.Summary.Length > 0,
                "toda línea lleva explicación (FR-002)");
            resultado.InputsHash.Should().HaveLength(64);
        }
    }

    [Fact]
    public void Hay_al_menos_dieciocho_archivos_de_casos()
    {
        CasoDoradoLiquidacion.Archivos().Should().HaveCountGreaterThanOrEqualTo(18);
    }

    [Fact]
    public void Calcular_dos_veces_da_el_mismo_hash_y_las_mismas_lineas()
    {
        var archivo = CasoDoradoLiquidacion.Archivos().First();
        var caso = CasoDoradoLiquidacion.Cargar(archivo)[0];
        var motor = new SettlementCalculationEngine();

        var a = motor.Calculate(caso.ConstruirEntrada());
        var b = motor.Calculate(caso.ConstruirEntrada());

        a.InputsHash.Should().Be(b.InputsHash);
        a.Lines.Select(l => (l.Code, l.Amount)).Should().Equal(b.Lines.Select(l => (l.Code, l.Amount)));
    }

    [Fact]
    public void Sin_un_parametro_requerido_el_motor_se_niega_nombrandolo()
    {
        var archivo = CasoDoradoLiquidacion.Archivos().First();
        var caso = CasoDoradoLiquidacion.Cargar(archivo)[0];
        var entrada = caso.ConstruirEntrada();
        var sinPrima = entrada.Parameters.Where(p => p.Code != SettlementParameterCodes.ServiceBonusDaysPerYear).ToList();
        var recortada = new SettlementInput
        {
            Kind = entrada.Kind, CutoffDate = entrada.CutoffDate, PeriodStart = entrada.PeriodStart, Employee = entrada.Employee,
            Policies = entrada.Policies, Parameters = sinPrima, Concepts = entrada.Concepts,
        };

        var acto = () => new SettlementCalculationEngine().Calculate(recortada);

        acto.Should().Throw<CalculationRefusedException>()
            .Which.MissingCodes.Should().Contain(SettlementParameterCodes.ServiceBonusDaysPerYear);
    }

    private static string Explicar(IEnumerable<CalculationLine> lineas) =>
        string.Join(" || ", lineas.Select(l =>
            $"[{l.Explanation.Form}] {l.Explanation.Summary}: " +
            string.Join("; ", l.Explanation.Steps.Select(s => s.Value is { } v ? $"{s.Label} = {v}" : $"{s.Label}: {s.Text}"))));
}
