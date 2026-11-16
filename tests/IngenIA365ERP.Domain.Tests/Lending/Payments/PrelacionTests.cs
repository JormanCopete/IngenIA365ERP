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

    // ------------------------------------------------------------------------------- 2026-11-16 (pedido del dueño): orden de rubros de la empresa --

    [Fact]
    public void Con_los_rubros_de_fabrica_seguro_interes_otros_cargos_y_capital_y_los_gastos_no_informados_al_final()
    {
        var p = Prelacion.PorDefecto.ConRubros(OrdenDeRubros.DeFabrica);

        p.Rubros.Should().Be(OrdenDeRubros.DeFabrica);
        p.ConceptosEnOrden.Should().Equal(
            LoanConcept.Insurance, LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense, LoanConcept.CurrentInterest,
            LoanConcept.CurrentInterestInSuspense, LoanConcept.OtherCharge, LoanConcept.Principal, LoanConcept.CollectionFee);
        p.Posicion(LoanConcept.Principal, deCuotaExtra: true).Should().BeGreaterThan(p.Posicion(LoanConcept.OtherCharge))
            .And.BeLessThan(p.Posicion(LoanConcept.Principal), "las cuotas extras van antes que el capital");
        p.Orden.Should().Be(Prelacion.PorDefecto.Orden, "el orden de las cuotas sigue siendo el de la línea");

        var informados = new Prelacion(AllocationOrder.OldestInstallmentFirst, [], true, false, gastosInformados: true).ConRubros(OrdenDeRubros.DeFabrica);
        informados.ConceptosEnOrden.Should().Equal(
            LoanConcept.Insurance, LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense, LoanConcept.CurrentInterest,
            LoanConcept.CurrentInterestInSuspense, LoanConcept.CollectionFee, LoanConcept.OtherCharge, LoanConcept.Principal);
    }

    [Fact]
    public void Con_otro_orden_el_capital_primero_y_sin_el_suspenso_con_su_concepto_los_suspensos_despues_de_todo()
    {
        var p = new Prelacion(AllocationOrder.OldestInstallmentFirst, [], suspensoConSuConcepto: false, cuotaEspecificaFueraDeOrden: false)
            .ConRubros(OrdenDeRubros.Leer("Capital,Interes,Seguro")!);

        p.ConceptosEnOrden.Should().Equal(
            LoanConcept.OtherCharge, LoanConcept.Principal, LoanConcept.DefaultInterest, LoanConcept.CurrentInterest, LoanConcept.Insurance,
            LoanConcept.DefaultInterestInSuspense, LoanConcept.CurrentInterestInSuspense, LoanConcept.CollectionFee);
        p.Posicion(LoanConcept.Principal, deCuotaExtra: true).Should().Be(p.Posicion(LoanConcept.OtherCharge) + 1,
            "las cuotas extras que no nombra van justo antes del capital, después de los otros cargos");
    }

    [Fact]
    public void Con_rubros_y_concepto_primero_el_capital_de_la_cuota_con_prima_va_antes_que_el_de_las_demas()
    {
        (int? Cuota, LoanConcept Concepto)[] saldos =
        [
            (1, LoanConcept.Principal), (2, LoanConcept.Principal), (1, LoanConcept.CurrentInterest), (2, LoanConcept.CurrentInterest), (null, LoanConcept.OtherCharge),
        ];
        var lineal = new Prelacion(AllocationOrder.ConceptFirst, [], true, false);
        var conRubros = lineal.ConRubros(OrdenDeRubros.DeFabrica);

        conRubros.Ordenar(saldos, s => s.Cuota, s => s.Concepto, s => s.Concepto == LoanConcept.Principal && s.Cuota == 2).Should().Equal(
            (1, LoanConcept.CurrentInterest), (2, LoanConcept.CurrentInterest), (null, LoanConcept.OtherCharge), (2, LoanConcept.Principal), (1, LoanConcept.Principal));
        (int? Cuota, LoanConcept Concepto)[] deLaLinea =
            [(1, LoanConcept.CurrentInterest), (2, LoanConcept.CurrentInterest), (1, LoanConcept.Principal), (2, LoanConcept.Principal), (null, LoanConcept.OtherCharge)];
        lineal.Ordenar(saldos, s => s.Cuota, s => s.Concepto, s => s.Concepto == LoanConcept.Principal && s.Cuota == 2).Should().Equal(deLaLinea,
            "sin rubros la cuota con prima no tiene puesto propio: la prelación de la línea, la de siempre");
    }

    [Fact]
    public void La_lista_de_defecto_de_la_linea_no_es_propia_y_una_distinta_si()
    {
        Prelacion.PorDefecto.TieneListaPropia.Should().BeFalse();
        Prelacion.DesdeSellada(AllocationOrder.OldestInstallmentFirst, "[\"CollectionFee\",\"Insurance\",\"DefaultInterest\",\"CurrentInterest\",\"Principal\"]",
            true, false).TieneListaPropia.Should().BeFalse("la pantalla y la plantilla siempre guardan la de defecto");
        Prelacion.DesdeSellada(AllocationOrder.OldestInstallmentFirst, "[]", true, false).TieneListaPropia.Should().BeFalse();
        Prelacion.DesdeSellada(AllocationOrder.OldestInstallmentFirst, "[\"Principal\",\"CurrentInterest\"]", true, false).TieneListaPropia.Should().BeTrue();
    }

    [Fact]
    public void Con_rubros_permitir_la_cuota_fuera_de_orden_los_conserva_y_los_mismos_rubros_no_crean_otra()
    {
        var p = Prelacion.PorDefecto.ConRubros(OrdenDeRubros.Leer("Capital,Interes")!);

        var q = p.PermitiendoCuotaFueraDeOrden();

        q.Rubros.Should().Be(p.Rubros);
        q.ConceptosEnOrden.Should().Equal(p.ConceptosEnOrden);
        p.ConRubros(OrdenDeRubros.Leer("Capital,Interes")!).Should().BeSameAs(p);
        Prelacion.PorDefecto.Rubros.Should().BeNull();
    }
}
