using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// El monto máximo de un permiso para <b>cualquier</b> usuario de la cooperativa (feature 012, I3, T655; contracts/api.md §1.3, §23.2;
/// T34) (nuevo). <c>ILimitesPorPermiso</c> responde por quien hace la petición; el crédito provisional necesita además saber si
/// <b>alguien</b> puede aprobar lo financiado (si nadie, <c>Inventory.Approval.AmountExceedsLimit</c>) y si el aprobador que decide
/// alcanza el monto. Mismo cálculo que la API (<c>LimitesPorPermiso</c>, que delega aquí): de los roles activos del usuario que
/// conceden el permiso por <c>SEC_RolePermissions</c> vivos, el <b>mayor</b> límite vigente a la fecha; un rol que lo concede sin
/// fila vigente, o con <c>MaxAmount</c> nulo, es sin límite.
/// </summary>
public static class LimitesDePermisoPorUsuario
{
    /// <summary>El máximo efectivo del usuario, o nulo si no tiene límite (también si ningún rol suyo lo concede).</summary>
    public static async Task<decimal?> DeAsync(IApplicationDbContext db, int usuario, string permiso, DateOnly fecha, CancellationToken ct)
    {
        var roles = await RolesQueConcedenAsync(db, usuario, permiso, ct);
        if (roles.Count == 0) return null;
        var limites = await LimitesVigentesAsync(db, roles, permiso, fecha, ct);
        return Efectivo(roles, limites);
    }

    /// <summary>
    /// El mayor máximo entre los usuarios activos que tienen el permiso, sin contar a <paramref name="excluidos"/>: nulo si alguno
    /// no tiene límite, cero si nadie lo tiene.
    /// </summary>
    public static async Task<decimal?> MayorDeLaCooperativaAsync(IApplicationDbContext db, string permiso, DateOnly fecha, IReadOnlyCollection<int> excluidos,
        CancellationToken ct)
    {
        var activos = db.Users.Where(u => u.IsActive && !u.IsDeleted).Select(u => u.Id);
        var concesiones = await db.UserRoles.AsNoTracking()
            .Where(ur => activos.Contains(ur.UserId)) // la junción rol-usuario no tiene borrado lógico
            .Join(db.Roles.Where(r => r.IsActive && !r.IsDeleted), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, RoleId = r.Id })
            .Join(db.RolePermissions.Where(rp => !rp.IsDeleted), x => x.RoleId, rp => rp.RoleId, (x, rp) => new { x.UserId, x.RoleId, rp.PermissionId })
            .Join(db.Permissions.Where(p => !p.IsDeleted), x => x.PermissionId, p => p.Id, (x, p) => new { x.UserId, x.RoleId, Codigo = p.Resource + "." + p.Action })
            .Where(x => x.Codigo == permiso)
            .Select(x => new { x.UserId, x.RoleId })
            .Distinct()
            .ToListAsync(ct);
        var porUsuario = concesiones.Where(c => !excluidos.Contains(c.UserId)).GroupBy(c => c.UserId).ToList();
        if (porUsuario.Count == 0) return 0m;

        var limites = await LimitesVigentesAsync(db, concesiones.Select(c => c.RoleId).Distinct().ToList(), permiso, fecha, ct);
        decimal mayor = 0m;
        foreach (var usuario in porUsuario)
        {
            if (Efectivo(usuario.Select(c => c.RoleId).Distinct().ToList(), limites) is not { } monto) return null;
            mayor = Math.Max(mayor, monto);
        }
        return mayor;
    }

    /// <summary>Los roles activos del usuario que conceden el permiso.</summary>
    public static Task<List<int>> RolesQueConcedenAsync(IApplicationDbContext db, int usuario, string permiso, CancellationToken ct) =>
        db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == usuario) // la junción rol-usuario no tiene borrado lógico
            .Join(db.Roles.Where(r => r.IsActive && !r.IsDeleted), ur => ur.RoleId, r => r.Id, (ur, r) => r.Id)
            .Join(db.RolePermissions.Where(rp => !rp.IsDeleted), rolId => rolId, rp => rp.RoleId, (rolId, rp) => new { rolId, rp.PermissionId })
            .Join(db.Permissions.Where(p => !p.IsDeleted), x => x.PermissionId, p => p.Id, (x, p) => new { x.rolId, Codigo = p.Resource + "." + p.Action })
            .Where(x => x.Codigo == permiso)
            .Select(x => x.rolId)
            .Distinct()
            .ToListAsync(ct);

    private static async Task<List<(int RoleId, decimal? MaxAmount)>> LimitesVigentesAsync(IApplicationDbContext db, IReadOnlyCollection<int> roles, string permiso,
        DateOnly fecha, CancellationToken ct) =>
        (await db.PermissionAmountLimits.AsNoTracking()
            .Where(l => roles.Contains(l.RoleId) && l.PermissionCode == permiso && l.ValidFrom <= fecha && (l.ValidTo == null || l.ValidTo >= fecha))
            .Select(l => new { l.RoleId, l.MaxAmount })
            .ToListAsync(ct))
        .Select(l => (l.RoleId, l.MaxAmount)).ToList();

    /// <summary>El mayor de los roles; un rol sin fila (o sin monto) = sin límite (nulo).</summary>
    private static decimal? Efectivo(IReadOnlyCollection<int> roles, IReadOnlyList<(int RoleId, decimal? MaxAmount)> limites)
    {
        decimal? mayor = null;
        foreach (var rol in roles)
        {
            var fila = limites.FirstOrDefault(l => l.RoleId == rol);
            if (fila.RoleId == 0 || fila.MaxAmount is not { } monto) return null;
            mayor = mayor is null ? monto : Math.Max(mayor.Value, monto);
        }
        return mayor;
    }
}
