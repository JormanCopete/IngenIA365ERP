using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Catalogos;

/// <summary>
/// Feature 009 (FR-088): una entidad institucional (EPS, ARL, fondo, caja, banco) se representa
/// como tercero por una persona de Personas. Resuelve el <c>PublicId</c> que manda la pantalla al
/// <c>Id</c> que guarda el catálogo, y rechaza una persona inexistente o eliminada.
/// </summary>
public static class PersonaVinculada
{
    public static readonly Error NoExiste = new("Person.NotFound", "La persona vinculada no existe o fue eliminada. Elíjala de Maestros › Personas.");

    /// <summary>Null se acepta (sin vínculo); un <c>PublicId</c> tiene que corresponder a una persona viva.</summary>
    public static async Task<Result<int?>> ResolverAsync(IApplicationDbContext db, Guid? personPublicId, CancellationToken ct)
    {
        if (personPublicId is null) return Result.Success<int?>(null);
        var id = await db.People.AsNoTracking().Where(p => p.PublicId == personPublicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return id is null ? Result.Failure<int?>(NoExiste) : Result.Success(id);
    }
}
