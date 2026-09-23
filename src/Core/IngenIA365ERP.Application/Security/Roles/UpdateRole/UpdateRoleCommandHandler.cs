using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.UpdateRole;

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionClaimsCache? _claimsCache;

    public UpdateRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionClaimsCache? claimsCache = null)
    {
        _db = db;
        _currentUser = currentUser;
        _claimsCache = claimsCache;
    }

    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId, ct);

        if (role is null)
        {
            return Result.Failure("Generic.NotFound", "El rol no existe.");
        }

        // Built-in: solo se permite editar Name, Description y permisos. IsAssignable
        // y Code quedan blindados. (FR-020: CompanyAdmin/Auditor son inmutables en
        // su naturaleza, pero permitimos refinar el set de permisos.)
        role.Name = request.Name;
        role.Description = request.Description;
        if (!role.IsBuiltIn)
        {
            role.IsAssignable = request.IsAssignable;
        }
        role.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        // Reasignar permisos: diff contra los actuales.
        var requestedPermissionIds = request.PermissionPublicIds?.Distinct().ToList() ?? [];
        var matchedPermissions = await _db.Permissions
            .Where(p => requestedPermissionIds.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId })
            .ToListAsync(ct);

        if (matchedPermissions.Count != requestedPermissionIds.Count)
        {
            var missing = requestedPermissionIds
                .Except(matchedPermissions.Select(p => p.PublicId))
                .ToList();
            return Result.Failure(
                RoleErrorCodes.PermissionsInvalid,
                $"Permisos no existen en el catálogo: {string.Join(", ", missing)}.");
        }

        // Se leen IGNORANDO el filtro de borrado lógico, y eso es el arreglo.
        //
        // Los vínculos no se borran: se marcan IsDeleted. Pero el índice único de
        // (RoleId, PermissionId) NO filtra por IsDeleted, así que volver a
        // conceder un permiso que se quitó antes intentaba INSERTAR una fila que
        // ya existe y reventaba con violación de clave. No es un caso raro de
        // bloqueo: es quitar un permiso y volver a ponerlo, que fallaba SIEMPRE.
        // Y el sembrado de arranque tampoco lo reparaba, porque también lee con
        // IgnoreQueryFilters y da la fila borrada por existente.
        var currentLinks = await _db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(ct);

        var existentes = currentLinks.Select(l => l.PermissionId).ToHashSet();
        var pedidos = matchedPermissions.Select(p => p.Id).ToHashSet();
        var actor = _currentUser.UserName ?? "SYSTEM";
        var ahora = DateTime.UtcNow;

        // Vivos que ya no se piden → al borrado lógico.
        foreach (var link in currentLinks.Where(l => !l.IsDeleted && !pedidos.Contains(l.PermissionId)))
        {
            link.IsDeleted = true;
            link.DeletedAt = ahora;
            link.DeletedBy = actor;
        }

        // Borrados que se vuelven a pedir → REVIVIR la fila, no insertar otra.
        // Revivir conserva el rastro de la primera concesión; insertar una
        // segunda fila del mismo par lo duplicaría, además de chocar con el
        // índice.
        foreach (var link in currentLinks.Where(l => l.IsDeleted && pedidos.Contains(l.PermissionId)))
        {
            link.IsDeleted = false;
            link.DeletedAt = null;
            link.DeletedBy = null;
            link.UpdatedAt = ahora;
            link.UpdatedBy = actor;
        }

        // Pares que nunca existieron → esos sí se insertan.
        foreach (var perm in matchedPermissions.Where(p => !existentes.Contains(p.Id)))
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = perm.Id,
                CreatedBy = actor,
                UpdatedBy = actor
            });
        }

        var bloqueo = await ComprobarQueNoDejaSinAdministradorAsync(role.Id, pedidos, ct);
        if (bloqueo is not null) return bloqueo;

        await _db.SaveChangesAsync(ct);

        // Invalidar el cache de permisos efectivos de los usuarios con este rol.
        // T075 expone el método; si aún no está cableado, el TTL del cache (30 min)
        // sirve de fallback (FR-019 — ≤ 30 min para propagar).
        if (_claimsCache is not null)
        {
            await _claimsCache.InvalidateRoleAsync(role.Id, ct);
        }

        return Result.Success();
    }

    /// <summary>Lo mínimo para poder volver a repartir permisos.</summary>
    private static readonly string[] ParaArreglarRoles =
        ["Security.Roles.View", "Security.Roles.Update"];

    /// <summary>
    /// Impide que el cambio deje a la cooperativa sin nadie capaz de arreglar los
    /// roles.
    ///
    /// <para>
    /// Sin esto, un solo PUT que vacíe los permisos de <c>CompanyAdmin</c> deja
    /// la cooperativa sin administración y <b>no hay vuelta atrás por HTTP</b>:
    /// arreglarlo requiere el propio permiso que se acaba de quitar. La resiembra
    /// de arranque tampoco repara —lee los vínculos con <c>IgnoreQueryFilters</c>
    /// y da los borrados por existentes— y el administrador maestro no puede
    /// rescatarla: para operar dentro de una cooperativa necesita una membresía
    /// activa que por diseño no tiene.
    /// </para>
    ///
    /// <para>
    /// <b>Sólo bloquea si el cambio es la causa.</b> Compara antes y después: si
    /// ya no había nadie —una cooperativa recién creada, sin usuarios todavía—
    /// el cambio no empeora nada y se deja pasar. Bloquear también ahí impediría
    /// configurar los roles de una cooperativa nueva, que es exactamente cuando
    /// hay que hacerlo.
    /// </para>
    /// </summary>
    private async Task<Result?> ComprobarQueNoDejaSinAdministradorAsync(
        int roleId, HashSet<int> permisosPedidos, CancellationToken ct)
    {
        var claves = await _db.Permissions
            .Where(p => !p.IsDeleted)
            .Select(p => new { p.Id, Codigo = p.Resource + "." + p.Action })
            .Where(p => ParaArreglarRoles.Contains(p.Codigo))
            .Select(p => p.Id)
            .ToListAsync(ct);

        // Si el catálogo no los tiene, no hay invariante que sostener.
        if (claves.Count < ParaArreglarRoles.Length) return null;

        var vinculos = (await _db.RolePermissions
            .Where(rp => !rp.IsDeleted && claves.Contains(rp.PermissionId))
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct))
            .Select(x => (x.RoleId, x.PermissionId))
            .ToList();

        // SEC_UserRoles es una tabla de union y NO maneja borrado logico: UserConfiguration ignora
        // IsDeleted y las demas columnas de auditoria de UserRole, asi que filtrar por ellas no
        // compila a SQL -«Translation of member 'IsDeleted' on entity type 'UserRole' failed»- y
        // tumbaba con 500 toda edicion de rol en un ambiente con el catalogo completo. El borrado
        // del usuario tampoco hay que pedirlo: User lleva filtro global !IsDeleted y EF lo aplica
        // al join, de modo que un usuario eliminado deja ur.User en null.
        var asignaciones = (await _db.UserRoles
            .Where(ur => ur.User != null && ur.User.IsActive)
            .Select(ur => new { ur.UserId, ur.RoleId })
            .ToListAsync(ct))
            .Select(x => (x.UserId, x.RoleId))
            .ToList();

        var antes = CuantosPuedenArreglar(asignaciones, vinculos, roleId, deEsteRol: null, claves);
        var despues = CuantosPuedenArreglar(
            asignaciones, vinculos, roleId,
            deEsteRol: claves.Where(permisosPedidos.Contains).ToHashSet(), claves);

        if (antes > 0 && despues == 0)
        {
            return Result.Failure(
                RoleErrorCodes.LastAdminLockout,
                "El cambio dejaría a la cooperativa sin ninguna persona que pueda volver a " +
                "editar roles, y eso no se puede deshacer desde la aplicación. Concedé " +
                "'Ver roles' y 'Editar rol' a algún rol con usuarios activos antes de quitarlos de éste.");
        }

        return null;
    }

    /// <summary>
    /// Cuántos usuarios activos reúnen los permisos necesarios.
    /// <paramref name="deEsteRol"/> null = estado actual; con valor = el estado
    /// que quedaría, sustituyendo lo que aporta el rol que se está editando.
    /// </summary>
    private static int CuantosPuedenArreglar(
        IReadOnlyList<(int UserId, int RoleId)> asignaciones,
        IReadOnlyList<(int RoleId, int PermissionId)> vinculos,
        int roleEditado,
        HashSet<int>? deEsteRol,
        IReadOnlyCollection<int> requeridos)
    {
        var porRol = new Dictionary<int, HashSet<int>>();
        foreach (var v in vinculos)
        {
            if (v.RoleId == roleEditado && deEsteRol is not null) continue;
            if (!porRol.TryGetValue(v.RoleId, out var set)) porRol[v.RoleId] = set = [];
            set.Add(v.PermissionId);
        }

        if (deEsteRol is not null && deEsteRol.Count > 0) porRol[roleEditado] = deEsteRol;

        var porUsuario = new Dictionary<int, HashSet<int>>();
        foreach (var a in asignaciones)
        {
            if (!porRol.TryGetValue(a.RoleId, out var permisos)) continue;
            if (!porUsuario.TryGetValue(a.UserId, out var acumulado)) porUsuario[a.UserId] = acumulado = [];
            acumulado.UnionWith(permisos);
        }

        return porUsuario.Count(u => requeridos.All(u.Value.Contains));
    }
}
