using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0516 (T32, R40; interfaces.md §3.6): el <c>SimulationId</c> de <see cref="HuellaDeCartera.DeSimulacion"/> es el
/// SHA-256 canónico de crédito, modalidad, valor, fecha valor, elección y distribución (líneas, plan nuevo, excedente). Cambia si cambia la
/// distribución; NO cambia si sólo corrió la causación de días que no tocan la distribución; orden, decimales y fechas son canónicos.
/// </summary>
public class HuellaDeSimulacionTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 20);
    private static readonly Guid Credito = new("6f1c2a9e-4b7d-4c0e-9a1f-3b5d7c9e1a2b");
    private static readonly RedondeoDeCartera Redondeo = RedondeoDeCartera.AlCentavo;

    private static readonly PlanDePagos Plan = GeneradorDePlan.Generar(new EntradaDePlan(1_200_000m, new DateOnly(2026, 8, 1), 12,
        RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment, 0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound, Redondeo));

    private static EstadoDelCredito Estado(decimal moraDeLa1 = 3_000m, decimal corrienteDeLa3 = 11_000m) => EstadoDelCredito.Armado(Hoy, Plan,
    [
        .. Plan.Cuotas.Select(c => new SaldoDeCuota(c.Numero, LoanConcept.Principal, c.Capital)),
        new(1, LoanConcept.DefaultInterest, moraDeLa1), new(1, LoanConcept.CurrentInterest, 21_705.10m), new(3, LoanConcept.CurrentInterest, corrienteDeLa3),
    ]);

    private static DistribucionDePago Simular(EstadoDelCredito estado, PaymentModality modalidad, decimal valor, PrepaymentChoice? eleccion = null) =>
        AplicadorDePagos.Distribuir(estado, new SolicitudDeDistribucion(modalidad, valor, Hoy) { Eleccion = eleccion }, Prelacion.PorDefecto, Redondeo);

    private static string Huella(DistribucionDePago d, Guid? credito = null, string? version = null) =>
        HuellaDeCartera.DeSimulacion(credito ?? Credito, version ?? VersionesDeReglas.Vigente, d);

    [Fact]
    public void Es_un_sha256_en_hexadecimal_y_se_repite()
    {
        var d = Simular(Estado(), PaymentModality.PastDue, 50_000m);

        Huella(d).Should().MatchRegex("^[0-9a-f]{64}$").And.Be(Huella(Simular(Estado(), PaymentModality.PastDue, 50_000m)));
    }

    [Fact]
    public void No_cambia_si_solo_corrio_la_causacion_de_dias_que_no_tocan_la_distribucion()
    {
        // La corriente de la cuota 3 no es exigible: un día más de causación no cambia lo que cubre un pago a vencidas.
        var antes = Simular(Estado(corrienteDeLa3: 11_000m), PaymentModality.PastDue, 50_000m);
        var despues = Simular(Estado(corrienteDeLa3: 11_614.74m), PaymentModality.PastDue, 50_000m);

        Huella(despues).Should().Be(Huella(antes), "se firma la distribución, no un contador del estado (T32)");
    }

    [Fact]
    public void Cambia_si_cambia_la_distribucion_o_cualquier_dato_de_la_solicitud()
    {
        var base_ = Simular(Estado(), PaymentModality.PastDue, 50_000m);
        var huella = Huella(base_);

        Huella(Simular(Estado(moraDeLa1: 3_066.80m), PaymentModality.PastDue, 50_000m)).Should().NotBe(huella, "otra mora: otra distribución");
        Huella(Simular(Estado(), PaymentModality.PastDue, 50_000.01m)).Should().NotBe(huella, "otro valor");
        Huella(Simular(Estado(), PaymentModality.FreeAmount, 50_000m)).Should().NotBe(huella, "otra modalidad");
        Huella(base_ with { FechaValor = Hoy.AddDays(1) }).Should().NotBe(huella, "otra fecha valor");
        Huella(base_, credito: Guid.NewGuid()).Should().NotBe(huella, "otro crédito");
        Huella(base_, version: "LND-2027.1").Should().NotBe(huella, "otra versión de reglas");
        Huella(base_ with { Excedente = base_.Excedente + 1m }).Should().NotBe(huella, "otro excedente");
    }

    [Fact]
    public void Las_dos_alternativas_de_un_abono_tienen_su_propia_huella_con_su_plan()
    {
        var alternativas = AplicadorDePagos.Alternativas(Estado(), new SolicitudDeDistribucion(PaymentModality.ExtraordinaryPrincipal, 600_000m, Hoy),
            Prelacion.PorDefecto, Redondeo);
        var sinEleccion = Simular(Estado(), PaymentModality.ExtraordinaryPrincipal, 600_000m);

        alternativas.Should().HaveCount(2);
        alternativas.Select(a => Huella(a)).Append(Huella(sinEleccion)).Should().OnlyHaveUniqueItems("plazo, cuota y saldo a favor son tres distribuciones");
        Huella(alternativas[0]).Should().Be(Huella(Simular(Estado(), PaymentModality.ExtraordinaryPrincipal, 600_000m, PrepaymentChoice.ReduceTerm)));
    }

    [Fact]
    public void El_orden_de_las_lineas_y_la_escala_de_los_decimales_no_cambian_la_huella()
    {
        var d = Simular(Estado(), PaymentModality.FreeAmount, 150_000m);
        var alReves = d with { Lineas = d.Lineas.Reverse().ToList() };
        var conOtraEscala = d with { Valor = 150_000.00m, Lineas = d.Lineas.Select(l => l with { Monto = l.Monto * 1.000m }).ToList() };

        Huella(alReves).Should().Be(Huella(d), "las líneas se ordenan por cuota y concepto");
        Huella(conOtraEscala).Should().Be(Huella(d), "los montos se escriben con dos decimales");
    }

    [Fact]
    public void Un_monto_con_mas_de_dos_decimales_no_se_redondea_se_rechaza()
    {
        var d = Simular(Estado(), PaymentModality.FreeAmount, 150_000m);

        ((Action)(() => Huella(d with { Excedente = 0.001m }))).Should().Throw<ArgumentException>();
    }
}
