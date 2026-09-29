using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing.Common;

/// <summary>Una línea de la recepción enlazada a la línea de orden que recibe. (nuevo)</summary>
public sealed record ParDeOrden(InventoryDocumentLine LineaDeOrden, InventoryDocumentLine LineaDeRecepcion);

/// <summary>
/// Las reglas de la recepción contra orden (feature 012, I5, T789; FR-049; contracts/api.md §14.9; data-model §5.5), en un solo sitio
/// para el borrador (que las devuelve como aviso lo que depende de otros documentos) y la confirmación (que las exige, y las vuelve a
/// mirar dentro del cerrojo, donde la orden está bloqueada como documento de origen):
/// <list type="bullet">
/// <item>la orden es del mismo proveedor que la recepción (<c>Inventory.Purchase.OrderFromOtherSupplier</c>);</item>
/// <item>la orden está confirmada y con su saldo abierto (<c>Inventory.PurchaseOrder.NotOpen</c>);</item>
/// <item>Σ lo recibido por línea de orden —las otras recepciones vigentes más ésta— cabe en lo ordenado más la tolerancia de cantidad
/// vigente a la fecha de operación (<see cref="CruceDeCompra.Recepcion"/>; si no, <c>Inventory.Purchase.OverReceiptBeyondTolerance</c>
/// con <c>{ lineNumber, ordered, received, tolerance }</c>).</item>
/// </list>
/// Lo recibido de menos es una recepción parcial: la orden sigue abierta por lo que falta. (nuevo)
/// </summary>
public sealed class RecepcionContraOrden(IApplicationDbContext db, PendientesDeCompra pendientes, ILectorDeParametros parametros)
{
    /// <summary>Los pares (línea de orden, línea de recepción) que la recepción ya guardó en sus vínculos <c>FromOrder</c>.</summary>
    public async Task<IReadOnlyList<ParDeOrden>> ParesAsync(InventoryDocument recepcion, CancellationToken ct)
    {
        var lineas = recepcion.Lines.Where(l => !l.IsDeleted).ToDictionary(l => l.Id);
        if (lineas.Count == 0 || recepcion.Id == 0) return [];
        var ids = lineas.Keys.ToList();
        var vinculos = await (
                from x in db.DocumentLineLinks.AsNoTracking()
                join l in db.DocumentLinks.AsNoTracking() on x.DocumentLinkId equals l.Id
                where ids.Contains(x.TargetLineId) && !x.IsDeleted && !l.IsDeleted && l.Kind == DocumentLinkKind.FromOrder
                select new { x.SourceLineId, x.TargetLineId })
            .ToListAsync(ct);
        if (vinculos.Count == 0) return [];
        var origenes = vinculos.Select(v => v.SourceLineId).Distinct().ToList();
        var deOrden = await db.InventoryDocumentLines.AsNoTracking().Where(l => origenes.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        return vinculos.Where(v => deOrden.ContainsKey(v.SourceLineId))
            .Select(v => new ParDeOrden(deOrden[v.SourceLineId], lineas[v.TargetLineId]))
            .OrderBy(p => p.LineaDeRecepcion.LineNumber)
            .ToList();
    }

    /// <summary>
    /// Proveedor y orden abierta, leídos de la base en el momento (dentro del cerrojo ven lo que otra confirmación ya dejó). Devuelve
    /// las órdenes (sin seguir) o el primer error.
    /// </summary>
    public async Task<Result<IReadOnlyList<InventoryDocument>>> OrdenesAsync(InventoryDocument recepcion, IReadOnlyList<ParDeOrden> pares, CancellationToken ct)
    {
        if (pares.Count == 0) return Result.Success<IReadOnlyList<InventoryDocument>>([]);
        var ids = pares.Select(p => p.LineaDeOrden.DocumentId).Distinct().ToList();
        var ordenes = await db.InventoryDocuments.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        foreach (var par in pares)
        {
            if (!ordenes.TryGetValue(par.LineaDeOrden.DocumentId, out var orden) || orden.Class != DocumentClass.PurchaseOrder)
                return Result.Failure<IReadOnlyList<InventoryDocument>>(ErroresDeCompras.OrigenDeOtraClase(par.LineaDeRecepcion.LineNumber, "una orden de compra"));
            var numero = VistaDeDocumentos.NumeroVisible(orden.Prefix, orden.Number);
            if (orden.CounterpartyPersonId != recepcion.CounterpartyPersonId)
                return Result.Failure<IReadOnlyList<InventoryDocument>>(ErroresDeCompras.OrderFromOtherSupplier(par.LineaDeRecepcion.LineNumber, orden.PublicId, numero));
            if (!orden.OrdenAbierta)
                return Result.Failure<IReadOnlyList<InventoryDocument>>(
                    ErroresDeCompras.OrderNotOpen(orden.PublicId, numero, orden.Status, orden.BalanceClosedAt, par.LineaDeRecepcion.LineNumber));
        }
        return Result.Success<IReadOnlyList<InventoryDocument>>(ordenes.Values.OrderBy(o => o.Id).ToList());
    }

    /// <summary>
    /// Lo recibido de más fuera de la tolerancia, por línea de orden: una falla por cada una que no cabe (vacío si todo cabe). Si las
    /// tolerancias no se pueden leer, ese error.
    /// </summary>
    public async Task<Result<IReadOnlyList<Error>>> RecibidoDeMasAsync(InventoryDocument recepcion, IReadOnlyList<ParDeOrden> pares, CancellationToken ct)
    {
        if (pares.Count == 0) return Result.Success<IReadOnlyList<Error>>([]);
        var tolerancias = await ToleranciasVigentesDeCompra.LeerAsync(parametros, recepcion.OperationDate, ct);
        if (tolerancias.IsFailure) return Result.Failure<IReadOnlyList<Error>>(tolerancias.Error);

        var porLinea = pares.GroupBy(p => p.LineaDeOrden.Id).ToList();
        var ya = await pendientes.ConsumidoAsync(porLinea.Select(g => g.Key).ToList(), recepcion.Id, ct);
        var errores = new List<Error>();
        foreach (var grupo in porLinea)
        {
            var orden = grupo.First().LineaDeOrden;
            var esta = grupo.Sum(p => p.LineaDeRecepcion.QuantityBase);
            if (orden.QuantityBase <= 0m || esta <= 0m) continue;
            var r = CruceDeCompra.Recepcion(new PedidoDeRecepcionContraOrden(orden.QuantityBase, ya.GetValueOrDefault(orden.Id), esta, tolerancias.Value));
            if (!r.Admitida)
                errores.Add(ErroresDeCompras.OverReceiptBeyondTolerance(grupo.Min(p => p.LineaDeRecepcion.LineNumber), orden.QuantityBase, r.Recibido, r.Tolerancia));
        }
        return Result.Success<IReadOnlyList<Error>>(errores);
    }

    /// <summary>Todas las reglas, para la confirmación: las órdenes que bloquear, o la primera falla.</summary>
    public async Task<Result<IReadOnlyList<InventoryDocument>>> ExigirAsync(InventoryDocument recepcion, CancellationToken ct)
    {
        var pares = await ParesAsync(recepcion, ct);
        var ordenes = await OrdenesAsync(recepcion, pares, ct);
        if (ordenes.IsFailure) return ordenes;
        var demas = await RecibidoDeMasAsync(recepcion, pares, ct);
        if (demas.IsFailure) return Result.Failure<IReadOnlyList<InventoryDocument>>(demas.Error);
        return demas.Value.Count > 0 ? Result.Failure<IReadOnlyList<InventoryDocument>>(demas.Value[0]) : ordenes;
    }
}
