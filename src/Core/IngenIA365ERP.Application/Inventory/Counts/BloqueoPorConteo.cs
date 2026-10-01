using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// La guarda del bloqueo por conteo (feature 012, US11, T393; FR-040, US11-1; data-model §8): mientras un conteo esté abierto con
/// <c>Conteo.BloquearMovimientos</c> sellado en verdadero, ningún documento que mueva existencia puede confirmar un producto de su
/// alcance en esa bodega —origen, destino o tránsito—: <c>Inventory.Count.ProductsLocked</c> con <c>data { countPublicId,
/// displayNumber, products[] }</c>. La llama el paso 1 del flujo canónico (<see cref="ConfirmacionDeDocumento"/>, también en la
/// reentrada de la última aprobación y en la anulación) y el guardado del borrador, donde vuelve en <c>warnings[]</c>. Un conteo que
/// admite movimientos no bloquea: lo movido se suma al teórico al cerrar. Otro producto u otra bodega, pasa; descartado o cerrado el
/// conteo, sus productos quedan libres. (nuevo)
/// </summary>
public sealed class BloqueoPorConteo(IApplicationDbContext db, VistaDeConteos conteos)
{
    /// <summary>El error del primer conteo que bloquea algún producto del documento, o nulo si ninguno.</summary>
    /// <param name="documento">El documento con sus líneas.</param>
    /// <param name="claseDelOriginal">En una anulación, la clase del anulado (la que dice si mueve existencia).</param>
    public async Task<Error?> EvaluarAsync(InventoryDocument documento, DocumentClass? claseDelOriginal, CancellationToken ct)
    {
        var clase = documento.Class == DocumentClass.Voiding && claseDelOriginal is { } original ? original : documento.Class;
        if (clase == DocumentClass.PhysicalCount || !MueveExistencia(ClasesDeDocumento.De(clase).Effect)) return null;

        var bodegas = new[] { documento.WarehouseId, documento.DestinationWarehouseId, documento.TransitWarehouseId }.OfType<int>().Distinct().ToList();
        var productos = documento.Lines.Where(l => !l.IsDeleted).Select(l => l.ProductId).Distinct().ToList();
        if (bodegas.Count == 0 || productos.Count == 0) return null;

        var abiertos = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.Class == DocumentClass.PhysicalCount && d.Status == DocumentStatus.Draft && d.CountSnapshotAt != null
                && d.WarehouseId != null && bodegas.Contains(d.WarehouseId.Value) && d.Id != documento.Id)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);

        foreach (var conteo in abiertos)
        {
            if (CriterioDelConteo.De(conteo.CountScopeJson).BloqueaMovimientos != true) continue;
            var bloqueados = await conteos.ProductosEnElAlcanceAsync(conteo, productos, ct);
            if (bloqueados.Count == 0) continue;

            var codigos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => bloqueados.Contains(p.Id))
                .OrderBy(p => p.Code).Select(p => p.Code).ToListAsync(ct);
            return InventoryErrors.CountProductsLocked(conteo.PublicId, VistaDeDocumentos.NumeroVisible(conteo.Prefix, conteo.Number), codigos);
        }
        return null;
    }

    /// <summary>¿La clase mueve cantidades? (entrada, salida o las dos; no el costo solo, ni lo que no toca el inventario).</summary>
    public static bool MueveExistencia(InventoryEffect efecto) => efecto is InventoryEffect.Entry or InventoryEffect.Exit or InventoryEffect.Both;
}
