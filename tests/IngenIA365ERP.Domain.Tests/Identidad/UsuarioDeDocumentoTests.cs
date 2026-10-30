using FluentAssertions;
using IngenIA365ERP.Domain.Common.Identidad;
using Xunit;

namespace IngenIA365ERP.Domain.Tests.Identidad;

/// <summary>
/// Feature 017 (T006): la regla del usuario derivado y la normalización, con los casos de la sección
/// «Pruebas obligatorias» del borrador del dueño.
/// </summary>
public class UsuarioDeDocumentoTests
{
    [Theory]
    [InlineData("CC", "1234567", "1234567")]
    [InlineData("CC", "1.234.567", "1234567")]
    [InlineData("CC", " 1 234 567 ", "1234567")]
    [InlineData("CE", "987654", "CE987654")]
    [InlineData("PPT", "123-456", "PPT123456")]
    [InlineData("PEP", "1234567", "PEP1234567")]
    [InlineData("TI", "1234567890", "TI1234567890")]
    [InlineData("PA", "ab12345", "PAAB12345")]
    [InlineData("pa", "AB-12.345", "PAAB12345")]
    public void Deriva_el_usuario_de_tipo_y_numero(string tipo, string numero, string usuario)
    {
        var doc = UsuarioDeDocumento.Validar(tipo, numero, soloDeAcceso: true, out var error);

        error.Should().BeNull();
        doc!.Usuario.Should().Be(usuario);
    }

    [Fact]
    public void CC_y_CE_con_el_mismo_numero_son_usuarios_distintos()
    {
        var cc = UsuarioDeDocumento.Validar("CC", "123456", true, out _)!;
        var ce = UsuarioDeDocumento.Validar("CE", "123456", true, out _)!;

        cc.Usuario.Should().Be("123456");
        ce.Usuario.Should().Be("CE123456");
        cc.Usuario.Should().NotBe(ce.Usuario);
    }

    [Theory]
    [InlineData("CC", "12A456", "sólo lleva dígitos")]
    [InlineData("CE", "", "Digite el número")]
    [InlineData("CC", "12", "entre 3 y 10")]
    [InlineData("XX", "123456", "Tipo de documento no válido")]
    [InlineData("NIT", "900123456", "Tipo de documento no válido")]
    public void Rechaza_lo_que_no_es_un_documento_de_acceso(string tipo, string numero, string motivo)
    {
        var doc = UsuarioDeDocumento.Validar(tipo, numero, soloDeAcceso: true, out var error);

        doc.Should().BeNull();
        error.Should().Contain(motivo);
    }

    [Theory]
    [InlineData("1234567", "1234567")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("1 234 567", "1234567")]
    [InlineData("CC1234567", "1234567")]
    [InlineData("cc 1.234.567", "1234567")]
    [InlineData("ce987654", "CE987654")]
    [InlineData("PA-ab12345", "PAAB12345")]
    [InlineData("ppt 123456", "PPT123456")]
    public void Normaliza_lo_que_se_escribe_en_el_ingreso(string escrito, string usuario) =>
        UsuarioDeDocumento.NormalizarIdentificador(escrito).Should().Be(usuario);

    [Fact]
    public void Un_CC_seguido_de_letras_no_se_toma_por_cedula() =>
        // «CCA123» no es la cédula A123: el prefijo sólo se quita si lo que sigue son dígitos.
        UsuarioDeDocumento.NormalizarIdentificador("CCA123").Should().Be("CCA123");

    [Theory]
    [InlineData("C", "1.234.567", "1234567")]
    [InlineData("CC", "1234567", "1234567")]
    [InlineData("CE", "987654", "CE987654")]
    [InlineData("E", "987654", "CE987654")]
    [InlineData("PT", "123456", "PPT123456")]
    [InlineData("PE", "123456", "PEP123456")]
    [InlineData("T", "1234567", "TI1234567")]
    [InlineData("P", "ab12", "PAAB12")]
    [InlineData("RC", "1234567", "RC1234567")]
    [InlineData("NI", "900.123.456", "NIT900123456")]
    [InlineData("N", "900123456", "NIT900123456")]
    [InlineData("X", "123", "X-123")]
    [InlineData("", "123", "SIN-123")]
    public void La_persona_de_la_cooperativa_deriva_con_sus_codigos(string idType, string taxId, string usuario) =>
        UsuarioDeDocumento.DerivarParaPersona(idType, taxId).Should().Be(usuario);

    [Fact]
    public void La_pista_nombra_los_prefijos_y_no_la_cedula()
    {
        var pista = UsuarioDeDocumento.Pista();

        pista.Should().Contain("CE").And.Contain("PPT").And.Contain("PA");
        pista.Should().StartWith("Si tu documento no es cédula");
    }
}
