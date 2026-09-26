using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IDestinatariosPorPermiso"/> de la API (feature 012, T39, T092; SC-022).
///
/// <para>
/// Los permisos de otros usuarios se leen como los resuelve la puerta (<c>UserPermissionResolver</c>): por
/// <c>SEC_UserRoles → SEC_Roles</c> activos <c>→ SEC_RolePermissions → SEC_Permissions</c> de la base de la cooperativa;
/// los globos de los roles integrados (<c>*</c>, <c>*.View</c>) ya están expandidos en filas por
/// <c>BuiltInRolesSeeder</c>, así que se compara código por código.
/// </para>
///
/// <para>
/// <b><c>CompanyAdmin</c> es el respaldo, no un destinatario más.</b> Concede todo: si contara, toda alerta llegaría
/// también a todos los administradores y «sin destinatario» no pasaría nunca, que es justo lo que el reporte de
/// completitud (SC-022) quiere mostrar. Por eso los destinatarios son quienes tienen el permiso por <b>otro</b> rol; sin
/// ninguno, van los titulares de <c>CompanyAdmin</c> y la alerta queda <c>WithoutRecipient</c>.
/// </para>
///
/// <para>
/// El alcance se filtra por usuario con los puertos de asignación: alcance total por permiso (contando todos sus
/// roles) o la bodega/punto de la alerta entre sus asignaciones. Una bodega que el puerto no conoce sólo la alcanza el
/// alcance total: falla cerrado.
/// </para>
/// </summary>
internal sealed class DestinatariosPorPermiso(
    IApplicationDbContext db,
    IAsignacionesDeBodega bodegas,
    IAsignacionesDePuntoDeVenta puntos) : IDestinatariosPorPermiso
{
    public const string RolDeRespaldo = "CompanyAdmin";

    public async Task<DestinatariosDeAlerta> ResolverAsync(
        IReadOnlyCollection<string> permisos, Guid? bodegaPublicId, Guid? puntoPublicId, CancellationToken ct)
    {
        var candidatos = await ConPermisoAsync(permisos, excluirRespaldo: true, ct);
        var conAlcance = new List<DestinatarioDeAlerta>(candidatos.Count);

        var bodega = bodegaPublicId is { } b ? (await bodegas.BuscarAsync([b], ct)).GetValueOrDefault(b) : null;
        var punto = puntoPublicId is { } p ? (await puntos.BuscarAsync([p], ct)).GetValueOrDefault(p) : null;
        HashSet<int> totalBodegas = bodegaPublicId is null ? [] : await ConPermisoIdsAsync([AlcanceDeInventarioDeLaPeticion.TodasLasBodegas], ct);
        HashSet<int> totalPuntos = puntoPublicId is null ? [] : await ConPermisoIdsAsync([AlcanceDeInventarioDeLaPeticion.TodosLosPuntos], ct);

        foreach (var candidato in candidatos)
        {
            if (bodegaPublicId is not null && !totalBodegas.Contains(candidato.UserId)
                && (bodega is null || !(await bodegas.BodegasDelUsuarioAsync(candidato.UserId, ct)).Ids.Contains(bodega.Id)))
                continue;
            if (puntoPublicId is not null && !totalPuntos.Contains(candidato.UserId)
                && (punto is null || !(await puntos.PuntosDelUsuarioAsync(candidato.UserId, ct)).Ids.Contains(punto.Id)))
                continue;
            conAlcance.Add(candidato);
        }

        if (conAlcance.Count > 0) return new DestinatariosDeAlerta(conAlcance, SinDestinatario: false);
        return new DestinatariosDeAlerta(await TitularesDelRespaldoAsync(ct), SinDestinatario: true);
    }

    public async Task<int> ContarActivosAsync(IReadOnlyCollection<string> permisos, CancellationToken ct) =>
        (await ConPermisoAsync(permisos, excluirRespaldo: true, ct)).Count;

    public async Task<IReadOnlyList<string>> TiposSinDestinatarioAsync(DateOnly fecha, CancellationToken ct)
    {
        var versiones = await db.AlertTypes.AsNoTracking().Where(t => t.IsEnabled).ToListAsync(ct);
        var sinDestinatario = new List<string>();
        foreach (var tipo in versiones.Where(v => v.VigenteEn(fecha)))
        {
            if (TiposDeAlerta.Buscar(tipo.TypeCode) is not { UsaDestinatarios: true }) continue;
            if (await ContarActivosAsync(tipo.Permisos(), ct) == 0) sinDestinatario.Add(tipo.TypeCode);
        }
        return sinDestinatario;
    }

    /// <summary>Usuarios activos cuyos roles activos conceden alguno de los permisos.</summary>
    private async Task<IReadOnlyList<DestinatarioDeAlerta>> ConPermisoAsync(
        IReadOnlyCollection<string> permisos, bool excluirRespaldo, CancellationToken ct)
    {
        if (permisos.Count == 0) return [];
        var codigos = permisos.ToArray();
        var roles = db.Roles.Where(r => r.IsActive && !r.IsDeleted && (!excluirRespaldo || r.Code != RolDeRespaldo));
        var rolesConPermiso = db.RolePermissions.Where(rp => !rp.IsDeleted)
            .Join(db.Permissions.Where(p => !p.IsDeleted && codigos.Contains(p.Resource + "." + p.Action)), rp => rp.PermissionId, p => p.Id, (rp, _) => rp.RoleId)
            .Join(roles, roleId => roleId, r => r.Id, (roleId, _) => roleId);
        var usuarios = db.UserRoles.Where(ur => rolesConPermiso.Contains(ur.RoleId)).Select(ur => ur.UserId);
        return await DescribirAsync(db.Users.Where(u => u.IsActive && usuarios.Contains(u.Id)), ct);
    }

    private async Task<HashSet<int>> ConPermisoIdsAsync(IReadOnlyCollection<string> permisos, CancellationToken ct) =>
        (await ConPermisoAsync(permisos, excluirRespaldo: false, ct)).Select(d => d.UserId).ToHashSet();

    /// <summary>Los titulares activos del rol de respaldo.</summary>
    private Task<IReadOnlyList<DestinatarioDeAlerta>> TitularesDelRespaldoAsync(CancellationToken ct)
    {
        var respaldo = db.Roles.Where(r => r.IsActive && !r.IsDeleted && r.Code == RolDeRespaldo).Select(r => r.Id);
        var usuarios = db.UserRoles.Where(ur => respaldo.Contains(ur.RoleId)).Select(ur => ur.UserId);
        return DescribirAsync(db.Users.Where(u => u.IsActive && usuarios.Contains(u.Id)), ct);
    }

    private async Task<IReadOnlyList<DestinatarioDeAlerta>> DescribirAsync(IQueryable<Domain.Entities.Security.User> usuarios, CancellationToken ct)
    {
        var filas = await usuarios.AsNoTracking()
            .Select(u => new { u.Id, u.PublicId, u.Username, u.Email })
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
        return filas.Select(f => new DestinatarioDeAlerta(f.Id, f.PublicId, f.Username, f.Email)).ToList();
    }
}
