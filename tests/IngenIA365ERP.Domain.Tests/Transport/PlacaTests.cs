using FluentAssertions;
using IngenIA365ERP.Domain.Transport;

namespace IngenIA365ERP.Domain.Tests.Transport;

/// <summary>Feature 020 (T059; R3): la placa normalizada es la que se guarda y la que mide la unicidad entre vivos.</summary>
public class PlacaTests
{
    [Theory]
    [InlineData("abc-123", "ABC123")]
    [InlineData(" ABC 123 ", "ABC123")]
    [InlineData("abc.12d", "ABC12D")]
    [InlineData("ABC123", "ABC123")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normaliza_mayusculas_sin_espacios_ni_separadores(string? entrada, string esperada) =>
        Placa.Normalizar(entrada).Should().Be(esperada);

    [Theory]
    [InlineData("ABC123", true)]
    [InlineData("abc-12d", true)]
    [InlineData("AB12", false)]
    [InlineData("ABCDEFGHIJK", false)]
    [InlineData("ÑBC123", false)]
    [InlineData("ABC#12", false)]
    public void Valida_de_cinco_a_diez_letras_y_numeros(string placa, bool valida) =>
        Placa.EsValida(placa).Should().Be(valida);
}
