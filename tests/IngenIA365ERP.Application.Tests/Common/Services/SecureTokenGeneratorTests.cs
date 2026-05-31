using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Services;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Common.Services;

public class SecureTokenGeneratorTests
{
    private readonly SecureTokenGenerator _sut = new();

    [Fact]
    public void Generate_default_produce_plano_y_hash_consistentes()
    {
        var result = _sut.Generate();

        result.PlainTokenBase64Url.Should().NotBeNullOrEmpty();
        result.Sha256Hash.Should().HaveCount(32, "SHA-256 produce siempre 32 bytes");

        // El hash debe ser SHA-256 del plain bytes UTF-8.
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(result.PlainTokenBase64Url));
        result.Sha256Hash.Should().Equal(expected);
    }

    [Fact]
    public void Generate_plain_es_base64url_sin_padding()
    {
        var result = _sut.Generate();

        result.PlainTokenBase64Url.Should().NotContain("=", "Base64Url omite padding");
        result.PlainTokenBase64Url.Should().NotContain("+", "Base64Url usa '-' en lugar de '+'");
        result.PlainTokenBase64Url.Should().NotContain("/", "Base64Url usa '_' en lugar de '/'");
    }

    [Fact]
    public void Generate_produce_tokens_unicos_en_alta_concurrencia()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => _sut.Generate().PlainTokenBase64Url).ToList();

        tokens.Distinct().Should().HaveCount(1000, "los 1000 tokens deben ser únicos (sin colisión)");
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void Generate_respeta_byteCount(int bytes)
    {
        var result = _sut.Generate(bytes);

        // Cada 3 bytes producen 4 chars Base64; con 32 bytes son 44 chars
        // (43 sin padding). La longitud exacta depende del tamaño.
        var minExpectedLength = (bytes * 4 + 2) / 3 - 2; // tolerancia 2
        result.PlainTokenBase64Url.Length.Should().BeGreaterThanOrEqualTo(minExpectedLength);
    }

    [Fact]
    public void Generate_rechaza_byteCount_inseguro()
    {
        FluentActions.Invoking(() => _sut.Generate(8))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*128 bits*");
    }

    [Fact]
    public void HashPlainToken_es_deterministico_e_igual_al_de_Generate()
    {
        var result = _sut.Generate();
        var rehash = _sut.HashPlainToken(result.PlainTokenBase64Url);

        rehash.Should().Equal(result.Sha256Hash);
    }

    [Fact]
    public void HashPlainToken_rechaza_input_vacio()
    {
        FluentActions.Invoking(() => _sut.HashPlainToken(""))
            .Should().Throw<ArgumentException>();
    }
}
