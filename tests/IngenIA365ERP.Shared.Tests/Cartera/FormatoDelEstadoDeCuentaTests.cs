using IngenIA365ERP.Shared.Services.Cartera;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// 2026-11-16 (pedido del dueño): los valores del estado de cuenta van sin símbolo de moneda y con o sin centavos según la preferencia del usuario;
/// el tamaño del texto se vuelve una clase conocida aunque llegue un valor desconocido.
/// </summary>
public class FormatoDelEstadoDeCuentaTests
{
    [Fact]
    public void Sin_centavos_y_sin_simbolo()
    {
        var texto = FormatoDelEstadoDeCuenta.Valor(1234567.89m, decimales: false);
        Assert.Equal("1.234.568", texto);
        Assert.DoesNotContain("$", texto);
        Assert.DoesNotContain("COP", texto);
    }

    [Fact]
    public void Con_centavos() => Assert.Equal("1.234.567,89", FormatoDelEstadoDeCuenta.Valor(1234567.89m, decimales: true));

    [Fact]
    public void Un_valor_que_no_aplica_es_raya() => Assert.Equal("—", FormatoDelEstadoDeCuenta.Valor((decimal?)null, decimales: true));

    [Theory]
    [InlineData("pequeno", "texto-estado-pequeno")]
    [InlineData("grande", "texto-estado-grande")]
    [InlineData("normal", "texto-estado-normal")]
    [InlineData("cualquiera", "texto-estado-normal")]
    [InlineData(null, "texto-estado-normal")]
    public void El_tamano_es_una_clase_conocida(string? tamano, string clase) => Assert.Equal(clase, FormatoDelEstadoDeCuenta.ClaseDeTamano(tamano));

    [Fact]
    public void Las_columnas_numericas_respetan_los_centavos()
    {
        var saldo = ColumnasDelEstadoDeCuenta.Todas.Single(c => c.Codigo == "saldo");
        var producto = new ProductoDelEstadoDeCuentaDto("Credito", Guid.NewGuid(), "C-1", "CONS · Consumo", "Crédito", "Deudor", "Vigente", null, null,
            null, 1500.5m, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null);
        Assert.Equal("1.501", saldo.Valor(producto, false));
        Assert.Equal("1.500,50", saldo.Valor(producto, true));
    }
}
