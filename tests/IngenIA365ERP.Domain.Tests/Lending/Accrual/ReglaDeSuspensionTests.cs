using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Accrual;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Accrual;

/// <summary>
/// Feature 013, US4 (T0467; T30, R38; FR-065): <see cref="ReglaDeSuspension"/>. Se suspende por la altura que llega al umbral vigente de la
/// modalidad (la primera altura que suspende), por más de una reestructuración, por insolvencia o por castigo; se reanuda por la regla
/// por defecto cuando ya no queda disparador. Lo nuevo va a suspenso y lo causado antes no se reclasifica.
/// </summary>
public class ReglaDeSuspensionTests
{
    private static DateOnly F(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(60, 61, null)]
    [InlineData(61, 61, MotivoDeSuspension.AlturaDeMora)]
    [InlineData(95, 121, null)] // el umbral vigente subió: la regla por defecto reanuda (caso dorado 12)
    [InlineData(400, null, null)] // la norma no fija umbral para la modalidad
    public void El_umbral_vigente_de_la_modalidad_suspende_y_la_regla_por_defecto_reanuda(int altura, int? umbral, MotivoDeSuspension? motivo) =>
        ReglaDeSuspension.Evaluar(new EntradaDeSuspension(altura, umbral, 0, false, false)).Should().Be(motivo);

    [Fact]
    public void Mas_de_una_reestructuracion_insolvencia_y_castigo_suspenden_sin_mirar_la_altura()
    {
        ReglaDeSuspension.Evaluar(new EntradaDeSuspension(0, 61, 1, false, false)).Should().BeNull("una reestructuración no basta");
        ReglaDeSuspension.Evaluar(new EntradaDeSuspension(0, 61, 2, false, false)).Should().Be(MotivoDeSuspension.Reestructuraciones);
        ReglaDeSuspension.Evaluar(new EntradaDeSuspension(0, 61, 0, true, false)).Should().Be(MotivoDeSuspension.Insolvencia);
        ReglaDeSuspension.Evaluar(new EntradaDeSuspension(90, 61, 3, true, true)).Should().Be(MotivoDeSuspension.Castigo, "el castigo manda");
    }

    [Fact]
    public void Las_transiciones_y_los_conceptos_salen_del_estado()
    {
        ReglaDeSuspension.Transicion(false, true).Should().Be(TransicionDeCausacion.Suspendida);
        ReglaDeSuspension.Transicion(true, false).Should().Be(TransicionDeCausacion.Reanudada);
        ReglaDeSuspension.Transicion(true, true).Should().BeNull();
        ReglaDeSuspension.Transicion(false, false).Should().BeNull();
        ReglaDeSuspension.ConceptoCorriente(true).Should().Be(LoanConcept.CurrentInterestInSuspense);
        ReglaDeSuspension.ConceptoCorriente(false).Should().Be(LoanConcept.CurrentInterest);
        ReglaDeSuspension.ConceptoDeMora(true).Should().Be(LoanConcept.DefaultInterestInSuspense);
        ReglaDeSuspension.ConceptoDeMora(false).Should().Be(LoanConcept.DefaultInterest);
    }

    [Fact]
    public void Un_credito_reestructurado_dos_veces_causa_todo_en_suspenso_desde_el_origen()
    {
        var estado = MotorDeCartera.Reconstruir(CreditosDePrueba.Simple(reestructuraciones: 2), F("2027-02-10"));
        estado.Suspendida.Should().BeTrue();
        estado.MotivoDeSuspension.Should().Be(MotivoDeSuspension.Reestructuraciones);
        estado.Saldo(LoanConcept.CurrentInterest).Should().Be(0m);
        estado.Saldo(LoanConcept.CurrentInterestInSuspense).Should().BePositive();
        estado.Transiciones.Should().BeEmpty("nació suspendido: no hay transición");
    }

    [Fact]
    public void La_insolvencia_suspende_desde_su_dia_y_lo_causado_antes_no_se_reclasifica()
    {
        var sin = MotorDeCartera.Reconstruir(CreditosDePrueba.Simple(), F("2027-02-04"));
        var con = MotorDeCartera.Reconstruir(CreditosDePrueba.Simple(insolvencia: F("2027-02-05")), F("2027-02-10"));
        con.Transiciones.Should().ContainSingle().Which.Should().Be(new TransicionDeEstado(F("2027-02-05"), TransicionDeCausacion.Suspendida, MotivoDeSuspension.Insolvencia));
        con.Saldo(LoanConcept.CurrentInterest).Should().Be(sin.Saldo(LoanConcept.CurrentInterest), "lo causado hasta el 04-02 sigue en corriente");
        con.Saldo(LoanConcept.CurrentInterestInSuspense).Should().BePositive();
    }
}
