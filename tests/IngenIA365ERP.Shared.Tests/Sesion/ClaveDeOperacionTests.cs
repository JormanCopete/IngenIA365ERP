using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// T13 / T057: la clave de idempotencia del cliente nace al iniciar la operación, se conserva en los
/// reintentos con el mismo contenido y se renueva sólo tras un éxito o si el contenido cambia.
/// </summary>
public class ClaveDeOperacionTests
{
    private static readonly object Cuerpo = new { value = "true", reason = "Prueba" };

    [Fact]
    public void Nace_con_una_clave_propia()
    {
        var a = new ClaveDeOperacion();
        var b = new ClaveDeOperacion();

        a.Valor.Should().NotBe(Guid.Empty);
        a.Valor.Should().NotBe(b.Valor);
    }

    [Fact]
    public void Se_conserva_en_los_reintentos_con_el_mismo_contenido()
    {
        var clave = new ClaveDeOperacion();
        var inicial = clave.Valor;

        clave.Para(Cuerpo).Should().Be(inicial, "el primer envío usa la clave con la que se abrió la operación");
        clave.Para(new { value = "true", reason = "Prueba" }).Should().Be(inicial);
        clave.Para(Cuerpo).Should().Be(inicial);
    }

    [Fact]
    public void Se_renueva_si_el_contenido_cambia()
    {
        var clave = new ClaveDeOperacion();
        var primera = clave.Para(Cuerpo);

        var segunda = clave.Para(new { value = "false", reason = "Prueba" });

        segunda.Should().NotBe(primera, "con otro contenido la misma clave sería Operation.KeyReused");
        clave.Para(new { value = "false", reason = "Prueba" }).Should().Be(segunda);
    }

    [Fact]
    public void Se_renueva_tras_un_exito()
    {
        var clave = new ClaveDeOperacion();
        var primera = clave.Para(Cuerpo);

        clave.Exito();

        clave.Valor.Should().NotBe(primera);
        clave.Para(Cuerpo).Should().Be(clave.Valor, "el mismo contenido después de un éxito es otra operación, con la clave nueva");
    }

    [Fact]
    public void Aplicar_pone_la_cabecera_una_sola_vez()
    {
        var clave = new ClaveDeOperacion();
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "https://erp.pruebas/x");

        clave.Aplicar(peticion, Cuerpo);
        clave.Aplicar(peticion, Cuerpo);

        peticion.Headers.GetValues(ClaveDeOperacion.Cabecera).Should().ContainSingle().Which.Should().Be(clave.Valor.ToString());
    }

    [Fact]
    public void Reconoce_la_respuesta_repetida()
    {
        using var repetida = new HttpResponseMessage(HttpStatusCode.Created);
        repetida.Headers.TryAddWithoutValidation(ClaveDeOperacion.CabeceraDeRepeticion, "true");
        using var nueva = new HttpResponseMessage(HttpStatusCode.Created);

        ClaveDeOperacion.FueRepeticion(repetida).Should().BeTrue();
        ClaveDeOperacion.FueRepeticion(nueva).Should().BeFalse();
    }
}
