using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

/// <summary>
/// Cómo se arma el <c>WarehouseDto</c> (feature 012, T222, T223; contracts/api.md §4.2): sucursal, tipo, la bodega de
/// tránsito de su sucursal, quién la activó, si es la bodega por defecto de quien pregunta (<c>INV_UserWarehouseScopes</c>,
/// por <see cref="IAlcanceDeInventario"/>) y el stock negativo vigente hoy para ella (<see cref="ILectorDeParametros"/>, con
/// la excepción por bodega). Lo usan los comandos y las consultas de bodegas, así todos devuelven lo mismo. (nuevo)
/// </summary>
public sealed class VistaDeBodegas(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
{
    public async Task<IReadOnlyList<WarehouseDto>> ArmarAsync(IReadOnlyList<Warehouse> bodegas, bool detalle, CancellationToken ct)
    {
        if (bodegas.Count == 0) return [];
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var hoy = reloj.HoyLocal;

        var sucursales = bodegas.Select(b => b.BranchId).Distinct().ToList();
        var ramas = await db.Branches.AsNoTracking().Where(b => sucursales.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new BranchRefDto(b.PublicId, b.LegacyCode, b.Name), ct);
        var tipos = bodegas.Select(b => b.WarehouseTypeId).Distinct().ToList();
        var porTipo = await db.WarehouseTypes.AsNoTracking().Where(t => tipos.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => new WarehouseTypeRefDto(t.PublicId, t.Code, t.Name, t.Behavior), ct);
        var transitos = (await db.Warehouses.AsNoTracking()
                .Where(w => sucursales.Contains(w.BranchId) && w.Behavior == WarehouseBehavior.Transit)
                .Select(w => new { w.BranchId, Ref = new WarehouseRefDto(w.PublicId, w.Code, w.Name) })
                .ToListAsync(ct))
            .GroupBy(x => x.BranchId).ToDictionary(g => g.Key, g => g.First().Ref);
        var activadores = bodegas.Select(b => b.ActivatedByUserId).OfType<int>().Distinct().ToList();
        var usuarios = activadores.Count == 0 ? new Dictionary<int, string>()
            : await db.Users.AsNoTracking().Where(u => activadores.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        var ids = bodegas.Select(b => b.Id).ToList();
        var ubicaciones = detalle
            ? (await db.WarehouseLocations.AsNoTracking().Where(l => ids.Contains(l.WarehouseId)).ToListAsync(ct)).ToLookup(l => l.WarehouseId)
            : null;
        var saldos = detalle
            ? (await db.InventoryDocuments.AsNoTracking()
                .Where(d => d.Class == DocumentClass.OpeningBalance && d.WarehouseId != null && ids.Contains(d.WarehouseId.Value)
                    && d.Status != DocumentStatus.Discarded && d.Status != DocumentStatus.Voided)
                .Select(d => new { d.WarehouseId, d.PublicId, d.Prefix, d.Number, d.Status, d.OperationDate })
                .ToListAsync(ct))
              .GroupBy(d => d.WarehouseId!.Value).ToDictionary(g => g.Key, g => g.First())
            : null;

        var resultado = new List<WarehouseDto>(bodegas.Count);
        foreach (var b in bodegas)
        {
            var negativo = await parametros.LeerComoAsync<bool>(ParametrosDeInventario.Modulo, ParametrosDeInventario.ExistenciasStockNegativoPermitido,
                hoy, ParameterScopeKind.Warehouse, b.Id, ct);
            var saldo = saldos is not null && saldos.TryGetValue(b.Id, out var s) ? s : null;
            resultado.Add(new WarehouseDto(
                b.PublicId, b.Code, b.Name,
                ramas.GetValueOrDefault(b.BranchId) ?? new BranchRefDto(Guid.Empty, null, string.Empty),
                porTipo[b.WarehouseTypeId],
                b.EsTransito,
                b.EsTransito ? null : transitos.GetValueOrDefault(b.BranchId),
                b.ActivationStatus, b.CutoffDate, b.ActivatedAt,
                b.ActivatedByUserId is int u ? usuarios.GetValueOrDefault(u) : null,
                b.Address, b.IsActive,
                alcance.BodegaPorDefecto == b.Id,
                negativo.IsSuccess && negativo.Value,
                ubicaciones?[b.Id].OrderByDescending(l => l.IsDefault).ThenBy(l => l.Code)
                    .Select(l => new WarehouseLocationDto(l.PublicId, l.Code, l.Name, l.IsDefault, l.IsActive)).ToList(),
                saldo is null ? null : new OpeningBalanceRefDto(saldo.PublicId,
                    saldo.Number is long n ? saldo.Prefix + n : null, saldo.Status, b.CutoffDate ?? saldo.OperationDate)));
        }
        return resultado;
    }

    public async Task<WarehouseDto> UnaAsync(Warehouse bodega, bool detalle, CancellationToken ct) => (await ArmarAsync([bodega], detalle, ct))[0];
}
