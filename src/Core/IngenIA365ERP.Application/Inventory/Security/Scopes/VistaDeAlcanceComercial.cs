using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Security.Scopes;

/// <summary>El alcance comercial de un usuario (contracts/api.md §16.3). (nuevo)</summary>
public sealed record UserCommercialScopeDto(
    ScopeUserDto User,
    bool HasAllWarehouses,
    bool HasAllPointsOfSale,
    IReadOnlyList<WarehouseScopeDto> Warehouses,
    IReadOnlyList<PointOfSaleScopeDto> PointsOfSale);

public sealed record ScopeUserDto(Guid PublicId, string Name, string? Email);

public sealed record WarehouseScopeDto(Guid WarehousePublicId, string Code, string Name, bool IsDefault);

public sealed record PointOfSaleScopeDto(Guid PointOfSalePublicId, string Code, string Name, bool IsDefault);

/// <summary>
/// Arma el <see cref="UserCommercialScopeDto"/> que devuelven la consulta y el reemplazo (feature 012, T35, T090). (nuevo)
///
/// <para>
/// Las asignaciones se leen <b>sólo</b> por los puertos (<see cref="IAsignacionesDeBodega"/>,
/// <see cref="IAsignacionesDePuntoDeVenta"/>) y se muestran sólo las que caen dentro del alcance de quien administra: lo
/// demás no existe para él (el mismo criterio del 404). El alcance total del usuario sale de sus permisos
/// (<c>Inventory.Scope.AllWarehouses</c>, <c>Inventory.Scope.AllPointsOfSale</c>), resueltos en la cooperativa de la
/// petición por <see cref="IAutoridadDeOtroAprobador"/>, que es la que sabe los permisos de un usuario que no es quien
/// tiene la sesión.
/// </para>
/// </summary>
public sealed class VistaDeAlcanceComercial(
    IApplicationDbContext db,
    IAsignacionesDeBodega bodegas,
    IAsignacionesDePuntoDeVenta puntos,
    IAlcanceDeInventario alcanceDeLaPeticion,
    IAutoridadDeOtroAprobador permisosDeUsuario)
{
    public const string PermisoTodasLasBodegas = "Inventory.Scope.AllWarehouses";
    public const string PermisoTodosLosPuntos = "Inventory.Scope.AllPointsOfSale";

    /// <summary>El usuario de la cooperativa por su <c>PublicId</c> (Id interno y lo que se muestra), o nulo.</summary>
    public async Task<(int Id, ScopeUserDto Dto)?> UsuarioAsync(Guid publicId, CancellationToken ct)
    {
        var fila = await db.Users.AsNoTracking()
            .Where(u => u.PublicId == publicId)
            .Select(u => new { u.Id, u.PublicId, u.Username, u.Email, u.PersonId })
            .FirstOrDefaultAsync(ct);
        if (fila is null) return null;

        var nombre = fila.Username;
        if (fila.PersonId is { } persona)
        {
            var p = await db.People.AsNoTracking().Where(x => x.Id == persona).Select(x => new { x.FirstName, x.LastName }).FirstOrDefaultAsync(ct);
            var completo = p is null ? string.Empty : $"{p.FirstName} {p.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(completo)) nombre = completo;
        }
        return (fila.Id, new ScopeUserDto(fila.PublicId, nombre, fila.Email));
    }

    public async Task<Result<UserCommercialScopeDto>> ArmarAsync(Guid userPublicId, CancellationToken ct)
    {
        if (await UsuarioAsync(userPublicId, ct) is not { } usuario) return Result.Failure<UserCommercialScopeDto>(Error.NotFound);
        return Result.Success(await ArmarAsync(usuario.Id, usuario.Dto, ct));
    }

    public async Task<UserCommercialScopeDto> ArmarAsync(int userId, ScopeUserDto usuario, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var deBodegas = await bodegas.BodegasDelUsuarioAsync(userId, ct);
        var dePuntos = await puntos.PuntosDelUsuarioAsync(userId, ct);

        return new UserCommercialScopeDto(
            usuario,
            await permisosDeUsuario.TienePermisoAsync(userId, PermisoTodasLasBodegas, ct),
            await permisosDeUsuario.TienePermisoAsync(userId, PermisoTodosLosPuntos, ct),
            deBodegas.Items.Where(a => alcance.IncluyeBodega(a.Elemento.Id))
                .OrderBy(a => a.Elemento.Code, StringComparer.Ordinal)
                .Select(a => new WarehouseScopeDto(a.Elemento.PublicId, a.Elemento.Code, a.Elemento.Name, a.IsDefault))
                .ToList(),
            dePuntos.Items.Where(a => alcance.IncluyePunto(a.Elemento.Id))
                .OrderBy(a => a.Elemento.Code, StringComparer.Ordinal)
                .Select(a => new PointOfSaleScopeDto(a.Elemento.PublicId, a.Elemento.Code, a.Elemento.Name, a.IsDefault))
                .ToList());
    }
}
