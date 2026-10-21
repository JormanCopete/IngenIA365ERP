using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Services;

/// <summary>
/// La lectura del registro central de valores nacionales sobre la base administrativa (feature 013, T37, T39, I-3;
/// data-model parte 3 §3): sólo lectura, sin seguimiento, en orden de <c>Sequence</c>. Es la única pieza fuera de
/// <c>Application/Saas/NationalValues</c> que toca <c>ADM_NationalValues</c> y <c>ADM_RegulatoryDocuments</c>, y sólo la usa el
/// replicador (<c>SoloElReplicadorLeeElRegistroCentral</c>). (nuevo)
/// </summary>
public sealed class RegistroCentralDeValoresNacionales(IAdminDbContext admin) : IRegistroCentralDeValoresNacionales
{
    public async Task<LecturaDeLaCentral> LeerDesdeAsync(long despuesDe, int maximo, CancellationToken ct)
    {
        maximo = Math.Max(1, maximo);
        var valores = await admin.PlatformNationalValues.AsNoTracking()
            .Where(v => v.Sequence > despuesDe)
            .OrderBy(v => v.Sequence)
            .Take(maximo)
            .Select(v => new ValorCentral(v.PublicId, v.Sequence, v.Code, v.Key, v.ValidFrom, v.ValidTo, v.ReferenceDate, v.Value, v.Unit,
                v.Label, v.Source, v.Reason, v.RegisteredAt, v.SupersedesPublicId))
            .ToListAsync(ct);
        var documentos = await admin.PlatformRegulatoryDocuments.AsNoTracking()
            .Where(d => d.Sequence > despuesDe)
            .OrderBy(d => d.Sequence)
            .Take(maximo)
            .Select(d => new DocumentoCentral(d.PublicId, d.Sequence, d.Kind, d.Code, d.ValidFrom, d.ValidTo, d.Content, d.ContentSha256,
                d.SchemaVersion, d.Verified, d.Source, d.Reason, d.RegisteredAt, d.SupersedesPublicId))
            .ToListAsync(ct);

        // Las dos tablas comparten la numeración: se corta en las primeras «máximo» Sequence de la unión, así la pasada
        // siguiente sigue desde la mayor copiada sin saltarse nada.
        var corte = valores.Select(v => v.Sequence).Concat(documentos.Select(d => d.Sequence)).OrderBy(s => s).Take(maximo).DefaultIfEmpty(0).Max();
        return new LecturaDeLaCentral(
            valores.Where(v => v.Sequence <= corte).ToList(),
            documentos.Where(d => d.Sequence <= corte).ToList());
    }
}
