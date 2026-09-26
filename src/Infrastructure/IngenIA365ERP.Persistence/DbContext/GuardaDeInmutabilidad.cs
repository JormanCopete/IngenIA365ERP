using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IngenIA365ERP.Persistence.DbContext;

/// <summary>
/// Hace cumplir la inmutabilidad antes de escribir (feature 012, T17, T18, T137; Principio XI). La llama
/// <see cref="ApplicationDbContext.SaveChangesAsync(CancellationToken)"/> antes que los interceptores (el de borrado
/// lógico todavía no convirtió un <c>Deleted</c> en <c>Modified</c>):
/// <list type="bullet">
/// <item>todo <see cref="IHechoInmutable"/> sólo se inserta: un <c>Modified</c> o <c>Deleted</c> se rechaza;</item>
/// <item>un <see cref="IInmutableTrasConfirmar"/> cuyo estado <b>original</b> ya lo fija (confirmado o anulado) sólo
/// cambia <see cref="IInmutableTrasConfirmar.PropiedadesMutablesTrasConfirmar"/>, y su estado sólo de <c>Confirmed</c> a
/// <c>Voided</c>; no se borra;</item>
/// <item>las líneas de un documento fijo no cambian ni se borran (las de un borrador que se confirma en este mismo
/// guardado sí: la confirmación escribe su costo y ubicación).</item>
/// </list>
/// Lanza <see cref="ImmutableEntityModifiedException"/> con la entidad y la propiedad. Todo tipo que implemente uno de
/// los dos marcadores queda cubierto por el marcador mismo (no hay lista que mantener); lo vigila
/// <c>LosHechosInmutablesNoSeModifican</c>. (nuevo)
/// </summary>
public static class GuardaDeInmutabilidad
{
    public static async Task VerificarAsync(Microsoft.EntityFrameworkCore.DbContext db, CancellationToken ct = default)
    {
        var lineas = Revisar(db.ChangeTracker.Entries());
        if (lineas.Count == 0) return;

        // Estado original del documento de cada línea cambiada: el del seguimiento si está cargado; si no, el de la base.
        var estados = new Dictionary<int, DocumentStatus>();
        foreach (var doc in db.ChangeTracker.Entries<InventoryDocument>())
        {
            var id = doc.Entity.Id;
            if (lineas.ContainsKey(id) && doc.State != EntityState.Added)
                estados[id] = (DocumentStatus)doc.OriginalValues[nameof(InventoryDocument.Status)]!;
        }

        var faltan = lineas.Keys.Where(id => !estados.ContainsKey(id)).ToList();
        if (faltan.Count > 0)
        {
            var leidos = await db.Set<InventoryDocument>().IgnoreQueryFilters().AsNoTracking()
                .Where(d => faltan.Contains(d.Id))
                .Select(d => new { d.Id, d.Status })
                .ToListAsync(ct);
            foreach (var d in leidos) estados[d.Id] = d.Status;
        }

        foreach (var (documentoId, linea) in lineas)
        {
            if (estados.TryGetValue(documentoId, out var estado) && IInmutableTrasConfirmar.EstaFijo(estado))
            {
                var propiedad = linea.State == EntityState.Deleted
                    ? null
                    : linea.Properties.FirstOrDefault(p => p.IsModified)?.Metadata.Name;
                throw new ImmutableEntityModifiedException(nameof(InventoryDocumentLine), propiedad,
                    $"el documento {documentoId} está {estado} y sus líneas ya no cambian.");
            }
        }
    }

    /// <summary>
    /// Revisa hechos y documentos fijos (lanza si algo no se admite) y devuelve las líneas de documento cambiadas o
    /// borradas, por documento, para comprobar el estado de su cabecera.
    /// </summary>
    public static Dictionary<int, EntityEntry> Revisar(IEnumerable<EntityEntry> entradas)
    {
        var lineas = new Dictionary<int, EntityEntry>();
        foreach (var entrada in entradas)
        {
            if (entrada.State is not (EntityState.Modified or EntityState.Deleted)) continue;
            var tipo = entrada.Metadata.ClrType.Name;

            switch (entrada.Entity)
            {
                case IHechoInmutable:
                    if (entrada.State == EntityState.Deleted)
                        throw new ImmutableEntityModifiedException(tipo, null, "es un hecho: no se borra.");
                    var cambiada = entrada.Properties.FirstOrDefault(p => p.IsModified);
                    throw new ImmutableEntityModifiedException(tipo, cambiada?.Metadata.Name, "es un hecho: sólo se inserta.");

                case IInmutableTrasConfirmar:
                    RevisarDocumento(entrada, tipo);
                    break;

                case InventoryDocumentLine linea:
                    lineas.TryAdd(linea.DocumentId, entrada);
                    break;
            }
        }
        return lineas;
    }

    private static void RevisarDocumento(EntityEntry entrada, string tipo)
    {
        var original = (DocumentStatus)entrada.OriginalValues[nameof(IInmutableTrasConfirmar.Status)]!;
        if (!IInmutableTrasConfirmar.EstaFijo(original)) return;

        if (entrada.State == EntityState.Deleted)
            throw new ImmutableEntityModifiedException(tipo, null, $"está {original}: no se borra; se anula con su documento contrario.");

        foreach (var propiedad in entrada.Properties.Where(p => p.IsModified))
        {
            var nombre = propiedad.Metadata.Name;
            if (!IInmutableTrasConfirmar.PropiedadesMutablesTrasConfirmar.Contains(nombre))
                throw new ImmutableEntityModifiedException(tipo, nombre, $"el documento está {original}.");

            if (nombre == nameof(IInmutableTrasConfirmar.Status))
            {
                var actual = (DocumentStatus)propiedad.CurrentValue!;
                if (actual != original && !(original == DocumentStatus.Confirmed && actual == DocumentStatus.Voided))
                    throw new ImmutableEntityModifiedException(tipo, nombre, $"de {original} sólo se pasa a {DocumentStatus.Voided} (no a {actual}).");
            }
        }
    }
}
