using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.UnlockUser;

/// <summary>
/// Desbloquea a alguien que quedó fuera por intentos fallidos.
///
/// <para>
/// <b>Antes no desbloqueaba nada.</b> Ponía a <c>null</c>
/// <c>SEC_Users.LockoutEndAt</c> y a cero <c>FailedLoginAttempts</c>, dos
/// columnas que ya no escribe nadie: el bloqueo por intentos vive en Redis, con
/// clave por correo normalizado, desde que el login pasó a la identidad central.
/// El botón existía, respondía OK, y la persona seguía bloqueada.
/// </para>
///
/// <para>
/// Peor aún, la guardia previa —«no está bloqueado ni tiene intentos
/// pendientes»— leía esas mismas columnas, así que rechazaba la operación
/// justamente cuando SÍ hacía falta.
/// </para>
///
/// <para>
/// Ahora limpia el contador real. Las columnas heredadas se siguen limpiando por
/// higiene, pero no son la verdad ni la deciden.
/// </para>
/// </summary>
public sealed class UnlockUserCommandHandler : IRequestHandler<UnlockUserCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoginAttemptCounter _contadorDeIntentos;

    public UnlockUserCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ILoginAttemptCounter contadorDeIntentos)
    {
        _db = db;
        _currentUser = currentUser;
        _contadorDeIntentos = contadorDeIntentos;
    }

    public async Task<Result> Handle(UnlockUserCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Result.Failure(UserErrorCodes.NotLocked,
                "El bloqueo por intentos se lleva por correo y este usuario no tiene uno registrado.");
        }

        var correo = user.Email.Trim().ToUpperInvariant();

        // LOS DOS ÁMBITOS. El bloqueo por contraseña y el del segundo factor se
        // llevan por separado a propósito —si compartieran contador, un login
        // correcto le regalaría un reset a quien está adivinando el TOTP—, pero
        // este handler sólo miraba el primero.
        //
        // Consecuencia: quien se equivocaba cinco veces con su código quedaba
        // encerrado, la pantalla de usuarios lo mostraba como NO bloqueado, y al
        // pulsar desbloquear recibía «el usuario no está bloqueado». No había
        // salida ninguna: sólo esperar a que el escalado bajara, que llega a una
        // hora por intento y se refresca con cada fallo.
        var porContrasena = await _contadorDeIntentos.CheckAsync(AmbitoDeIntentos.Password, correo, ct);
        var porSegundoFactor = await _contadorDeIntentos.CheckAsync(AmbitoDeIntentos.Mfa, correo, ct);

        var hayAlgoQueLimpiar =
            porContrasena.IsLocked || porContrasena.FailureCount > 0 ||
            porSegundoFactor.IsLocked || porSegundoFactor.FailureCount > 0;

        // Se acepta también con intentos acumulados sin bloqueo activo: dejar el
        // contador a medio camino significa que el siguiente error vuelve a
        // bloquear, y quien pidió el desbloqueo no lo entendería.
        if (!hayAlgoQueLimpiar)
        {
            return Result.Failure(UserErrorCodes.NotLocked,
                "El usuario no está bloqueado ni tiene intentos fallidos pendientes.");
        }

        await _contadorDeIntentos.ResetAsync(AmbitoDeIntentos.Password, correo, ct);
        await _contadorDeIntentos.ResetAsync(AmbitoDeIntentos.Mfa, correo, ct);

        // Higiene de las columnas heredadas. No deciden nada, pero dejarlas con
        // valores viejos invita al siguiente a creérselas.
        user.LockoutEndAt = null;
        user.FailedLoginAttempts = 0;
        user.UpdatedBy = _currentUser.UserName ?? "SYSTEM";
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
