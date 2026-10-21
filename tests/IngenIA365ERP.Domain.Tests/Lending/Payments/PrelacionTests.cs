using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;

namespace IngenIA365ERP.Domain.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0514 (FR-030, FR-072, FR-121; data-model §1.8): la prelación sellada. Por defecto gastos informados → seguros → mora con
/// su suspenso → corriente con su suspenso → capital, la cuota más antigua primero; <c>ConceptFirst</c> como alternativa; los gastos sin
/// constancia van al final; la lista sellada se lee del JSON de <c>LoanTermsVersion</c> y se valida.
/// </summary>
public class PrelacionTests
{
    [Fact]
    public void Por_defecto_seguros_mora_con_su_suspenso_corriente_con_su_suspenso_y_capital_y_los_gastos_no_informados_al_final()
    {
        Prelacion.PorDefecto.Orden.Should().Be(AllocationOrder.OldestInstallmentFirst);
        Prelacion.PorDefecto.ConceptosEnOrden.Should().Equal(
            LoanConcept.Insurance, LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense, LoanConcept.CurrentInterest,
            LoanConcept.CurrentInterestInSuspense, LoanConcept.Principal, LoanConcept.OtherCharge, LoanConcept.CollectionFee);
        Prelacion.PorDefecto.CuotaEspecificaFueraDeOrden.Should().BeFalse();
    }

    [Fact]
    public void Con_los_gastos_informados_van_primero()
    {
        var p = new Prelacion(AllocationOrder.OldestInstallmentFirst, Prelacion.ConceptosPorDefecto, suspensoConSuConcepto: true,
            cuotaEspecificaFueraDeOrden: false, gastosInformados: true);

        p.ConceptosEnOrden[0].Should().Be(LoanConcept.CollectionFee);
        p.Posicion(LoanConcept.CollectionFee).Should().BeLessThan(p.Posicion(LoanConcept.Insurance));
    }

    [Fact]
    public void Sin_el_suspenso_con_su_concepto_los_suspensos_van_despues_del_capital()
    {
        var p = new Prelacion(AllocationOrder.OldestInstallmentFirst, Prelacion.ConceptosPorDefecto, suspensoConSuConcepto: false, cuotaEspecificaFueraDeOrden: false);

        p.Posicion(LoanConcept.DefaultInterestInSuspense).Should().BeGreaterThan(p.Posicion(LoanConcept.Principal));
        p.Posicion(LoanConcept.CurrentInterestInSuspense).Should().BeGreaterThan(p.Posicion(LoanConcept.Principal));
    }

    [Fact]
    public void La_lista_sellada_se_lee_del_json_y_vacia_es_la_de_defecto()
    {
        var sellada = Prelacion.DesdeSellada(AllocationOrder.ConceptFirst, "[\"Principal\",\"CurrentInterest\",\"DefaultInterest\",\"Insurance\"]",
            suspensoConSuConcepto: true, cuotaEspecificaFueraDeOrden: true);

        sellada.Orden.Should().Be(AllocationOrder.ConceptFirst);
        sellada.ConceptosEnOrden.Take(6).Should().Equal(LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense,
            LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance);
        sellada.CuotaEspecificaFueraDeOrden.Should().BeTrue();
        Prelacion.DesdeSellada(AllocationOrder.OldestInstallmentFirst, "[]", true, false).ConceptosEnOrden
            .Should().Equal(Prelacion.PorDefecto.ConceptosEnOrden);
        Prelacion.DesdeSellada(AllocationOrder.OldestInstallmentFirst, null, true, false).ConceptosEnOrden
            .Should().Equal(Prelacion.PorDefecto.ConceptosEnOrden);
    }

    [Theory]
    [InlineData("[\"Principal\",\"Principal\"]")]
    [InlineData("[\"Advance\"]")]
    [InlineData("[\"CreditBalance\",\"Principal\"]")]
    [InlineData("[\"Capital\"]")]
    public void Una_lista_con_un_concepto_repetido_desconocido_o_que_no_se_cobra_se_rechaza(string lista)
    {
        var leer = () => Prelacion.DesdeSellada(AllocationOrder.OldestInstallmentFirst, lista, true, false);

        leer.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(Prelacion.CodigoPrelacionInvalida);
    }

    [Fact]
    public void La_mas_antigua_primero_ordena_por_cuota_y_concepto_primero_por_concepto()
    {
        (int? Cuota, LoanConcept Concepto)[] saldos =
        [
            (2, LoanConcept.Principal), (1, LoanConcept.CurrentInterest), (2, LoanConcept.Insurance), (1, LoanConcept.Principal),
            (null, LoanConcept.OtherCharge), (2, LoanConcept.DefaultInterest),
        ];

        Prelacion.PorDefecto.Ordenar(saldos, s => s.Cuota, s => s.Concepto).Should().Equal(
            (null, LoanConcept.OtherCharge), (1, LoanConcept.CurrentInterest), (1, LoanConcept.Principal),
            (2, LoanConcept.Insurance), (2, LoanConcept.DefaultInterest), (2, LoanConcept.Principal));

        var conceptoPrimero = new Prelacion(AllocationOrder.ConceptFirst, [], true, false);
        conceptoPrimero.Ordenar(saldos, s => s.Cuota, s => s.Concepto).Should().Equal(
            (2, LoanConcept.Insurance), (2, LoanConcept.DefaultInterest), (1, LoanConcept.CurrentInterest),
            (1, LoanConcept.Principal), (2, LoanConcept.Principal), (null, LoanConcept.OtherCharge));
    }

    [Fact]
    public void Permitir_la_cuota_fuera_de_orden_conserva_lo_demas()
    {
        var p = new Prelacion(AllocationOrder.ConceptFirst, [LoanConcept.Principal, LoanConcept.CurrentInterest], false, false);

        var q = p.PermitiendoCuotaFueraDeOrden();

        q.CuotaEspecificaFueraDeOrden.Should().BeTrue();
        q.Orden.Should().Be(p.Orden);
        q.ConceptosEnOrden.Should().Equal(p.ConceptosEnOrden);
        q.PermitiendoCuotaFueraDeOrden().Should().BeSameAs(q);
    }
}
