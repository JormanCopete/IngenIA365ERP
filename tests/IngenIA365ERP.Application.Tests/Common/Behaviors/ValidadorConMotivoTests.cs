using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;

namespace IngenIA365ERP.Application.Tests.Common.Behaviors;

/// <summary>T053 (contracts/api.md §2.8): el motivo es obligatorio y de hasta 500 caracteres.</summary>
public class ValidadorConMotivoTests
{
    public sealed record AnularDePrueba(string Reason, int Otro) : IConMotivo;

    private sealed class Validador : ValidadorConMotivo<AnularDePrueba>;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_motivo_no_valida(string? motivo)
    {
        new Validador().Validate(new AnularDePrueba(motivo!, 1)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Hasta_quinientos_caracteres()
    {
        new Validador().Validate(new AnularDePrueba(new string('a', ValidadorConMotivo<AnularDePrueba>.LargoMaximo), 1)).IsValid.Should().BeTrue();
        new Validador().Validate(new AnularDePrueba(new string('a', 501), 1)).IsValid.Should().BeFalse();
    }
}
