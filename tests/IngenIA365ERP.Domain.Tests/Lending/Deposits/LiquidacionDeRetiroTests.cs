using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Tests.Lending.Deposits;

/// <summary>
/// Feature 014, US10 (T099, T104; data-model §6.1, §6.2; FR-076 a FR-078, D14, D15): la liquidación pura del retiro del asociado —los aportes primero y
/// después los ahorros, el descuento por pérdidas sobre los aportes, el cruce en la prelación de los estatutos y la devolución del remanente— y las
/// transiciones del trámite. (nuevo)
/// </summary>
public class LiquidacionDeRetiroTests
{
    private static CuentaEnElRetiro Aportes(decimal minimo, decimal ordinarios, decimal extraordinarios = 0m) =>
        new(1, "APORTES-000001", LineNature.Contribution, null, new Dictionary<MemberAccountConcept, decimal>
        {
            [MemberAccountConcept.ContributionMinimum] = minimo,
            [MemberAccountConcept.ContributionOrdinary] = ordinarios,
            [MemberAccountConcept.ContributionExtraordinary] = extraordinarios,
        });

    private static CuentaEnElRetiro Ahorro(decimal capital, int id = 2) =>
        new(id, $"AHVISTA-00000{id}", LineNature.Deposit, DepositClass.Demand, new Dictionary<MemberAccountConcept, decimal>
        {
            [MemberAccountConcept.Principal] = capital,
            [MemberAccountConcept.Withholding] = 999m,
        });

    private static void SumaLaDevolucion(LiquidacionDeRetiroCalculada l) =>
        l.Lineas.Where(x => x.Clase != MembershipWithdrawalLineKind.Refund).Sum(x => x.Monto).Should().Be(l.ADevolver,
            "Σ de las líneas que no son de devolución = lo que se devuelve (research E65)");

    [Fact]
    public void El_ejemplo_del_quickstart_cruza_2_500_000_y_devuelve_1_500_000()
    {
        var l = LiquidacionDeRetiro.Calcular([Aportes(1_000_000m, 2_000_000m), Ahorro(1_000_000m)],
            [new CreditoEnElRetiro(10, "CR-0001", 2_500_000m, 0, 0.18m)], null);

        l.Aportes.Should().Be(3_000_000m);
        l.Ahorros.Should().Be(1_000_000m, "la memoria (retención, GMF) no es del titular");
        l.Cruzado.Should().Be(2_500_000m);
        l.ADevolver.Should().Be(1_500_000m);
        l.Cruces.Should().ContainSingle().Which.Total.Should().BeTrue();
        // Los aportes se toman primero, en el orden del débito de aportes (extraordinarios, revalorizados, ordinarios, mínimo).
        l.Cruces[0].Debitos.Should().BeEquivalentTo(new[]
        {
            new DebitoDeCuentaEnElRetiro(1, MemberAccountConcept.ContributionOrdinary, 2_000_000m),
            new DebitoDeCuentaEnElRetiro(1, MemberAccountConcept.ContributionMinimum, 500_000m),
        });
        l.Devolucion.Should().BeEquivalentTo(new[]
        {
            new DebitoDeCuentaEnElRetiro(1, MemberAccountConcept.ContributionMinimum, 500_000m),
            new DebitoDeCuentaEnElRetiro(2, MemberAccountConcept.Principal, 1_000_000m),
        });
        l.DevolucionDeAportes([1]).Should().Be(500_000m);
        SumaLaDevolucion(l);
    }

    [Fact]
    public void Los_estatutos_pueden_cruzar_primero_los_ahorros_y_queda_el_saldo_pendiente_de_los_creditos()
    {
        // Decisiones del dueño del 2026-10-18: D41 (el orden de los fondos es de los estatutos) y D45 (lo que se sigue debiendo).
        var l = LiquidacionDeRetiro.Calcular([Aportes(1_000_000m, 2_000_000m), Ahorro(1_000_000m)],
            [new CreditoEnElRetiro(10, "CR-0001", 2_500_000m, 0, 0.18m)], null, OrdenDeFondosAlRetiro.AhorrosPrimero);

        l.Cruzado.Should().Be(2_500_000m);
        l.Cruces[0].Debitos.Should().BeEquivalentTo(new[]
        {
            new DebitoDeCuentaEnElRetiro(2, MemberAccountConcept.Principal, 1_000_000m),
            new DebitoDeCuentaEnElRetiro(1, MemberAccountConcept.ContributionOrdinary, 1_500_000m),
        });
        l.Devolucion.Should().OnlyContain(d => d.CuentaId == 1, "con los ahorros primero, lo que sobra es de aportes");
        l.SaldoPendienteDeCreditos.Should().Be(0m);
        SumaLaDevolucion(l);

        var corta = LiquidacionDeRetiro.Calcular([Aportes(500_000m, 500_000m)], [new CreditoEnElRetiro(10, "CR-0001", 1_500_000m, 0, 0.18m)], null);
        corta.SaldoPendienteDeCreditos.Should().Be(500_000m);
    }

    [Fact]
    public void Con_obligaciones_mayores_se_cruza_todo_y_la_devolucion_es_cero()
    {
        var l = LiquidacionDeRetiro.Calcular([Aportes(500_000m, 500_000m), Ahorro(200_000m)],
            [new CreditoEnElRetiro(10, "CR-0001", 3_000_000m, 0, 0.18m)], null);

        l.Cruzado.Should().Be(1_200_000m);
        l.ADevolver.Should().Be(0m);
        l.Devolucion.Should().BeEmpty();
        l.Cruces.Should().ContainSingle().Which.Total.Should().BeFalse("el crédito conserva el saldo restante (US10-1)");
        SumaLaDevolucion(l);
    }

    [Fact]
    public void La_prelacion_toma_primero_los_vencidos_y_luego_la_mayor_tasa()
    {
        var creditos = new[]
        {
            new CreditoEnElRetiro(1, "CR-A", 100_000m, 0, 0.12m),
            new CreditoEnElRetiro(2, "CR-B", 100_000m, 0, 0.24m),
            new CreditoEnElRetiro(3, "CR-C", 100_000m, 15, 0.10m),
            new CreditoEnElRetiro(4, "CR-D", 100_000m, 40, 0.10m),
        };

        LiquidacionDeRetiro.Prelacion(creditos).Select(c => c.Numero).Should().Equal("CR-D", "CR-C", "CR-B", "CR-A");

        var l = LiquidacionDeRetiro.Calcular([Aportes(250_000m, 0m)], creditos, null);
        l.Cruces.Select(c => (c.Credito.Numero, c.Monto, c.Prelacion)).Should().Equal(("CR-D", 100_000m, 1), ("CR-C", 100_000m, 2), ("CR-B", 50_000m, 3));
        SumaLaDevolucion(l);
    }

    [Fact]
    public void Las_perdidas_se_descuentan_de_los_aportes_antes_de_cruzar_al_centavo()
    {
        var l = LiquidacionDeRetiro.Calcular([Aportes(1_000_000m, 2_000_000m), Ahorro(1_000_000m)],
            [new CreditoEnElRetiro(10, "CR-0001", 2_500_000m, 0, 0.18m)], new PerdidaEnElRetiro(2025, 0.04m));

        l.Perdidas.Should().Be(120_000m, "4 % de 3.000.000 de aportes (mensajes.md §4.1, el ejemplo de PerdidaAplicadaAlRetiro)");
        l.Perdida.Should().OnlyContain(d => d.CuentaId == 1, "sale de los aportes, nunca de los ahorros");
        l.Lineas.Where(x => x.Clase == MembershipWithdrawalLineKind.LossDeduction).Sum(x => x.Monto).Should().Be(-120_000m);
        l.Cruzado.Should().Be(2_500_000m);
        l.ADevolver.Should().Be(1_380_000m);
        SumaLaDevolucion(l);
    }

    [Fact]
    public void Sin_creditos_ni_perdidas_todo_se_devuelve_y_la_proporcion_fuera_de_rango_no_se_admite()
    {
        var l = LiquidacionDeRetiro.Calcular([Aportes(500_000m, 300_000m)], [], null);
        l.ADevolver.Should().Be(800_000m);
        l.Cruces.Should().BeEmpty();
        SumaLaDevolucion(l);

        var mal = () => LiquidacionDeRetiro.Calcular([Aportes(500_000m, 0m)], [], new PerdidaEnElRetiro(2025, 1.5m));
        mal.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ------------------------------------------------------------------------------------------------------- trámite --

    private static MembershipWithdrawal Radicado() =>
        MembershipWithdrawal.Radicar("RTA-000001", 7, 3, MembershipWithdrawalCause.Voluntary, null, [Guid.NewGuid()], new DateOnly(2026, 12, 1), 5);

    [Fact]
    public void El_tramite_sigue_sus_transiciones_y_una_reliquidacion_sube_la_version()
    {
        var r = Radicado();
        r.Status.Should().Be(MembershipWithdrawalStatus.Filed);
        r.AdmiteLiquidar.Should().BeFalse();
        var liquidar = () => r.Liquidar(1, DateTime.UtcNow, 1m, 0m, 0m, 0m, 1m, new DateOnly(2027, 1, 1), false, null);
        liquidar.Should().Throw<ReglaDeCarteraException>();

        r.Aprobar(9, new DateOnly(2026, 12, 1), 6, "Acta 15", DateTime.UtcNow, Guid.NewGuid());
        r.Status.Should().Be(MembershipWithdrawalStatus.Approved);
        r.Liquidar(1, DateTime.UtcNow, 3_000_000m, 1_000_000m, 2_500_000m, 0m, 1_500_000m, new DateOnly(2027, 1, 30), false, 11);
        r.SettlementVersion.Should().Be(1);
        r.PorRehacer("Se reversó el pago del cruce");
        r.Status.Should().Be(MembershipWithdrawalStatus.ToRedo);
        r.AdmiteLiquidar.Should().BeTrue();
        r.Liquidar(1, DateTime.UtcNow, 3_000_000m, 1_000_000m, 2_500_000m, 0m, 1_500_000m, new DateOnly(2027, 1, 30), false, 12);
        r.SettlementVersion.Should().Be(2);
        r.ToRedoReason.Should().BeNull();
        r.Pagar(DateTime.UtcNow);
        r.Status.Should().Be(MembershipWithdrawalStatus.Paid);
        r.EsVivo.Should().BeFalse();
    }

    [Fact]
    public void Solo_se_desiste_antes_de_aprobar_y_los_soportes_quedan_como_lista()
    {
        var r = Radicado();
        r.SupportAttachmentPublicIdsJson.Should().StartWith("[\"").And.EndWith("\"]");
        r.Desistir("Ya no se retira", DateTime.UtcNow);
        r.Status.Should().Be(MembershipWithdrawalStatus.Desisted);

        var otro = Radicado();
        otro.Rechazar(3, 6, "El consejo lo negó", DateTime.UtcNow);
        var desistir = () => otro.Desistir("Tarde", DateTime.UtcNow);
        desistir.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(MembershipWithdrawal.CodigoTransicionInvalida);
    }
}
