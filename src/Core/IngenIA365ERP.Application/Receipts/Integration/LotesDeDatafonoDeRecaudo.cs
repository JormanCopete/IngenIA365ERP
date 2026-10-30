using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Integration;

/// <summary>
/// <see cref="ILotesDeDatafonoDeRecaudo"/> (feature 015, T2, fase 44; FR-133): los lotes de <c>RCD_CashCountTerminalBatches</c> por el día de su arqueo, con su datáfono y su
/// red; el último arqueo de cada lote. (nuevo)
/// </summary>
public sealed class LotesDeDatafonoDeRecaudo(IApplicationDbContext db) : ILotesDeDatafonoDeRecaudo
{
    public async Task<IReadOnlyList<LoteDeDatafonoDto>> PorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime().AddDays(-1);
        var fin = hasta.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Local).ToUniversalTime().AddDays(1);
        var filas = await (from b in db.ReceiptsCashCountTerminalBatches.AsNoTracking()
                           join l in db.ReceiptsCashCountLines.AsNoTracking() on b.CashCountLineId equals l.Id
                           join c in db.ReceiptsCashCounts.AsNoTracking() on l.CashCountId equals c.Id
                           join t in db.CardTerminals.AsNoTracking().IgnoreQueryFilters() on b.CardTerminalId equals t.Id
                           join a in db.CardAcquirers.AsNoTracking().IgnoreQueryFilters() on t.CardAcquirerId equals a.Id
                           where c.CountedAt >= inicio && c.CountedAt <= fin
                           select new { t.PublicId, t.Code, Red = a.PublicId, RedCodigo = a.Code, b.BatchNumber, c.CountedAt, b.Amount, b.Id })
            .ToListAsync(ct);
        return filas.Select(f => (f, Dia: DateOnly.FromDateTime(f.CountedAt.ToLocalTime())))
            .Where(x => x.Dia >= desde && x.Dia <= hasta)
            .GroupBy(x => (x.f.PublicId, x.f.BatchNumber))
            .Select(g => g.OrderByDescending(x => x.f.CountedAt).ThenByDescending(x => x.f.Id).First())
            .Select(x => new LoteDeDatafonoDto(x.f.PublicId, x.f.Code, x.f.Red, x.f.RedCodigo, x.f.BatchNumber, x.Dia, x.f.Amount))
            .ToList();
    }
}
