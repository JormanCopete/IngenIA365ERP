using IngenIA365ERP.API.Filters.CentralIdentity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="ILimitesPorPermiso"/> (feature 012, T34, T082; contracts/api.md §1.3; pregunta C4). De los roles
/// activos del usuario de <see cref="IActorActual"/> que conceden el permiso, el <b>mayor</b> límite vigente a la
/// fecha; un rol que lo concede sin fila vigente —o con <c>MaxAmount</c> nulo— significa sin límite.
///
/// <para>
/// «Conceder» se lee igual que <see cref="PermisosDeLaPeticion"/>: por <c>SEC_RolePermissions</c> vivos. Los comodines
/// de los roles integrados (<c>*</c>, <c>*.View</c>) no se evalúan aquí porque el sembrador ya los materializa en
/// filas; así un rol con <c>*</c> concede el permiso exactamente cuando la puerta de la ruta lo deja pasar. El
/// administrador maestro no lleva roles por cooperativa: sin límite, como su atajo en la puerta.
/// </para>
/// </summary>
internal sealed class LimitesPorPermiso(IApplicationDbContext db, IActorActual actorActual, IHttpContextAccessor accessor)
    : ILimitesPorPermiso
{
    public async Task<decimal?> MontoMaximoAsync(string permiso, DateOnly fecha, CancellationToken ct = default)
    {
        if (accessor.HttpContext is { } http && RequireMasterAdminAttribute.Check(http).IsAllowed) return null;

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return null;

        return await MontoMaximoDeAsync(db, usuario, permiso, fecha, ct);
    }

    /// <summary>El mismo cálculo para cualquier usuario (lo reusa el aprobador presente).</summary>
    internal static async Task<decimal?> MontoMaximoDeAsync(IApplicationDbContext db, int usuario, string permiso, DateOnly fecha, CancellationToken ct)
    {
        var roles = await RolesQueConcedenAsync(db, usuario, permiso, ct);
        if (roles.Count == 0) return null;

        var limites = await db.PermissionAmountLimits.AsNoTracking()
            .Where(l => roles.Contains(l.RoleId) && l.PermissionCode == permiso
                        && l.ValidFrom <= fecha && (l.ValidTo == null || l.ValidTo >= fecha))
            .Select(l => new { l.RoleId, l.MaxAmount })
            .ToListAsync(ct);

        decimal? mayor = null;
        foreach (var rol in roles)
        {
            var fila = limites.FirstOrDefault(l => l.RoleId == rol);
            if (fila?.MaxAmount is not { } monto) return null; // un rol sin fila (o sin monto) = sin límite
            mayor = mayor is null ? monto : Math.Max(mayor.Value, monto);
        }

        return mayor;
    }

    /// <summary>Los roles activos del usuario que conceden el permiso.</summary>
    internal static Task<List<int>> RolesQueConcedenAsync(IApplicationDbContext db, int usuario, string permiso, CancellationToken ct) =>
        db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == usuario) // la junción rol-usuario no tiene borrado lógico
            .Join(db.Roles.Where(r => r.IsActive && !r.IsDeleted), ur => ur.RoleId, r => r.Id, (ur, r) => r.Id)
            .Join(db.RolePermissions.Where(rp => !rp.IsDeleted), rolId => rolId, rp => rp.RoleId, (rolId, rp) => new { rolId, rp.PermissionId })
            .Join(db.Permissions.Where(p => !p.IsDeleted), x => x.PermissionId, p => p.Id, (x, p) => new { x.rolId, Codigo = p.Resource + "." + p.Action })
            .Where(x => x.Codigo == permiso)
            .Select(x => x.rolId)
            .Distinct()
            .ToListAsync(ct);
}
