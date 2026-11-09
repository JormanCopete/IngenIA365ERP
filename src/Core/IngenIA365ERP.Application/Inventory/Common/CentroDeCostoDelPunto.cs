using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// El centro de costo que un documento de venta hereda de su punto de venta (<c>INV_PointsOfSale.CostCenterId</c>). La matriz
/// contable de inventario usa el centro del documento como filtro de sus reglas (<c>ResolutorDeReglas</c>): una cuenta de ingreso que
/// exige centro tiene reglas con centro, y una venta POS sin centro no encontraba ninguna (<c>Inventory.Prevalidation.NotPostable</c>).
/// Un documento con centro propio lo conserva (la nota crédito copia el del original); sin punto, o con un punto sin centro, nada
/// cambia. Sólo ventas: la anulación refleja al original tal cual y los movimientos de caja no son ventas. (nuevo)
/// </summary>
public static class CentroDeCostoDelPunto
{
    public static bool Aplica(InventoryDocument documento) =>
        documento.CostCenterId is null
        && documento.PointOfSaleId is not null
        && documento.Class != DocumentClass.Voiding
        && ClasesDeDocumento.GrupoDe(documento.Class, null) == DocumentClassGroup.Sales;

    /// <summary>Si el documento es una venta de un punto y no trae centro, le pone el del punto (si el punto tiene uno).</summary>
    public static async Task CompletarAsync(IApplicationDbContext db, InventoryDocument documento, CancellationToken ct)
    {
        if (!Aplica(documento)) return;
        var punto = documento.PointOfSaleId!.Value;
        documento.CostCenterId = await db.PointsOfSale.AsNoTracking().Where(p => p.Id == punto).Select(p => p.CostCenterId).FirstOrDefaultAsync(ct);
    }
}
