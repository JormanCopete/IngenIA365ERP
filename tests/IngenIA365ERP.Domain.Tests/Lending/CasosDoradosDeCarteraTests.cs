using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, T0330 y T0331 (quickstart §1.2, R45; SC-001, SC-002, SC-017, SC-023): el motor de Cartera coincide con la hoja de
/// cálculo <c>Casos/calcular.py</c> (decimal de 40 dígitos, independiente del código) en los casos dorados de US1. En esta fase la
/// teoría corre los casos <b>sin operaciones</b>: plan esperado cuota a cuota (vencimiento, días, capital, interés, seguro, saldo),
/// tasas (diaria y nominal exactas a 18 decimales; efectivas con y sin cargos a 6) y veredicto de usura con su holgura. US4 extiende
/// el corredor con las operaciones y consultas sin cambiar el esquema (C105). Pendientes de validar por la contadora (D1, C7 §7.7).
/// </summary>
public class CasosDoradosDeCarteraTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoDeCartera.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_once_casos_de_plan_y_usura_de_US1()
    {
        // Los demás números de quickstart §1.2 son de US4 (10, 12–15, 22–24, 26, 28, 30), US5 (11, 16–21, 31) y US6 (25).
        CasoDoradoDeCartera.Archivos().Select(Path.GetFileNameWithoutExtension).Should().Contain(
        [
            "01-cuota-fija-30-360", "02-cuota-fija-real-365-bisiesto", "03-cuota-fija-real-360-usura", "04-abono-constante",
            "05-cuotas-extraordinarias", "06-pago-unico", "07-gracia-a-capital", "08-gracia-total-primera-cuota",
            "09-gracia-total-distribuida", "27-periodicidad-semanal-y-quincenal", "29-equivalencia-lineal",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_la_hoja_de_calculo(string archivo)
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, archivo));
        if (caso.Operaciones.Count > 0) return; // US4 corre los casos con operaciones.

        foreach (var (nombre, condiciones, e) in caso.Escenarios())
        {
            var (plan, usura) = MotorDeCartera.SimularConUsura(condiciones.Entrada(), caso.UsuraDe(condiciones));

            using (new AssertionScope(nombre))
            {
                plan.RulesVersion.Should().Be(VersionesDeReglas.Vigente);
                plan.TasaDiaria.Should().Be(e.Tasas.Diaria, "tasa diaria (18 decimales, la única aproximación)");
                plan.TasaNominalDelPeriodo.Should().Be(e.Tasas.NominalDelPeriodo, "nominal del período");
                plan.CuotaNivelada.Should().Be(e.Tasas.CuotaNivelada, "cuota nivelada");
                plan.InteresDeGracia.Should().Be(e.Plan.InteresDeGracia, "interés de la gracia total");
                plan.ResiduoDeLaUltimaCuota.Should().Be(e.Plan.Residuo, "residuo que absorbe la última cuota");

                plan.Cuotas.Should().HaveCount(e.Plan.Cuotas.Count, "número de cuotas");
                foreach (var (real, esperada) in plan.Cuotas.Zip(e.Plan.Cuotas))
                {
                    var donde = $"cuota {esperada.Numero}";
                    real.Numero.Should().Be(esperada.Numero, donde);
                    real.Vencimiento.Should().Be(esperada.Vencimiento, $"{donde}: vencimiento");
                    real.Dias.Should().Be(esperada.Dias, $"{donde}: días");
                    real.Capital.Should().Be(esperada.Capital, $"{donde}: capital");
                    real.Interes.Should().Be(esperada.Interes, $"{donde}: interés");
                    real.Seguro.Should().Be(esperada.Seguro, $"{donde}: seguro");
                    real.SaldoFinal.Should().Be(esperada.Saldo, $"{donde}: saldo");
                }

                plan.TotalCapital.Should().Be(condiciones.Monto, "Σ capital = monto (SC-017)");
                plan.TotalCapital.Should().Be(e.Plan.Totales.Capital);
                plan.TotalInteres.Should().Be(e.Plan.Totales.Interes, "total de interés");
                plan.TotalSeguro.Should().Be(e.Plan.Totales.Seguro, "total de seguro");

                decimal.Round(usura.EfectivaConCargos, 6).Should().Be(e.Tasas.Efectiva, "efectiva con cargos");
                decimal.Round(usura.EfectivaSinCargos, 6).Should().Be(e.Tasas.EfectivaSinCargos, "efectiva sin cargos");
                usura.Veredicto.Should().Be(e.Usura.Veredicto, "veredicto de usura");
                usura.Usura.Should().Be(e.Usura.Usura, "usura usada");
                (usura.Holgura is { } h ? decimal.Round(h, 6) : (decimal?)null).Should().Be(e.Usura.Holgura, "holgura");
                if (usura.Veredicto != VeredictoDeUsura.SinUsura) usura.UsuraPublicId.Should().NotBeNull();
                plan.Explicacion.Lineas.Should().Contain(l => l.Contains("redondeada a 18 decimales"));
            }
        }
    }
}
