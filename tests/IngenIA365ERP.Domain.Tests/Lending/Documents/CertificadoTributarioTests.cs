using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, K5, T0964 (FR-131; US13-3; adaptadores-y-archivos.md §8.6): el certificado tributario suma por crédito los intereses
/// <b>efectivamente pagados</b> en el año —lo aplicado por el contrato (<c>PaymentAllocation</c>, <c>PaymentReallocation</c>) a los cuatro conceptos de
/// interés con fecha valor en el año, neto de los reversos conocidos a R— y el saldo de capital y de intereses al 31 de diciembre con R = la emisión;
/// separa los de vivienda; un crédito cancelado en el año aparece con saldo cero; uno desembolsado después del año o cancelado antes no aparece.
/// </summary>
public class CertificadoTributarioTests
{
    private const short Año = 2027;
    private static readonly DateOnly Cierre = new(2027, 12, 31);
    private static readonly DateTime Emision = CreditosDePrueba.Instante(new DateOnly(2028, 1, 20));

    private static EntradaDeReconstruccion Credito(DateOnly desembolso, params EntradaDeOperacion[] mas)
    {
        var e = CreditosDePrueba.Simple(desembolso: desembolso, cuotas: 24);
        return e with { Operaciones = [.. e.Operaciones, .. mas], InformacionHasta = Emision };
    }

    private static EntradaDeOperacion Pago(string id, DateOnly fecha, decimal valor, long secuencia, PaymentModality modalidad = PaymentModality.CycleInstallment) =>
        new(id, LoanOperationKind.Payment, fecha, CreditosDePrueba.Instante(fecha, 16), secuencia) { Contenido = new ContenidoDePago(modalidad, valor) };

    private static decimal InteresDe(EstadoDelCredito estado, params string[] pagos) =>
        pagos.SelectMany(p => estado.Pagos[p].Lineas)
            .Where(l => l.Concepto is LoanConcept.CurrentInterest or LoanConcept.DefaultInterest or LoanConcept.CurrentInterestInSuspense
                or LoanConcept.DefaultInterestInSuspense)
            .Sum(l => l.Monto);

    [Fact]
    public void Suma_los_intereses_pagados_con_fecha_valor_en_el_año_y_el_saldo_al_31_de_diciembre()
    {
        var entrada = Credito(new DateOnly(2027, 1, 15),
            Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10),
            Pago("P2", new DateOnly(2027, 3, 15), 160_000m, 11),
            Pago("P3", new DateOnly(2028, 1, 15), 160_000m, 12));
        var alCierre = MotorDeCartera.Reconstruir(entrada, Cierre);

        var c = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "Libre inversión", false, alCierre)]);

        var credito = c.Creditos.Should().ContainSingle().Subject;
        credito.InteresesPagados.Should().Be(InteresDe(alCierre, "P1", "P2")).And.BeGreaterThan(0m);
        credito.SaldoDeCapital.Should().Be(alCierre.Saldo(LoanConcept.Principal));
        credito.SaldoDeIntereses.Should().Be(alCierre.Saldo(LoanConcept.CurrentInterest) + alCierre.Saldo(LoanConcept.DefaultInterest)
                                             + alCierre.Saldo(LoanConcept.CurrentInterestInSuspense) + alCierre.Saldo(LoanConcept.DefaultInterestInSuspense));
        credito.SaldoTotal.Should().Be(alCierre.ValorParaCancelar, "capital, intereses y lo demás cobrable menos el anticipo");
        c.Vivienda.Should().BeEmpty();
        c.TotalInteresesPagados.Should().Be(credito.InteresesPagados);
        c.Explicacion.Texto.Should().Contain("CR-1");
    }

    [Fact]
    public void Los_intereses_son_netos_de_los_reversos_conocidos_a_la_emision()
    {
        var reverso = new EntradaDeOperacion("R2", LoanOperationKind.PaymentReversal, new DateOnly(2027, 3, 15),
            CreditosDePrueba.Instante(new DateOnly(2028, 1, 10)), 13) { Reversa = "P2" };
        var entrada = Credito(new DateOnly(2027, 1, 15),
            Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10),
            Pago("P2", new DateOnly(2027, 3, 15), 160_000m, 11),
            reverso);

        var conocido = MotorDeCartera.Reconstruir(entrada, Cierre);
        var desconocido = MotorDeCartera.Reconstruir(entrada with { InformacionHasta = CreditosDePrueba.Instante(new DateOnly(2028, 1, 5)) }, Cierre);

        CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, conocido)]).Creditos.Single().InteresesPagados
            .Should().Be(InteresDe(conocido, "P1"), "el reverso registrado antes de la emisión quita el pago");
        CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, desconocido)]).Creditos.Single().InteresesPagados
            .Should().Be(InteresDe(desconocido, "P1", "P2"), "con R anterior al reverso el pago todavía cuenta");
    }

    [Fact]
    public void Separa_los_de_vivienda_y_ordena_por_numero()
    {
        var uno = MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 1, 15), Pago("P1", new DateOnly(2027, 2, 15), 160_000m, 10)), Cierre);
        var dos = MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 2, 1)), Cierre);

        var c = CertificadoTributario.Armar(Año,
        [
            new CreditoParaCertificar("CR-9", "Vivienda", true, dos),
            new CreditoParaCertificar("CR-3", "Consumo", false, uno),
        ]);

        c.Creditos.Select(x => x.Numero).Should().Equal("CR-3");
        c.Vivienda.Select(x => x.Numero).Should().Equal("CR-9");
        c.TotalInteresesDeVivienda.Should().Be(0m, "sin pagos no hay intereses pagados");
        c.Vivienda[0].SaldoDeCapital.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Un_credito_cancelado_en_el_año_aparece_con_saldo_cero()
    {
        var entrada = Credito(new DateOnly(2027, 1, 15), Pago("P1", new DateOnly(2027, 6, 15), 4_000_000m, 10, PaymentModality.FullPayoff));
        var alCierre = MotorDeCartera.Reconstruir(entrada, Cierre);

        var credito = CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, alCierre)]).Creditos.Should().ContainSingle().Subject;

        credito.SaldoTotal.Should().Be(0m);
        credito.InteresesPagados.Should().Be(InteresDe(alCierre, "P1")).And.BeGreaterThan(0m);
    }

    [Fact]
    public void Uno_desembolsado_despues_del_año_o_cancelado_antes_no_aparece()
    {
        var anterior = CreditosDePrueba.Simple(desembolso: new DateOnly(2026, 1, 15), cuotas: 24);
        anterior = anterior with
        {
            Operaciones = [.. anterior.Operaciones, Pago("P1", new DateOnly(2026, 3, 15), 4_000_000m, 10, PaymentModality.FullPayoff)],
            InformacionHasta = Emision,
        };

        var c = CertificadoTributario.Armar(Año,
        [
            new CreditoParaCertificar("CR-DESPUES", "L", false, null),
            new CreditoParaCertificar("CR-ANTES", "L", false, MotorDeCartera.Reconstruir(anterior, Cierre)),
        ]);

        c.Vacio.Should().BeTrue();
    }

    [Fact]
    public void El_estado_tiene_que_ser_al_31_de_diciembre()
    {
        var estado = MotorDeCartera.Reconstruir(Credito(new DateOnly(2027, 1, 15)), new DateOnly(2027, 12, 30));

        var armar = () => CertificadoTributario.Armar(Año, [new CreditoParaCertificar("CR-1", "L", false, estado)]);

        armar.Should().Throw<ArgumentException>();
    }
}
