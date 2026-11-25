using FluentAssertions;
using IngenIA365ERP.Shared.Services;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Comunes;

/// <summary>
/// El aviso de sesión de caja de un día anterior (2026-11-24) mide «hoy» en Colombia: a las 8 p. m. de Colombia ya es mañana en UTC y
/// la sesión de hoy no debe verse vieja; pasada la medianoche de Colombia, la de ayer sí.
/// </summary>
public sealed class DiaDeCajaTests
{
    [Fact]
    public void A_las_ocho_de_la_noche_de_Colombia_sigue_siendo_hoy()
    {
        var instante = new DateTimeOffset(2026, 11, 25, 1, 0, 0, TimeSpan.Zero); // 24/11 20:00 en Colombia
        DiaDeCaja.En(instante).Should().Be(new DateOnly(2026, 11, 24));
    }

    [Fact]
    public void Pasada_la_medianoche_de_Colombia_es_el_dia_siguiente()
    {
        var instante = new DateTimeOffset(2026, 11, 25, 5, 30, 0, TimeSpan.Zero); // 25/11 00:30 en Colombia
        DiaDeCaja.En(instante).Should().Be(new DateOnly(2026, 11, 25));
    }

    [Theory]
    [InlineData("2026-11-23", "2026-11-24", true)]
    [InlineData("2026-11-24", "2026-11-24", false)]
    [InlineData("2026-11-25", "2026-11-24", false)]
    public void Solo_la_sesion_de_un_dia_anterior_se_avisa(string sesion, string hoy, bool avisa) =>
        DiaDeCaja.EsDeUnDiaAnterior(DateOnly.Parse(sesion), DateOnly.Parse(hoy)).Should().Be(avisa);
}
