using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Auth;
using IngenIA365ERP.Domain.Entities.Admin;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// Feature 017 (T025): «Correo o usuario» se resuelve en un solo sitio. Con «@» sólo sirve un correo
/// verificado; sin «@», el usuario de documento exacto tras normalizar. La llave del contador de
/// intentos es la de la cuenta cuando la hay: alternar correo y usuario no da intentos extra.
/// </summary>
public class IngresoConCorreoOUsuarioTests
{
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private static readonly Guid Ana = Guid.NewGuid();

    private ResolutorDeIdentificador Resolutor() => new(_identidad);

    private CentralUser Cuenta(bool verificado = true) => new()
    {
        Id = Ana,
        Email = "ana@coop.test",
        NormalizedEmail = "ANA@COOP.TEST",
        EmailConfirmed = verificado,
        DocumentType = "CC",
        DocumentNumber = "1234567",
        DocumentUsername = "1234567",
    };

    [Fact]
    public async Task Un_correo_verificado_resuelve_la_cuenta()
    {
        _identidad.FindByEmailAsync("ana@coop.test", Arg.Any<CancellationToken>()).Returns(Cuenta());

        var r = await Resolutor().ResolverAsync("  ana@coop.test ", default);

        r.Cuenta!.Id.Should().Be(Ana);
        r.Tipo.Should().Be(LoginIdentifierKind.Email);
        r.ClaveDeIntentos.Should().Be(ClaveDeIntentos.DeCuenta(Ana));
    }

    [Fact]
    public async Task Un_correo_sin_verificar_no_sirve_para_entrar()
    {
        _identidad.FindByEmailAsync("ana@coop.test", Arg.Any<CancellationToken>()).Returns(Cuenta(verificado: false));

        var r = await Resolutor().ResolverAsync("ana@coop.test", default);

        r.Cuenta.Should().BeNull();
        r.Tipo.Should().Be(LoginIdentifierKind.Unknown);
        r.ClaveDeIntentos.Should().Be(ClaveDeIntentos.DeIdentificador("ANA@COOP.TEST"),
            "un correo que nadie comprobó no debe poder bloquear la cuenta de otro");
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("1.234.567")]
    [InlineData("CC 1234567")]
    [InlineData(" cc1.234.567 ")]
    public async Task La_cedula_se_resuelve_con_o_sin_puntos_y_prefijo(string escrito)
    {
        _identidad.FindByDocumentUsernameAsync("1234567", Arg.Any<CancellationToken>()).Returns(Cuenta());

        var r = await Resolutor().ResolverAsync(escrito, default);

        r.Cuenta!.Id.Should().Be(Ana);
        r.Tipo.Should().Be(LoginIdentifierKind.DocumentUsername);
        r.ClaveDeIntentos.Should().Be(ClaveDeIntentos.DeCuenta(Ana), "el mismo contador que el correo");
    }

    [Fact]
    public async Task Un_usuario_inexistente_cuenta_por_el_texto_normalizado()
    {
        var r = await Resolutor().ResolverAsync("ce-999", default);

        r.Cuenta.Should().BeNull();
        r.Normalizado.Should().Be("CE999");
        r.ClaveDeIntentos.Should().Be(ClaveDeIntentos.DeIdentificador("CE999"));
    }

    [Fact]
    public async Task Un_texto_vacio_no_consulta_nada()
    {
        var r = await Resolutor().ResolverAsync("   ", default);

        r.Cuenta.Should().BeNull();
        await _identidad.DidNotReceive().FindByDocumentUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
