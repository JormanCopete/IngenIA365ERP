using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Collections;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Stages;

/// <summary>
/// La semilla de las etapas de cobranza por defecto (feature 013, K4, T0941; data-model §26.4; FR-117): preventiva, administrativa, prejurídica y
/// jurídica, «pendiente de validar» (las crea <see cref="EtapasDeCartera.CreadoPorLaSemilla"/>) y vigentes «desde siempre», como las demás semillas.
/// La llama <c>CollectionStagesSeeder</c> al arrancar la API con las etapas de su JSON. <b>Idempotente</b>: si la cooperativa ya tiene alguna etapa
/// —sembrada, suya o dada de baja— no hace nada: una cooperativa que versionó sus etapas no recibe las de la semilla otra vez. No registra la etapa
/// de ningún crédito: eso es el ciclo diario y, para lo migrado, la corrida de C64 (<c>RegisterMigratedCollectionStagesCommand</c>). (nuevo)
/// </summary>
public static class EtapasPorDefecto
{
    /// <summary>Desde cuándo rigen las etapas sembradas: «desde siempre».</summary>
    public static readonly DateOnly VigenciaDeLaSemilla = new(2000, 1, 1);

    /// <summary>Siembra <paramref name="etapas"/> si la cooperativa no tiene ninguna. Devuelve cuántas insertó. Lanza si la semilla no es válida.</summary>
    public static async Task<int> SembrarAsync(IApplicationDbContext db, IReadOnlyList<CollectionStageInput> etapas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        if (await db.CollectionStages.IgnoreQueryFilters().AnyAsync(ct)) return 0;

        var orden = 0;
        var nuevas = etapas.OrderBy(e => e.DaysFrom).Select(e =>
        {
            orden++;
            var s = CollectionStage.Nueva(CodigoDeCatalogo.Normalizar(e.Code) ?? e.Code, e.Name, orden, e.DaysFrom, e.DaysTo, e.IsLegal, VigenciaDeLaSemilla,
                AccionesDeEtapa.Escribir(e.SuggestedActions), AccionesDeEtapa.Escribir(e.TemplateCodes), e.FeeRule?.Regla().Serializar());
            s.CreatedBy = EtapasDeCartera.CreadoPorLaSemilla;
            return s;
        }).ToList();
        if (EtapasDeCartera.Validar(nuevas) is { } problema)
            throw new InvalidOperationException($"Las etapas de cobranza de la semilla no son válidas: {problema.Mensaje}");
        db.CollectionStages.AddRange(nuevas);
        await db.SaveChangesAsync(ct);
        return nuevas.Count;
    }
}
