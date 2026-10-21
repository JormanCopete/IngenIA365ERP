using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Configuration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Configuration;

/// <summary>Una versión de línea leída con sus cuatro hijas y como borrador completo (para versionar desde ella o controlarla). (nuevo)</summary>
public sealed record VersionDeLineaLeida(
    CreditLineVersion Version,
    IReadOnlyList<CreditLineRateBand> Bandas,
    IReadOnlyList<CreditLineDocumentRequirement> Documentos,
    IReadOnlyList<CreditLineInsurance> Seguros,
    IReadOnlyList<CreditLineCollateralRule> Garantias)
{
    public BorradorDeVersionDeLinea Borrador() => BorradorDeVersionDeLinea.De(Version, Bandas, Documentos, Seguros, Garantias);
}

/// <summary>
/// El lector único de la versión de una línea que rige a la fecha D con lo registrado hasta el instante R (feature 013, US1, T0352;
/// data-model §0.6): <see cref="VigenciaBitemporal.VigenteDeCartera{T}"/> sobre <c>LND_CreditLineVersions</c> —la anterior rige hasta la
/// víspera de la siguiente, sin <c>ValidTo</c> guardado; dos con el mismo <c>ValidFrom</c>, gana el <c>VersionNumber</c> mayor— y sus
/// hijas. Lo usan la configuración, la réplica (que lo construye sobre su contexto) y, desde US2, US3 y US6, la simulación, la solicitud,
/// el desembolso y la venta de Comercial. (nuevo)
/// </summary>
public sealed class VersionVigenteDeLinea(IApplicationDbContext db)
{
    /// <summary>La versión que rige el <paramref name="fecha"/> (nula si todavía no hay una).</summary>
    public async Task<VersionDeLineaLeida?> LeerAsync(int lineaId, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => v.CreditLineId == lineaId && !v.IsDeleted).ToListAsync(ct);
        var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, fecha, informacionHasta ?? DateTime.MaxValue);
        return vigente is null ? null : await ConHijasAsync(vigente, ct);
    }

    /// <summary>
    /// La que rige el <paramref name="fecha"/> o, si la línea todavía no tiene una vigente, la primera que regirá (la reapertura y la
    /// réplica revisan lo que la línea ofrece aunque su primera versión empiece después).
    /// </summary>
    public async Task<VersionDeLineaLeida?> LeerVigenteOProximaAsync(int lineaId, DateOnly fecha, CancellationToken ct = default)
    {
        var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => v.CreditLineId == lineaId && !v.IsDeleted).ToListAsync(ct);
        var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, fecha, DateTime.MaxValue)
            ?? versiones.Where(v => v.ValidFrom > fecha).OrderBy(v => v.ValidFrom).ThenByDescending(v => v.VersionNumber).FirstOrDefault();
        return vigente is null ? null : await ConHijasAsync(vigente, ct);
    }

    /// <summary>Una versión por su Id interno (la que selló una solicitud o un crédito).</summary>
    public async Task<VersionDeLineaLeida?> LeerVersionAsync(int versionId, CancellationToken ct = default)
    {
        var version = await db.CreditLineVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && !v.IsDeleted, ct);
        return version is null ? null : await ConHijasAsync(version, ct);
    }

    /// <summary>Las vigentes de varias líneas a la fecha, en una consulta por tabla (la lista de líneas).</summary>
    public async Task<IReadOnlyDictionary<int, VersionDeLineaLeida>> LeerVariasAsync(IReadOnlyCollection<int> lineas, DateOnly fecha,
        DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        var ids = lineas.Distinct().ToArray();
        var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
        var vigentes = versiones.GroupBy(v => v.CreditLineId)
            .Select(g => VigenciaBitemporal.VigenteDeCartera(g, fecha, informacionHasta ?? DateTime.MaxValue))
            .OfType<CreditLineVersion>()
            .ToList();
        var idsDeVersion = vigentes.Select(v => v.Id).ToArray();
        var bandas = await db.CreditLineRateBands.AsNoTracking().Where(x => idsDeVersion.Contains(x.CreditLineVersionId)).ToListAsync(ct);
        var documentos = await db.CreditLineDocumentRequirements.AsNoTracking().Where(x => idsDeVersion.Contains(x.CreditLineVersionId)).ToListAsync(ct);
        var seguros = await db.CreditLineInsurances.AsNoTracking().Where(x => idsDeVersion.Contains(x.CreditLineVersionId)).ToListAsync(ct);
        var garantias = await db.CreditLineCollateralRules.AsNoTracking().Where(x => idsDeVersion.Contains(x.CreditLineVersionId)).ToListAsync(ct);
        return vigentes.ToDictionary(v => v.CreditLineId, v => new VersionDeLineaLeida(v,
            bandas.Where(x => x.CreditLineVersionId == v.Id).OrderBy(x => x.Order).ToList(),
            documentos.Where(x => x.CreditLineVersionId == v.Id).OrderBy(x => x.Order).ToList(),
            seguros.Where(x => x.CreditLineVersionId == v.Id).OrderBy(x => x.Code, StringComparer.Ordinal).ToList(),
            garantias.Where(x => x.CreditLineVersionId == v.Id).OrderBy(x => x.Order).ToList()));
    }

    /// <summary>La víspera de la versión siguiente conocida, o nulo si es la última (el <c>ValidTo</c> implícito de §0.6).</summary>
    public static DateOnly? VigenteHasta(CreditLineVersion version, IEnumerable<CreditLineVersion> versiones)
    {
        var siguiente = versiones
            .Where(v => v.Id != version.Id && (v.ValidFrom > version.ValidFrom || v.ValidFrom == version.ValidFrom && v.VersionNumber > version.VersionNumber))
            .Select(v => (DateOnly?)v.ValidFrom)
            .Min();
        return siguiente?.AddDays(-1);
    }

    private async Task<VersionDeLineaLeida> ConHijasAsync(CreditLineVersion v, CancellationToken ct) => new(
        v,
        await db.CreditLineRateBands.AsNoTracking().Where(x => x.CreditLineVersionId == v.Id).OrderBy(x => x.Order).ToListAsync(ct),
        await db.CreditLineDocumentRequirements.AsNoTracking().Where(x => x.CreditLineVersionId == v.Id).OrderBy(x => x.Order).ToListAsync(ct),
        (await db.CreditLineInsurances.AsNoTracking().Where(x => x.CreditLineVersionId == v.Id).ToListAsync(ct)).OrderBy(x => x.Code, StringComparer.Ordinal).ToList(),
        await db.CreditLineCollateralRules.AsNoTracking().Where(x => x.CreditLineVersionId == v.Id).OrderBy(x => x.Order).ToListAsync(ct));
}
