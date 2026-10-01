using FluentAssertions;
using IngenIA365ERP.Domain.Common.Text;

namespace IngenIA365ERP.Domain.Tests.Common.Text;

/// <summary>
/// Feature 012, T102 (T43, FR-020): la normalización de la búsqueda de productos. El mismo normalizador escribe
/// <c>SearchText</c> y arma la consulta, así lo que se busca y lo guardado se comparan en el mismo alfabeto.
/// </summary>
public class NormalizadorDeBusquedaTests
{
    [Theory]
    [InlineData("café", "CAFE")]
    [InlineData("Azúcar  Morena", "AZUCAR MORENA")]
    [InlineData("pingüino", "PINGUINO")]
    [InlineData("ÁÉÍÓÚ àèìòù âêîôû", "AEIOU AEIOU AEIOU")]
    [InlineData("  arroz\t\tdiana \n 500g ", "ARROZ DIANA 500G")]
    [InlineData("piña", "PIÑA")]
    [InlineData("AÑO", "AÑO")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normaliza_a_mayusculas_sin_tildes_conservando_la_enie(string? entrada, string esperado) =>
        NormalizadorDeBusqueda.Normalizar(entrada).Should().Be(esperado);

    [Fact]
    public void La_enie_no_se_confunde_con_la_ene()
    {
        NormalizadorDeBusqueda.Normalizar("pina").Should().NotBe(NormalizadorDeBusqueda.Normalizar("piña"));
        NormalizadorDeBusqueda.Normalizar("PIÑA").Should().Be(NormalizadorDeBusqueda.Normalizar("piña"));
    }

    [Fact]
    public void Parte_en_terminos_sin_repetir_en_el_orden_escrito()
    {
        NormalizadorDeBusqueda.Terminos("  leche  entera leche 1L ").Should().Equal("LECHE", "ENTERA", "1L");
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" é ")]
    [InlineData("")]
    [InlineData(null)]
    public void Menos_de_dos_caracteres_no_busca(string? consulta)
    {
        NormalizadorDeBusqueda.MinimoDeCaracteres.Should().Be(2);
        NormalizadorDeBusqueda.Terminos(consulta).Should().BeEmpty();
        NormalizadorDeBusqueda.PatronesContiene(consulta).Should().BeEmpty();
    }

    [Fact]
    public void Dos_caracteres_ya_buscan() => NormalizadorDeBusqueda.Terminos("ab").Should().Equal("AB");

    [Theory]
    [InlineData("50%", @"50\%")]
    [InlineData("A_B", @"A\_B")]
    [InlineData("[X]", @"\[X]")]
    [InlineData(@"C\D", @"C\\D")]
    [InlineData("NORMAL", "NORMAL")]
    public void Escapa_los_comodines_de_LIKE(string termino, string esperado)
    {
        NormalizadorDeBusqueda.CaracterDeEscape.Should().Be('\\');
        NormalizadorDeBusqueda.EscaparParaLike(termino).Should().Be(esperado);
    }

    [Fact]
    public void Un_patron_contiene_por_cada_termino()
    {
        NormalizadorDeBusqueda.PatronesContiene("jabón 10%").Should().Equal("%JABON%", @"%10\%%");
    }
}
