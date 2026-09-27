using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Inventory;

/// <summary>
/// Implementación de <see cref="IToqueDeSesionDeCaja"/> (feature 012, I3, T606): un <c>ExecuteUpdateAsync</c> condicionado a
/// <c>Status = Open</c> sobre la conexión y la transacción del propio <see cref="ApplicationDbContext"/>, portable entre PostgreSQL y
/// SQL Server. Si la sesión está seguida en el contexto (el cierre la carga para cerrarla), le pone al día el token de concurrencia y
/// <c>LastActivityAt</c>: el <c>UPDATE</c> cambia el <c>rowversion</c> (SQL Server) o el <c>xmin</c> (PostgreSQL), y sin esto el
/// <c>SaveChanges</c> que la cierra respondía <c>Concurrency.StaleRowVersion</c> siempre, en los dos motores (lo destapó la e2e T563,
/// 2026-09-27; las pruebas de Application usan un doble y no lo veían). (nuevo)
/// </summary>
public sealed class ToqueDeSesionDeCaja(ApplicationDbContext db) : IToqueDeSesionDeCaja
{
    public async Task<bool> TocarAsync(int cashSessionId, DateTime ahoraUtc, CancellationToken ct = default)
    {
        var filas = await db.CashSessions
            .Where(s => s.Id == cashSessionId && s.Status == CashSessionStatus.Open)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActivityAt, ahoraUtc), ct);
        if (filas != 1) return false;

        var seguida = db.ChangeTracker.Entries<Domain.Entities.Inventory.Pos.CashSession>().FirstOrDefault(e => e.Entity.Id == cashSessionId);
        if (seguida is null) return true;
        var token = seguida.Metadata.GetProperties().FirstOrDefault(p => p.IsConcurrencyToken);
        if (token is not null)
        {
            var nombre = token.Name;
            var vigente = await db.CashSessions.AsNoTracking().Where(s => s.Id == cashSessionId)
                .Select(s => EF.Property<object>(s, nombre)).FirstAsync(ct);
            seguida.Property(nombre).OriginalValue = vigente;
            if (!seguida.Property(nombre).IsModified) seguida.Property(nombre).CurrentValue = vigente;
        }
        var actividad = seguida.Property(x => x.LastActivityAt);
        actividad.OriginalValue = ahoraUtc;
        if (!actividad.IsModified) actividad.CurrentValue = ahoraUtc;
        return true;
    }
}
