using FluentAssertions;
using IngenIA365ERP.Domain.Inventory.Units;

namespace IngenIA365ERP.Domain.Tests.Inventory.Units;

/// <summary>
/// Feature 012, T187 (FR-017, FR-025; quickstart §3.3.3): la conversión de una cantidad en una unidad de empaque a la
/// unidad base, en decimal exacto. El kardex va siempre en la base; lo que la conversión no da exacto queda en la misma
/// línea como cantidad de redondeo visible, y lo que la base no admite se rechaza nombrando la línea, la unidad, los
/// decimales que admite y la cantidad que resultaría.
/// </summary>
public class ConversionDeUnidadesTests
{
    private static PedidoDeConversion Pedido(decimal cantidad, decimal factor, int decimalesDeLaUnidad = 0, int decimalesDeLaBase = 0,
        int linea = 1, string unidad = "CAJA12", string baseCodigo = "UND") =>
        new(linea, unidad, cantidad, factor, decimalesDeLaUnidad, baseCodigo, decimalesDeLaBase);

    [Fact]
    public void Tres_cajas_de_doce_son_36_en_la_unidad_base()
    {
        var r = ConversionDeUnidades.Convertir(Pedido(3m, 12m));

        r.Admitida.Should().BeTrue();
        r.QuantityBase.Should().Be(36m);
        r.RoundingQuantity.Should().Be(0m);
    }

    [Fact]
    public void Un_factor_con_seis_decimales_se_multiplica_en_decimal_exacto()
    {
        // 2 bolsas de 0,453592 kg con la base en kilos de 4 decimales.
        var r = ConversionDeUnidades.Convertir(Pedido(2m, 0.453592m, 0, 4, unidad: "LB", baseCodigo: "KG"));

        r.Admitida.Should().BeTrue();
        r.QuantityBase.Should().Be(0.9072m);
        r.RoundingQuantity.Should().Be(0.907184m - 0.9072m, "la diferencia queda visible en la misma línea");
    }

    [Fact]
    public void Una_conversion_que_no_da_exacta_deja_la_diferencia_en_la_linea()
    {
        // Tres tercios de unidad: 3 × 0,333333 = 0,999999 → 1 unidad, redondeo −0,000001.
        var r = ConversionDeUnidades.Convertir(Pedido(3m, 0.333333m, 0, 0, unidad: "TERCIO"));

        r.Admitida.Should().BeTrue();
        r.QuantityBase.Should().Be(1m);
        r.RoundingQuantity.Should().Be(-0.000001m);
        (r.QuantityBase + r.RoundingQuantity).Should().Be(3m * 0.333333m, "cantidad × factor = base + redondeo, sin perder nada");
    }

    [Fact]
    public void La_base_sin_decimales_no_recibe_uno_y_medio()
    {
        var r = ConversionDeUnidades.Convertir(Pedido(1.5m, 1m, 0, 0, linea: 4, unidad: "UND"));

        r.Admitida.Should().BeFalse();
        r.Rechazo.Should().NotBeNull();
        r.Rechazo!.LineNumber.Should().Be(4);
        r.Rechazo.UnitCode.Should().Be("UND");
        r.Rechazo.AllowedDecimals.Should().Be(0);
        r.Rechazo.QuantityBase.Should().Be(1.5m);
    }

    [Fact]
    public void Un_empaque_que_daria_fraccion_de_una_base_entera_se_rechaza_nombrando_la_base()
    {
        // Media docena de un producto por docena: 1 MEDIA (factor 0,5) no cabe en una base sin decimales.
        var r = ConversionDeUnidades.Convertir(Pedido(1m, 0.5m, 0, 0, linea: 2, unidad: "MEDIA"));

        r.Admitida.Should().BeFalse();
        r.Rechazo!.UnitCode.Should().Be("UND", "lo que no admite los decimales es la unidad base");
        r.Rechazo.AllowedDecimals.Should().Be(0);
        r.Rechazo.QuantityBase.Should().Be(0.5m);
    }

    [Fact]
    public void Una_cantidad_con_mas_decimales_de_los_que_admite_su_unidad_se_rechaza()
    {
        var r = ConversionDeUnidades.Convertir(Pedido(1.25m, 12m, 1, 0));

        r.Admitida.Should().BeFalse();
        r.Rechazo!.UnitCode.Should().Be("CAJA12");
        r.Rechazo.AllowedDecimals.Should().Be(1);
    }

    [Fact]
    public void La_base_con_decimales_admite_la_fraccion()
    {
        var r = ConversionDeUnidades.Convertir(Pedido(1.5m, 1m, 3, 3, unidad: "KG", baseCodigo: "KG"));

        r.Admitida.Should().BeTrue();
        r.QuantityBase.Should().Be(1.5m);
    }

    [Fact]
    public void Una_cantidad_que_se_vuelve_cero_en_la_base_se_rechaza()
    {
        var r = ConversionDeUnidades.Convertir(Pedido(1m, 0.00001m, 0, 4, unidad: "MG", baseCodigo: "KG"));

        r.Admitida.Should().BeFalse();
        r.Rechazo!.QuantityBase.Should().Be(0m);
    }
}
