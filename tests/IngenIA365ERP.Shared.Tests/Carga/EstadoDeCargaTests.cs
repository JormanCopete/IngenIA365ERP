using FluentAssertions;
using IngenIA365ERP.Shared.Services;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Carga;

/// <summary>
/// La bandera que enciende <c>IndicadorDeCarga</c>. Lo que importa: se apaga sola aunque el
/// método falle, y dos cargas solapadas la mantienen encendida hasta la última — con un
/// booleano suelto la primera en terminar la apagaba con la otra todavía en vuelo.
/// </summary>
public class EstadoDeCargaTests
{
    [Fact]
    public void Se_enciende_al_iniciar_y_se_apaga_al_salir_del_using()
    {
        var estado = new EstadoDeCarga();
        estado.Activa.Should().BeFalse();
        using (estado.Iniciar())
        {
            estado.Activa.Should().BeTrue();
        }
        estado.Activa.Should().BeFalse();
    }

    [Fact]
    public void Dos_cargas_solapadas_la_mantienen_encendida_hasta_la_ultima()
    {
        var estado = new EstadoDeCarga();
        var lista = estado.Iniciar();
        var catalogos = estado.Iniciar();
        lista.Dispose();
        estado.Activa.Should().BeTrue("los catálogos siguen en vuelo");
        catalogos.Dispose();
        estado.Activa.Should().BeFalse();
    }

    [Fact]
    public void Se_apaga_aunque_el_metodo_lance()
    {
        var estado = new EstadoDeCarga();
        var accion = () =>
        {
            using var carga = estado.Iniciar();
            throw new InvalidOperationException("la API no respondió");
        };
        accion.Should().Throw<InvalidOperationException>();
        estado.Activa.Should().BeFalse("un error nunca deja el spinner girando para siempre");
    }

    [Fact]
    public void Desechar_dos_veces_el_mismo_inicio_no_descuenta_dos_veces()
    {
        var estado = new EstadoDeCarga();
        var a = estado.Iniciar();
        var b = estado.Iniciar();
        a.Dispose();
        a.Dispose();
        estado.Activa.Should().BeTrue("b sigue abierto; el segundo Dispose de a no cuenta");
        b.Dispose();
        estado.Activa.Should().BeFalse();
    }
}
