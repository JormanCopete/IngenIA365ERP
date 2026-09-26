using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// Lo que Inventario le aporta a la plataforma de parámetros (feature 012, T226; FR-012; contracts/api.md §7; T21). En US1
/// sólo el ámbito <see cref="ParameterScopeKind.Warehouse"/> de <see cref="IResolutorDeAmbitoDeParametro"/>: el
/// <c>scopePublicId</c> es una bodega de <c>INV_Warehouses</c> dentro del alcance de quien pide
/// (<see cref="IAlcanceDeInventario"/>); inexistente o fuera, el mismo 404 <c>Inventory.Warehouse.NotFound</c>. Con esto
/// <c>AddParameterVersionCommand</c> —y la plantilla de bodegas (T229)— guardan <c>Existencias.StockNegativoPermitido</c> por
/// bodega. Los demás ámbitos siguen sin resolverse (tipo de documento, US3 T286; punto y caja, US5) e
/// <see cref="IReglasDeParametros"/> sigue vacío hasta que US3 lo complete aquí mismo. Reemplaza a
/// <see cref="ResolutorDeAmbitoVacio"/> en el contenedor. (nuevo)
/// </summary>
public sealed class ReglasDePlataformaDeInventario(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion) : IResolutorDeAmbitoDeParametro
{
    public async Task<Result<AmbitoDeParametro>> ResolverAsync(ParameterScopeKind ambito, Guid publicId, CancellationToken ct)
    {
        if (ambito != ParameterScopeKind.Warehouse) return Result.Failure<AmbitoDeParametro>(Error.NotFound);

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == publicId)
            .Select(w => new { w.Id, w.PublicId, w.Code, w.Name }).FirstOrDefaultAsync(ct);
        return bodega is null || !alcance.IncluyeBodega(bodega.Id)
            ? Result.Failure<AmbitoDeParametro>(ErroresDeAlcance.BodegaInexistente())
            : Result.Success(new AmbitoDeParametro(ParameterScopeKind.Warehouse, bodega.Id, bodega.PublicId, bodega.Code, bodega.Name));
    }

    public async Task<IReadOnlyList<AmbitoDeParametro>> DescribirAsync(ParameterScopeKind ambito, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ambito != ParameterScopeKind.Warehouse || ids.Count == 0) return [];
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var bodegas = await db.Warehouses.AsNoTracking().Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, w.PublicId, w.Code, w.Name }).ToListAsync(ct);
        return bodegas.Where(b => alcance.IncluyeBodega(b.Id))
            .Select(b => new AmbitoDeParametro(ParameterScopeKind.Warehouse, b.Id, b.PublicId, b.Code, b.Name))
            .ToList();
    }
}
