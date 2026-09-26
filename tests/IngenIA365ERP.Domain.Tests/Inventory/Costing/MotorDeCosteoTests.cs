using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Tests.Inventory.Costing;

/// <summary>
/// Feature 012, T280–T282: lo que los casos dorados no cubren de <see cref="MotorDeCosteo"/>, <see cref="Redondeo"/> y
/// <see cref="Retroactivo"/> —PEPS todavía no, el reparto del residuo por bodega, el rechazo que nombra el movimiento
/// donde el retroactivo deja el ámbito en negativo y los valores admitidos de los parámetros de redondeo—.
/// </summary>
public class MotorDeCosteoTests
{
    private static readonly ParametrosDeCosteo Defecto = new();

    [Fact]
    public void Peps_llega_en_I5()
    {
        var acto = () => MotorDeCosteo.Aplicar(EstadoDeCosto.Vacio,
            new MovimientoDeCosto(1m, ValoracionDelMovimiento.AlCostoIndicado, 100m), Defecto with { Metodo = CostMethod.Fifo });
        acto.Should().Throw<NotSupportedException>().WithMessage("*I5*");
    }

    [Fact]
    public void Un_movimiento_sin_cantidad_no_se_valora()
    {
        var acto = () => MotorDeCosteo.Aplicar(EstadoDeCosto.Vacio, new MovimientoDeCosto(0m, ValoracionDelMovimiento.AlCostoVigente), Defecto);
        acto.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Con_el_negativo_prohibido_la_salida_que_no_alcanza_se_rechaza_con_lo_disponible()
    {
        var estado = EstadoDeCosto.Con(3m, 3000m, 1000m);
        var r = MotorDeCosteo.Aplicar(estado, new MovimientoDeCosto(-4m, ValoracionDelMovimiento.AlCostoVigente), Defecto);

        r.Admitido.Should().BeFalse();
        r.Rechazo!.Codigo.Should().Be("Inventory.Stock.Insufficient");
        r.Rechazo.Disponible.Should().Be(3m);
        r.Rechazo.Pedido.Should().Be(4m);
        r.Lineas.Should().BeEmpty();
        r.Estado.Should().Be(estado);
    }

    [Fact]
    public void Al_peso_los_montos_se_redondean_lejos_del_cero()
    {
        var estado = EstadoDeCosto.Con(3m, 1000m, 400m); // promedio 333,333333
        var r = MotorDeCosteo.Aplicar(estado, new MovimientoDeCosto(-1.5m, ValoracionDelMovimiento.AlCostoVigente), Defecto with { Montos = RedondeoDeMontos.Peso });

        r.Lineas.Single().TotalCost.Should().Be(-500m, "1,5 × 333,333333 = 499,9999995 → 500 al peso");
        r.Estado.Value.Should().Be(500m);
    }

    [Theory]
    [InlineData(ResiduoDeRedondeo.MayorValor, new[] { 10666.66, 10666.67 })]
    [InlineData(ResiduoDeRedondeo.UltimaLinea, new[] { 10666.67, 10666.66 })]
    public void El_valor_por_bodega_suma_el_valor_del_ambito_y_el_residuo_queda_visible(ResiduoDeRedondeo residuo, double[] esperado)
    {
        // 20.000 por 21.333,33 con promedio 1,066667: 10.000 × 1,066667 = 10.666,67 en cada bodega suma 21.333,34.
        var valores = Redondeo.ValorPorBodega(21333.33m, 1.066667m, [10000m, 10000m], RedondeoDeMontos.Centavo, residuo);

        valores.Should().Equal(esperado.Select(e => (decimal)e));
        valores.Sum().Should().Be(21333.33m);
    }

    [Fact]
    public void El_residuo_va_a_la_parte_de_mayor_valor()
    {
        // 24,9975 → 25,00; 50,005 → 50,01; 24,9975 → 25,00: suman 100,01 y el centavo de más sale de la mayor.
        var partes = Redondeo.Repartir(100m, [24.9975m, 50.005m, 24.9975m], RedondeoDeMontos.Centavo, ResiduoDeRedondeo.MayorValor);
        partes.Should().Equal(25.00m, 50.00m, 25.00m);
        partes.Sum().Should().Be(100m);
    }

    [Theory]
    [InlineData("Centavo", RedondeoDeMontos.Centavo)]
    [InlineData("peso", RedondeoDeMontos.Peso)]
    public void Redondeo_Montos_admite_sus_dos_valores(string valor, RedondeoDeMontos esperado) =>
        Redondeo.MontosDesde(valor).Should().Be(esperado);

    [Fact]
    public void Un_valor_de_redondeo_no_admitido_es_un_error_visible()
    {
        var acto = () => Redondeo.ResiduoDesde("Primera");
        acto.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void El_retroactivo_que_deja_un_saldo_intermedio_negativo_se_rechaza_nombrando_el_movimiento()
    {
        // 01/09 saldo de A 10 × 1.000; 03/09 saldo de B 5 × 1.300; 05/09 venta de 12 → quedan 3.
        // Anular el saldo de B en su fecha deja 10 el 03/09 y −2 después de la venta del 05/09.
        var saldoB = new MovimientoDeCosto(5m, ValoracionDelMovimiento.AlCostoIndicado, 1300m);
        var historia = new List<MovimientoRegistrado>
        {
            new(1, 1, new DateOnly(2026, 9, 1), new MovimientoDeCosto(10m, ValoracionDelMovimiento.AlCostoIndicado, 1000m), 10000m, 1000m, false),
            new(2, 2, new DateOnly(2026, 9, 3), saldoB, 6500m, 1300m, false),
            new(3, 3, new DateOnly(2026, 9, 5), new MovimientoDeCosto(-12m, ValoracionDelMovimiento.AlCostoVigente), -13200m, 1100m, true),
        };
        var anulacion = new MovimientoRetroactivo(new DateOnly(2026, 9, 3), 4,
            new MovimientoDeCosto(-5m, ValoracionDelMovimiento.DevolucionDeEntrada, 1300m, ReferenciaDeKardex.A(2), EsAnulacion: true));

        var r = Retroactivo.Insertar(new PedidoRetroactivo(EstadoDeCosto.Vacio, historia, [anulacion], Defecto));

        r.Admitido.Should().BeFalse();
        r.Rechazo!.Codigo.Should().Be("Inventory.Stock.Insufficient");
        r.Rechazo.MovimientoEntryId.Should().Be(3);
        r.Rechazo.Mensaje.Should().Contain("movimiento 3");
        r.Ajustes.Should().BeEmpty();
    }

    [Fact]
    public void Lo_registrado_antes_de_la_fecha_del_retroactivo_no_se_recalcula()
    {
        var historia = new List<MovimientoRegistrado>
        {
            new(1, 1, new DateOnly(2026, 9, 1), new MovimientoDeCosto(10m, ValoracionDelMovimiento.AlCostoIndicado, 1000m), 10000m, 1000m, false),
            new(2, 2, new DateOnly(2026, 9, 2), new MovimientoDeCosto(-4m, ValoracionDelMovimiento.AlCostoVigente), -4000m, 1000m, true),
        };
        var nuevo = new MovimientoRetroactivo(new DateOnly(2026, 9, 3), 3, new MovimientoDeCosto(6m, ValoracionDelMovimiento.AlCostoIndicado, 1300m));

        var r = Retroactivo.Insertar(new PedidoRetroactivo(EstadoDeCosto.Vacio, historia, [nuevo], Defecto));

        r.Admitido.Should().BeTrue();
        r.Ajustes.Should().BeEmpty("la venta del 02/09 es anterior al saldo del 03/09");
        r.EstadoFinal.Should().BeEquivalentTo(EstadoDeCosto.Con(12m, 13800m, 1300m));
    }
}
