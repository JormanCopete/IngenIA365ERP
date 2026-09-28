using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Sales.Payments;

namespace IngenIA365ERP.Domain.Tests.Sales.Payments;

/// <summary>
/// Feature 012, I3, T548 (contracts/api.md §22.4 y §22.5, FR-056, FR-097, FR-101, T26): el validador puro de los pagos de un
/// documento. La suma de lo aplicado iguala exactamente el total a pagar (<c>AmountDue</c> = total − retenciones sufridas);
/// vueltas sólo en los medios que las admiten; referencia según el medio, repetible entre pagos del mismo medio sólo si es
/// distinta; <c>last4</c> de cuatro dígitos y ningún campo con forma de número de tarjeta; un medio sin pago parcial cubre
/// todo lo que falta; todo medio que se arquea exige una sesión abierta del usuario; un pago de clase crédito vuelve la venta
/// «a crédito».
/// </summary>
public class ValidadorDePagosTests
{
    private const int Sesion = 7;

    private static readonly MedioDePagoCopia Efectivo = new(1, "EFECTIVO", PaymentMeansClass.Cash, AllowsChange: true, AllowsPartial: true,
        RequiresReference: false, ReferenceKind: null, ReferenceMinLength: null, ReferenceMaxLength: null, UniqueReference: false, CashCountMethod.PhysicalCount);

    private static readonly MedioDePagoCopia Visa = new(2, "VISARB", PaymentMeansClass.CreditCard, AllowsChange: false, AllowsPartial: true,
        RequiresReference: true, PaymentReferenceKind.Approval, ReferenceMinLength: 4, ReferenceMaxLength: 8, UniqueReference: false, CashCountMethod.VoucherTotal);

    private static readonly MedioDePagoCopia Bono = new(3, "BONO", PaymentMeansClass.Voucher, AllowsChange: false, AllowsPartial: false,
        RequiresReference: true, PaymentReferenceKind.VoucherNumber, ReferenceMinLength: null, ReferenceMaxLength: null, UniqueReference: true, CashCountMethod.ByReference);

    private static readonly MedioDePagoCopia CreditoAsociado = new(4, "CREDASOC", PaymentMeansClass.AssociateCredit, AllowsChange: false, AllowsPartial: true,
        RequiresReference: false, ReferenceKind: null, ReferenceMinLength: null, ReferenceMaxLength: null, UniqueReference: false, CashCountMethod.None);

    private static ResultadoDeCobro Validar(decimal amountDue, params PagoPropuesto[] pagos) =>
        ValidadorDePagos.Validar(new PedidoDeCobro(amountDue, pagos, [Sesion]));

    private static IEnumerable<string> Codigos(ResultadoDeCobro r) => r.Errors.Select(e => e.Code);

    [Fact]
    public void Un_pago_mixto_que_suma_exacto_es_valido_y_calcula_las_vueltas()
    {
        var r = Validar(500_000m,
            new PagoPropuesto(Visa, 200_000m, Reference: "123456", Last4: "4242", CashSessionId: Sesion),
            new PagoPropuesto(Efectivo, 200_000m, Tendered: 250_000m, CashSessionId: Sesion),
            new PagoPropuesto(Bono, 100_000m, Reference: "nav-2026 001", CashSessionId: Sesion));

        r.IsValid.Should().BeTrue(string.Join(", ", Codigos(r)));
        r.Paid.Should().Be(500_000m);
        r.Missing.Should().Be(0m);
        r.Excess.Should().Be(0m);
        r.Payments[1].Change.Should().Be(50_000m, "entregado − aplicado");
        r.Payments[2].NormalizedReference.Should().Be("NAV2026001", "mayúsculas, sin espacios ni guiones");
        r.IsCredit.Should().BeFalse();
    }

    [Fact]
    public void La_suma_se_compara_con_el_total_a_pagar_y_dice_cuanto_falta_o_sobra()
    {
        var falta = Validar(100_000m, new PagoPropuesto(Efectivo, 90_000m, CashSessionId: Sesion));
        var e = falta.Errors.Single();
        e.Code.Should().Be(ValidadorDePagos.TotalMismatch);
        e.Data["amountDue"].Should().Be(100_000m);
        e.Data["paid"].Should().Be(90_000m);
        e.Data["missing"].Should().Be(10_000m);
        e.Data["excess"].Should().Be(0m);

        var sobra = Validar(100_000m, new PagoPropuesto(Efectivo, 100_000.01m, CashSessionId: Sesion));
        sobra.Errors.Single().Data["excess"].Should().Be(0.01m, "la igualdad es exacta, en decimal");

        // T26: con 2.500 de retención sufrida, el total 102.500 se paga con 100.000.
        Validar(100_000m, new PagoPropuesto(Efectivo, 100_000m, CashSessionId: Sesion)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Las_vueltas_solo_en_medios_que_las_admiten_y_nunca_negativas()
    {
        Codigos(Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Tendered: 120_000m, Reference: "1234", CashSessionId: Sesion)))
            .Should().Contain(ValidadorDePagos.ChangeNotAllowed);
        Codigos(Validar(100_000m, new PagoPropuesto(Efectivo, 100_000m, Tendered: 90_000m, CashSessionId: Sesion)))
            .Should().Contain(ValidadorDePagos.ChangeNotAllowed, "lo entregado no puede ser menos que lo aplicado");
        Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Tendered: 100_000m, Reference: "1234", CashSessionId: Sesion))
            .IsValid.Should().BeTrue("entregar lo mismo que se aplica no son vueltas");
    }

    [Fact]
    public void La_referencia_es_obligatoria_segun_el_medio_y_respeta_su_longitud()
    {
        var sin = Validar(100_000m, new PagoPropuesto(Visa, 100_000m, CashSessionId: Sesion)).Errors.Single();
        sin.Code.Should().Be(ValidadorDePagos.ReferenceRequired);
        sin.Data["paymentMeansCode"].Should().Be("VISARB");
        sin.Data["referenceKind"].Should().Be(PaymentReferenceKind.Approval);

        Codigos(Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Reference: "123", CashSessionId: Sesion)))
            .Should().Equal(ValidadorDePagos.ReferenceInvalid);
        Codigos(Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Reference: "123456789", CashSessionId: Sesion)))
            .Should().Equal(ValidadorDePagos.ReferenceInvalid);
    }

    [Fact]
    public void El_mismo_medio_se_repite_con_referencias_distintas_pero_no_con_la_misma()
    {
        Validar(300_000m,
            new PagoPropuesto(Visa, 100_000m, Reference: "111111", CashSessionId: Sesion),
            new PagoPropuesto(Visa, 200_000m, Reference: "222222", CashSessionId: Sesion)).IsValid.Should().BeTrue();

        var repetida = Validar(200_000m,
            new PagoPropuesto(Bono, 100_000m, Reference: "NAV-001", CashSessionId: Sesion),
            new PagoPropuesto(Bono, 100_000m, Reference: "nav 001", CashSessionId: Sesion));
        var e = repetida.Errors.Single(x => x.Code == ValidadorDePagos.DuplicateReference);
        e.PaymentIndex.Should().Be(1, "el segundo pago repite la referencia normalizada del primero");
    }

    [Theory]
    [InlineData("4242")]
    [InlineData("0001")]
    public void Last4_con_cuatro_digitos_es_valido(string last4) =>
        Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Reference: "1234", Last4: last4, CashSessionId: Sesion)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("424")]
    [InlineData("42424")]
    [InlineData("42a2")]
    public void Last4_que_no_son_cuatro_digitos_se_rechaza(string last4)
    {
        var e = Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Reference: "1234", Last4: last4, CashSessionId: Sesion)).Errors.Single();
        e.Code.Should().Be(ValidadorDePagos.ReferenceInvalid);
        e.Data["field"].Should().Be("last4");
    }

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("pago 5500000000000004 visa")]
    [InlineData("4111111111111")]
    [InlineData("4111111111111111111")]
    public void Cualquier_campo_con_forma_de_numero_de_tarjeta_se_rechaza(string texto)
    {
        ValidadorDePagos.PareceNumeroDeTarjeta(texto).Should().BeTrue();
        Codigos(Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Reference: "1234", AuthorizationCode: texto, CashSessionId: Sesion)))
            .Should().Contain(ValidadorDePagos.CardNumberNotAllowed);
        Codigos(Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Reference: texto, CashSessionId: Sesion)))
            .Should().Contain(ValidadorDePagos.CardNumberNotAllowed);
        Codigos(Validar(100_000m, new PagoPropuesto(Visa, 100_000m, Reference: "1234", TerminalBatchNumber: texto, CashSessionId: Sesion)))
            .Should().Contain(ValidadorDePagos.CardNumberNotAllowed);
    }

    [Theory]
    [InlineData("411111111111")]
    [InlineData("41111111111111111111")]
    [InlineData("1234 5678 9012 3456 7890")]
    [InlineData("APROB-778899")]
    [InlineData(null)]
    public void Lo_que_no_tiene_13_a_19_digitos_seguidos_no_parece_tarjeta(string? texto) =>
        ValidadorDePagos.PareceNumeroDeTarjeta(texto).Should().BeFalse();

    [Fact]
    public void Un_medio_sin_pago_parcial_cubre_todo_lo_que_falta()
    {
        Codigos(Validar(150_000m,
                new PagoPropuesto(Bono, 100_000m, Reference: "B1", CashSessionId: Sesion),
                new PagoPropuesto(Efectivo, 50_000m, CashSessionId: Sesion)))
            .Should().Equal(ValidadorDePagos.PartialNotAllowed);

        Validar(150_000m,
            new PagoPropuesto(Efectivo, 50_000m, CashSessionId: Sesion),
            new PagoPropuesto(Bono, 100_000m, Reference: "B1", CashSessionId: Sesion)).IsValid.Should().BeTrue("el bono cubre lo que falta");
    }

    [Fact]
    public void Un_medio_que_se_arquea_exige_una_sesion_abierta_del_usuario_y_el_credito_no()
    {
        Codigos(Validar(100_000m, new PagoPropuesto(Efectivo, 100_000m))).Should().Equal(ValidadorDePagos.CashSessionRequired);
        Codigos(Validar(100_000m, new PagoPropuesto(Efectivo, 100_000m, CashSessionId: 99)))
            .Should().Equal([ValidadorDePagos.CashSessionRequired], "la sesión 99 no es una sesión abierta del usuario");
        Validar(100_000m, new PagoPropuesto(CreditoAsociado, 100_000m)).IsValid.Should().BeTrue("el crédito no se arquea (CountMethod = None)");
    }

    [Fact]
    public void Un_pago_de_clase_credito_vuelve_a_credito_la_forma_de_pago()
    {
        var r = Validar(300_000m,
            new PagoPropuesto(Efectivo, 100_000m, CashSessionId: Sesion),
            new PagoPropuesto(CreditoAsociado, 200_000m));
        r.IsValid.Should().BeTrue();
        r.IsCredit.Should().BeTrue();
        ClasesDeMedio.EsCredito(PaymentMeansClass.CustomerCredit).Should().BeTrue();
        ClasesDeMedio.EsCredito(PaymentMeansClass.CreditCard).Should().BeFalse("la tarjeta de crédito es de contado para la cooperativa");
    }

    [Fact]
    public void Un_valor_no_positivo_se_rechaza_y_sin_pagos_falta_todo()
    {
        Codigos(Validar(0m, new PagoPropuesto(Efectivo, 0m, CashSessionId: Sesion))).Should().Contain(ValidadorDePagos.AmountInvalid);
        var r = Validar(10_000m);
        r.Errors.Single().Data["missing"].Should().Be(10_000m);
    }

    [Theory]
    [InlineData(PaymentMeansClass.Cash, CashCountMethod.PhysicalCount)]
    [InlineData(PaymentMeansClass.Check, CashCountMethod.PhysicalCount)]
    [InlineData(PaymentMeansClass.CreditCard, CashCountMethod.VoucherTotal)]
    [InlineData(PaymentMeansClass.DebitCard, CashCountMethod.VoucherTotal)]
    [InlineData(PaymentMeansClass.BankDeposit, CashCountMethod.ByReference)]
    [InlineData(PaymentMeansClass.Transfer, CashCountMethod.ByReference)]
    [InlineData(PaymentMeansClass.Voucher, CashCountMethod.ByReference)]
    [InlineData(PaymentMeansClass.AssociateCredit, CashCountMethod.None)]
    [InlineData(PaymentMeansClass.CustomerCredit, CashCountMethod.None)]
    [InlineData(PaymentMeansClass.Other, CashCountMethod.ByReference)]
    public void El_arqueo_por_defecto_sale_de_la_clase(PaymentMeansClass clase, CashCountMethod metodo) =>
        ClasesDeMedio.ArqueoPorDefecto(clase).Should().Be(metodo);
}
