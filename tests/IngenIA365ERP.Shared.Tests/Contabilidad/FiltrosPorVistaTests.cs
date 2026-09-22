using FluentAssertions;
using IngenIA365ERP.Shared.Services.Contabilidad;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Contabilidad;

/// <summary>
/// Feature 009 E2 (US5): al cambiar de informe, lo que la vista nueva no muestra deja de viajar. Hasta
/// el 2026-09-20 «nivel» y «con terceros» marcados en el balance seguían en la query del libro diario
/// sin ningún control en pantalla que los mostrara.
/// </summary>
public class FiltrosPorVistaTests
{
    [Theory]
    [InlineData(ContabilidadClient.Vistas.BalanceDePrueba, true, true)]
    [InlineData(ContabilidadClient.Vistas.LibroMayor, true, false)]
    [InlineData(ContabilidadClient.Vistas.LibroDiario, false, false)]
    [InlineData(ContabilidadClient.Vistas.RelacionDeComprobantes, false, false)]
    [InlineData(ContabilidadClient.Vistas.DocumentosPendientes, false, false)]
    [InlineData(ContabilidadClient.Vistas.SaldoDiarioPromedio, false, false)]
    public void El_nivel_es_del_balance_y_del_mayor_y_con_terceros_solo_del_balance(string vista, bool nivel, bool terceros)
    {
        FiltrosPorVista.MuestraNivel(vista).Should().Be(nivel);
        FiltrosPorVista.MuestraConTerceros(vista).Should().Be(terceros);
    }

    [Fact]
    public void Cambiar_a_una_vista_sin_nivel_ni_terceros_los_apaga_y_conserva_el_resto()
    {
        var f = new FiltrosDeInformeModelo { Level = 4, WithThirdParties = true, IncludeClosing = true, VoucherType = "CG", From = new DateOnly(2026, 1, 1) };

        FiltrosPorVista.AjustarAlCambiarVista(f, ContabilidadClient.Vistas.LibroDiario);

        f.Level.Should().BeNull();
        f.WithThirdParties.Should().BeFalse();
        f.IncludeClosing.Should().BeTrue("la casilla se muestra en todas las vistas: sigue a la vista de quien la marcó");
        f.VoucherType.Should().Be("CG");
        f.From.Should().Be(new DateOnly(2026, 1, 1));
        f.ToQuery().Should().NotContain("level=").And.NotContain("withThirdParties=");
    }

    [Fact]
    public void Cambiar_al_mayor_conserva_el_nivel_y_apaga_con_terceros()
    {
        var f = new FiltrosDeInformeModelo { Level = 6, WithThirdParties = true };

        FiltrosPorVista.AjustarAlCambiarVista(f, ContabilidadClient.Vistas.LibroMayor);

        f.Level.Should().Be(6);
        f.WithThirdParties.Should().BeFalse();
    }

    [Fact]
    public void Cambiar_al_balance_no_toca_nada()
    {
        var f = new FiltrosDeInformeModelo { Level = 2, WithThirdParties = true };

        FiltrosPorVista.AjustarAlCambiarVista(f, ContabilidadClient.Vistas.BalanceDePrueba);

        f.Level.Should().Be(2);
        f.WithThirdParties.Should().BeTrue();
    }
}
