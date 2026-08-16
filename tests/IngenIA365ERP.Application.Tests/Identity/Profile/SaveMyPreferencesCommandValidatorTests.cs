using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Identity.Profile.Preferencias;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

/// <summary>
/// El validador es la frontera de seguridad de esta funcionalidad: el valor
/// termina como atributo del elemento raíz del documento o como destino de una
/// navegación, y lo escribe el propio usuario.
/// </summary>
public class SaveMyPreferencesCommandValidatorTests
{
    private readonly SaveMyPreferencesCommandValidator _validador = new();

    private static SaveMyPreferencesCommand Cmd(string clave, string? valor) =>
        new(new Dictionary<string, string?> { [clave] = valor });

    [Theory]
    [InlineData("ui.tema", "claro")]
    [InlineData("ui.tema", "oscuro")]
    [InlineData("ui.densidad", "compacta")]
    [InlineData("ui.escala", "muy-grande")]
    [InlineData("ui.contraste", "alto")]
    public void ValoresDelCatalogo_SonAceptados(string clave, string valor)
    {
        _validador.Validate(Cmd(clave, valor)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ClaveDesconocida_EsRechazada()
    {
        // Sin esto la tabla sería un almacén libre de datos por usuario.
        _validador.Validate(Cmd("ui.loQueSea", "x")).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("ui.tema", "azul")]
    [InlineData("ui.escala", "gigante")]
    [InlineData("ui.contraste", "medio")]
    public void ValorFueraDelCatalogo_EsRechazado(string clave, string valor)
    {
        _validador.Validate(Cmd(clave, valor)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValorVacio_EsAceptado_PorqueEsLaFormaDeVolverAlDefecto()
    {
        _validador.Validate(Cmd("ui.tema", "")).IsValid.Should().BeTrue();
        _validador.Validate(Cmd("ui.tema", null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void PaginaInicio_RutaInterna_EsAceptada()
    {
        _validador.Validate(Cmd("ui.pagina-inicio", "/cartera/recaudos")).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://sitio-externo.example/phishing")]  // absoluta
    [InlineData("//sitio-externo.example/phishing")]        // relativa al protocolo
    [InlineData("javascript:alert(1)")]
    [InlineData("/../../etc/passwd")]
    [InlineData("cartera/recaudos")]                        // sin barra inicial
    public void PaginaInicio_DestinoNoInterno_EsRechazado(string valor)
    {
        // Si esto pasara, configurar la página de inicio dejaría al usuario
        // saliendo del ERP cada vez que entra.
        _validador.Validate(Cmd("ui.pagina-inicio", valor)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Favoritos_ListaDeRutasInternas_EsAceptada()
    {
        var json = JsonSerializer.Serialize(new[] { "/cartera/recaudos", "/contabilidad/saldos" });
        _validador.Validate(Cmd("ui.favoritos", json)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Favoritos_ConUnaRutaExterna_EsRechazada()
    {
        var json = JsonSerializer.Serialize(new[] { "/cartera/recaudos", "https://externo.example" });
        _validador.Validate(Cmd("ui.favoritos", json)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Favoritos_JsonInvalido_EsRechazado()
    {
        _validador.Validate(Cmd("ui.favoritos", "{no es json")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Favoritos_MasDeTreinta_EsRechazado()
    {
        var json = JsonSerializer.Serialize(Enumerable.Range(0, 31).Select(i => $"/r{i}"));
        _validador.Validate(Cmd("ui.favoritos", json)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CuerpoVacio_EsRechazado()
    {
        var comando = new SaveMyPreferencesCommand(new Dictionary<string, string?>());
        _validador.Validate(comando).IsValid.Should().BeFalse();
    }
}
