using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Inventory;

/// <summary>
/// Implementación de <see cref="IToqueDeSesionDeCaja"/> (feature 012, I3, T606): un <c>ExecuteUpdateAsync</c> condicionado a
/// <c>Status = Open</c> sobre la conexión y la transacción del propio <see cref="ApplicationDbContext"/>, portable entre PostgreSQL y
/// SQL Server. No toca el seguimiento del contexto. (nuevo)
/// </summary>
public sealed class ToqueDeSesionDeCaja(ApplicationDbContext db) : IToqueDeSesionDeCaja
{
    public async Task<bool> TocarAsync(int cashSessionId, DateTime ahoraUtc, CancellationToken ct = default)
    {
        var filas = await db.CashSessions
            .Where(s => s.Id == cashSessionId && s.Status == CashSessionStatus.Open)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActivityAt, ahoraUtc), ct);
        return filas == 1;
    }
}
