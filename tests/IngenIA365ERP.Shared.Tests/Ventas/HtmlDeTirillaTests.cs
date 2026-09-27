using FluentAssertions;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T639 (contracts/api.md §20.2 <c>TicketDto</c>): la tirilla impresa. Se imprime al ancho del formato; la copia lleva «COPIA»; el
/// texto que viene de la base se codifica; de la tarjeta sólo salen los últimos cuatro dígitos; el pie de pruebas se imprime. (nuevo)
/// </summary>
public class HtmlDeTirillaTests
{
    private static TirillaDto Tirilla(int formato = 80, bool copia = false, string cliente = "Consumidor final") => new(
        formato, copia,
        new EncabezadoDeTirillaDto("Cooperativa Florida", "900123456", "Sede centro", "Calle 1", null, "Responsable de IVA"),
        new DocumentoDeTirillaDto("Documento equivalente POS", "POS", 15, new DateTime(2026, 9, 27, 15, 0, 0, DateTimeKind.Utc), "C1", "Cajero Uno", null),
        new TerceroDeTirillaDto(cliente, "CC", "222222222222"),
        [new LineaDeTirillaDto("ARZ-1", "Arroz 500 g", 3m, "UND", 4500m, 0m, 13500m, "G")],
        [new ImpuestoDeTirillaDto("IVA 19 %", 11345m, 2155m)],
        [],
        new TotalesDeTirillaDto(11345m, 0m, 2155m, 13500m, 13500m),
        [new PagoDeTirillaDto("Efectivo", 10000m, null, null), new PagoDeTirillaDto("Visa", 3500m, "123456", "4242")],
        0m, null, ["SIN VALIDEZ FISCAL"]);

    [Theory]
    [InlineData(58, "size:58mm")]
    [InlineData(80, "size:80mm")]
    [InlineData(216, "size:190mm")]
    public void Se_imprime_al_ancho_del_formato(int formato, string esperado) =>
        HtmlDeTirilla.Documento(Tirilla(formato)).Should().Contain(esperado);

    [Fact]
    public void La_copia_lo_dice_y_la_primera_entrega_no()
    {
        HtmlDeTirilla.Cuerpo(Tirilla(copia: true)).Should().Contain("COPIA");
        HtmlDeTirilla.Cuerpo(Tirilla(copia: false)).Should().NotContain("COPIA");
    }

    [Fact]
    public void El_texto_se_codifica()
    {
        var html = HtmlDeTirilla.Cuerpo(Tirilla(cliente: "<script>alert(1)</script>"));

        html.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
    }

    [Fact]
    public void De_la_tarjeta_solo_salen_los_ultimos_cuatro_y_el_pie_se_imprime()
    {
        var html = HtmlDeTirilla.Cuerpo(Tirilla());

        html.Should().Contain("****4242").And.Contain("SIN VALIDEZ FISCAL").And.Contain("POS15");
    }
}
