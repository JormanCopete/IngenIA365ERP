using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Security.Scopes;

/// <summary>
/// La implementación real de <see cref="IAsignacionesDeBodega"/> (feature 012, T224; T35, FR-009; data-model §21) sobre
/// <c>INV_UserWarehouseScopes</c>: reemplaza a <see cref="SinAsignacionesDeBodega"/> en el contenedor, así
/// <c>AlcanceDeInventarioDeLaPeticion</c> (T089) y el <c>PUT /api/inventory/scopes/users/{userPublicId}</c> (T090) leen y
/// persisten por el mismo puerto sin cambios. Lee sólo filas vivas; reemplazar da de baja lógica las retiradas, agrega las
/// nuevas y deja a lo sumo una por defecto (<c>Inventory.Scope.DefaultDuplicate</c>). No guarda: guarda el comando. (nuevo)
/// </summary>
public sealed class AsignacionesDeBodegaEnBase(IApplicationDbContext db, IDateTimeService reloj) : IAsignacionesDeBodega
{
    public async Task<AsignacionesDeAlcance> BodegasDelUsuarioAsync(int userId, CancellationToken ct)
    {
        var filas = await db.UserWarehouseScopes.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Join(db.Warehouses.AsNoTracking(), s => s.WarehouseId, w => w.Id,
                (s, w) => new { w.Id, w.PublicId, w.Code, w.Name, s.IsDefault })
            .OrderBy(x => x.Code)
            .ToListAsync(ct);
        return new AsignacionesDeAlcance(filas.Select(f => new AsignacionDeAlcance(new ElementoDeAlcance(f.Id, f.PublicId, f.Code, f.Name), f.IsDefault)).ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>> BuscarAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct)
    {
        if (publicIds.Count == 0) return new Dictionary<Guid, ElementoDeAlcance>();
        return await db.Warehouses.AsNoTracking()
            .Where(w => publicIds.Contains(w.PublicId))
            .Select(w => new ElementoDeAlcance(w.Id, w.PublicId, w.Code, w.Name))
            .ToDictionaryAsync(e => e.PublicId, ct);
    }

    public async Task<Result> ReemplazarAsync(int userId, IReadOnlyList<AsignacionPedida> asignaciones, CancellationToken ct)
    {
        if (asignaciones.Count(a => a.IsDefault) > 1) return Result.Failure(ErroresDeAlcance.PorDefectoRepetido("warehouse"));
        var pedidas = asignaciones.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.Any(a => a.IsDefault));
        var ids = pedidas.Keys.ToList();
        var existen = await db.Warehouses.Where(w => ids.Contains(w.Id)).Select(w => w.Id).ToListAsync(ct);
        if (existen.Count != ids.Count) return Result.Failure(ErroresDeAlcance.BodegaInexistente());

        var vigentes = await db.UserWarehouseScopes.Where(s => s.UserId == userId).ToListAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var vieja in vigentes)
        {
            if (pedidas.TryGetValue(vieja.WarehouseId, out var porDefecto))
            {
                vieja.IsDefault = porDefecto;
                continue;
            }
            vieja.IsDeleted = true;
            vieja.DeletedAt = ahora;
            vieja.IsDefault = false;
        }
        foreach (var (bodegaId, porDefecto) in pedidas.Where(p => vigentes.All(v => v.WarehouseId != p.Key)))
            db.UserWarehouseScopes.Add(new UserWarehouseScope { UserId = userId, WarehouseId = bodegaId, IsDefault = porDefecto });
        return Result.Success();
    }
}
