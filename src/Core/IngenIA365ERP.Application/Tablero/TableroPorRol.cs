using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tablero;

/// <summary>Un rol con su tablero (vacío = sus usuarios ven el sugerido).</summary>
public sealed record TableroDeRolDto(string Codigo, string Nombre, IReadOnlyList<TarjetaDelTablero> Tarjetas);

/// <summary>Los roles con sus tableros y todos los indicadores del sistema, con el permiso que cada uno exige.</summary>
public sealed record TablerosPorRolDto(IReadOnlyList<TableroDeRolDto> Roles, IReadOnlyList<IndicadorDisponibleDto> Indicadores);

/// <summary>
/// <c>GET /api/dashboard/roles</c> (<c>Security.Roles.View</c>): el administrador ve el tablero de cada rol y todos los indicadores,
/// también los que él no puede abrir; cada usuario del rol verá sólo los que sus permisos le dejan.
/// </summary>
public sealed record GetRoleDashboardsQuery : IRequest<Result<TablerosPorRolDto>>;

public sealed class GetRoleDashboardsQueryHandler(IApplicationDbContext db, CatalogoDelTablero catalogo)
    : IRequestHandler<GetRoleDashboardsQuery, Result<TablerosPorRolDto>>
{
    public async Task<Result<TablerosPorRolDto>> Handle(GetRoleDashboardsQuery request, CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().Where(r => r.IsActive && !r.IsDeleted).OrderBy(r => r.Name)
            .Select(r => new { r.Code, r.Name }).ToListAsync(ct);
        var guardados = await db.SystemSettings.AsNoTracking()
            .Where(s => s.SettingKey.StartsWith(TableroDelRol.Prefijo))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, ct);

        var lista = roles.Select(r => new TableroDeRolDto(r.Code, r.Name,
            TarjetasDelTablero.Leer(guardados.GetValueOrDefault(TableroDelRol.Clave(r.Code))) ?? [])).ToList();
        return Result.Success(new TablerosPorRolDto(lista, catalogo.Indicadores.Select(i => CatalogoDelTablero.Dto(i.Definicion)).ToList()));
    }
}

/// <summary>
/// <c>PUT /api/dashboard/roles/{rol}</c> (<c>Security.Roles.Create</c>, el permiso que administra roles): fija el tablero del rol.
/// Vacío lo quita y sus usuarios vuelven al sugerido. No toca el tablero que un usuario se armó: ése manda sobre el del rol.
/// </summary>
public sealed record SetRoleDashboardCommand(string Rol, IReadOnlyList<TarjetaDelTablero> Tarjetas) : IRequest<Result<TableroDeRolDto>>;

public sealed class SetRoleDashboardCommandHandler(IApplicationDbContext db, CatalogoDelTablero catalogo)
    : IRequestHandler<SetRoleDashboardCommand, Result<TableroDeRolDto>>
{
    public async Task<Result<TableroDeRolDto>> Handle(SetRoleDashboardCommand request, CancellationToken ct)
    {
        var rol = await db.Roles.AsNoTracking().Where(r => r.Code == request.Rol && r.IsActive && !r.IsDeleted)
            .Select(r => new { r.Code, r.Name }).FirstOrDefaultAsync(ct);
        if (rol is null) return Result.Failure<TableroDeRolDto>(ErroresDelTablero.RolNoExiste(request.Rol));

        var tarjetas = request.Tarjetas ?? [];
        if (!TarjetasDelTablero.EsValido(tarjetas) || tarjetas.Any(t => catalogo.Buscar(t.Codigo) is null))
            return Result.Failure<TableroDeRolDto>(ErroresDelTablero.TableroInvalido());
        var limpias = tarjetas.Select(t => t with { SoloLoMio = t.SoloLoMio && catalogo.Buscar(t.Codigo)!.Definicion.AdmiteSoloLoMio }).ToList();

        var clave = TableroDelRol.Clave(rol.Code);
        var fila = await db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == clave, ct);
        if (limpias.Count == 0)
        {
            if (fila is not null) db.SystemSettings.Remove(fila);
        }
        else
        {
            if (fila is null)
            {
                fila = new SystemSetting
                {
                    SettingKey = clave,
                    ValueType = "Json",
                    Description = $"Tablero de inicio del rol {rol.Name}.",
                };
                db.SystemSettings.Add(fila);
            }
            fila.SettingValue = TarjetasDelTablero.Serializar(limpias);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(new TableroDeRolDto(rol.Code, rol.Name, limpias));
    }
}
