using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// El estado de una copia de valor nacional o de documento normativo (feature 013, T41, R7; data-model parte 3 §4.1, §4.4):
/// <b>derivado</b>, nunca columna. Validada si su revisión más reciente es <c>Confirmed</c>, objetada si es <c>Objected</c>,
/// pendiente si no tiene revisión. No bloquea el cálculo. (nuevo)
/// </summary>
public enum EstadoDeRevision
{
    PendienteDeValidar = 0,
    Validada = 1,
    Objetada = 2,
}

/// <summary>La última revisión de una copia: decisión, quién y cuándo.</summary>
public sealed record UltimaRevision(NationalValueReviewDecision Decision, string By, DateTime At, string? Note);

/// <summary>Lee la revisión más reciente de un conjunto de copias en una consulta. (nuevo)</summary>
public static class RevisionesDeCopias
{
    public static async Task<IReadOnlyDictionary<Guid, UltimaRevision>> UltimasAsync(
        IApplicationDbContext db, IReadOnlyCollection<Guid> copias, CancellationToken ct)
    {
        if (copias.Count == 0) return new Dictionary<Guid, UltimaRevision>();
        var filas = await db.NationalValueReviews.AsNoTracking()
            .Where(r => copias.Contains(r.TargetPublicId))
            .Select(r => new { r.TargetPublicId, r.Decision, r.ReviewedByName, r.ReviewedAt, r.Note, r.Id })
            .ToListAsync(ct);
        return filas
            .GroupBy(r => r.TargetPublicId)
            .ToDictionary(g => g.Key, g =>
            {
                var ultima = g.OrderByDescending(r => r.ReviewedAt).ThenByDescending(r => r.Id).First();
                return new UltimaRevision(ultima.Decision, ultima.ReviewedByName, ultima.ReviewedAt, ultima.Note);
            });
    }

    public static EstadoDeRevision Estado(UltimaRevision? revision) => revision?.Decision switch
    {
        NationalValueReviewDecision.Confirmed => EstadoDeRevision.Validada,
        NationalValueReviewDecision.Objected => EstadoDeRevision.Objetada,
        _ => EstadoDeRevision.PendienteDeValidar,
    };
}
