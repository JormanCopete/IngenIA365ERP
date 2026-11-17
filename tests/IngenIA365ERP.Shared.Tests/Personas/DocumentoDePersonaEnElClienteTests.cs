using FluentAssertions;
using IngenIA365ERP.Shared.Models.Personas;
using IngenIA365ERP.Shared.Services.Core;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Personas;

/// <summary>
/// 2026-11-16: el formulario aplica las mismas reglas del documento que el servidor (<c>DocumentoDePersona</c>): catálogo de la 017,
/// número normalizado, NIT sin DV y DV de la DIAN. Al editar, sólo si cambió el documento o el DV.
/// </summary>
public class DocumentoDePersonaEnElClienteTests
{
    private static PersonaFormularioModelo Nueva(string tipo, string numero, string? dv = null)
    {
        var m = PersonaFormularioModelo.Nuevo(numero);
        m.IdType = tipo;
        m.TaxIdCheckDigit = dv;
        m.FirstName = "Ana";
        m.LastName = "Pérez";
        return m;
    }

    [Theory]
    [InlineData("C", "CE987654", "El número de cédula de ciudadanía sólo lleva dígitos.")]
    [InlineData("C", "-", "Digite el número de documento.")]
    [InlineData("X", "123456", "Tipo de documento no válido.")]
    [InlineData("NI", "900123456-8", "Escriba el NIT sin el dígito de verificación: el dígito va en su propio campo.")]
    [InlineData("C", "12345678901", "El número de cédula de ciudadanía debe tener entre 3 y 10 caracteres.")]
    public void Rechaza_lo_que_el_servidor_rechazaria(string tipo, string numero, string aviso)
    {
        Nueva(tipo, numero).Validar().Should().Contain(aviso);
    }

    [Theory]
    [InlineData("C", "1.023.456.789", null)]
    [InlineData("CE", "987654", null)]
    [InlineData("PA", "AB123", null)]
    [InlineData("NI", "900123456", "8")]
    public void Acepta_los_documentos_validos(string tipo, string numero, string? dv)
    {
        Nueva(tipo, numero, dv).Validar().Should().BeEmpty();
    }

    [Fact]
    public void El_DV_distinto_dice_el_correcto_y_el_DV_es_un_solo_digito()
    {
        DocumentoDePersonaEnElCliente.DigitoDeVerificacion("900123456").Should().Be(8);
        Nueva("NI", "900123456", "7").Validar().Should().Contain("El dígito de verificación del NIT 900123456 es 8, no 7.");
        Nueva("NI", "900123456", "A").Validar().Should().Contain("El dígito de verificación es un solo número de 0 a 9.");
    }

    [Fact]
    public void Al_editar_un_documento_viejo_que_no_cambia_no_bloquea_y_si_cambia_si()
    {
        var modelo = PersonaFormularioModelo.DesdeDto(new PersonaDto
        {
            PublicId = Guid.NewGuid(), IdType = "C", TaxId = "1", FirstName = "Ana", LastName = "Pérez", Status = "A", PersonType = "01",
        });

        modelo.Validar().Should().BeEmpty("la fila vieja se edita sin tocar su documento");
        modelo.TaxId = "CE1";
        modelo.Validar().Should().Contain("El número de cédula de ciudadanía sólo lleva dígitos.");
    }

    [Fact]
    public void La_fecha_de_expedicion_futura_se_rechaza()
    {
        var m = Nueva("C", "1023456789");
        m.IdIssueDateDt = DateTime.Today.AddDays(1);
        m.Validar().Should().Contain("La fecha de expedición no puede ser futura.");
    }

    [Fact]
    public void La_ciudad_se_muestra_con_su_departamento_y_la_retirada_se_marca()
    {
        new OpcionDeCiudad(Guid.NewGuid(), "La Unión", "Valle del Cauca").Texto.Should().Be("La Unión (Valle del Cauca)");
        new OpcionDeCiudad(Guid.NewGuid(), "Cali").Texto.Should().Be("Cali");

        var ciudad = Guid.NewGuid();
        var modelo = PersonaFormularioModelo.DesdeDto(new PersonaDto
        {
            PublicId = Guid.NewGuid(), IdType = "C", TaxId = "1023456789", FirstName = "Ana", LastName = "Pérez",
            CityPublicId = ciudad, CityName = "Granada", CityRetired = true,
        });

        modelo.CiudadRetirada.Should().NotBeNull();
        modelo.CiudadRetirada!.Texto.Should().Be("Granada — retirada");
        modelo.AInput().CityPublicId.Should().Be(ciudad, "se conserva sin cambio si no la cambian");
    }
}
