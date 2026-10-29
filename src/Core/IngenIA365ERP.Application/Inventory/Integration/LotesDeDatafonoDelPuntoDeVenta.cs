using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// <see cref="ILotesDeDatafonoDelPuntoDeVenta"/> (feature 015, T2, fase 44, T0463; E-03): los lotes de <c>INV_CashCountTerminalBatches</c> de los arqueos confirmados, por el
/// día del arqueo, con su datáfono y su red. Si un lote se contó en varios arqueos, el último. (nuevo)
/// </summary>
public sealed class LotesDeDatafonoDelPuntoDeVenta(IApplicationDbContext db) : ILotesDeDatafonoDelPuntoDeVenta
{
    public async Task<IReadOnlyList<LoteDeDatafonoDto>> PorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime().AddDays(-1);
        var fin = hasta.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local).ToUniversalTime().AddDays(1);
        var filas = await (from b in db.CashCountTerminalBatches.AsNoTracking()
                           join l in db.CashCountLines.AsNoTracking() on b.CashCountLineId equals l.Id
                           join c in db.CashCounts.AsNoTracking() on l.CashCountId equals c.Id
                           join t in db.CardTerminals.AsNoTracking().IgnoreQueryFilters() on b.CardTerminalId equals t.Id
                           join a in db.CardAcquirers.AsNoTracking().IgnoreQueryFilters() on t.CardAcquirerId equals a.Id
                           where !b.IsDeleted && c.Status == DocumentStatus.Confirmed && c.CountedAt >= inicio && c.CountedAt <= fin
                           select new { t.PublicId, t.Code, Red = a.PublicId, RedCodigo = a.Code, b.BatchNumber, c.CountedAt, b.BatchTotal, b.Id })
            .ToListAsync(ct);
        return filas.Select(f => (f, Dia: DateOnly.FromDateTime(f.CountedAt.ToLocalTime())))
            .Where(x => x.Dia >= desde && x.Dia <= hasta)
            .GroupBy(x => (x.f.PublicId, x.f.BatchNumber))
            .Select(g => g.OrderByDescending(x => x.f.CountedAt).ThenByDescending(x => x.f.Id).First())
            .Select(x => new LoteDeDatafonoDto(x.f.PublicId, x.f.Code, x.f.Red, x.f.RedCodigo, x.f.BatchNumber, x.Dia, x.f.BatchTotal))
            .ToList();
    }
}
