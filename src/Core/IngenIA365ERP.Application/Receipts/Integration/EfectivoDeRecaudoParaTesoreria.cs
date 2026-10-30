using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Integration;

/// <summary>
/// <see cref="IEfectivoDeRecaudo"/> (feature 015, T2, fase 43; FR-137): el saldo de cada bóveda activa (<see cref="MovimientosDeBoveda.SaldoAsync"/>) y el efectivo esperado
/// de cada sesión viva (<see cref="SesionesDeCaja.EfectivoAsync"/>), con su oficina. Lo de una caja principal que hace de bóveda se cuenta una vez, como bóveda. (nuevo)
/// </summary>
public sealed class EfectivoDeRecaudoParaTesoreria(IApplicationDbContext db) : IEfectivoDeRecaudo
{
    public async Task<IReadOnlyList<EfectivoEnCustodiaDto>> SaldosAsync(CancellationToken ct)
    {
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(b => b.Id, b => (b.PublicId, b.Name), ct);
        var lista = new List<EfectivoEnCustodiaDto>();
        var bovedas = await db.Vaults.AsNoTracking().Where(v => v.Status == ReceiptsCatalogStatus.Active).ToListAsync(ct);
        foreach (var b in bovedas)
        {
            var (pub, nombre) = oficinas.GetValueOrDefault(b.BranchId, (Guid.Empty, string.Empty));
            lista.Add(new EfectivoEnCustodiaDto(pub, nombre, IEfectivoDeRecaudo.ClaseBoveda, $"Bóveda {nombre}".Trim(), await MovimientosDeBoveda.SaldoAsync(db, b.Id, ct)));
        }
        var vivas = CashSession.Vivas.ToList();
        var sesiones = await (from s in db.ReceiptsCashSessions.AsNoTracking()
                              join c in db.CashBoxes.AsNoTracking() on s.CashBoxId equals c.Id
                              join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                              where vivas.Contains(s.Status)
                              select new { Sesion = s, Caja = c.Name, p.BranchId }).ToListAsync(ct);
        foreach (var s in sesiones)
        {
            var (pub, nombre) = oficinas.GetValueOrDefault(s.BranchId, (Guid.Empty, string.Empty));
            lista.Add(new EfectivoEnCustodiaDto(pub, nombre, IEfectivoDeRecaudo.ClaseSesion, s.Caja, await SesionesDeCaja.EfectivoAsync(db, s.Sesion, ct)));
        }
        return lista;
    }
}
