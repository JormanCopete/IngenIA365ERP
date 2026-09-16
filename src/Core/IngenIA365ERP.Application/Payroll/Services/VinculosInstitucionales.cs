using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>Qué entidades de un catálogo institucional siguen sin persona vinculada (feature 009, FR-088).</summary>
public static class VinculosInstitucionales
{
    /// <summary>La primera entidad viva sin vínculo, con su nombre; null si todas lo tienen.</summary>
    public static async Task<string?> SinPersonaAsync(IApplicationDbContext db, EntidadInstitucional entidad, CancellationToken ct) =>
        (await TodasSinPersonaAsync(db, entidad, ct)).FirstOrDefault() is { } nombre ? $"{TercerosDeNomina.Nombre(entidad)} {nombre}" : null;

    public static async Task<IReadOnlyList<string>> TodasSinPersonaAsync(IApplicationDbContext db, EntidadInstitucional entidad, CancellationToken ct)
    {
        var nombres = entidad switch
        {
            EntidadInstitucional.Eps => await db.HealthInsuranceProviders.AsNoTracking().Where(x => !x.IsDeleted && x.PersonId == null).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct),
            EntidadInstitucional.Arl => await db.WorkRiskProviders.AsNoTracking().Where(x => !x.IsDeleted && x.PersonId == null).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct),
            EntidadInstitucional.FondoDePensiones => await db.PensionProviders.AsNoTracking().Where(x => !x.IsDeleted && x.PersonId == null).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct),
            EntidadInstitucional.FondoDeCesantias => await db.SeveranceProviders.AsNoTracking().Where(x => !x.IsDeleted && x.PersonId == null).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct),
            EntidadInstitucional.CajaDeCompensacion => await db.FamilyCompensationFunds.AsNoTracking().Where(x => !x.IsDeleted && x.PersonId == null).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct),
            _ => [],
        };
        return nombres;
    }
}
