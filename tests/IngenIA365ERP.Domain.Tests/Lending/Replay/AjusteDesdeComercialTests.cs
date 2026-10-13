using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// Feature 013, US6, T0566 (FR-054, T24; mensajes.md §12.3): <see cref="ReconstruccionDeCredito"/> interpreta <c>CommercialAdjustment</c> desde su
/// fecha valor con el signo que manda Comercial —la nota crédito baja, la débito sube—; la anulación espeja causación, mora y cargos desde el
/// origen y deja lo pagado como saldo a favor; y una reconstrucción con R anterior al registro del ajuste no cambia. Las cifras exactas las fija
/// el caso dorado 25 (calcular.py); aquí van las propiedades.
/// </summary>
public class AjusteDesdeComercialTests
{
    private static readonly DateOnly Desembolso = new(2027, 1, 15);

    private static EntradaDeOperacion Ajuste(string id, ClaseDeAjusteComercial clase, decimal monto, DateOnly fecha, long secuencia,
        PrepaymentChoice recalculo = PrepaymentChoice.ReduceInstallment) =>
        new(id, LoanOperationKind.CommercialAdjustment, fecha, CreditosDePrueba.Instante(fecha, 16), secuencia)
        {
            Contenido = new ContenidoDeAjusteComercial(clase, monto) { Recalculo = recalculo },
        };

    private static EntradaDeOperacion Pago(string id, decimal valor, DateOnly fecha, long secuencia) =>
        new(id, LoanOperationKind.Payment, fecha, CreditosDePrueba.Instante(fecha, 14), secuencia)
        {
            Contenido = new ContenidoDePago(PaymentModality.FreeAmount, valor),
        };

    private static EntradaDeReconstruccion Con(EntradaDeReconstruccion e, params EntradaDeOperacion[] ops) =>
        e with { Operaciones = [.. e.Operaciones, .. ops] };

    [Fact]
    public void La_nota_credito_baja_el_capital_desde_su_fecha_valor_y_reversa_el_interes_de_lo_devuelto()
    {
        var credito = CreditosDePrueba.Simple();
        var nota = Desembolso.AddDays(20);
        var antes = MotorDeCartera.Reconstruir(credito, nota);
        var despues = MotorDeCartera.Reconstruir(Con(credito, Ajuste("N1", ClaseDeAjusteComercial.CreditNote, -600_000m, nota, 2)), nota, nota);

        despues.Saldo(LoanConcept.Principal).Should().Be(antes.Saldo(LoanConcept.Principal) - 600_000m);
        var interes = antes.Saldo(LoanConcept.CurrentInterest);
        interes.Should().BeGreaterThan(0m);
        // Lo devuelto es la quinta parte del capital: se reversa la quinta parte del interés causado, al centavo.
        despues.Saldo(LoanConcept.CurrentInterest).Should().Be(interes - decimal.Round(interes * 600_000m / 3_000_000m, 2, MidpointRounding.AwayFromZero));
        despues.Libro.Movimientos.Should().Contain(m => m.Clase == LoanTransactionKind.CommercialAdjustment && m.Concepto == LoanConcept.Principal
                                                        && m.Monto == -600_000m && m.OperacionId == "N1");
        despues.Plan!.TotalCapital.Should().Be(2_400_000m, "el plan se regenera sobre el capital que queda");
        despues.Plan.Cuotas.Should().HaveCount(antes.Plan!.Cuotas.Count, "con «reducir la cuota» el plazo no cambia");
        despues.Plan.Cuotas[1].Total.Should().BeLessThan(antes.Plan.Cuotas[1].Total);
    }

    [Fact]
    public void Con_reducir_el_plazo_la_nota_credito_quita_cuotas()
    {
        var credito = CreditosDePrueba.Simple();
        var nota = Desembolso.AddDays(20);
        var despues = MotorDeCartera.Reconstruir(
            Con(credito, Ajuste("N1", ClaseDeAjusteComercial.Return, -1_000_000m, nota, 2, PrepaymentChoice.ReduceTerm)), nota.AddDays(1));

        despues.Plan!.Cuotas.Count.Should().BeLessThan(12);
        despues.Saldo(LoanConcept.Principal).Should().Be(2_000_000m);
    }

    [Fact]
    public void Una_devolucion_mayor_que_el_capital_deja_el_exceso_como_saldo_a_favor()
    {
        var credito = CreditosDePrueba.Simple();
        var fecha = Desembolso.AddDays(10);
        var despues = MotorDeCartera.Reconstruir(Con(credito, Ajuste("N1", ClaseDeAjusteComercial.Return, -3_250_000m, fecha, 2)), fecha.AddMonths(2));

        despues.Saldo(LoanConcept.Principal).Should().Be(0m);
        despues.Saldo(LoanConcept.CurrentInterest).Should().Be(0m, "después no queda base que cause");
        despues.SaldoAFavor.Should().Be(250_000m);
    }

    [Fact]
    public void La_nota_debito_sube_el_capital_y_conserva_las_cuotas()
    {
        var credito = CreditosDePrueba.Simple();
        var nota = Desembolso.AddDays(5);
        var antes = MotorDeCartera.Reconstruir(credito, nota);
        var despues = MotorDeCartera.Reconstruir(Con(credito, Ajuste("ND", ClaseDeAjusteComercial.DebitNote, 500_000m, nota, 2)), nota);

        despues.Saldo(LoanConcept.Principal).Should().Be(3_500_000m);
        despues.Plan!.Cuotas.Should().HaveCount(antes.Plan!.Cuotas.Count);
        despues.Plan.TotalCapital.Should().Be(3_500_000m);
    }

    [Fact]
    public void La_anulacion_deja_el_credito_sin_efecto_desde_su_origen_y_lo_pagado_como_saldo_a_favor()
    {
        var credito = CreditosDePrueba.Simple();
        var pago = Desembolso.AddMonths(1);
        var anulacion = Desembolso.AddMonths(2).AddDays(10);
        var entrada = Con(credito, Pago("P1", 200_000m, pago, 2), Ajuste("A1", ClaseDeAjusteComercial.Voiding, -3_000_000m, anulacion, 3));

        var mucho = anulacion.AddMonths(6);
        var e = MotorDeCartera.Reconstruir(entrada, mucho, anulacion);

        e.ValorParaCancelar.Should().Be(0m, "capital, corriente, mora y cargos quedan en cero");
        e.SaldoAFavor.Should().Be(200_000m, "lo pagado vuelve como saldo a favor");
        e.AlturaDeMora.Dias.Should().Be(0);
        e.Libro.Movimientos.Where(m => m.Dia > anulacion && m.Clase == LoanTransactionKind.Accrual).Should().BeEmpty("sin base no se causa nada");
        e.Libro.Movimientos.Where(m => m.OperacionId == "A1").Should().Contain(m => m.Concepto == LoanConcept.DefaultInterest,
            "la mora causada hasta la anulación se espeja");
    }

    [Fact]
    public void La_anulacion_por_rechazo_de_la_DIAN_es_la_misma_anulacion()
    {
        var credito = CreditosDePrueba.Simple();
        var fecha = Desembolso.AddDays(40);
        var a = MotorDeCartera.Reconstruir(Con(credito, Ajuste("A1", ClaseDeAjusteComercial.Voiding, -3_000_000m, fecha, 2)), fecha.AddDays(30));
        var b = MotorDeCartera.Reconstruir(Con(credito, Ajuste("A1", ClaseDeAjusteComercial.VoidingByDianRejection, -3_000_000m, fecha, 2)), fecha.AddDays(30));

        b.Huella.Should().Be(a.Huella);
    }

    [Fact]
    public void Con_R_anterior_al_registro_del_ajuste_la_reconstruccion_no_cambia()
    {
        var credito = CreditosDePrueba.Simple();
        var nota = Desembolso.AddDays(20);
        var conNota = Con(credito, Ajuste("N1", ClaseDeAjusteComercial.CreditNote, -600_000m, nota, 2));
        var d = nota.AddMonths(2);

        var r = CreditosDePrueba.Instante(nota, 15);
        var sinSaberlo = MotorDeCartera.Reconstruir(conNota with { InformacionHasta = r }, d);
        var nunca = MotorDeCartera.Reconstruir(credito with { InformacionHasta = r }, d);

        sinSaberlo.Huella.Should().Be(nunca.Huella, "el ajuste se registró después de R");
        MotorDeCartera.Reconstruir(conNota, d).Huella.Should().NotBe(MotorDeCartera.Reconstruir(credito, d).Huella);
    }

    [Fact]
    public void Sin_su_contenido_el_ajuste_no_se_reconstruye()
    {
        var credito = CreditosDePrueba.Simple();
        var fecha = Desembolso.AddDays(20);
        var sinContenido = new EntradaDeOperacion("N1", LoanOperationKind.CommercialAdjustment, fecha, CreditosDePrueba.Instante(fecha), 2);

        var accion = () => MotorDeCartera.Reconstruir(Con(credito, sinContenido), fecha.AddDays(1));

        accion.Should().Throw<ReglaDeCarteraException>().Which.Problema.Codigo.Should().Be(ReconstruccionDeCredito.CodigoOperacionNoSoportada);
    }

    [Theory]
    [InlineData("CreditNote", ClaseDeAjusteComercial.CreditNote)]
    [InlineData("VoidingByDianRejection", ClaseDeAjusteComercial.VoidingByDianRejection)]
    [InlineData("Replacement", null)]
    [InlineData("creditnote", null)]
    public void La_clase_se_lee_del_texto_de_Comercial(string texto, ClaseDeAjusteComercial? esperada) =>
        ContenidoDeAjusteComercial.DeTexto(texto).Should().Be(esperada);
}
