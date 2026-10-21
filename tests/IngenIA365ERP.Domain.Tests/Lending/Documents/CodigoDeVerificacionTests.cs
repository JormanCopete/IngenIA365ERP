using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Documents;

namespace IngenIA365ERP.Domain.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, T0764 (T80; adaptadores-y-archivos.md §8.4): el código de verificación tiene 16 caracteres del alfabeto Crockford (80 bits), se
/// muestra en cuatro grupos y la normalización acepta minúsculas, guiones y espacios y lleva <c>I</c>/<c>L</c> → <c>1</c> y <c>O</c> → <c>0</c>.
/// </summary>
public class CodigoDeVerificacionTests
{
    private static Func<int, byte[]> Fijos(params byte[] bytes) => n =>
    {
        n.Should().Be(CodigoDeVerificacion.BytesAleatorios, "se piden 80 bits");
        return bytes;
    };

    [Fact]
    public void Ochenta_bits_dan_dieciseis_caracteres_del_alfabeto_de_Crockford()
    {
        var codigo = CodigoDeVerificacion.Generar(_ => System.Security.Cryptography.RandomNumberGenerator.GetBytes(CodigoDeVerificacion.BytesAleatorios));

        codigo.Should().HaveLength(16);
        codigo.Should().MatchRegex("^[0-9A-HJKMNP-TV-Z]{16}$", "Crockford no usa I, L, O ni U");
        CodigoDeVerificacion.Alfabeto.Should().HaveLength(32).And.NotContainAny("I", "L", "O", "U");
    }

    [Fact]
    public void Codifica_los_bits_en_orden_de_cinco_en_cinco()
    {
        CodigoDeVerificacion.Generar(Fijos(new byte[10])).Should().Be("0000000000000000");
        CodigoDeVerificacion.Generar(Fijos(Enumerable.Repeat((byte)0xFF, 10).ToArray())).Should().Be("ZZZZZZZZZZZZZZZZ");
        // 0x08 = 00001000 → los primeros cinco bits 00001 = «1», los tres siguientes 000 seguidos de ceros.
        CodigoDeVerificacion.Generar(Fijos(0x08, 0, 0, 0, 0, 0, 0, 0, 0, 0)).Should().Be("1000000000000000");
        // El último byte llega al último carácter: 0x1F = …11111 → «Z».
        CodigoDeVerificacion.Generar(Fijos(0, 0, 0, 0, 0, 0, 0, 0, 0, 0x1F)).Should().Be("000000000000000Z");
    }

    [Fact]
    public void Dos_fuentes_distintas_dan_codigos_distintos_y_la_misma_fuente_el_mismo()
    {
        var a = CodigoDeVerificacion.Generar(Fijos(1, 2, 3, 4, 5, 6, 7, 8, 9, 10));
        var b = CodigoDeVerificacion.Generar(Fijos(1, 2, 3, 4, 5, 6, 7, 8, 9, 11));
        a.Should().NotBe(b);
        CodigoDeVerificacion.Generar(Fijos(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)).Should().Be(a);
    }

    [Fact]
    public void Una_fuente_que_entrega_menos_bytes_no_da_codigo()
    {
        var generar = () => CodigoDeVerificacion.Generar(_ => new byte[9]);
        generar.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Se_muestra_en_cuatro_grupos_de_cuatro()
    {
        CodigoDeVerificacion.Formatear("7K2MQ9XD4HTPB1RW").Should().Be("7K2M-Q9XD-4HTP-B1RW");
    }

    [Theory]
    [InlineData("7K2M-Q9XD-4HTP-B1RW", "7K2MQ9XD4HTPB1RW")]
    [InlineData("7k2m q9xd 4htp b1rw", "7K2MQ9XD4HTPB1RW")]
    [InlineData("  7K2MQ9XD4HTPBIRW ", "7K2MQ9XD4HTPB1RW")]
    [InlineData("7K2MQ9XD4HTPBLRW", "7K2MQ9XD4HTPB1RW")]
    [InlineData("7K2MQ9XD4HTPB1RW", "7K2MQ9XD4HTPB1RW")]
    [InlineData("OOOO-oooo-0000-0000", "0000000000000000")]
    public void La_normalizacion_acepta_minusculas_guiones_y_espacios_y_las_confusiones_de_Crockford(string digitado, string esperado)
    {
        CodigoDeVerificacion.Normalizar(digitado).Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7K2M-Q9XD-4HTP")]
    [InlineData("7K2M-Q9XD-4HTP-B1RW-X")]
    [InlineData("7K2M-Q9XD-4HTP-B1RU")]
    [InlineData("7K2M-Q9XD-4HTP-B1R*")]
    public void Lo_que_no_puede_ser_un_codigo_no_se_normaliza(string? digitado)
    {
        CodigoDeVerificacion.Normalizar(digitado).Should().BeNull();
    }
}
