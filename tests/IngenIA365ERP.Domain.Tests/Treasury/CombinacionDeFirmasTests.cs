using FluentAssertions;
using IngenIA365ERP.Domain.Treasury.Firmas;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, T2, fase 42 (FR-208; pedido del dueño para T2): la combinación de firmas de un lote se valida contra las aprobaciones. Un firmante cuyo tope para firmar solo
/// cubre el monto basta; si no, hacen falta dos firmantes distintos que firman en conjunto; quien aprobó sin ser firmante vigente no cuenta; una cuenta sin firmantes no exige
/// nada. Prueba escrita, sin correr.
/// </summary>
public class CombinacionDeFirmasTests
{
    private static readonly FirmanteVigente Ana = new(1, "A", 5_000_000m, true);
    private static readonly FirmanteVigente Beto = new(2, "B", null, true);
    private static readonly FirmanteVigente Carla = new(3, "B", null, true);
    private static readonly FirmanteVigente Dario = new(4, "C", null, false);

    [Fact]
    public void Sin_firmantes_registrados_no_exige_nada()
    {
        var v = CombinacionDeFirmas.Evaluar(10_000_000m, [], [9]);
        (v.ExigeFirmas, v.Cumple).Should().Be((false, true));
    }

    [Fact]
    public void Un_firmante_con_tope_suficiente_firma_solo()
    {
        var v = CombinacionDeFirmas.Evaluar(5_000_000m, [Ana, Beto], [1]);
        (v.ExigeFirmas, v.Cumple).Should().Be((true, true));
        v.Firmaron.Should().Equal(1);
    }

    [Fact]
    public void Sobre_el_tope_solo_hacen_falta_dos_en_conjunto()
    {
        CombinacionDeFirmas.Evaluar(8_000_000m, [Ana, Beto], [1]).Cumple.Should().BeFalse();
        CombinacionDeFirmas.Evaluar(8_000_000m, [Ana, Beto], [1, 2]).Cumple.Should().BeTrue();
        CombinacionDeFirmas.Evaluar(8_000_000m, [Beto, Carla], [2, 3]).Cumple.Should().BeTrue();
    }

    [Fact]
    public void Quien_no_firma_en_conjunto_no_completa_la_pareja()
    {
        var v = CombinacionDeFirmas.Evaluar(8_000_000m, [Beto, Dario], [2, 4]);
        v.Cumple.Should().BeFalse();
        v.Motivo.Should().Contain("dos que firmen en conjunto");
    }

    [Fact]
    public void Las_aprobaciones_de_quien_no_es_firmante_no_cuentan()
    {
        var v = CombinacionDeFirmas.Evaluar(1_000m, [Ana, Beto], [7, 8]);
        (v.ExigeFirmas, v.Cumple).Should().Be((true, false));
        v.Firmaron.Should().BeEmpty();
        v.Motivo.Should().Contain("Ninguno");
    }

    [Fact]
    public void El_mismo_firmante_dos_veces_no_hace_una_pareja()
    {
        CombinacionDeFirmas.Evaluar(8_000_000m, [Beto, Carla], [2, 2]).Cumple.Should().BeFalse();
    }
}
