using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.BulkCharges;

namespace IngenIA365ERP.Domain.Tests.Lending.BulkCharges;

/// <summary>Feature 026 (2026-11-25): a quién se le carga (D3), con qué forma de pago, el ciclo del primer pago y la línea de la corrida.</summary>
public class ReglasDeCargoMasivoTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 1);
    private static readonly RepaymentChannel[] Todas = [RepaymentChannel.Cashier, RepaymentChannel.OwnPayroll, RepaymentChannel.PaymasterDeduction];

    private static ReglasDeCargoMasivo.Asociado Activo() => new(true, "A", null, null, false, false, false);

    [Fact]
    public void El_asociado_activo_se_carga() => ReglasDeCargoMasivo.Exclusion(Activo(), Fecha).Should().BeNull();

    [Fact]
    public void El_que_no_es_asociado_queda_fuera() =>
        ReglasDeCargoMasivo.Exclusion(Activo() with { EsAsociado = false }, Fecha).Should().Be(MotivosDeCargoMasivo.NoAsociado);

    [Fact]
    public void El_retirado_queda_fuera_por_estado_o_por_fecha()
    {
        ReglasDeCargoMasivo.Exclusion(Activo() with { Estado = "R" }, Fecha).Should().Be(MotivosDeCargoMasivo.Retirado);
        ReglasDeCargoMasivo.Exclusion(Activo() with { FechaDeRetiro = Fecha.AddDays(-1) }, Fecha).Should().Be(MotivosDeCargoMasivo.Retirado);
    }

    [Fact]
    public void El_reingresado_despues_del_retiro_se_carga() =>
        ReglasDeCargoMasivo.Exclusion(Activo() with { Estado = "R", FechaDeRetiro = Fecha.AddYears(-1), FechaDeReingreso = Fecha.AddMonths(-1) }, Fecha)
            .Should().BeNull();

    [Fact]
    public void El_fallecido_queda_fuera_por_marca_o_por_caso_vivo()
    {
        ReglasDeCargoMasivo.Exclusion(Activo() with { Fallecido = true }, Fecha).Should().Be(MotivosDeCargoMasivo.Fallecido);
        ReglasDeCargoMasivo.Exclusion(Activo() with { CasoDeFallecimientoVivo = true }, Fecha).Should().Be(MotivosDeCargoMasivo.Fallecido);
    }

    [Fact]
    public void El_retiro_en_tramite_queda_fuera() =>
        ReglasDeCargoMasivo.Exclusion(Activo() with { RetiroVivo = true }, Fecha).Should().Be(MotivosDeCargoMasivo.RetiroEnTramite);

    [Fact]
    public void Con_forma_fija_va_la_fija() =>
        ReglasDeCargoMasivo.Forma(BulkChargeChannelMode.Fixed, RepaymentChannel.Cashier, "N", true, true, Todas, RepaymentChannel.Cashier)
            .Should().Be(new ReglasDeCargoMasivo.FormaResuelta(RepaymentChannel.Cashier, null, null));

    [Fact]
    public void De_cada_asociado_nomina_con_pagaduria_va_a_la_pagaduria() =>
        ReglasDeCargoMasivo.Forma(BulkChargeChannelMode.FromAssociate, null, "N", true, true, Todas, RepaymentChannel.Cashier).Forma
            .Should().Be(RepaymentChannel.PaymasterDeduction);

    [Fact]
    public void De_cada_asociado_nomina_sin_pagaduria_va_a_la_nomina_propia() =>
        ReglasDeCargoMasivo.Forma(BulkChargeChannelMode.FromAssociate, null, "N", false, true, Todas, RepaymentChannel.Cashier).Forma
            .Should().Be(RepaymentChannel.OwnPayroll);

    [Fact]
    public void De_cada_asociado_caja_va_a_caja() =>
        ReglasDeCargoMasivo.Forma(BulkChargeChannelMode.FromAssociate, null, "C", true, true, Todas, RepaymentChannel.OwnPayroll).Forma
            .Should().Be(RepaymentChannel.Cashier);

    [Fact]
    public void Si_la_linea_no_admite_la_de_la_ficha_va_la_de_defecto_con_aviso() =>
        ReglasDeCargoMasivo.Forma(BulkChargeChannelMode.FromAssociate, null, "N", true, true, [RepaymentChannel.Cashier], RepaymentChannel.Cashier)
            .Should().Be(new ReglasDeCargoMasivo.FormaResuelta(RepaymentChannel.Cashier, MotivosDeCargoMasivo.FormaNoAdmitida, null));

    [Fact]
    public void La_nomina_propia_sin_su_convenio_se_omite() =>
        ReglasDeCargoMasivo.Forma(BulkChargeChannelMode.Fixed, RepaymentChannel.OwnPayroll, null, false, false, Todas, RepaymentChannel.Cashier).Omision
            .Should().Be(MotivosDeCargoMasivo.SinNominaPropia);

    [Fact]
    public void La_pagaduria_fija_sin_convenio_se_omite() =>
        ReglasDeCargoMasivo.Forma(BulkChargeChannelMode.Fixed, RepaymentChannel.PaymasterDeduction, "N", false, true, Todas, RepaymentChannel.Cashier).Omision
            .Should().Be(MotivosDeCargoMasivo.SinPagaduria);

    [Theory]
    [InlineData(10, AccrualDiscountCycle.FirstFortnight, true)]
    [InlineData(20, AccrualDiscountCycle.FirstFortnight, false)]
    [InlineData(31, AccrualDiscountCycle.SecondFortnight, true)]
    public void El_primer_pago_cae_en_el_ciclo(int dia, AccrualDiscountCycle ciclo, bool esperado) =>
        ReglasDeCargoMasivo.PrimerPagoEnCiclo(new DateOnly(2026, 12, dia), ciclo).Should().Be(esperado);

    [Fact]
    public void Sin_ciclo_cualquier_fecha_sirve() => ReglasDeCargoMasivo.PrimerPagoEnCiclo(new DateOnly(2026, 12, 20), null).Should().BeTrue();

    [Fact]
    public void Los_datos_invalidos_se_rechazan()
    {
        ReglasDeCargoMasivo.ValidarDatos(0m, 1, Fecha, Fecha.AddDays(30), null, BulkChargeCounterpart.Debtor, false)!.Codigo
            .Should().Be(ReglasDeCargoMasivo.CodigoFueraDeRango);
        ReglasDeCargoMasivo.ValidarDatos(13_557m, 1, Fecha, Fecha, null, BulkChargeCounterpart.Debtor, false)!.Codigo
            .Should().Be(ReglasDeCargoMasivo.CodigoPrimerPagoAnterior);
        ReglasDeCargoMasivo.ValidarDatos(13_557m, 1, Fecha, new DateOnly(2026, 12, 20), AccrualDiscountCycle.FirstFortnight, BulkChargeCounterpart.Debtor,
            false)!.Codigo.Should().Be(ReglasDeCargoMasivo.CodigoFueraDelCiclo);
        ReglasDeCargoMasivo.ValidarDatos(13_557m, 1, Fecha, Fecha.AddDays(30), null, BulkChargeCounterpart.Person, false)!.Codigo
            .Should().Be(ReglasDeCargoMasivo.CodigoSinTercero);
        ReglasDeCargoMasivo.ValidarDatos(13_557m, 1, Fecha, Fecha.AddDays(30), null, BulkChargeCounterpart.Debtor, false).Should().BeNull();
    }

    [Theory]
    [InlineData(202612, true)]
    [InlineData(202613, false)]
    [InlineData(2026, false)]
    public void El_periodo_es_aaaamm(int periodo, bool valido) => ReglasDeCargoMasivo.PeriodoValido(periodo).Should().Be(valido);

    [Fact]
    public void La_linea_de_la_corrida_reserva_el_periodo_solo_al_confirmar_y_lo_libera_al_anular()
    {
        var linea = new BulkChargeLine { RunId = 1, PersonId = 7, CreditLineId = 5, Period = 202612 };
        linea.HoldsPeriod.Should().BeFalse();
        linea.Reservar();
        linea.HoldsPeriod.Should().BeTrue();
        linea.Crear(99, DateTime.UtcNow);
        linea.Status.Should().Be(BulkChargeLineStatus.Created);
        linea.HoldsPeriod.Should().BeTrue();
        linea.Anular("error de digitación", DateTime.UtcNow);
        linea.Status.Should().Be(BulkChargeLineStatus.Voided);
        linea.HoldsPeriod.Should().BeFalse();
    }

    [Fact]
    public void La_corrida_sigue_su_ciclo_de_vida()
    {
        var run = new BulkChargeRun { Number = "CGM-000001", PreparedByUserId = 1 };
        run.SellarVistaPrevia("abc", 3, 2, 1, 27_114m, DateTime.UtcNow);
        run.Confirmar(1, 10, DateTime.UtcNow);
        run.Status.Should().Be(BulkChargeRunStatus.Queued);
        run.ExigirBorrador().Should().NotBeNull();
        run.Iniciar();
        run.Terminar(null, null, DateTime.UtcNow);
        run.Status.Should().Be(BulkChargeRunStatus.Completed);
        run.IniciarAnulacion("error", 1, 11, DateTime.UtcNow);
        run.TerminarAnulacion(quedaAlgoVivo: false);
        run.Status.Should().Be(BulkChargeRunStatus.Voided);
    }
}
