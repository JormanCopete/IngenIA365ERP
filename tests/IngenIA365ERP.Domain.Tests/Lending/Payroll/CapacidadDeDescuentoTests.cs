using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Payroll;

namespace IngenIA365ERP.Domain.Tests.Lending.Payroll;

/// <summary>
/// Feature 013, K2, T0805 (FR-110, D13; Ley 1527 de 2012): <see cref="CapacidadDeDescuento"/>. El tope efectivo es el más estricto entre el
/// límite legal (<c>LIBRANZA_CAPACIDAD_DESCUENTO</c>), el parámetro de la cooperativa (<c>Libranza.CapacidadDeDescuento</c>) y el tope del
/// convenio (<c>DeductionCapFraction</c>); la base es el salario o la pensión después de los descuentos de ley certificados; los descuentos
/// vigentes más la cuota nueva caben en el tope × la base, y se devuelve lo disponible. Las cifras están hechas a mano (decimal en Python
/// para el redondeo del centavo). Las fracciones de las pruebas son de ejemplo: los valores reales llegan por parámetro.
/// </summary>
public class CapacidadDeDescuentoTests
{
    [Fact]
    public void El_tope_efectivo_es_el_mas_estricto_de_los_tres()
    {
        CapacidadDeDescuento.TopeEfectivo(new TopesDeDescuento(0.50m, 0.45m, 0.40m)).Should().Be((0.40m, OrigenDelTopeDeDescuento.Convenio));
        CapacidadDeDescuento.TopeEfectivo(new TopesDeDescuento(0.50m, 0.45m)).Should().Be((0.45m, OrigenDelTopeDeDescuento.ParametroDeLaCooperativa));
        CapacidadDeDescuento.TopeEfectivo(new TopesDeDescuento(0.50m)).Should().Be((0.50m, OrigenDelTopeDeDescuento.LimiteLegal));
        CapacidadDeDescuento.TopeEfectivo(new TopesDeDescuento(0.50m, Convenio: 0.55m)).Should().Be((0.50m, OrigenDelTopeDeDescuento.LimiteLegal),
            "un convenio más laxo nunca sube el tope legal");
        CapacidadDeDescuento.TopeEfectivo(new TopesDeDescuento(0.50m, 0.50m, 0.50m)).Origen.Should().Be(OrigenDelTopeDeDescuento.LimiteLegal,
            "a igual valor manda el legal");
    }

    [Fact]
    public void Los_descuentos_vigentes_mas_la_cuota_nueva_caben_en_el_tope_sobre_la_base_certificada()
    {
        var topes = new TopesDeDescuento(0.50m, 0.45m, 0.40m);

        var alcanza = CapacidadDeDescuento.Evaluar(2_500_000m, topes, descuentosVigentes: 650_000m, cuotaNueva: 300_000m);
        alcanza.Permitido.Should().Be(1_000_000m, "2.500.000 × 0,40 del convenio");
        alcanza.Disponible.Should().Be(350_000m);
        alcanza.Alcanza.Should().BeTrue("650.000 + 300.000 = 950.000 ≤ 1.000.000");

        var justo = CapacidadDeDescuento.Evaluar(2_500_000m, topes, 650_000m, 350_000m);
        justo.Alcanza.Should().BeTrue("igual al permitido cabe");

        var excede = CapacidadDeDescuento.Evaluar(2_500_000m, topes, 650_000m, 400_000m);
        excede.Alcanza.Should().BeFalse("1.050.000 > 1.000.000");
        excede.Disponible.Should().Be(350_000m, "lo que se muestra al rechazar (Lending.Payroll.DeductionCapacityExceeded, available)");
    }

    [Fact]
    public void Lo_permitido_se_redondea_al_centavo_y_lo_disponible_nunca_es_negativo()
    {
        CapacidadDeDescuento.Evaluar(1_873_456.78m, new TopesDeDescuento(0.50m), 0m, 0m).Permitido.Should().Be(936_728.39m);
        CapacidadDeDescuento.Evaluar(1_873_456.78m, new TopesDeDescuento(0.50m, 0.333333m), 0m, 0m).Permitido.Should().Be(624_484.97m,
            "1.873.456,78 × 0,333333 = 624.484,96884774 → al centavo");

        var sobregirado = CapacidadDeDescuento.Evaluar(1_000_000m, new TopesDeDescuento(0.50m), 600_000m, 1m);
        sobregirado.Disponible.Should().Be(0m);
        sobregirado.Alcanza.Should().BeFalse();
    }

    [Fact]
    public void Sin_base_certificada_no_alcanza()
    {
        CapacidadDeDescuento.Evaluar(0m, new TopesDeDescuento(0.50m), 0m, 0m).Alcanza.Should().BeFalse();
    }

    [Fact]
    public void Un_tope_que_no_es_fraccion_o_un_monto_negativo_se_rechazan()
    {
        ((Action)(() => CapacidadDeDescuento.Evaluar(1m, new TopesDeDescuento(0m), 0m, 0m))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => CapacidadDeDescuento.Evaluar(1m, new TopesDeDescuento(1.2m), 0m, 0m))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => CapacidadDeDescuento.Evaluar(1m, new TopesDeDescuento(0.5m, Convenio: -0.1m), 0m, 0m))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => CapacidadDeDescuento.Evaluar(-1m, new TopesDeDescuento(0.5m), 0m, 0m))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => CapacidadDeDescuento.Evaluar(1m, new TopesDeDescuento(0.5m), -1m, 0m))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void El_tope_de_un_convenio_no_supera_el_limite_legal()
    {
        CapacidadDeDescuento.RespetaElLimite(null, 0.50m).Should().BeTrue("sin tope propio rige el legal");
        CapacidadDeDescuento.RespetaElLimite(0.40m, 0.50m).Should().BeTrue();
        CapacidadDeDescuento.RespetaElLimite(0.50m, 0.50m).Should().BeTrue();
        CapacidadDeDescuento.RespetaElLimite(0.55m, 0.50m).Should().BeFalse("Lending.Parameter.BelowLegalFloor al guardar la versión");
        CapacidadDeDescuento.RespetaElLimite(0m, 0.50m).Should().BeFalse();
    }
}
