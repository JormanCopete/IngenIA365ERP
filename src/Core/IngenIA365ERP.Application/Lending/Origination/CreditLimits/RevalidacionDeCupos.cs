using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

/// <summary>
/// La revalidación de los cupos de una persona (feature 013, US2 bloque 6B; T0405; FR-040, FR-142; api.md §3.7, §7.1):
/// <list type="bullet">
/// <item><see cref="RevisarAsync"/>: el gancho de <c>RegisterRelatedPartyCommand</c>/<c>EndRelatedPartyCommand</c>. Un cupo que rige de una
/// persona que ahora es vinculada y que no otorgó el consejo queda con el bloqueo automático <c>LimitRevalidation</c> (el cupo como disparador) y
/// la alerta <c>Cartera.CupoPorRevalidar</c>. No guarda.</item>
/// <item><see cref="QueFaltaAsync"/>: lo que le falta a un cupo para cumplir (el consejo para un vinculado; la concentración con su disponible); lo
/// usa <c>RevalidateCreditLimitCommand</c>, que levanta el bloqueo si ya cumple.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class RevalidacionDeCupos(
    IApplicationDbContext db,
    VinculadosVigentes vinculados,
    ValidacionDeConcentracion concentracion,
    DisponibleDeCupo disponibles,
    IAlertas alertas,
    IActorActual actorActual,
    IDateTimeService reloj,
    TareasTrasElCommit? trasElCommit = null)
{
    /// <summary>Bloquea los cupos vigentes de un vinculado sin decisión del consejo; devuelve cuántos bloqueó (sin guardar).</summary>
    public async Task<int> RevisarAsync(int personId, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if ((await vinculados.DeAsync([personId], hoy, ct)).Count == 0) return 0;

        var todas = await VersionesDeCupo.DeAsync(db, [personId], ct);
        var vigentes = todas.GroupBy(v => (v.Kind, v.CreditLineId)).Select(g => VersionesDeCupo.Vigente(g, hoy)).OfType<CreditLimit>()
            .Where(v => v.Amount > 0m).ToList();
        var bloqueados = 0;
        var usuario = (await actorActual.ObtenerAsync(ct)).UserId;
        foreach (var cupo in vigentes)
        {
            if (await DelConsejoAsync(cupo, ct)) continue;
            var clave = cupo.PublicId.ToString();
            var yaBloqueado = await db.DebtorBlocks.AnyAsync(b => b.PersonId == personId && b.Reason == DebtorBlockReason.LimitRevalidation
                && b.SourceKey == clave && b.LiftedAt == null && !b.IsDeleted, ct);
            if (yaBloqueado) continue;

            db.DebtorBlocks.Add(DebtorBlock.Nuevo(personId, DebtorBlockReason.LimitRevalidation, clave, hoy,
                "La persona es vinculada y el cupo no lo otorgó el consejo de administración: revalídelo.", usuario));
            var alerta = new AlertaALevantar(TiposDeAlerta.CupoPorRevalidar, "Cupo por revalidar",
                "Una persona con cupo vigente quedó registrada como vinculada y su cupo no lo decidió el consejo de administración. El cupo queda " +
                "bloqueado hasta revalidarlo en Cartera › Cupos (lo decide el consejo, FR-040).",
                "CreditLimit", cupo.PublicId, DedupKey: $"{TiposDeAlerta.CupoPorRevalidar}:{cupo.PublicId:N}");
            if (trasElCommit is not null) trasElCommit.Agregar(c => alertas.LevantarAsync(alerta, c));
            else await alertas.LevantarAsync(alerta, ct);
            bloqueados++;
        }
        return bloqueados;
    }

    /// <summary>Nulo si el cupo cumple; si no, lo que falta (el consejo, la concentración).</summary>
    public async Task<Error?> QueFaltaAsync(CreditLimit cupo, DateOnly fecha, CancellationToken ct)
    {
        if ((await vinculados.DeAsync([cupo.PersonId], fecha, ct)).Count > 0 && !await DelConsejoAsync(cupo, ct))
        {
            var consejo = await db.AttributionLevels.AsNoTracking().Where(n => n.IsBoardLevel).OrderByDescending(n => n.Id).FirstOrDefaultAsync(ct);
            var organo = consejo?.GoverningBodyId is { } g
                ? await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == g).Select(b => b.Name).FirstOrDefaultAsync(ct)
                : null;
            return ErroresDeDecisionesYCupos.NivelRequerido(consejo?.Code ?? "CONSEJO", consejo?.Name ?? "Consejo de administración",
                organo ?? "el consejo de administración");
        }

        var d = await disponibles.DetalleAsync(cupo.PersonId, fecha, ct);
        var medido = d.Consolidado?.Cupo.PublicId == cupo.PublicId ? d.Consolidado
            : d.PorLinea.Concat(d.Rotativos).FirstOrDefault(c => c.Cupo.PublicId == cupo.PublicId);
        var conc = await concentracion.ValidarAsync(cupo.PersonId, medido?.Disponible ?? cupo.Amount, conGarantiaAdmisible: false, fecha, null, ct);
        return conc.IsFailure ? conc.Error : null;
    }

    /// <summary>Los bloqueos «cupo por revalidar» vigentes de alguna versión del cupo (con seguimiento, para levantarlos).</summary>
    public async Task<IReadOnlyList<DebtorBlock>> BloqueosDeRevalidacionAsync(int personId, IReadOnlyList<CreditLimit> delMismoCupo, CancellationToken ct)
    {
        var claves = delMismoCupo.Select(v => v.PublicId.ToString()).ToArray();
        return await db.DebtorBlocks.Where(b => b.PersonId == personId && b.Reason == DebtorBlockReason.LimitRevalidation && claves.Contains(b.SourceKey)
            && b.LiftedAt == null && !b.IsDeleted).ToListAsync(ct);
    }

    /// <summary>¿El cupo lo otorgó una decisión del nivel del consejo?</summary>
    private async Task<bool> DelConsejoAsync(CreditLimit cupo, CancellationToken ct) =>
        cupo.DecisionId is { } d && await (from decision in db.CreditDecisions.AsNoTracking()
                                           join nivel in db.AttributionLevels.AsNoTracking() on decision.LevelId equals nivel.Id
                                           where decision.Id == d && nivel.IsBoardLevel
                                           select decision.Id).AnyAsync(ct);
}
