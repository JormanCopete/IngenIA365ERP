using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>La versión de la política que rige y su contenido leído (o nada: sin política, rige la norma tal cual).</summary>
public sealed record PoliticaVigente(RiskPolicyVersion Version, PoliticaSarc Politica);

/// <summary>
/// El lector único de la política SARC validada vigente (feature 013, US9, T0700; data-model §23.10, R11): la versión <b>validada</b> con
/// <c>ValidFrom ≤ fecha</c> conocida en R (registrada y validada hasta R), la de <c>ValidFrom</c> más reciente y, entre iguales, la de mayor
/// número. Lo usan el cierre (que la sella en <c>LND_MonthlyCloses.RiskPolicyVersionId</c>), la calificación informativa y
/// <c>GetRiskPolicyQuery</c>. Una política que ya no se puede leer con su esquema no rige (la norma sí), y lo dice el error. (nuevo)
/// </summary>
public sealed class PoliticaSarcVigente(IApplicationDbContext db)
{
    public async Task<PoliticaVigente?> LeerAsync(DateOnly fecha, DateTime? informacionHasta, CancellationToken ct)
    {
        var r = informacionHasta ?? DateTime.MaxValue;
        var version = await db.RiskPolicyVersions.AsNoTracking()
            .Where(v => !v.IsDeleted && v.ValidFrom <= fecha && v.ValidatedAt != null && v.ValidatedAt <= r && v.RegisteredAt <= r)
            .OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (version is null) return null;
        var leida = PoliticaSarc.Leer(version.Overrides, PoliticaSarc.VersionDeEsquemaVigente);
        return leida.EsValido ? new PoliticaVigente(version, leida.Documento!) : null;
    }
}
