using FluentAssertions;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T640 (FR-059, contracts/api.md §20.2): lo que entra por el campo de lectura del POS. «3*» antes del código multiplica; el
/// multiplicador solo queda esperando la lectura siguiente; lo demás es un código exacto que decide el servidor. (nuevo)
/// </summary>
public class LecturaDelPosTests
{
    [Fact]
    public void Un_codigo_solo_es_una_lectura_de_una_unidad()
    {
        var l = LecturaDelPos.Interpretar("  7702001000012 ");

        l.Codigo.Should().Be("7702001000012");
        l.Cantidad.Should().BeNull();
        l.Error.Should().BeNull();
    }

    [Theory]
    [InlineData("3*7702001", 3)]
    [InlineData("2,5*ARZ-1", 2.5)]
    [InlineData("0.75 * ARZ-1", 0.75)]
    public void El_multiplicador_antes_del_codigo_es_la_cantidad(string texto, double cantidad)
    {
        var l = LecturaDelPos.Interpretar(texto);

        l.Codigo.Should().NotBeNullOrWhiteSpace();
        l.Cantidad.Should().Be((decimal)cantidad);
    }

    [Fact]
    public void El_multiplicador_solo_espera_la_lectura_siguiente()
    {
        var l = LecturaDelPos.Interpretar("4*");

        l.Codigo.Should().BeNull();
        l.Cantidad.Should().Be(4m);
        l.EsSoloMultiplicador.Should().BeTrue();
    }

    [Theory]
    [InlineData("0*ARZ")]
    [InlineData("-2*ARZ")]
    public void Un_multiplicador_que_no_es_positivo_es_un_error(string texto)
    {
        var l = LecturaDelPos.Interpretar(texto);

        l.Error.Should().NotBeNullOrWhiteSpace();
        l.Codigo.Should().BeNull();
    }

    [Fact]
    public void Lo_que_no_empieza_por_un_numero_es_un_codigo_con_asterisco()
    {
        var l = LecturaDelPos.Interpretar("AB*12");

        l.Codigo.Should().Be("AB*12");
        l.Cantidad.Should().BeNull();
    }

    [Fact]
    public void Vacio_no_es_nada()
    {
        var l = LecturaDelPos.Interpretar("   ");

        l.Codigo.Should().BeNull();
        l.Cantidad.Should().BeNull();
        l.Error.Should().BeNull();
        l.EsSoloMultiplicador.Should().BeFalse();
    }

    [Fact]
    public void El_multiplicador_pendiente_se_aplica_a_la_lectura_que_no_trae_el_suyo()
    {
        var pendiente = LecturaDelPos.Interpretar("6*").Cantidad;

        var l = LecturaDelPos.Interpretar("7702001").ConMultiplicador(pendiente);

        l.Cantidad.Should().Be(6m);
        LecturaDelPos.Interpretar("2*7702001").ConMultiplicador(pendiente).Cantidad.Should().Be(2m, "el propio de la lectura manda");
    }
}
