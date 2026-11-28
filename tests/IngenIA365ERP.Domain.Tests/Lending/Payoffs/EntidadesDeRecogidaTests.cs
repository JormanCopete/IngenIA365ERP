using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;

namespace IngenIA365ERP.Domain.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T005): la recogida proyectada de la solicitud y la aplicada en el desembolso nacen por sus fábricas, con el identificador de su clase
/// y sólo ése, y la aplicada se aplica y se reversa una vez.
/// </summary>
public class EntidadesDeRecogidaTests
{
    private static readonly DesgloseDeRecogida Desglose = new(Capital: 2_000_000m, InteresCorriente: 30_000m, Seguro: 5_000m);

    private static ProyeccionDeRecogida Proyeccion(SentInstallmentState envio = SentInstallmentState.None) => new(
        new DateOnly(2026, 11, 28), new DateTime(2026, 11, 20, 10, 0, 0), Desglose, "sha256:" + new string('a', 64), "Corrido",
        InstallmentTreatment.Include, new CuotaCausadaDeRecogida(7, 210_000m), envio);

    [Fact]
    public void La_proyectada_de_un_credito_guarda_su_desglose_y_su_cuota_causada()
    {
        var p = CreditApplicationPayoff.Proyectar(10, ProductoRecogido.Credito(5, "CR-000123"), PayoffMode.Total, 999m, 1,
            Proyeccion(SentInstallmentState.SentWithoutResponse));

        p.ProductKind.Should().Be(PayoffProductKind.Loan);
        p.LoanId.Should().Be(5);
        p.AccountId.Should().BeNull();
        p.ProductNumber.Should().Be("CR-000123");
        p.RequestedAmount.Should().BeNull("un total no lleva valor pedido");
        p.ProjectedAmount.Should().Be(Desglose.Total);
        p.Desglose().Should().Be(Desglose);
        p.CurrentInstallmentNumber.Should().Be(7);
        p.CurrentInstallmentAmount.Should().Be(210_000m);
        p.SentInstallmentState.Should().Be(SentInstallmentState.SentWithoutResponse);
        p.InterestMode.Should().Be("Corrido");
        p.TieneProyeccion.Should().BeTrue();
    }

    [Fact]
    public void Un_parcial_o_un_abono_exigen_valor_positivo()
    {
        var credito = ProductoRecogido.Credito(5, "CR-1");
        var act = () => CreditApplicationPayoff.Proyectar(10, credito, PayoffMode.Partial, null, 1, Proyeccion());
        act.Should().Throw<ArgumentOutOfRangeException>();

        var cuenta = ProductoRecogido.Cuenta(8, "AP-1");
        var sinCuota = new ProyeccionDeRecogida(new DateOnly(2026, 11, 28), new DateTime(2026, 11, 20), DesgloseDeRecogida.Vacio, "sha256:x");
        var abono = () => CreditApplicationPayoff.Proyectar(10, cuenta, PayoffMode.Deposit, 0m, 1, sinCuota);
        abono.Should().Throw<ArgumentOutOfRangeException>();

        CreditApplicationPayoff.Proyectar(10, cuenta, PayoffMode.Deposit, 50_000m, 2, sinCuota).RequestedAmount.Should().Be(50_000m);
    }

    [Fact]
    public void La_cuota_causada_solo_aplica_a_un_credito()
    {
        var act = () => CreditApplicationPayoff.Proyectar(10, ProductoRecogido.Cuenta(8, "AP-1"), PayoffMode.Installments, null, 1, Proyeccion());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void El_producto_lleva_el_identificador_de_su_clase_y_solo_ese()
    {
        var factura = ProductoRecogido.Factura(" FV-77 ", "FV-77");
        factura.Clase.Should().Be(PayoffProductKind.CommercialReceivable);
        factura.CommercialReceivableReference.Should().Be("FV-77");
        factura.LoanId.Should().BeNull();
        factura.AccountId.Should().BeNull();

        ((Action)(() => ProductoRecogido.Credito(0, "CR-1"))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => ProductoRecogido.Factura(new string('x', 61), "FV"))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => ProductoRecogido.Cuenta(3, " "))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void La_aplicada_guarda_la_diferencia_se_aplica_y_se_reversa_una_vez()
    {
        var ahora = Desglose with { Capital = 1_820_000m };
        var diferencia = DiferenciaDeRecogida.Comparar(Desglose, ahora);
        var a = DisbursementPayoff.Evaluar(900, 33, ProductoRecogido.Credito(5, "CR-1"), PayoffMode.Total, InstallmentTreatment.Include, "Corrido",
            Desglose.Total, ahora.Total, diferencia, false, "CAR:op:prod", "asesor", new DateTime(2026, 11, 28, 9, 0, 0));

        a.Difference.Should().Be(-180_000m);
        a.DifferenceCause.Should().Be(PayoffDifferenceCause.Payments);
        DesgloseDeRecogida.Leer(a.DifferenceDetail).Capital.Should().Be(-180_000m);
        a.AppliedAmount.Should().Be(0m);

        a.Aplicar(ahora.Total, operacionDePagoId: 77);
        a.AppliedAmount.Should().Be(ahora.Total);
        a.PaymentOperationId.Should().Be(77);

        a.Reversar(new DateTime(2026, 11, 29), "CAR:op:prod:reverso");
        a.Reversar(new DateTime(2026, 11, 30), "CAR:op:prod:reverso");
        a.ReversedAt.Should().Be(new DateTime(2026, 11, 29), "repetir con la misma clave no hace nada");
        var otra = () => a.Reversar(new DateTime(2026, 11, 30), "otra");
        otra.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be("Lending.Payoff.AlreadyReversed");
    }

    [Fact]
    public void El_omitido_no_se_aplica_y_su_actual_es_cero()
    {
        var diferencia = DiferenciaDeRecogida.Comparar(Desglose, DesgloseDeRecogida.Vacio, productoCerrado: true);
        var a = DisbursementPayoff.Evaluar(900, 33, ProductoRecogido.Credito(5, "CR-1"), PayoffMode.Total, null, null, Desglose.Total, 123m,
            diferencia, true, "CAR:op:prod", "asesor", new DateTime(2026, 11, 28));

        a.CurrentAmount.Should().Be(0m);
        a.Difference.Should().Be(-Desglose.Total);
        a.DifferenceCause.Should().Be(PayoffDifferenceCause.ProductClosed);
        var aplicar = () => a.Aplicar(1m, operacionDePagoId: 1);
        aplicar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be("Lending.Payoff.Omitted");
    }

    [Fact]
    public void La_operacion_aplicada_corresponde_a_la_clase_y_quien_reconoce_va_con_su_instante()
    {
        var sinDiferencia = DiferenciaDeRecogida.Comparar(Desglose, Desglose);
        var cuenta = DisbursementPayoff.Evaluar(900, null, ProductoRecogido.Cuenta(8, "AP-1"), PayoffMode.Installments, null, null, 0m, 50_000m,
            sinDiferencia, false, "CAR:op:cta", null, null);

        var mal = () => cuenta.Aplicar(50_000m, operacionDePagoId: 77);
        mal.Should().Throw<ArgumentException>();
        cuenta.Aplicar(50_000m, operacionDeCuentaId: 12);
        cuenta.AccountOperationId.Should().Be(12);

        var sinInstante = () => DisbursementPayoff.Evaluar(900, null, ProductoRecogido.Credito(5, "CR-1"), PayoffMode.Total, null, null, 0m, 1m,
            sinDiferencia, false, "CAR:x", "asesor", null);
        sinInstante.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T063, T066): la que deja la cuota causada a su canal guarda la cuota y lo que debe (los dos o ninguno, sólo excluyéndola),
    /// queda pendiente hasta que la cuota se pague (una vez) y deja de estarlo si se reversa. (nuevo)
    /// </summary>
    [Fact]
    public void La_que_deja_la_cuota_causada_la_guarda_hasta_que_se_paga()
    {
        var sinDiferencia = DiferenciaDeRecogida.Comparar(Desglose, Desglose);
        DisbursementPayoff Evaluada(InstallmentTreatment? tratamiento, int? cuota, decimal? valor, bool omitido = false) =>
            DisbursementPayoff.Evaluar(900, 33, ProductoRecogido.Credito(5, "CR-1"), PayoffMode.Total, tratamiento, "Corrido", Desglose.Total, Desglose.Total,
                sinDiferencia, omitido, "CAR:op:prod", null, null, cuota, valor);

        var a = Evaluada(InstallmentTreatment.Exclude, 7, 312_400m);
        (a.LeftInstallmentNumber, a.LeftInstallmentAmount, a.LeftInstallmentSettledAt, a.CuotaDejadaPendiente)
            .Should().Be(((int?)7, (decimal?)312_400m, (DateTime?)null, true));
        a.MarcarCuotaDejadaPagada(new DateTime(2026, 12, 30));
        a.MarcarCuotaDejadaPagada(new DateTime(2026, 12, 31));
        a.LeftInstallmentSettledAt.Should().Be(new DateTime(2026, 12, 30), "se marca una vez");
        a.CuotaDejadaPendiente.Should().BeFalse();

        var reversada = Evaluada(InstallmentTreatment.Exclude, 7, 312_400m);
        reversada.Reversar(new DateTime(2026, 11, 29), "CAR:op:prod:reverso");
        reversada.CuotaDejadaPendiente.Should().BeFalse("al anular el crédito nuevo la cuota vuelve a ser del crédito viejo entero");

        Evaluada(InstallmentTreatment.Include, null, null).LeftInstallmentNumber.Should().BeNull();
        ((Action)(() => Evaluada(InstallmentTreatment.Exclude, 7, null))).Should().Throw<ArgumentException>("la cuota va con su valor");
        ((Action)(() => Evaluada(InstallmentTreatment.Include, 7, 1m))).Should().Throw<ArgumentException>("sólo se deja excluyéndola");
        ((Action)(() => Evaluada(InstallmentTreatment.Exclude, 7, 1m, omitido: true))).Should().Throw<ArgumentException>("un omitido no deja nada");
        var sinCuota = () => Evaluada(InstallmentTreatment.Include, null, null).MarcarCuotaDejadaPagada(DateTime.UtcNow);
        sinCuota.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be("Lending.Payoff.NoLeftInstallment");
    }
}
