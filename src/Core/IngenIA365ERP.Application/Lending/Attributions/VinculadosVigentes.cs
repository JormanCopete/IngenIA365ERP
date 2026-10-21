using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Attributions;

/// <summary>
/// El lector de las personas vinculadas vigentes (feature 013, T0298; FR-040, T63, T64; data-model §3.12): los vínculos de
/// <c>LND_RelatedParties</c> de unas personas vigentes a una fecha. Lo usan la vista previa y, desde US2, el enrutamiento de la
/// solicitud, el cupo, la novedad y la condonación (un vínculo vigente los lleva al consejo, FR-040), y los puntos de acta (la persona
/// por la que existe el vínculo no vota, regla calificada). No audita: es una lectura interna, no la consulta de la pantalla
/// (<c>ListRelatedPartiesQuery</c>, que sí audita). (nuevo)
/// </summary>
public sealed class VinculadosVigentes(IApplicationDbContext db)
{
    /// <summary>Los vínculos vigentes el <paramref name="fecha"/> cuya persona vinculada es una de <paramref name="personas"/> (Ids de <c>COR_People</c>).</summary>
    public async Task<IReadOnlyList<RelatedParty>> DeAsync(IReadOnlyCollection<int> personas, DateOnly fecha, CancellationToken ct = default)
    {
        if (personas.Count == 0) return [];
        var ids = personas.Distinct().ToList();
        return await db.RelatedParties.AsNoTracking()
            .Where(v => !v.IsDeleted && ids.Contains(v.PersonId) && v.ValidFrom <= fecha && (v.ValidTo == null || v.ValidTo >= fecha))
            .OrderBy(v => v.PersonId).ThenBy(v => v.ValidFrom)
            .ToListAsync(ct);
    }

    /// <summary>Si alguna de <paramref name="personas"/> es vinculada el <paramref name="fecha"/>.</summary>
    public async Task<bool> HayAlgunoAsync(IReadOnlyCollection<int> personas, DateOnly fecha, CancellationToken ct = default) =>
        (await DeAsync(personas, fecha, ct)).Count > 0;
}
