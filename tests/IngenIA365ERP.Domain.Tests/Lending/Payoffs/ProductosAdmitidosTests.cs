using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;

namespace IngenIA365ERP.Domain.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T003, R15): el lector de <c>Recogida.ProductosAdmitidos</c>. El defecto es lo de siempre (sólo créditos, total o parcial); un texto
/// inválido se lee como el defecto; cada producto admite sólo sus modos; el patrón del parámetro y el lector dicen lo mismo.
/// </summary>
public class ProductosAdmitidosTests
{
    [Fact]
    public void El_defecto_son_los_creditos_total_o_parcial()
    {
        ProductosAdmitidos.Defecto.Should().Be("Creditos:Total,Parcial");
        var a = ProductosAdmitidos.PorDefecto;
        a.Admite(ProductoDeRecogida.Creditos, PayoffMode.Total).Should().BeTrue();
        a.Admite(ProductoDeRecogida.Creditos, PayoffMode.Partial).Should().BeTrue();
        a.Modos.Keys.Should().Equal(ProductoDeRecogida.Creditos);
        foreach (var p in ProductosAdmitidos.Productos.Where(p => p != ProductoDeRecogida.Creditos))
            a.Admite(p).Should().BeFalse(p.ToString());
    }

    [Fact]
    public void Lee_varios_productos_con_sus_modos()
    {
        var a = ProductosAdmitidos.Leer("Creditos:Total;Aportes:Cuotas,Abono;AhorroContractual:Cuotas;FacturasComerciales:Total,Parcial");

        a.ModosDe(ProductoDeRecogida.Creditos).Should().BeEquivalentTo([PayoffMode.Total]);
        a.Admite(ProductoDeRecogida.Creditos, PayoffMode.Partial).Should().BeFalse();
        a.ModosDe(ProductoDeRecogida.Aportes).Should().BeEquivalentTo([PayoffMode.Installments, PayoffMode.Deposit]);
        a.ModosDe(ProductoDeRecogida.AhorroContractual).Should().BeEquivalentTo([PayoffMode.Installments]);
        a.ModosDe(ProductoDeRecogida.FacturasComerciales).Should().BeEquivalentTo([PayoffMode.Total, PayoffMode.Partial]);
        a.Admite(ProductoDeRecogida.AhorroALaVista).Should().BeFalse();
        a.ModosDe(ProductoDeRecogida.AhorroALaVista).Should().BeEmpty();
    }

    [Fact]
    public void No_distingue_mayusculas_y_admite_espacios_alrededor_de_los_separadores()
    {
        var a = ProductosAdmitidos.Leer(" creditos : total , PARCIAL ; ahorroalavista:abono ");

        a.ModosDe(ProductoDeRecogida.Creditos).Should().BeEquivalentTo([PayoffMode.Total, PayoffMode.Partial]);
        a.ModosDe(ProductoDeRecogida.AhorroALaVista).Should().BeEquivalentTo([PayoffMode.Deposit]);
    }

    [Fact]
    public void Un_producto_repetido_junta_sus_modos()
    {
        var a = ProductosAdmitidos.Leer("Creditos:Total;Creditos:Parcial");

        a.ModosDe(ProductoDeRecogida.Creditos).Should().BeEquivalentTo([PayoffMode.Total, PayoffMode.Partial]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Creditos")]
    [InlineData("Creditos:")]
    [InlineData(":Total")]
    [InlineData("Creditos:Total;")]
    [InlineData("Cdat:Total")]
    [InlineData("Creditos:Cuotas")]
    [InlineData("AhorroALaVista:Cuotas")]
    [InlineData("AhorroContractual:Abono")]
    [InlineData("Aportes:Total")]
    [InlineData("Creditos:Total,")]
    [InlineData("Creditos:Total:Parcial")]
    public void Un_texto_invalido_se_lee_como_el_defecto(string? texto)
    {
        ProductosAdmitidos.IntentarLeer(texto).Should().BeNull();
        var a = ProductosAdmitidos.Leer(texto);
        a.Should().BeSameAs(ProductosAdmitidos.PorDefecto);
        ProductosAdmitidos.Escribir(a).Should().Be(ProductosAdmitidos.Defecto);
    }

    [Theory]
    [InlineData("Creditos:Total,Parcial", true)]
    [InlineData("Creditos:Total", true)]
    [InlineData("Aportes:Cuotas,Abono;AhorroALaVista:Abono;AhorroPermanente:Abono;AhorroContractual:Cuotas", true)]
    [InlineData("Creditos : Total , Parcial ; FacturasComerciales : Parcial", true)]
    [InlineData("Creditos:Total;Creditos:Parcial", true)]
    [InlineData("AhorroALaVista:Cuotas", false)]
    [InlineData("AhorroContractual:Abono", false)]
    [InlineData("Creditos:Cuotas", false)]
    [InlineData("Cdat:Abono", false)]
    [InlineData("Creditos:Total;", false)]
    [InlineData("creditos:total", false)]
    public void El_patron_del_parametro_acepta_lo_que_el_lector_lee(string texto, bool admitido)
    {
        Regex.IsMatch(texto, ProductosAdmitidos.Patron).Should().Be(admitido);
        if (admitido) ProductosAdmitidos.IntentarLeer(texto).Should().NotBeNull();
    }

    [Fact]
    public void Escribir_y_leer_es_ida_y_vuelta_en_el_orden_de_la_pantalla()
    {
        const string texto = "FacturasComerciales:Parcial,Total;Aportes:Abono,Cuotas;Creditos:Parcial";

        var escrito = ProductosAdmitidos.Escribir(ProductosAdmitidos.Leer(texto));

        escrito.Should().Be("Creditos:Parcial;Aportes:Cuotas,Abono;FacturasComerciales:Total,Parcial");
        ProductosAdmitidos.Escribir(ProductosAdmitidos.Leer(escrito)).Should().Be(escrito);
    }

    [Fact]
    public void Cada_producto_tiene_su_clase_y_las_cuentas_su_producto()
    {
        ProductosAdmitidos.Clase(ProductoDeRecogida.Creditos).Should().Be(PayoffProductKind.Loan);
        ProductosAdmitidos.Clase(ProductoDeRecogida.FacturasComerciales).Should().Be(PayoffProductKind.CommercialReceivable);
        foreach (var p in new[] { ProductoDeRecogida.Aportes, ProductoDeRecogida.AhorroALaVista, ProductoDeRecogida.AhorroPermanente,
                     ProductoDeRecogida.AhorroContractual })
            ProductosAdmitidos.Clase(p).Should().Be(PayoffProductKind.Account, p.ToString());

        ProductosAdmitidos.DeCuenta(LineNature.Contribution, null).Should().Be(ProductoDeRecogida.Aportes);
        ProductosAdmitidos.DeCuenta(LineNature.Deposit, DepositClass.Demand).Should().Be(ProductoDeRecogida.AhorroALaVista);
        ProductosAdmitidos.DeCuenta(LineNature.Deposit, DepositClass.Permanent).Should().Be(ProductoDeRecogida.AhorroPermanente);
        ProductosAdmitidos.DeCuenta(LineNature.Deposit, DepositClass.Contractual).Should().Be(ProductoDeRecogida.AhorroContractual);
        ProductosAdmitidos.DeCuenta(LineNature.Deposit, DepositClass.TermDeposit).Should().BeNull("el CDAT nunca se recoge");
        ProductosAdmitidos.DeCuenta(LineNature.Placement, null).Should().BeNull();
    }
}
