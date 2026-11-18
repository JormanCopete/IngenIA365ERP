using FluentAssertions;
using IngenIA365ERP.Shared.Components.Ventas;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>2026-11-18: los filtros de cotizaciones de la lista y de «Cargar cotización» se traducen igual al filtro de la API.</summary>
public class FiltroDeCotizacionesTests
{
    [Fact]
    public void Traduce_numero_fechas_cliente_y_usuario()
    {
        var f = new FiltroDeCotizaciones(FiltroDeCotizaciones.Confirmada)
        {
            Numero = 12, Cliente = "pérez", Usuario = "rafa", Desde = new DateOnly(2026, 11, 1), Hasta = new DateOnly(2026, 11, 30),
        };

        var api = f.ParaLaApi(2, 10);

        api.Class.Should().Be(TextosDeVentas.ClaseCotizacion);
        api.Status.Should().Be(TextosDeVentas.EstadoConfirmado);
        api.Number.Should().Be(12);
        api.Customer.Should().Be("pérez");
        api.CreatedBy.Should().Be("rafa");
        api.From.Should().Be(new DateOnly(2026, 11, 1));
        api.To.Should().Be(new DateOnly(2026, 11, 30));
        api.Page.Should().Be(2);
        api.PageSize.Should().Be(10);
    }

    [Fact]
    public void Con_persona_no_manda_el_texto_del_cliente()
    {
        var persona = Guid.NewGuid();
        var api = new FiltroDeCotizaciones { Cliente = "x" }.ParaLaApi(1, 10, persona);

        api.Person.Should().Be(persona);
        api.Customer.Should().BeNull();
    }

    [Theory]
    [InlineData(FiltroDeCotizaciones.Vigente, "Valid")]
    [InlineData(FiltroDeCotizaciones.Vencida, "Expired")]
    public void La_vigencia_va_por_validity(string estado, string esperado) =>
        new FiltroDeCotizaciones(estado).ParaLaApi(1, 10).Validity.Should().Be(esperado);

    [Fact]
    public void Limpiar_vuelve_al_estado_de_entrada_y_ya_no_hay_criterio()
    {
        var f = new FiltroDeCotizaciones(FiltroDeCotizaciones.Confirmada) { Numero = 3, Estado = FiltroDeCotizaciones.Vencida };
        f.HayCriterio.Should().BeTrue();

        f.Limpiar(FiltroDeCotizaciones.Confirmada);

        f.HayCriterio.Should().BeFalse();
        f.Estado.Should().Be(FiltroDeCotizaciones.Confirmada);
    }
}
