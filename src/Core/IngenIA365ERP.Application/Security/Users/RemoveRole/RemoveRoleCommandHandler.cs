using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.RemoveRole;

public sealed class RemoveRoleCommandHandler : IRequestHandler<RemoveRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IPermissionClaimsCache? _claimsCache;

    public RemoveRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IPermissionClaimsCache? claimsCache = null)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _claimsCache = claimsCache;
    }

    public async Task<Result> Handle(RemoveRoleCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId, ct);
        if (role is null) return Result.Failure("Generic.NotFound", "El rol no existe.");

        var link = await _db.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);

        if (link is null)
        {
            return Result.Failure(UserErrorCodes.NotAssignedRole,
                "El usuario no tiene asignado ese rol.");
        }

        // Borrado fisico, y no es una decision de estilo: es la unica que funciona.
        //
        // Antes se marcaba link.IsDeleted/DeletedAt/DeletedBy/UpdatedBy. Las CUATRO
        // estan Ignore()-adas en la configuracion de la tabla puente
        // (UserConfiguration, el bloque UsingEntity): la junction no maneja borrado
        // logico a proposito. Sin ninguna propiedad mapeada modificada, EF no emitia
        // SQL, SaveChangesAsync devolvia 0 y el handler respondia exito.
        //
        // O sea: quitar un rol contestaba 204 y no quitaba nada. Un administrador
        // creia haber revocado un acceso que seguia vivo.
        //
        // Tampoco vale Remove(link): SoftDeleteInterceptor convierte el estado
        // Deleted en Modified, y volveriamos al mismo no-op. ExecuteDeleteAsync va
        // directo al SQL, sin pasar por el rastreador de cambios.
        var filasBorradas = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id && ur.RoleId == role.Id)
            .ExecuteDeleteAsync(ct);

        if (filasBorradas == 0)
        {
            return Result.Failure(UserErrorCodes.NotAssignedRole,
                "El usuario no tiene asignado ese rol.");
        }

        if (_claimsCache is not null)
        {
            // La cooperativa sale de la peticion, que es de donde tambien la toma
            // PermisosDeLaPeticion al poblar el cache: las dos puntas tienen que
            // usar la misma clave o la invalidacion no encuentra nada. Es el Id
            // interno, no el PublicId.
            await _claimsCache.InvalidateAsync(user.Id,
                _currentUser.TenantId ?? string.Empty, ct);
        }
        return Result.Success();
    }
}
