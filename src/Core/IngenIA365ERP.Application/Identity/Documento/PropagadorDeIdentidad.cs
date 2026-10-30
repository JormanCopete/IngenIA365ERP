using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Documento;

/// <summary>
/// Feature 017 (D-08): lleva a <c>SEC_Users</c> de cada cooperativa de la persona lo que cambió en su
/// identidad central —el usuario de documento al registrarlo o corregirlo, y el vínculo
/// <c>CentralUserId</c> al fusionar dos identidades—. Recorre las cooperativas donde tiene membresía (de
/// cualquier estado: una suspendida que vuelve tiene que encontrarse bien) y abre cada base por su
/// cuenta. Es idempotente: sólo escribe lo que difiere, así que reintentarlo completa lo que faltó.
/// Devuelve las cooperativas donde no pudo, para dejarlas anotadas; nunca lanza por una de ellas.
/// </summary>
public sealed class PropagadorDeIdentidad(
    IAdminDbContext db,
    ITenantDbContextFactory fabrica,
    ILogger<PropagadorDeIdentidad> logger)
{
    /// <summary>El usuario de documento de <paramref name="centralUserId"/> en todas sus cooperativas.</summary>
    public Task<IReadOnlyList<Guid>> UsuarioAsync(Guid centralUserId, string? usuario, CancellationToken ct) =>
        RecorrerAsync(centralUserId, async (bd, ct2) =>
        {
            var filas = await bd.Users.IgnoreQueryFilters()
                .Where(u => u.CentralUserId == centralUserId && u.CentralUsername != usuario)
                .ToListAsync(ct2);
            foreach (var fila in filas) fila.CentralUsername = usuario;
            if (filas.Count > 0) await bd.SaveChangesAsync(ct2);
        }, ct);

    /// <summary>
    /// Feature 017 (US7): el correo nuevo de la persona en el <c>Email</c> y el <c>CentralUserPublicEmail</c> de
    /// sus filas. <c>Username</c> no se toca: es la clave con la que la auditoría vieja la nombra.
    /// </summary>
    public Task<IReadOnlyList<Guid>> CorreoAsync(Guid centralUserId, string correo, CancellationToken ct) =>
        RecorrerAsync(centralUserId, async (bd, ct2) =>
        {
            var filas = await bd.Users.IgnoreQueryFilters()
                .Where(u => u.CentralUserId == centralUserId && (u.Email != correo || u.CentralUserPublicEmail != correo))
                .ToListAsync(ct2);
            foreach (var fila in filas)
            {
                fila.Email = correo;
                fila.CentralUserPublicEmail = correo;
            }
            if (filas.Count > 0) await bd.SaveChangesAsync(ct2);
        }, ct);

    /// <summary>
    /// Tras una fusión: las filas que apuntaban a <paramref name="origen"/> pasan a <paramref name="destino"/>
    /// en las cooperativas donde ahora está el destino. Como la fusión se niega si comparten cooperativa,
    /// en cada base hay a lo sumo una de las dos.
    /// </summary>
    public Task<IReadOnlyList<Guid>> VinculoAsync(Guid origen, Guid destino, string? usuario, CancellationToken ct) =>
        RecorrerAsync(destino, async (bd, ct2) =>
        {
            var filas = await bd.Users.IgnoreQueryFilters()
                .Where(u => u.CentralUserId == origen || (u.CentralUserId == destino && u.CentralUsername != usuario))
                .ToListAsync(ct2);
            foreach (var fila in filas)
            {
                fila.CentralUserId = destino;
                fila.CentralUsername = usuario;
            }
            if (filas.Count > 0) await bd.SaveChangesAsync(ct2);
        }, ct);

    private async Task<IReadOnlyList<Guid>> RecorrerAsync(
        Guid centralUserId, Func<IApplicationDbContext, CancellationToken, Task> paso, CancellationToken ct)
    {
        var cooperativas = await (
                from m in db.TenantMemberships.AsNoTracking()
                join t in db.Tenants.AsNoTracking() on m.TenantId equals t.PublicId
                where m.CentralUserId == centralUserId
                select new { t.PublicId, t.DatabaseName, t.SchemaName, t.ConnectionString })
            .Distinct()
            .ToListAsync(ct);

        var fallidas = new List<Guid>();
        foreach (var c in cooperativas)
        {
            try
            {
                await using var ambito = fabrica.Abrir(c.DatabaseName ?? c.SchemaName, c.ConnectionString);
                await paso(ambito.Db, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "No se pudo llevar la identidad {CentralUserId} a la cooperativa {Cooperativa}; queda pendiente.",
                    centralUserId, c.PublicId);
                fallidas.Add(c.PublicId);
            }
        }
        return fallidas;
    }
}
