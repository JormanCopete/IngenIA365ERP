using System.Text.Json;
using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Compliance;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.RestrictiveLists;

/// <summary>
/// La carga vigente de las listas restrictivas sobre <c>COR_RestrictiveListLoads</c>/<c>Entries</c> (feature 013, T70, T0087;
/// data-model parte 3 §4.8): por cada <c>ListCode</c>, la carga de mayor <c>ValidFrom ≤ fecha</c> (y, si dos empiezan el mismo día, la de
/// mayor versión). Reemplaza al defecto <see cref="SinCargaDeListasRestrictivas"/>; la consume el canal <c>CARGA-PROPIA</c>, que compara en
/// memoria (SQL Server no tiene <c>pg_trgm</c>). Dos consultas: las cargas y las entradas de las vigentes. (nuevo)
/// </summary>
public sealed class CargaDeListasRestrictivasEnBase(IApplicationDbContext db) : ICargaDeListasRestrictivas
{
    public async Task<CargaDeListasRestrictivas> LeerVigenteAsync(DateOnly fecha, CancellationToken ct)
    {
        var cargas = await db.RestrictiveListLoads.AsNoTracking()
            .Where(c => c.ValidFrom <= fecha)
            .Select(c => new { c.Id, c.ListCode, c.Version, c.ValidFrom, c.PublishedOn })
            .ToListAsync(ct);
        var vigentes = cargas
            .GroupBy(c => c.ListCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.ValidFrom).ThenByDescending(c => c.Version).First())
            .OrderBy(c => c.ListCode, StringComparer.Ordinal)
            .ToList();
        if (vigentes.Count == 0) return CargaDeListasRestrictivas.Vacia;

        var ids = vigentes.Select(c => c.Id).ToList();
        var porId = vigentes.ToDictionary(c => c.Id);
        var entradas = await db.RestrictiveListEntries.AsNoTracking()
            .Where(e => ids.Contains(e.LoadId))
            .OrderBy(e => e.Id)
            .ToListAsync(ct);

        return new CargaDeListasRestrictivas(
            vigentes.Select(c => new VersionDeListaConsultada(c.ListCode, c.Version, c.ValidFrom, c.PublishedOn)).ToList(),
            entradas.Select(e => new EntradaDeListaRestrictiva(
                porId[e.LoadId].ListCode, porId[e.LoadId].Version, e.EntityKind, e.DocumentType, e.DocumentNumberNormalized,
                e.FullName, e.NameNormalized, Alias(e), e.Program)).ToList());
    }

    private static IReadOnlyList<string> Alias(RestrictiveListEntry entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada.Aliases)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(entrada.Aliases) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
