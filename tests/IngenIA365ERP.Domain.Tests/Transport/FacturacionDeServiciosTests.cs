using FluentAssertions;
using IngenIA365ERP.Domain.Transport;

namespace IngenIA365ERP.Domain.Tests.Transport;

/// <summary>Feature 020 (T122; R9, R10; FR-034): el período, el prorrateo a mitad de mes y el reparto del valor a pagar entre las líneas.</summary>
public class FacturacionDeServiciosTests
{
    private static readonly PeriodoDeServicio Diciembre = new(2026, 12);

    [Fact]
    public void El_periodo_se_lee_y_escribe_como_aaaamm()
    {
        PeriodoDeServicio.TryParse("202612", out var p).Should().BeTrue();
        p.Inicio.Should().Be(new DateOnly(2026, 12, 1));
        p.Fin.Should().Be(new DateOnly(2026, 12, 31));
        p.Texto().Should().Be("diciembre 2026");
        PeriodoDeServicio.TryParse("202613", out _).Should().BeFalse();
    }

    [Fact]
    public void A_mes_completo_se_cobra_uno_aunque_empiece_a_mitad_de_mes() =>
        Prorrateo.Cantidad(Diciembre, new DateOnly(2026, 12, 16), null, prorratear: false).Should().Be(1m);

    [Fact]
    public void Proporcional_cobra_los_dias_cubiertos() =>
        Prorrateo.Cantidad(Diciembre, new DateOnly(2026, 12, 16), null, prorratear: true).Should().Be(Math.Round(16m / 31m, 4));

    [Fact]
    public void Una_suscripcion_que_no_se_cruza_con_el_mes_no_se_cobra() =>
        Prorrateo.Cantidad(Diciembre, new DateOnly(2027, 1, 1), null, prorratear: false).Should().Be(0m);

    [Fact]
    public void El_reparto_suma_exacto_y_deja_el_residuo_en_la_ultima_linea()
    {
        var partes = RepartoDePagosPorLinea.Repartir(100m, [1m, 1m, 1m]);

        partes.Sum().Should().Be(100m);
        partes[0].Should().Be(33.33m);
        partes[^1].Should().Be(33.34m);
    }

    [Fact]
    public void Sin_retenciones_el_reparto_devuelve_los_mismos_pesos() =>
        RepartoDePagosPorLinea.Repartir(70_000m, [50_000m, 20_000m]).Should().Equal(50_000m, 20_000m);
}
