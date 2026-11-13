using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// El centro de costo que un documento de venta toma cuando no trae uno propio. La matriz contable de inventario usa el centro del
/// documento como filtro de sus reglas (<c>ResolutorDeReglas</c>): una cuenta de ingreso que exige centro tiene reglas con centro, y
/// una venta sin centro no encontraba ninguna (<c>Inventory.Prevalidation.NotPostable</c>).
/// <list type="number">
/// <item>Un documento con centro propio lo conserva (la nota crédito copia el del original).</item>
/// <item>Una venta de un punto toma el del punto (<c>INV_PointsOfSale.CostCenterId</c>; 2026-11-08, el POS).</item>
/// <item>Una venta sin punto —la factura de oficina, el ciclo comercial, sus notas— toma el del parámetro
/// <c>Ventas.CentroDeCostoSinPuntoDeVenta</c> (el código de la cooperativa, <c>LegacyCode</c>; 2026-11-13: en producción ninguna
/// factura de <c>/ventas/facturas/nueva</c> se había podido confirmar). Un punto sin centro también cae al parámetro.</item>
/// </list>
/// Vacío, o un código que no existe, deja el documento sin centro: la validación previa lo dice. Sólo ventas: la anulación refleja al
/// original tal cual y los movimientos de caja no son ventas. Nunca guarda. (nuevo)
/// </summary>
public static class CentroDeCostoDelPunto
{
    public static bool Aplica(InventoryDocument documento) =>
        documento.CostCenterId is null
        && documento.Class != DocumentClass.Voiding
        && ClasesDeDocumento.GrupoDe(documento.Class, null) == DocumentClassGroup.Sales;

    /// <summary>Sólo el del punto (sin leer el parámetro): lo que hacía hasta el 2026-11-13.</summary>
    public static Task CompletarAsync(IApplicationDbContext db, InventoryDocument documento, CancellationToken ct) =>
        CompletarAsync(db, documento, null, default, ct);

    /// <summary>
    /// Si el documento es una venta sin centro, le pone el de su punto o, si no lo tiene, el de
    /// <c>Ventas.CentroDeCostoSinPuntoDeVenta</c> vigente a <paramref name="fecha"/> (con <paramref name="parametros"/> nulo, sólo el del punto).
    /// </summary>
    public static async Task CompletarAsync(IApplicationDbContext db, InventoryDocument documento, ILectorDeParametros? parametros, DateOnly fecha,
        CancellationToken ct)
    {
        if (!Aplica(documento)) return;
        if (documento.PointOfSaleId is int punto)
        {
            documento.CostCenterId = await db.PointsOfSale.AsNoTracking().Where(p => p.Id == punto).Select(p => p.CostCenterId).FirstOrDefaultAsync(ct);
            if (documento.CostCenterId is not null) return;
        }
        if (parametros is null) return;
        documento.CostCenterId = await DelParametroAsync(db, parametros, fecha, ct);
    }

    /// <summary>El centro de <c>Ventas.CentroDeCostoSinPuntoDeVenta</c> a <paramref name="fecha"/>, o nulo si está vacío o no existe.</summary>
    public static async Task<int?> DelParametroAsync(IApplicationDbContext db, ILectorDeParametros parametros, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasCentroDeCostoSinPuntoDeVenta, fecha, ct: ct);
        if (leido.IsFailure || string.IsNullOrWhiteSpace(leido.Value.Texto)) return null;
        var codigo = leido.Value.Texto.Trim().ToUpperInvariant();
        return await db.CostCenters.AsNoTracking()
            .Where(c => !c.IsDeleted && c.LegacyCode != null && c.LegacyCode.ToUpper() == codigo)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
    }
}
