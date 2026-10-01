using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.ElectronicInvoicing;

/// <summary>
/// Implementación de <see cref="IArrendamientoDeDocumentoElectronico"/> (feature 012, I4, T712; contracts/dian.md §6.4): el <c>UPDATE</c> del
/// contrato como <c>ExecuteUpdateAsync</c> condicionado —fila libre o vencida y, si se respeta la espera, <c>NextAttemptAt</c> cumplido—, sobre la
/// conexión y la transacción del propio contexto, portable entre PostgreSQL y SQL Server. Si la fila ya estaba seguida en el contexto, le pone al
/// día el token de concurrencia y las dos columnas del arrendamiento: sin eso el <c>SaveChanges</c> que registra el resultado respondería
/// <c>Concurrency.StaleRowVersion</c> (la lección de <c>ToqueDeSesionDeCaja</c>). (nuevo)
/// </summary>
public sealed class ArrendamientoDeDocumentosElectronicos(ApplicationDbContext db) : IArrendamientoDeDocumentoElectronico
{
    public async Task<bool> TomarAsync(int documentoId, DateTime ahora, DateTime hasta, string dueno, bool respetarEspera, CancellationToken ct)
    {
        var filas = await db.ElectronicDocuments
            .Where(d => d.Id == documentoId && (d.LeaseUntil == null || d.LeaseUntil < ahora)
                        && (!respetarEspera || d.NextAttemptAt == null || d.NextAttemptAt <= ahora))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, hasta).SetProperty(x => x.LeaseOwner, dueno), ct);
        if (filas != 1) return false;

        var seguida = db.ChangeTracker.Entries<ElectronicDocument>().FirstOrDefault(e => e.Entity.Id == documentoId);
        if (seguida is null) return true;
        var token = seguida.Metadata.GetProperties().FirstOrDefault(p => p.IsConcurrencyToken);
        if (token is not null)
        {
            var nombre = token.Name;
            var vigente = await db.ElectronicDocuments.AsNoTracking().Where(d => d.Id == documentoId)
                .Select(d => EF.Property<object>(d, nombre)).FirstAsync(ct);
            seguida.Property(nombre).OriginalValue = vigente;
            if (!seguida.Property(nombre).IsModified) seguida.Property(nombre).CurrentValue = vigente;
        }
        seguida.Property(x => x.LeaseUntil).OriginalValue = hasta;
        seguida.Property(x => x.LeaseUntil).CurrentValue = hasta;
        seguida.Property(x => x.LeaseOwner).OriginalValue = dueno;
        seguida.Property(x => x.LeaseOwner).CurrentValue = dueno;
        return true;
    }
}
