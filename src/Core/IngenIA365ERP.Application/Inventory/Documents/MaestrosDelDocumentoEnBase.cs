using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Los maestros del documento genérico sobre las tablas de US1 (feature 012, T209–T211; T17; data-model §5): bodegas,
/// ubicaciones, productos con su unidad base y sus alternas, causas de ajuste y canales. Reemplaza a
/// <see cref="MaestrosDelDocumentoSinCatalogo"/> en el contenedor. Nunca filtra por alcance (lo hace quien pregunta). El
/// corte de <c>INV_Setup</c> es de US3: hasta entonces <see cref="CorteAsync"/> responde «sin corte» y US3 lo completa aquí.
/// (nuevo)
/// </summary>
public sealed class MaestrosDelDocumentoEnBase(IApplicationDbContext db) : IMaestrosDelDocumento
{
    public async Task<IReadOnlyList<BodegaDelDocumento>> BodegasAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) =>
        publicIds.Count == 0 ? [] : Bodegas(await db.Warehouses.AsNoTracking().Where(w => publicIds.Contains(w.PublicId)).ToListAsync(ct));

    public async Task<IReadOnlyList<BodegaDelDocumento>> BodegasPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : Bodegas(await db.Warehouses.AsNoTracking().Where(w => ids.Contains(w.Id)).ToListAsync(ct));

    public async Task<IReadOnlyList<ProductoDelDocumento>> ProductosAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) =>
        publicIds.Count == 0 ? [] : await db.Products.AsNoTracking().Where(p => publicIds.Contains(p.PublicId))
            .Select(p => new ProductoDelDocumento(p.Id, p.PublicId, p.Code, p.Name, p.Kind == ProductKind.Inventoriable || p.Kind == ProductKind.Variant, p.Status))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductoDelDocumento>> ProductosPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Select(p => new ProductoDelDocumento(p.Id, p.PublicId, p.Code, p.Name, p.Kind == ProductKind.Inventoriable || p.Kind == ProductKind.Variant, p.Status))
            .ToListAsync(ct);

    public async Task<UnidadDelDocumento?> UnidadAsync(int productId, Guid unitPublicId, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().Include(p => p.BaseUnit).FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (producto?.BaseUnit is not { } unidadBase) return null;
        if (unidadBase.PublicId == unitPublicId)
            return new UnidadDelDocumento(unidadBase.Id, unidadBase.PublicId, unidadBase.Code, 1m, unidadBase.AllowedDecimals, unidadBase.AllowedDecimals, unidadBase.Code);

        var alterna = await db.ProductUnits.AsNoTracking().Include(u => u.Unit)
            .FirstOrDefaultAsync(u => u.ProductId == productId && u.Unit!.PublicId == unitPublicId, ct);
        return alterna?.Unit is not { } unidad ? null
            : new UnidadDelDocumento(unidad.Id, unidad.PublicId, unidad.Code, alterna.Factor, unidad.AllowedDecimals, unidadBase.AllowedDecimals, unidadBase.Code);
    }

    public async Task<IReadOnlyList<UnidadDelDocumento>> UnidadesPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.UnitsOfMeasure.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new UnidadDelDocumento(u.Id, u.PublicId, u.Code, 1m, u.AllowedDecimals, u.AllowedDecimals))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UbicacionDelDocumento>> UbicacionesAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) =>
        publicIds.Count == 0 ? [] : await db.WarehouseLocations.AsNoTracking().Where(l => publicIds.Contains(l.PublicId) && l.IsActive)
            .Select(l => new UbicacionDelDocumento(l.Id, l.PublicId, l.Code, l.Name, l.WarehouseId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UbicacionDelDocumento>> UbicacionesPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.WarehouseLocations.AsNoTracking().Where(l => ids.Contains(l.Id))
            .Select(l => new UbicacionDelDocumento(l.Id, l.PublicId, l.Code, l.Name, l.WarehouseId))
            .ToListAsync(ct);

    public async Task<ReferenciaDelCatalogo?> CausaDeAjusteAsync(Guid publicId, CancellationToken ct) =>
        await db.AdjustmentCauses.AsNoTracking().Where(c => c.PublicId == publicId && c.IsActive)
            .Select(c => new ReferenciaDelCatalogo(c.Id, c.PublicId, c.Code, c.Name)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ReferenciaDelCatalogo>> CausasDeAjustePorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.AdjustmentCauses.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new ReferenciaDelCatalogo(c.Id, c.PublicId, c.Code, c.Name)).ToListAsync(ct);

    public async Task<ReferenciaDelCatalogo?> CanalDeVentaAsync(Guid publicId, CancellationToken ct) =>
        await db.SalesChannels.AsNoTracking().Where(c => c.PublicId == publicId && c.IsActive)
            .Select(c => new ReferenciaDelCatalogo(c.Id, c.PublicId, c.Code, c.Name)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ReferenciaDelCatalogo>> CanalesDeVentaPorIdAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.SalesChannels.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new ReferenciaDelCatalogo(c.Id, c.PublicId, c.Code, c.Name)).ToListAsync(ct);

    /// <summary>El corte de <c>INV_Setup</c> lo completa US3; hasta entonces no hay restricción.</summary>
    public Task<CorteDeInventario> CorteAsync(CancellationToken ct) => Task.FromResult(CorteDeInventario.SinCorte);

    /// <summary>
    /// La bodega tal como la ve el documento: la de tránsito se considera activa cuando lo está alguna bodega de su sucursal
    /// (contracts/api.md §4.2); inactiva = dada de baja para lo nuevo.
    /// </summary>
    private IReadOnlyList<BodegaDelDocumento> Bodegas(IReadOnlyList<Warehouse> bodegas)
    {
        var sucursalesConTransito = bodegas.Where(b => b.EsTransito).Select(b => b.BranchId).Distinct().ToList();
        var sucursalesActivas = sucursalesConTransito.Count == 0 ? new HashSet<int>()
            : db.Warehouses.AsNoTracking()
                .Where(w => sucursalesConTransito.Contains(w.BranchId) && w.Behavior == WarehouseBehavior.Operational
                    && w.ActivationStatus == WarehouseActivationStatus.Active)
                .Select(w => w.BranchId).Distinct().ToHashSet();
        return bodegas.Select(b => new BodegaDelDocumento(
            b.Id, b.PublicId, b.Code, b.Name, b.BranchId, b.EsTransito,
            b.ActivationStatus == WarehouseActivationStatus.Active || (b.EsTransito && sucursalesActivas.Contains(b.BranchId)),
            !b.IsActive, b.CutoffDate)).ToList();
    }
}
