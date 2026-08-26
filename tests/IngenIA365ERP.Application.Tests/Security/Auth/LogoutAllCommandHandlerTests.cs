using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Security.Auth.LogoutAll;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Auth;

/// <summary>
/// Cerrar todas las sesiones: el paso 4 del procedimiento de rotación de claves
/// ante sospecha de fuga.
///
/// <para>
/// <b>Lo que fija.</b> El handler leía <c>ICurrentUserService.UserId</c>, que
/// sale del claim <c>uid</c> —entero de Fase 0— o de <c>NameIdentifier</c>
/// pasado por <c>int.TryParse</c>. El emisor central no pone <c>uid</c> y su
/// <c>sub</c> es un GUID, así que con cualquier sesión de hoy devolvía «No
/// autenticado». Nadie lo noto porque su cliente es una persona con curl, no
/// codigo: ninguna busqueda de llamadores lo encuentra.
/// </para>
///
/// <para>
/// Un endpoint de contención de incidentes que no responde se descubre roto
/// justo cuando hace falta. Estas pruebas lo atan a la identidad central.
/// </para>
/// </summary>
public class LogoutAllCommandHandlerTests
{
    private static readonly Guid Usuario = Guid.NewGuid();

    private static ICurrentCentralUserContext Sesion(Guid? id)
    {
        var ctx = Substitute.For<ICurrentCentralUserContext>();
        ctx.CentralUserId.Returns(id);
        ctx.IsAuthenticated.Returns(id is not null);
        return ctx;
    }

    [Fact]
    public async Task Con_sesion_central_rota_el_stamp_del_usuario_que_llama()
    {
        // Rotar el SecurityStamp es lo que invalida TODAS las sesiones: el
        // refresh compara el guardado en la sesion contra el actual.
        var identidad = Substitute.For<ICentralIdentityProvider>();
        identidad.RotateSecurityStampAsync(Usuario, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new LogoutAllCommandHandler(Sesion(Usuario), identidad)
            .Handle(new LogoutAllCommand(), default);

        result.IsSuccess.Should().BeTrue();
        await identidad.Received(1).RotateSecurityStampAsync(Usuario, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_sesion_no_rota_nada()
    {
        // Y sobre todo: no puede rotarle el stamp a otra persona. El usuario
        // sale del token, nunca del cuerpo de la peticion.
        var identidad = Substitute.For<ICentralIdentityProvider>();

        var result = await new LogoutAllCommandHandler(Sesion(null), identidad)
            .Handle(new LogoutAllCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Identity.Unauthenticated");
        await identidad.DidNotReceiveWithAnyArgs().RotateSecurityStampAsync(default, default);
    }

    [Fact]
    public async Task Si_la_cuenta_ya_no_existe_lo_dice_en_vez_de_fingir_exito()
    {
        // Devolver Success sin haber cerrado nada seria lo peor posible aqui:
        // quien ejecuta el runbook creeria que contuvo el incidente.
        var identidad = Substitute.For<ICentralIdentityProvider>();
        identidad.RotateSecurityStampAsync(Usuario, Arg.Any<CancellationToken>()).Returns(false);

        var result = await new LogoutAllCommandHandler(Sesion(Usuario), identidad)
            .Handle(new LogoutAllCommand(), default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Generic.NotFound");
    }
}
