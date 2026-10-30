using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Security;

/// <summary>
/// La comparación de la segregación <b>por persona</b> (feature 015, T0060; R-31; FR-012): dos usuarios de <c>SEC_Users</c> son la misma persona si
/// comparten <c>PersonId</c>; un usuario sin persona sólo es igual a sí mismo. La usan la segregación de Recaudo y la de Tesorería; el usuario es el
/// <c>SEC_Users.Id</c> que resuelve <c>IActorActual</c> (o el aprobador presente identificado), nunca el entero del token. (nuevo)
/// </summary>
public sealed class PersonasDeLosUsuarios(IApplicationDbContext db)
{
    /// <summary>¿Son <paramref name="usuarioA"/> y <paramref name="usuarioB"/> la misma persona?</summary>
    public async Task<bool> MismaPersonaAsync(int usuarioA, int usuarioB, CancellationToken ct = default)
    {
        if (usuarioA == usuarioB) return true;
        var personas = await db.Users.AsNoTracking().IgnoreQueryFilters()
            .Where(u => u.Id == usuarioA || u.Id == usuarioB)
            .Select(u => new { u.Id, u.PersonId })
            .ToListAsync(ct);
        var a = personas.FirstOrDefault(p => p.Id == usuarioA)?.PersonId;
        var b = personas.FirstOrDefault(p => p.Id == usuarioB)?.PersonId;
        return a is not null && a == b;
    }

    /// <summary>¿Es <paramref name="usuario"/> la misma persona que alguno de <paramref name="otros"/>?</summary>
    public async Task<bool> AlgunaEsLaMismaAsync(int usuario, IEnumerable<int> otros, CancellationToken ct = default)
    {
        foreach (var otro in otros.Distinct())
        {
            if (await MismaPersonaAsync(usuario, otro, ct)) return true;
        }
        return false;
    }

    /// <summary>La persona (<c>COR_People.Id</c>) de un usuario, o nula si no la tiene.</summary>
    public Task<int?> PersonaDeAsync(int usuario, CancellationToken ct = default) =>
        db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == usuario).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
}
