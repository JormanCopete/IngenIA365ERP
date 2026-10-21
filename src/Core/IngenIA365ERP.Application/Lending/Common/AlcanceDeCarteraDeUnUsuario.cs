using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// El alcance de Cartera de <b>otro</b> usuario que el que hace la petición (feature 014, respuesta 71 del dueño, E250): el de quien aprueba una operación
/// sobre la atribución. Lo mismo que <see cref="AlcanceDeCarteraDelActor"/> pero por el Id de <c>SEC_Users</c> y con los permisos que dan los roles activos
/// de ese usuario (no los de la sesión). Falla cerrado: sin cooperativa o sin usuario, nada. (nuevo)
/// </summary>
public sealed class AlcanceDeCarteraDeUnUsuario(IApplicationDbContext db, IAdminDbContext admin, ICurrentTenantService cooperativaActual, OficinasDelUsuario oficinasDelUsuario)
{
    public async Task<AlcanceDeCartera> DeAsync(int usuarioId, CancellationToken ct)
    {
        if (!Guid.TryParse(cooperativaActual.TenantId, out var publica)) return AlcanceDeCartera.Vacio;
        var cooperativa = await admin.Tenants.AsNoTracking().Where(t => t.PublicId == publica && t.IsActive).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        if (cooperativa is null) return AlcanceDeCartera.Vacio;

        var permisos = await (from ur in db.UserRoles.AsNoTracking()
                              join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                              join rp in db.RolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                              join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                              where ur.UserId == usuarioId && r.IsActive && !r.IsDeleted && !rp.IsDeleted && !p.IsDeleted
                              select p.Resource + "." + p.Action).Distinct().ToListAsync(ct);
        var todasLasOficinas = permisos.Contains(AlcanceDeCarteraDelActor.TodasLasOficinas, StringComparer.OrdinalIgnoreCase);
        var todasLasLineas = permisos.Contains(AlcanceDeCarteraDelActor.TodasLasLineas, StringComparer.OrdinalIgnoreCase);
        var oficinas = await oficinasDelUsuario.LeerAsync(usuarioId, cooperativa.Value, ct);
        List<int> lineas = todasLasLineas
            ? []
            : await db.UserCreditLineScopes.AsNoTracking().Where(s => s.UserId == usuarioId && !s.IsDeleted).Select(s => s.CreditLineId).ToListAsync(ct);
        return new AlcanceDeCartera(todasLasOficinas, todasLasOficinas ? new HashSet<int>() : oficinas.Sucursales, oficinas.PorDefecto, todasLasLineas, lineas.ToHashSet());
    }
}
