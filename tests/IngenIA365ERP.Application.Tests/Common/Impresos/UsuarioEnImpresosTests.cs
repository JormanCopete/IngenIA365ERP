using IngenIA365ERP.Application.Common.Impresos;

namespace IngenIA365ERP.Application.Tests.Common.Impresos;

/// <summary>
/// 2026-11-15 (pedido del dueño): en los documentos impresos el usuario ya no sale con su correo; la cooperativa elige nombre, nombre
/// completo, iniciales, cargo o correo (<c>Core.PrintedUserDisplay</c>).
/// </summary>
public class UsuarioEnImpresosTests
{
    private static readonly UsuarioEnImpresos.Datos Ana = new("Ana", "María", "Pérez", "Gómez", "Tesorera");

    [Theory]
    [InlineData(UsuarioEnImpresos.Nombre, "Ana Pérez")]
    [InlineData(UsuarioEnImpresos.NombreCompleto, "Ana María Pérez Gómez")]
    [InlineData(UsuarioEnImpresos.Iniciales, "AMPG")]
    [InlineData(UsuarioEnImpresos.Cargo, "Tesorera")]
    public void Cada_modo_saca_lo_suyo(string modo, string esperado) =>
        Assert.Equal(esperado, UsuarioEnImpresos.Formatear(modo, Ana));

    [Fact]
    public void El_sobrenombre_sale_si_lo_tiene_y_si_no_el_nombre()
    {
        Assert.Equal("AMP", UsuarioEnImpresos.Formatear(UsuarioEnImpresos.Sobrenombre, Ana with { Sobrenombre = " AMP " }));
        Assert.Equal("Ana Pérez", UsuarioEnImpresos.Formatear(UsuarioEnImpresos.Sobrenombre, Ana));
    }

    [Fact]
    public void Sin_cargo_el_modo_cargo_saca_el_nombre() =>
        Assert.Equal("Ana Pérez", UsuarioEnImpresos.Formatear(UsuarioEnImpresos.Cargo, Ana with { Cargo = null }));

    [Fact]
    public void Sin_persona_no_hay_texto_y_se_deja_lo_guardado()
    {
        Assert.Null(UsuarioEnImpresos.Formatear(UsuarioEnImpresos.Nombre, new UsuarioEnImpresos.Datos(null, null, null, null, null)));

        var f = new UsuarioEnImpresos.Formateador(UsuarioEnImpresos.Nombre, new Dictionary<string, UsuarioEnImpresos.Datos>(StringComparer.OrdinalIgnoreCase)
        {
            ["ana@coop.co"] = Ana,
            ["sinpersona@coop.co"] = new(null, null, null, null, null),
        });
        Assert.Equal("Ana Pérez", f.Texto("ANA@coop.co"));
        Assert.Equal("sinpersona@coop.co", f.Texto("sinpersona@coop.co"));
        Assert.Equal("Juan Externo", f.Texto("Juan Externo"));
        Assert.Null(f.Texto(null));
        Assert.Equal(string.Empty, f.TextoONada(null));
    }

    [Fact]
    public void Con_correo_se_deja_todo_como_esta()
    {
        var f = new UsuarioEnImpresos.Formateador(UsuarioEnImpresos.Correo, new Dictionary<string, UsuarioEnImpresos.Datos> { ["ana@coop.co"] = Ana });
        Assert.Equal("ana@coop.co", f.Texto("ana@coop.co"));
    }

    [Theory]
    [InlineData(null, UsuarioEnImpresos.Nombre)]
    [InlineData("", UsuarioEnImpresos.Nombre)]
    [InlineData("inventado", UsuarioEnImpresos.Nombre)]
    [InlineData("iniciales", UsuarioEnImpresos.Iniciales)]
    [InlineData("Correo", UsuarioEnImpresos.Correo)]
    public void Un_valor_vacio_o_desconocido_es_nombre(string? valor, string esperado) =>
        Assert.Equal(esperado, UsuarioEnImpresos.Normalizar(valor));
}
