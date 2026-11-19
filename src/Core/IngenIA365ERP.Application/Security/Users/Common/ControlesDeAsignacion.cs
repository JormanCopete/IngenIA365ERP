using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.Common;

/// <summary>
/// 2026-11-19 (pedido del dueño: «más seguro»): los dos controles que faltaban al asignar y quitar roles a una persona.
///
/// <list type="bullet">
/// <item><b>Nadie se concede a sí mismo lo que no tiene.</b> Quien puede asignar roles podía asignarse uno con más permisos
/// que los suyos —por ejemplo el de administrador— y nadie lo notaba hasta revisar la auditoría. Ahora asignarse un rol a uno
/// mismo sólo pasa si ese rol no trae ningún permiso nuevo para quien lo pide; lo demás lo asigna otra persona.</item>
/// <item><b>Quitar un rol no deja la cooperativa sin quien arregle los roles.</b> Editar un rol ya lo impedía
/// (<c>UpdateRoleCommandHandler</c>); quitarle el rol de administrador a la última persona que lo tenía, no. Mismo criterio:
/// sólo se bloquea si el cambio es la causa.</item>
/// </list>
/// </summary>
public static class ControlesDeAsignacion
{
    /// <summary>Lo mínimo para poder volver a repartir permisos (el mismo par que vigila la edición de roles).</summary>
    private static readonly string[] ParaArreglarRoles = ["Security.Roles.View", "Security.Roles.Update"];

    /// <summary>Los permisos del rol que la persona todavía no tiene por sus otros roles.</summary>
    public static async Task<int> PermisosNuevosParaAsync(IApplicationDbContext db, int userId, int roleId, CancellationToken ct)
    {
        var rolesActuales = db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId);
        var actuales = db.RolePermissions
            .Where(rp => !rp.IsDeleted && rolesActuales.Contains(rp.RoleId))
            .Select(rp => rp.PermissionId);
        return await db.RolePermissions
            .Where(rp => !rp.IsDeleted && rp.RoleId == roleId && !actuales.Contains(rp.PermissionId))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .CountAsync(ct);
    }

    /// <summary>
    /// Si quitarle el rol a la persona deja a la cooperativa sin ningún usuario activo capaz de ver y editar roles, cuando
    /// antes sí lo había.
    /// </summary>
    public static async Task<bool> DejaSinAdministradorAsync(IApplicationDbContext db, int userId, int roleId, CancellationToken ct)
    {
        var claves = await db.Permissions
            .Where(p => !p.IsDeleted)
            .Select(p => new { p.Id, Codigo = p.Resource + "." + p.Action })
            .Where(p => ParaArreglarRoles.Contains(p.Codigo))
            .Select(p => p.Id)
            .ToListAsync(ct);
        if (claves.Count < ParaArreglarRoles.Length) return false;

        var vinculos = await db.RolePermissions
            .Where(rp => !rp.IsDeleted && claves.Contains(rp.PermissionId))
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);

        // SEC_UserRoles no maneja borrado lógico; un usuario eliminado deja ur.User en null por el filtro global.
        var asignaciones = await db.UserRoles
            .Where(ur => ur.User != null && ur.User.IsActive)
            .Select(ur => new { ur.UserId, ur.RoleId })
            .ToListAsync(ct);

        int Cuantos(bool sinEsta) => asignaciones
            .Where(a => !(sinEsta && a.UserId == userId && a.RoleId == roleId))
            .GroupBy(a => a.UserId)
            .Count(g =>
            {
                var permisos = g.SelectMany(a => vinculos.Where(v => v.RoleId == a.RoleId).Select(v => v.PermissionId)).ToHashSet();
                return claves.All(permisos.Contains);
            });

        return Cuantos(sinEsta: false) > 0 && Cuantos(sinEsta: true) == 0;
    }
}
