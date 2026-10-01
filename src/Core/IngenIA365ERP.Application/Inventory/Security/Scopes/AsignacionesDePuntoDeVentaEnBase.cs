using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Security;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Security.Scopes;

/// <summary>
/// La implementación real de <see cref="IAsignacionesDePuntoDeVenta"/> (feature 012, I3, T596; T35, FR-009; data-model §21) sobre
/// <c>INV_UserPointOfSaleScopes</c> (T575), con el mismo molde que <see cref="AsignacionesDeBodegaEnBase"/>: reemplaza a
/// <see cref="SinAsignacionesDePuntoDeVenta"/> en el contenedor, así <c>AlcanceDeInventarioDeLaPeticion</c> (T089, que falla
/// cerrado sin filas salvo <c>Inventory.Scope.AllPointsOfSale</c>) y el <c>PUT /api/inventory/scopes/users/{userPublicId}</c>
/// (T090) leen y persisten los puntos por el mismo puerto sin cambios. Lee sólo filas vivas de puntos vivos; reemplazar da de
/// baja lógica las retiradas, agrega las nuevas y deja a lo sumo una por defecto (<c>Inventory.Scope.DefaultDuplicate</c>). No
/// guarda: guarda el comando. (nuevo)
/// </summary>
public sealed class AsignacionesDePuntoDeVentaEnBase(IApplicationDbContext db, IDateTimeService reloj) : IAsignacionesDePuntoDeVenta
{
    public async Task<AsignacionesDeAlcance> PuntosDelUsuarioAsync(int userId, CancellationToken ct)
    {
        var filas = await db.UserPointOfSaleScopes.AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .Join(db.PointsOfSale.AsNoTracking().Where(p => !p.IsDeleted), s => s.PointOfSaleId, w => w.Id,
                (s, w) => new { w.Id, w.PublicId, w.Code, w.Name, s.IsDefault })
            .OrderBy(x => x.Code)
            .ToListAsync(ct);
        return new AsignacionesDeAlcance(filas.Select(f => new AsignacionDeAlcance(new ElementoDeAlcance(f.Id, f.PublicId, f.Code, f.Name), f.IsDefault)).ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>> BuscarAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct)
    {
        if (publicIds.Count == 0) return new Dictionary<Guid, ElementoDeAlcance>();
        return await db.PointsOfSale.AsNoTracking()
            .Where(w => publicIds.Contains(w.PublicId) && !w.IsDeleted)
            .Select(w => new ElementoDeAlcance(w.Id, w.PublicId, w.Code, w.Name))
            .ToDictionaryAsync(e => e.PublicId, ct);
    }

    public async Task<Result> ReemplazarAsync(int userId, IReadOnlyList<AsignacionPedida> asignaciones, CancellationToken ct)
    {
        if (asignaciones.Count(a => a.IsDefault) > 1) return Result.Failure(ErroresDeAlcance.PorDefectoRepetido("pointOfSale"));
        var pedidas = asignaciones.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.Any(a => a.IsDefault));
        var ids = pedidas.Keys.ToList();
        var existen = await db.PointsOfSale.Where(w => ids.Contains(w.Id) && !w.IsDeleted).Select(w => w.Id).ToListAsync(ct);
        if (existen.Count != ids.Count) return Result.Failure(ErroresDeAlcance.PuntoInexistente());

        var vigentes = await db.UserPointOfSaleScopes.Where(s => s.UserId == userId && !s.IsDeleted).ToListAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var vieja in vigentes)
        {
            if (pedidas.TryGetValue(vieja.PointOfSaleId, out var porDefecto))
            {
                vieja.IsDefault = porDefecto;
                continue;
            }
            vieja.IsDeleted = true;
            vieja.DeletedAt = ahora;
            vieja.IsDefault = false;
        }
        foreach (var (puntoId, porDefecto) in pedidas.Where(p => vigentes.All(v => v.PointOfSaleId != p.Key)))
            db.UserPointOfSaleScopes.Add(new UserPointOfSaleScope { UserId = userId, PointOfSaleId = puntoId, IsDefault = porDefecto });
        return Result.Success();
    }
}
