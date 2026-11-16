using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;

namespace IngenIA365ERP.Domain.Tests.Lending.Payments;

/// <summary>
/// 2026-11-16 (pedido del dueño; <c>Pagos.PrelacionDeRubros</c>): el orden de los rubros de la empresa. De fábrica seguro, administración, interés, cuotas
/// extras y capital; los rubros que no nombra van antes del capital (si nombra sólo uno de seguro y administración, el otro va a su lado); un nombre que
/// no es un rubro o repetido no sirve; el texto sellado en un pago se lee igual y, si no sirve, falla el crédito. Desde la fase 2 de la feature 024 la
/// administración es su propio concepto y puede ir separada del seguro.
/// </summary>
public class OrdenDeRubrosTests
{
    [Fact]
    public void El_de_fabrica_es_seguro_administracion_interes_cuotas_extras_y_capital_y_cumple_la_forma()
    {
        OrdenDeRubros.Defecto.Should().Be("Seguro,Administracion,Interes,CuotasExtras,Capital");
        OrdenDeRubros.DeFabrica.Completo.Should().Equal(RubroDeCartera.Seguro, RubroDeCartera.Administracion, RubroDeCartera.Interes,
            RubroDeCartera.GastosDeCobranza, RubroDeCartera.OtrosCargos, RubroDeCartera.CuotasExtras, RubroDeCartera.Capital);
        Regex.IsMatch(OrdenDeRubros.Defecto, OrdenDeRubros.Patron).Should().BeTrue();
        OrdenDeRubros.Leer(OrdenDeRubros.Defecto).Should().Be(OrdenDeRubros.DeFabrica);
    }

    [Theory]
    [InlineData("Capital,Interes", "Seguro,Administracion,GastosDeCobranza,OtrosCargos,CuotasExtras,Capital,Interes")]
    [InlineData("Interes", "Interes,Seguro,Administracion,GastosDeCobranza,OtrosCargos,CuotasExtras,Capital")]
    [InlineData("Administracion,Interes,Capital", "Seguro,Administracion,Interes,GastosDeCobranza,OtrosCargos,CuotasExtras,Capital")]
    [InlineData("Capital,CuotasExtras,Seguro", "Interes,GastosDeCobranza,OtrosCargos,Capital,CuotasExtras,Seguro,Administracion")]
    [InlineData("OtrosCargos,Seguro,Administracion,Interes,GastosDeCobranza,CuotasExtras,Capital",
        "OtrosCargos,Seguro,Administracion,Interes,GastosDeCobranza,CuotasExtras,Capital")]
    public void Los_que_no_nombra_van_antes_del_capital_y_el_capital_al_final(string texto, string completo)
    {
        var orden = OrdenDeRubros.Leer(texto)!;

        orden.Texto.Should().Be(texto);
        string.Join(",", orden.Completo).Should().Be(completo, "los que faltan van antes del primer rubro de capital, en el orden del enum");
    }

    [Theory]
    [InlineData("", "Indique al menos un rubro.")]
    [InlineData("Seguro,Prima", "«Prima» no es un rubro")]
    [InlineData("seguro,Capital", "«seguro» no es un rubro")]
    [InlineData("3,Capital", "«3» no es un rubro")]
    [InlineData("Capital,Seguro,Capital", "«Capital» va una sola vez.")]
    public void Un_texto_vacio_con_un_rubro_desconocido_o_repetido_no_sirve(string texto, string motivo)
    {
        OrdenDeRubros.Problema(texto).Should().StartWith(motivo);
        OrdenDeRubros.Leer(texto).Should().BeNull();
    }

    [Fact]
    public void El_texto_sellado_vacio_es_la_prelacion_de_la_linea_y_uno_que_no_sirve_falla_el_credito()
    {
        OrdenDeRubros.DesdeSellado(null).Should().BeNull();
        OrdenDeRubros.DesdeSellado(" ").Should().BeNull();
        OrdenDeRubros.DesdeSellado("Capital,Interes").Should().Be(OrdenDeRubros.Leer("Capital,Interes"));

        var leer = () => OrdenDeRubros.DesdeSellado("Capital,Capital");
        leer.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(OrdenDeRubros.CodigoOrdenInvalido);
    }

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 2): la administración es un rubro propio —su concepto <c>Administration</c>—, así que puede ir
    /// separada del seguro, y la prelación la pone en su puesto.
    /// </summary>
    [Fact]
    public void Seguro_y_administracion_pueden_ir_separados_y_cada_uno_cubre_su_concepto()
    {
        const string texto = "Seguro,Interes,Administracion,Capital";
        OrdenDeRubros.Problema(texto).Should().BeNull();
        var orden = OrdenDeRubros.Leer(texto)!;
        orden.Completo.Should().Equal(RubroDeCartera.Seguro, RubroDeCartera.Interes, RubroDeCartera.Administracion, RubroDeCartera.GastosDeCobranza,
            RubroDeCartera.OtrosCargos, RubroDeCartera.CuotasExtras, RubroDeCartera.Capital);

        var p = Prelacion.PorDefecto.ConRubros(orden);
        p.Posicion(LoanConcept.Insurance).Should().BeLessThan(p.Posicion(LoanConcept.CurrentInterest));
        p.Posicion(LoanConcept.CurrentInterest).Should().BeLessThan(p.Posicion(LoanConcept.Administration),
            "la administración va después del interés, donde la nombró la empresa");
        p.Posicion(LoanConcept.Administration).Should().BeLessThan(p.Posicion(LoanConcept.Principal));
    }

    [Fact]
    public void Dos_ordenes_con_el_mismo_texto_son_iguales()
    {
        OrdenDeRubros.Leer("Capital,Interes,Seguro").Should().Be(OrdenDeRubros.Leer("Capital,Interes,Seguro"));
        OrdenDeRubros.Leer("Capital,Interes,Seguro")!.GetHashCode().Should().Be(OrdenDeRubros.Leer("Capital,Interes,Seguro")!.GetHashCode());
        OrdenDeRubros.Leer("Capital,Interes,Seguro").Should().NotBe(OrdenDeRubros.DeFabrica);
    }
}
