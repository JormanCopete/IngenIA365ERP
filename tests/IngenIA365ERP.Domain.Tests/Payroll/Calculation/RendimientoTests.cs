using System.Diagnostics;
using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Domain.Tests.Payroll.Calculation;

/// <summary>
/// T132 / SC-004: el motor puro liquida 200 empleados con 8 novedades cada uno en menos de
/// 10 segundos. El presupuesto total del ciclo es 60 s; esto deja el resto a la persistencia.
/// Los insumos son los de la semilla (conceptos y parámetros 2026), sin base de datos.
/// </summary>
public class RendimientoTests
{
    private static readonly DateTime Inicio = new(2026, 3, 1);
    private static readonly DateTime Fin = new(2026, 3, 31);

    [Fact]
    public void Doscientos_empleados_con_ocho_novedades_se_liquidan_en_menos_de_diez_segundos()
    {
        var conceptos = PayrollConceptDefinitionsSeeder.Catalogo();
        var id = 0;
        foreach (var c in conceptos) c.Id = ++id;
        var parametros = PayrollLegalParametersSeeder.Catalogo();
        id = 0;
        foreach (var p in parametros) p.Id = ++id;

        PayrollCalculationEngine.MissingRequiredParameters(parametros, Fin).Should().BeEmpty("la semilla trae las vigencias del año");

        var entradas = Enumerable.Range(1, 200).Select(i => Entrada(i, conceptos, parametros)).ToList();
        var motor = new PayrollCalculationEngine();

        // Calentamiento: JIT y caches estáticas fuera de la medición.
        motor.Calculate(entradas[0]);

        var reloj = Stopwatch.StartNew();
        var resultados = entradas.Select(motor.Calculate).ToList();
        reloj.Stop();

        resultados.Should().HaveCount(200);
        resultados.Should().OnlyContain(r => r.Refusals.Count == 0, "ningún empleado sintético debe quedar sin liquidar");
        resultados.Should().OnlyContain(r => r.Lines.Count >= 12, "salario, horas, recargos, comisión, bonificación, libranza, salud, pensión, aportes y provisiones");
        resultados.Should().OnlyContain(r => r.Totals.Earnings - r.Totals.Deductions == r.Totals.Net);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), $"200 empleados × 8 novedades tardaron {reloj.Elapsed.TotalSeconds:0.00} s");
    }

    private static CalculationInput Entrada(int i, IReadOnlyList<Domain.Entities.Payroll.PayrollConceptDefinition> conceptos, IReadOnlyList<Domain.Entities.Payroll.PayrollLegalParameter> parametros)
    {
        // Salarios entre 1,6 y 9,5 millones: mezcla de auxilio de transporte, fondo de solidaridad y retención.
        var salario = 1_600_000m + (i % 40) * 200_000m;
        var empleado = new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Guid Nov(int n) => new(i, (short)n, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        return new CalculationInput
        {
            Period = new PeriodInput(Inicio, Fin, PayrollPeriodicity.Monthly),
            Employee = new EmployeeInput
            {
                PublicId = empleado,
                DisplayName = $"Empleado {i}",
                Class = EmployeeClass.Standard,
                JoinDate = new DateTime(2020, 1, 15),
                SalaryHistory = [new SalaryChangeInput(new DateTime(2020, 1, 15), salario)],
                Affiliations = new AffiliationsInput { Health = true, Pension = true, WorkRiskClass = 1 + i % 5, FamilyCompensation = true },
                WithholdingProcedure = 1,
            },
            Novelties =
            [
                new NoveltyInput { PublicId = Nov(1), ConceptCode = "HEX_DIURNA", Quantity = 4m + i % 6 },
                new NoveltyInput { PublicId = Nov(2), ConceptCode = "HEX_NOCTURNA", Quantity = 2m + i % 4 },
                new NoveltyInput { PublicId = Nov(3), ConceptCode = "RECARGO_NOCTURNO", Quantity = 8m },
                new NoveltyInput { PublicId = Nov(4), ConceptCode = "COMISION", Amount = 150_000m + (i % 7) * 50_000m },
                new NoveltyInput { PublicId = Nov(5), ConceptCode = "BONIF_NO_SALARIAL", Amount = 100_000m },
                new NoveltyInput { PublicId = Nov(6), ConceptCode = "LIBRANZA", Amount = 80_000m },
                new NoveltyInput { PublicId = Nov(7), ConceptCode = "INCAP_GENERAL", StartDate = new DateTime(2026, 3, 10), EndDate = new DateTime(2026, 3, 12) },
                new NoveltyInput { PublicId = Nov(8), ConceptCode = "LIC_REMUNERADA", StartDate = new DateTime(2026, 3, 20), EndDate = new DateTime(2026, 3, 21) },
            ],
            Concepts = conceptos,
            Parameters = parametros,
            Policies = new CalculationPolicies { Rounding = PayrollRounding.Peso },
        };
    }
}
