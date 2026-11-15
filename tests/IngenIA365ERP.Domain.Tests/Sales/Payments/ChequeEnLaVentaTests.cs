using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Sales.Payments;

namespace IngenIA365ERP.Domain.Tests.Sales.Payments;

/// <summary>
/// Feature 022, T045 y T049 (FR-048, Q17): el cheque en una venta lleva banco librado, número, cuenta y fecha, exige cliente identificado y su
/// referencia (su número) y su cuenta no se confunden con un número de tarjeta.
/// </summary>
public class ChequeEnLaVentaTests
{
    private const int Sesion = 7;

    private static readonly MedioDePagoCopia Cheque = new(9, "CHEQUE", PaymentMeansClass.Check, AllowsChange: false, AllowsPartial: true,
        RequiresReference: true, PaymentReferenceKind.CheckNumber, ReferenceMinLength: null, ReferenceMaxLength: null, UniqueReference: false,
        CashCountMethod.PhysicalCount);

    private static readonly DatosDeCheque Completo = new(5, "1234567890123456", "0011223344556677", new DateOnly(2026, 11, 8), true, "Girador de ensayo");

    private static ResultadoDeCobro Validar(PagoPropuesto pago, bool consumidorFinal = false) =>
        ValidadorDePagos.Validar(new PedidoDeCobro(100_000m, [pago], [Sesion], consumidorFinal));

    [Fact]
    public void Un_cheque_completo_de_un_cliente_identificado_es_valido_aunque_su_numero_y_su_cuenta_tengan_16_digitos()
    {
        var r = Validar(new PagoPropuesto(Cheque, 100_000m, Reference: Completo.Number, CashSessionId: Sesion, Cheque: Completo));

        r.IsValid.Should().BeTrue(string.Join(", ", r.Errors.Select(e => e.Code)));
    }

    [Fact]
    public void Sin_banco_numero_cuenta_o_fecha_es_CheckDataRequired_con_lo_que_falta()
    {
        var r = Validar(new PagoPropuesto(Cheque, 100_000m, Reference: "77", CashSessionId: Sesion, Cheque: new DatosDeCheque(null, "77", null, null)));

        var e = r.Errors.Single(x => x.Code == ValidadorDePagos.CheckDataRequired);
        e.PaymentIndex.Should().Be(0);
        ((IEnumerable<string>)e.Data["missing"]!).Should().Equal("bank", "accountNumber", "issueDate");
    }

    [Fact]
    public void Un_cheque_sin_datos_tambien_los_pide_todos()
    {
        var r = Validar(new PagoPropuesto(Cheque, 100_000m, Reference: "77", CashSessionId: Sesion));

        ((IEnumerable<string>)r.Errors.Single(x => x.Code == ValidadorDePagos.CheckDataRequired).Data["missing"]!)
            .Should().Equal("bank", "number", "accountNumber", "issueDate");
    }

    [Fact]
    public void El_cheque_del_consumidor_final_se_rechaza()
    {
        var r = Validar(new PagoPropuesto(Cheque, 100_000m, Reference: "77", CashSessionId: Sesion, Cheque: Completo), consumidorFinal: true);

        r.Errors.Select(e => e.Code).Should().Contain(ValidadorDePagos.CheckRequiresIdentifiedCustomer);
    }

    [Fact]
    public void Fuera_del_cheque_la_referencia_con_forma_de_tarjeta_sigue_rechazada()
    {
        var transferencia = new MedioDePagoCopia(7, "TRANSF", PaymentMeansClass.Transfer, false, true, true, PaymentReferenceKind.Deposit, null, null, false,
            CashCountMethod.ByReference);

        var r = Validar(new PagoPropuesto(transferencia, 100_000m, Reference: "4111 1111 1111 1111", CashSessionId: Sesion));

        r.Errors.Select(e => e.Code).Should().Contain(ValidadorDePagos.CardNumberNotAllowed);
    }

    [Fact]
    public void El_consumidor_final_puede_pagar_con_otros_medios()
    {
        var efectivo = new MedioDePagoCopia(1, "EFECTIVO", PaymentMeansClass.Cash, true, true, false, null, null, null, false, CashCountMethod.PhysicalCount);

        Validar(new PagoPropuesto(efectivo, 100_000m, CashSessionId: Sesion), consumidorFinal: true).IsValid.Should().BeTrue();
    }
}
