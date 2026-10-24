using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

/// <summary>
/// Lo que Recaudo ofrece a Contabilidad para la plantilla y la completitud de su matriz (feature 015, US8, T0260; FR-143): oficinas con puntos, puntos, cajas
/// (con su punto), los medios habilitados en los puntos y los conceptos varios, por código y con los mismos códigos que los hechos ponen en sus dimensiones
/// (<see cref="EmisionDeRecaudo.CodigoDeOficina"/>). Sólo lectura. (nuevo)
/// </summary>
public sealed class DimensionesDeRecaudo(IApplicationDbContext db) : IDimensionesDeRecaudo
{
    public async Task<IReadOnlyList<DimensionDeMatrizDto>> DimensionesAsync(CancellationToken ct)
    {
        var lista = new List<DimensionDeMatrizDto>();
        var puntos = await (from p in db.CollectionPoints.AsNoTracking()
                            join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on p.BranchId equals b.Id
                            select new { p.Id, p.Code, p.Name, b.LegacyCode, OficinaId = b.Id, Oficina = b.Name }).ToListAsync(ct);
        foreach (var o in puntos.GroupBy(p => p.OficinaId))
            lista.Add(new DimensionDeMatrizDto("BranchCode", EmisionDeRecaudo.CodigoDeOficina(o.First().LegacyCode, o.Key), o.First().Oficina));
        foreach (var p in puntos)
            lista.Add(new DimensionDeMatrizDto("CollectionPointCode", p.Code, p.Name, EmisionDeRecaudo.CodigoDeOficina(p.LegacyCode, p.OficinaId)));
        var cajas = await (from c in db.CashBoxes.AsNoTracking()
                           join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                           select new { c.Code, c.Name, Punto = p.Code }).ToListAsync(ct);
        lista.AddRange(cajas.Select(c => new DimensionDeMatrizDto("CashBoxCode", c.Code, c.Name, c.Punto)));
        var medios = await (from mp in db.PaymentMeansCollectionPoints.AsNoTracking()
                            join m in db.PaymentMeans.AsNoTracking() on mp.PaymentMeansId equals m.Id
                            join p in db.CollectionPoints.AsNoTracking() on mp.CollectionPointId equals p.Id
                            select new { m.Code, m.Name, Punto = p.Code }).ToListAsync(ct);
        lista.AddRange(medios.Select(m => new DimensionDeMatrizDto("MeansCode", m.Code, m.Name, m.Punto)));
        var conceptos = await db.MiscConcepts.AsNoTracking().Select(c => new { c.Code, c.Name }).ToListAsync(ct);
        lista.AddRange(conceptos.Select(c => new DimensionDeMatrizDto("MiscConceptCode", c.Code, c.Name)));
        return lista;
    }
}
