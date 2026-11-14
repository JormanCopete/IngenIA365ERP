using FluentAssertions;
using IngenIA365ERP.Shared.Services.Compras;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// 2026-11-13: lo que las pantallas de compras traen de un documento origen. La factura y el documento soporte con lo pendiente de una
/// recepción llevan su parte del descuento de la recepción; la recepción contra una orden deja que el servidor derive la parte del descuento
/// de la orden y sólo manda lo que la persona escribió.
/// </summary>
public class DescuentosDeCompraTests
{
    [Fact]
    public void Lo_pendiente_lleva_su_parte_del_descuento_de_la_recepcion()
    {
        // Recepción de 36 unidades con 3.600 de descuento; faltan por facturar 12.
        DescuentosDeCompra.Proporcional(3_600m, 12m, 36m).Should().Be(1_200m);
        DescuentosDeCompra.Proporcional(1_000m, 1m, 3m).Should().Be(333.33m, "al centavo");
        DescuentosDeCompra.Proporcional(1_000m, 3m, 3m).Should().Be(1_000m, "todo lo recibido lleva el descuento entero, sin residuo");
    }

    [Fact]
    public void Sin_descuento_o_sin_cantidad_no_hay_descuento()
    {
        DescuentosDeCompra.Proporcional(null, 5m, 10m).Should().Be(0m);
        DescuentosDeCompra.Proporcional(0m, 5m, 10m).Should().Be(0m);
        DescuentosDeCompra.Proporcional(500m, 0m, 10m).Should().Be(0m);
        DescuentosDeCompra.Proporcional(500m, 5m, 0m).Should().Be(0m);
    }

    [Fact]
    public void La_tasa_de_la_orden_es_su_descuento_sobre_su_bruto_y_estima_lo_que_dara_el_servidor()
    {
        // La línea de la orden: 100 a 1.000, 10.000 de descuento, neto 90.000.
        var tasa = DescuentosDeCompra.TasaDeLaOrden(10_000m, 90_000m);

        tasa.Should().Be(0.1m);
        DescuentosDeCompra.EstimadoConLaOrden(60m, 1_000m, tasa).Should().Be(6_000m, "se reciben 60 de 100");
        DescuentosDeCompra.TasaDeLaOrden(null, 90_000m).Should().Be(0m);
    }

    [Fact]
    public void El_descuento_guardado_es_el_de_la_orden_solo_si_coincide_con_su_tasa()
    {
        DescuentosDeCompra.EsElDeLaOrden(6_000m, 60_000m, 0.1m).Should().BeTrue("lo derivó el servidor");
        DescuentosDeCompra.EsElDeLaOrden(6_000.4m, 60_000m, 0.1m).Should().BeTrue("con el redondeo al peso de la cooperativa");
        DescuentosDeCompra.EsElDeLaOrden(1_000m, 60_000m, 0.1m).Should().BeFalse("lo escribió la persona: se manda tal cual");
        DescuentosDeCompra.EsElDeLaOrden(0m, 60_000m, 0.1m).Should().BeFalse("un cero escrito quita el de la orden");
    }
}
