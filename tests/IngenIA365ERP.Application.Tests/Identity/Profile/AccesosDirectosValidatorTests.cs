using FluentAssertions;
using IngenIA365ERP.Application.Identity.Profile.Preferencias;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

/// <summary>2026-11-10: los accesos directos del tablero (ui.accesos) son JSON del usuario; ícono y color terminan como clases del HTML.</summary>
public class AccesosDirectosValidatorTests
{
    private readonly SaveMyPreferencesCommandValidator _validador = new();

    private bool Valido(string json) =>
        _validador.Validate(new SaveMyPreferencesCommand(new Dictionary<string, string?> { ["ui.accesos"] = json })).IsValid;

    [Fact]
    public void Acepta_accesos_con_y_sin_personalizacion() =>
        Valido("""[{"ruta":"/recaudo/caja"},{"ruta":"/cartera/creditos","nombre":"Créditos","icono":"e-money","color":"exito"}]""")
            .Should().BeTrue();

    [Fact]
    public void Vaciar_es_valido() => Valido("").Should().BeTrue();

    [Theory]
    [InlineData("""[{"ruta":"https://otro.sitio"}]""")]
    [InlineData("""[{"ruta":"/a","icono":"e-x\" onclick=\"y"}]""")]
    [InlineData("""[{"ruta":"/a","color":"#ff0000"}]""")]
    [InlineData("""[{"ruta":"/a"},{"ruta":"/A"}]""")]
    [InlineData("""[{"ruta":"/a","nombre":"   "}]""")]
    [InlineData("""no es json""")]
    public void Rechaza_lo_que_no_es_admitido(string json) => Valido(json).Should().BeFalse();

    [Fact]
    public void No_admite_mas_de_diez()
    {
        var once = "[" + string.Join(",", Enumerable.Range(1, 11).Select(i => $$"""{"ruta":"/o{{i}}"}""")) + "]";
        Valido(once).Should().BeFalse();
    }
}
