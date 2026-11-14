using FluentAssertions;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// 2026-11-13: el diálogo de descuento (F7) del punto de venta manda un porcentaje o un valor, nunca los dos: el último que se escribe borra el
/// otro. Antes, con los dos escritos, se mandaba el porcentaje aunque el último fuera el valor.
/// </summary>
public class DescuentoDigitadoTests
{
    [Fact]
    public void El_valor_escrito_despues_del_porcentaje_es_el_que_se_manda()
    {
        var d = new DescuentoDigitado { Porcentaje = 10m };
        d.Valor = 500m;

        d.Porcentaje.Should().BeNull("escribir el valor borra el porcentaje");
        d.Pedido().Should().Be(new DescuentoRequest(Amount: 500m));
    }

    [Fact]
    public void El_porcentaje_escrito_despues_del_valor_va_como_fraccion()
    {
        var d = new DescuentoDigitado { Valor = 500m };
        d.Porcentaje = 10m;

        d.Valor.Should().BeNull();
        d.Pedido().Should().Be(new DescuentoRequest(Percent: 0.1m));
    }

    [Fact]
    public void Vacio_o_en_cero_no_manda_descuento()
    {
        var d = new DescuentoDigitado { Porcentaje = 0m };
        d.Pedido().Should().BeNull();

        d.Limpiar();
        (d.Porcentaje, d.Valor, d.Pedido()).Should().Be(((decimal?)null, (decimal?)null, (DescuentoRequest?)null));
    }
}
