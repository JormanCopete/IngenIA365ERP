using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Application.Security.Users.UnlockUser;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// Desbloquear a quien quedó fuera por intentos fallidos.
///
/// <para>
/// <b>Antes no desbloqueaba nada.</b> Ponía a <c>null</c>
/// <c>SEC_Users.LockoutEndAt</c> y a cero <c>FailedLoginAttempts</c> —dos
/// columnas que ya no escribe nadie— mientras el bloqueo real vive en Redis. El
/// botón respondía OK y la persona seguía sin poder entrar. Y la guardia previa
/// leía esas mismas columnas, así que rechazaba la operación justo cuando sí
/// hacía falta.
/// </para>
/// </summary>
public class UnlockUserCommandHandlerTests
{
    private static readonly Guid Publico = Guid.NewGuid();

    private readonly ILoginAttemptCounter _intentos = Substitute.For<ILoginAttemptCounter>();

    private static ICurrentUserService Actor()
    {
        var u = Substitute.For<ICurrentUserService>();
        u.UserName.Returns("admin@demo");
        return u;
    }

    private (IApplicationDbContext Db, UnlockUserCommandHandler Handler) Preparar(string? correo = "ana@demo.test")
    {
        var db = TestDbContextFactory.Create();
        db.Users.Add(new User
        {
            Id = 1, PublicId = Publico, Username = "ana", Email = correo, IsActive = true,
        });
        ((DbContext)db).SaveChanges();

        return (db, new UnlockUserCommandHandler(db, Actor(), _intentos));
    }

    private void Estado(bool bloqueado, int fallos) =>
        _intentos.CheckAsync(Arg.Any<AmbitoDeIntentos>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutState(bloqueado, bloqueado ? 60 : 0, fallos));

    [Fact]
    public async Task Desbloquear_limpia_el_contador_real_no_una_columna()
    {
        var (_, handler) = Preparar();
        Estado(bloqueado: true, fallos: 5);

        var result = await handler.Handle(new UnlockUserCommand(Publico), default);

        result.IsSuccess.Should().BeTrue();
        await _intentos.Received(1).ResetAsync(
            AmbitoDeIntentos.Password, "ANA@DEMO.TEST", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tambien_desbloquea_con_intentos_acumulados_aunque_no_haya_bloqueo_activo()
    {
        // Dejar el contador a medio camino significa que el siguiente error
        // vuelve a bloquear, y quien pidió el desbloqueo no lo entendería.
        var (_, handler) = Preparar();
        Estado(bloqueado: false, fallos: 3);

        var result = await handler.Handle(new UnlockUserCommand(Publico), default);

        result.IsSuccess.Should().BeTrue();
        await _intentos.Received(1).ResetAsync(
            AmbitoDeIntentos.Password, "ANA@DEMO.TEST", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_bloqueo_ni_intentos_se_rechaza_y_no_toca_nada()
    {
        var (_, handler) = Preparar();
        Estado(bloqueado: false, fallos: 0);

        var result = await handler.Handle(new UnlockUserCommand(Publico), default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(UserErrorCodes.NotLocked);
        await _intentos.DidNotReceiveWithAnyArgs().ResetAsync(default, default!, default);
    }

    [Fact]
    public async Task Sin_correo_se_dice_por_que_en_vez_de_fingir_exito()
    {
        // El contador se lleva por correo normalizado: sin correo no hay nada
        // que limpiar, y devolver Success haría creer que se desbloqueó.
        var (_, handler) = Preparar(correo: null);

        var result = await handler.Handle(new UnlockUserCommand(Publico), default);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("correo");
    }
}
