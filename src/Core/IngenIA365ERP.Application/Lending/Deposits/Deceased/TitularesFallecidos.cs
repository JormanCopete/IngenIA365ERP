using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Deceased;

/// <summary>
/// ¿Es fallecido el titular? (feature 014, US14, T210; FR-065, FR-072): lo es si el maestro de personas lo marca (<c>COR_People.IsDeceased</c>) <b>o</b> si la
/// Cartera tiene registrado su fallecimiento en un caso vivo (registrado, con entrega pendiente o aprobada). La Cartera no escribe el maestro de personas:
/// el caso basta para suspender la causación según la línea y para dejar el CDAT a disposición al vencer. (nuevo)
/// </summary>
public static class TitularesFallecidos
{
    /// <summary>¿Es fallecida la persona?</summary>
    public static async Task<bool> EsAsync(IApplicationDbContext db, int personaId, CancellationToken ct) =>
        await db.People.AsNoTracking().IgnoreQueryFilters().AnyAsync(p => p.Id == personaId && p.IsDeceased, ct)
        || await db.DeceasedHolderCases.AsNoTracking().AnyAsync(c => c.PersonId == personaId && !c.IsDeleted
            && (c.Status == DeceasedCaseStatus.Registered || c.Status == DeceasedCaseStatus.DeliveryPending || c.Status == DeceasedCaseStatus.Approved), ct);

    /// <summary>Las fallecidas de <paramref name="personas"/>.</summary>
    public static async Task<HashSet<int>> DeAsync(IApplicationDbContext db, IReadOnlyCollection<int> personas, CancellationToken ct)
    {
        if (personas.Count == 0) return [];
        var ids = personas.ToArray();
        var delMaestro = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id) && p.IsDeceased).Select(p => p.Id).ToListAsync(ct);
        var conCaso = await db.DeceasedHolderCases.AsNoTracking().Where(c => ids.Contains(c.PersonId) && !c.IsDeleted
                && (c.Status == DeceasedCaseStatus.Registered || c.Status == DeceasedCaseStatus.DeliveryPending || c.Status == DeceasedCaseStatus.Approved))
            .Select(c => c.PersonId).ToListAsync(ct);
        return [.. delMaestro, .. conCaso];
    }
}
