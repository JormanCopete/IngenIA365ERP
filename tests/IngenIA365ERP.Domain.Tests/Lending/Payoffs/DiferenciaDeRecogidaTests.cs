using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;

namespace IngenIA365ERP.Domain.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T008, R9): el comparador de desgloses. Diferencia total y por concepto («ahora − proyectado») y las causas: capital menor o anticipo
/// mayor → pagos; con el capital igual, lo que se causa distinto → causación; mora → mora; producto cerrado → cerrado; lo demás → otro.
/// </summary>
public class DiferenciaDeRecogidaTests
{
    /// <summary>Un crédito proyectado al 28/11: 2.000.000 de capital, 30.000 de interés, 5.000 de seguro y 2.000 de administración.</summary>
    private static readonly DesgloseDeRecogida Proyectado = new(Capital: 2_000_000m, InteresCorriente: 30_000m, Seguro: 5_000m, Administracion: 2_000m);

    [Fact]
    public void Sin_cambios_no_hay_diferencia()
    {
        var r = DiferenciaDeRecogida.Comparar(Proyectado, Proyectado with { });

        r.Total.Should().Be(0m);
        r.Causa.Should().Be(PayoffDifferenceCause.None);
        r.HayDiferencia.Should().BeFalse();
        r.PorConcepto.Should().Be(DesgloseDeRecogida.Vacio);
    }

    [Fact]
    public void Un_pago_en_caja_baja_el_capital_y_la_causa_es_pagos()
    {
        // El asociado pagó una cuota el 25/11: el capital bajó 180.000 y el interés 25.000.
        var ahora = Proyectado with { Capital = 1_820_000m, InteresCorriente = 5_000m };

        var r = DiferenciaDeRecogida.Comparar(Proyectado, ahora);

        r.Total.Should().Be(-205_000m, "se recoge menos y el neto sube");
        r.PorConcepto.Capital.Should().Be(-180_000m);
        r.PorConcepto.InteresCorriente.Should().Be(-25_000m);
        r.Causa.Should().Be(PayoffDifferenceCause.Payments, "el cambio del interés lo explica el pago");
    }

    [Fact]
    public void Con_el_capital_igual_mas_interes_o_seguro_es_causacion()
    {
        var ahora = Proyectado with { InteresCorriente = 34_000m, Seguro = 5_400m };

        var r = DiferenciaDeRecogida.Comparar(Proyectado, ahora);

        r.Total.Should().Be(4_400m);
        r.Causa.Should().Be(PayoffDifferenceCause.Accrual);
    }

    [Fact]
    public void El_interes_devuelto_por_proporcionales_resta_y_es_causacion()
    {
        var proyectado = Proyectado with { InteresDevuelto = 10_000m };
        var ahora = Proyectado with { InteresDevuelto = 4_000m };

        var r = DiferenciaDeRecogida.Comparar(proyectado, ahora);

        r.Total.Should().Be(6_000m, "se devuelve menos: se recoge más");
        r.PorConcepto.InteresDevuelto.Should().Be(-6_000m);
        r.Causa.Should().Be(PayoffDifferenceCause.Accrual);
    }

    [Fact]
    public void La_mora_es_su_propia_causa_y_se_suma_a_la_causacion()
    {
        var ahora = Proyectado with { InteresCorriente = 31_000m, Mora = 1_200m };

        var r = DiferenciaDeRecogida.Comparar(Proyectado, ahora);

        r.Causa.Should().Be(PayoffDifferenceCause.Accrual | PayoffDifferenceCause.Arrears);
        r.Total.Should().Be(2_200m);
    }

    [Fact]
    public void Un_anticipo_nuevo_es_un_pago_aunque_el_capital_no_cambie()
    {
        var ahora = Proyectado with { Anticipo = 50_000m };

        var r = DiferenciaDeRecogida.Comparar(Proyectado, ahora);

        r.Total.Should().Be(-50_000m);
        r.Causa.Should().Be(PayoffDifferenceCause.Payments);
    }

    [Fact]
    public void Otros_cargos_o_un_capital_mayor_son_otra_causa()
    {
        DiferenciaDeRecogida.Comparar(Proyectado, Proyectado with { OtrosCargos = 15_000m }).Causa.Should().Be(PayoffDifferenceCause.Other);

        var conCapitalMayor = DiferenciaDeRecogida.Comparar(Proyectado, Proyectado with { Capital = 2_100_000m, InteresCorriente = 31_000m });
        conCapitalMayor.Causa.Should().Be(PayoffDifferenceCause.Other, "un capital mayor (un pago reversado) no lo explica ninguna regla");
    }

    [Fact]
    public void Un_pago_con_mora_y_otros_cargos_combina_las_causas()
    {
        var ahora = Proyectado with { Capital = 1_900_000m, Mora = 800m, OtrosCargos = 3_000m };

        DiferenciaDeRecogida.Comparar(Proyectado, ahora).Causa.Should()
            .Be(PayoffDifferenceCause.Payments | PayoffDifferenceCause.Arrears | PayoffDifferenceCause.Other);
    }

    [Fact]
    public void Un_cambio_que_se_compensa_en_el_total_igual_es_diferencia()
    {
        var ahora = Proyectado with { Seguro = 6_000m, Administracion = 1_000m };

        var r = DiferenciaDeRecogida.Comparar(Proyectado, ahora);

        r.Total.Should().Be(0m);
        r.HayDiferencia.Should().BeTrue();
        r.Causa.Should().Be(PayoffDifferenceCause.Accrual);
    }

    [Fact]
    public void El_producto_cerrado_no_se_recoge_y_su_diferencia_es_todo_lo_proyectado()
    {
        var r = DiferenciaDeRecogida.Comparar(Proyectado, Proyectado, productoCerrado: true);

        r.Causa.Should().Be(PayoffDifferenceCause.ProductClosed);
        r.Total.Should().Be(-Proyectado.Total);
        r.PorConcepto.Capital.Should().Be(-2_000_000m);
        r.HayDiferencia.Should().BeTrue();
    }

    [Fact]
    public void El_producto_cerrado_sin_proyeccion_igual_se_reconoce()
    {
        var r = DiferenciaDeRecogida.Comparar(DesgloseDeRecogida.Vacio, DesgloseDeRecogida.Vacio, productoCerrado: true);

        r.Total.Should().Be(0m);
        r.HayDiferencia.Should().BeTrue("la omisión entra en la huella y se reconoce (FR-015)");
    }

    [Fact]
    public void El_total_del_desglose_resta_lo_que_se_descuenta()
    {
        var d = new DesgloseDeRecogida(Capital: 1_000m, InteresCorriente: 100m, InteresDevuelto: 30m, InteresCobradoPorProporcionales: 20m, Mora: 10m,
            Seguro: 5m, Administracion: 4m, OtrosCargos: 3m, Anticipo: 50m, SaldoAFavor: 2m);

        d.Total.Should().Be(1_000m + 100m - 30m + 20m + 10m + 5m + 4m + 3m - 50m - 2m);
    }

    [Fact]
    public void El_desglose_se_guarda_y_se_lee_igual()
    {
        var d = Proyectado with { InteresDevuelto = 1_234.56m, Anticipo = 7m };

        var json = d.Serializar();

        json.Should().Contain("\"capital\":2000000").And.NotContain("total", "el total sale de los conceptos");
        DesgloseDeRecogida.Leer(json).Should().Be(d);
        DesgloseDeRecogida.Leer("{}").Should().Be(DesgloseDeRecogida.Vacio);
        DesgloseDeRecogida.Leer(null).Should().Be(DesgloseDeRecogida.Vacio);
        DesgloseDeRecogida.Vacio.TieneAlgo.Should().BeFalse();
        d.TieneAlgo.Should().BeTrue();
    }
}
