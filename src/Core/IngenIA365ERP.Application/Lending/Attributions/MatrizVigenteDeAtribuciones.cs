using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Attributions;

/// <summary>
/// Una versión de la matriz de atribuciones con sus niveles y reglas, y su forma para <see cref="EnrutadorDeAtribuciones"/> (los
/// órganos por su código y las líneas por su código). (nuevo)
/// </summary>
public sealed record MatrizVigente(
    AttributionMatrix Matriz,
    IReadOnlyList<AttributionLevel> Niveles,
    IReadOnlyList<AttributionRule> Reglas,
    MatrizDeAtribuciones ParaElMotor);

/// <summary>
/// El lector único de la matriz de atribuciones que rige (feature 013, T0293, T63; data-model §3.1, §0.6): toma de
/// <c>LND_AttributionMatrices</c> la versión vigente a la fecha D con lo registrado hasta el instante R por
/// <see cref="VigenciaBitemporal.VigenteDeCartera"/> —la anterior queda vigente hasta la víspera de la siguiente, sin <c>ValidTo</c>
/// guardado— y la entrega con sus niveles y reglas. Lo usan la vista previa, la desactivación de órganos y, desde US2, la originación
/// (que sella la versión en cada solicitud). (nuevo)
/// </summary>
public sealed class MatrizVigenteDeAtribuciones(IApplicationDbContext db)
{
    /// <summary>La versión que rige el <paramref name="fecha"/> con lo registrado hasta <paramref name="informacionHasta"/>; nula si no hay.</summary>
    public async Task<MatrizVigente?> LeerAsync(DateOnly fecha, DateTime informacionHasta, CancellationToken ct = default)
    {
        // Las versiones son pocas (una por cambio del reglamento): se traen enteras y la regla bitemporal decide.
        var versiones = await db.AttributionMatrices.AsNoTracking().Where(m => !m.IsDeleted).ToListAsync(ct);
        var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, fecha, informacionHasta);
        return vigente is null ? null : await ArmarAsync(vigente, ct);
    }

    /// <summary>Una versión por su Id interno (la que selló una solicitud), con sus niveles y reglas.</summary>
    public async Task<MatrizVigente?> LeerVersionAsync(int matrixId, CancellationToken ct = default)
    {
        var matriz = await db.AttributionMatrices.AsNoTracking().FirstOrDefaultAsync(m => m.Id == matrixId && !m.IsDeleted, ct);
        return matriz is null ? null : await ArmarAsync(matriz, ct);
    }

    /// <summary>La víspera de la versión siguiente conocida, o nulo si es la última (el <c>ValidTo</c> implícito de §0.6).</summary>
    public static DateOnly? VigenteHasta(AttributionMatrix matriz, IEnumerable<AttributionMatrix> versiones) =>
        versiones.Where(v => v.Id != matriz.Id && (v.ValidFrom > matriz.ValidFrom || v.ValidFrom == matriz.ValidFrom && v.VersionNumber > matriz.VersionNumber))
            .Select(v => (DateOnly?)v.ValidFrom)
            .Min() is { } siguiente
            ? siguiente == matriz.ValidFrom ? matriz.ValidFrom.AddDays(-1) : siguiente.AddDays(-1)
            : null;

    private async Task<MatrizVigente> ArmarAsync(AttributionMatrix matriz, CancellationToken ct)
    {
        var niveles = await db.AttributionLevels.AsNoTracking().Where(n => n.MatrixId == matriz.Id).OrderBy(n => n.Rank).ToListAsync(ct);
        var reglas = await db.AttributionRules.AsNoTracking().Where(r => r.MatrixId == matriz.Id).OrderBy(r => r.Order).ToListAsync(ct);

        var idsDeOrgano = niveles.Where(n => n.GoverningBodyId is not null).Select(n => n.GoverningBodyId!.Value).Distinct().ToList();
        var organos = await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters()
            .Where(b => idsDeOrgano.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Code, ct);
        var idsDeLinea = reglas.Where(r => r.CreditLineId is not null).Select(r => r.CreditLineId!.Value).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters()
            .Where(l => idsDeLinea.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        var codigoDeNivel = niveles.ToDictionary(n => n.Id, n => n.Code);

        var motor = new MatrizDeAtribuciones(
            niveles.Select(n => new NivelDeAtribucion(n.Code, n.Name, n.Rank, n.Kind, n.MaxConsolidatedAmount, n.IsExceptionLevel, n.IsBoardLevel,
                n.PermissionCode, n.GoverningBodyId is { } b && organos.TryGetValue(b, out var codigo) ? codigo : null)).ToList(),
            reglas.Select(r => new ReglaDeAtribucion(r.Order, r.Subject, codigoDeNivel[r.MinimumLevelId])
            {
                Linea = r.CreditLineId is { } l && lineas.TryGetValue(l, out var codigoDeLinea) ? codigoDeLinea : null,
                Modalidad = r.ModalityCode,
                MontoMinimo = r.MinConsolidatedAmount,
                MontoMaximo = r.MaxConsolidatedAmount,
                PlazoMinimo = r.MinTermMonths,
                PlazoMaximo = r.MaxTermMonths,
                ClaseDeGarantia = r.CollateralKind,
                PuntajeBajoElMinimo = r.ScoreBelowMinimum,
                ConAlertas = r.HasWarnings,
                ExcepcionALaPolitica = r.PolicyException,
                MoraMinima = r.MinDaysPastDue,
                MoraMaxima = r.MaxDaysPastDue,
                Concepto = r.Concept,
                Mecanismo = r.NoveltyMechanism,
                Causa = r.NoveltyCause,
                MargenNegociadoMaximo = r.MaxNegotiatedSpread,
            }).ToList());
        return new MatrizVigente(matriz, niveles, reglas, motor);
    }
}
