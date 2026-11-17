using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Tablero;

/// <summary>De dónde salió el tablero que se muestra: el que el usuario armó, el de su rol o el sugerido.</summary>
public enum OrigenDelTablero
{
    Usuario = 1,
    Rol = 2,
    Sugerido = 3,
}

/// <summary>El tablero de quien entra: sus tarjetas (sólo las que puede ver), de dónde salieron y todo lo que puede agregar.</summary>
public sealed record MiTableroDto(
    OrigenDelTablero Origen,
    string? Rol,
    IReadOnlyList<TarjetaDelTablero> Tarjetas,
    IReadOnlyList<IndicadorDisponibleDto> Disponibles);

/// <summary>Una cifra calculada. <paramref name="SoloLoMio"/> dice con qué alcance salió: la tarjeta lo muestra siempre.</summary>
public sealed record ValorDeIndicadorDto(
    string Codigo,
    string Titulo,
    TipoDeIndicador Tipo,
    FormatoDeIndicador Formato,
    bool SoloLoMio,
    decimal? Valor,
    decimal? Anterior,
    string? EtiquetaDelAnterior,
    IReadOnlyList<PuntoDeSerie> Serie,
    string? Nota,
    string? Ruta);

/// <summary>Un pendiente con su cuenta (sólo los mayores que cero).</summary>
public sealed record PendienteDto(string Codigo, string Titulo, string Grupo, int Cantidad, string Ruta);

/// <summary>Los errores del tablero.</summary>
public static class ErroresDelTablero
{
    public static Error IndicadorNoExiste(string codigo) => new("Dashboard.IndicatorNotFound", $"El indicador «{codigo}» no existe o no lo puede ver.");

    public static Error RolNoExiste(string rol) => new("Dashboard.RoleNotFound", $"El rol «{rol}» no existe.");

    public static Error TableroInvalido() => new("Dashboard.Invalid",
        $"El tablero admite hasta {CodigosDelTablero.MaximoDeTarjetas} indicadores existentes, sin repetir.");
}

/// <summary>Dónde se guarda el tablero de cada rol: una fila de <c>COR_SystemSettings</c> por rol, con su código.</summary>
public static class TableroDelRol
{
    public const string Prefijo = "Tablero.Rol.";

    public static string Clave(string codigoDelRol) => Prefijo + codigoDelRol;

    /// <summary>Cuántas tarjetas lleva el tablero sugerido (las marcadas <c>PorDefecto</c>, luego las demás).</summary>
    public const int TarjetasSugeridas = 8;
}

// -------------------------------------------------------------------------------------------------------- mi tablero --

/// <summary><c>GET /api/dashboard/mine</c>: el tablero de quien entra.</summary>
public sealed record GetMyDashboardQuery : IRequest<Result<MiTableroDto>>;

/// <summary>
/// El tablero se resuelve en orden: el que el usuario guardó (<c>ui.tablero</c>), el del primero de sus roles que tenga uno (por
/// nombre), o el sugerido. En cualquier caso sólo quedan las tarjetas que el usuario puede ver; una tarjeta con «sólo lo mío» de un
/// indicador que no lo admite se muestra con todo.
/// </summary>
public sealed class GetMyDashboardQueryHandler(
    CatalogoDelTablero catalogo,
    IAdminDbContext admin,
    IApplicationDbContext db,
    ICurrentCentralUserContext usuarioCentral,
    IActorActual actorActual)
    : IRequestHandler<GetMyDashboardQuery, Result<MiTableroDto>>
{
    public async Task<Result<MiTableroDto>> Handle(GetMyDashboardQuery request, CancellationToken ct)
    {
        var visibles = await catalogo.VisiblesAsync(ct);
        var porCodigo = visibles.ToDictionary(i => i.Definicion.Codigo, StringComparer.Ordinal);
        var disponibles = visibles.Select(i => CatalogoDelTablero.Dto(i.Definicion)).ToList();

        var (origen, rol, guardadas) = await GuardadoAsync(ct);
        IReadOnlyList<TarjetaDelTablero> tarjetas = guardadas is null
            ? Sugerido(visibles)
            : guardadas.Where(t => porCodigo.ContainsKey(t.Codigo))
                .Select(t => t with { SoloLoMio = t.SoloLoMio && porCodigo[t.Codigo].Definicion.AdmiteSoloLoMio })
                .ToList();

        return Result.Success(new MiTableroDto(guardadas is null ? OrigenDelTablero.Sugerido : origen, rol, tarjetas, disponibles));
    }

    private async Task<(OrigenDelTablero Origen, string? Rol, IReadOnlyList<TarjetaDelTablero>? Tarjetas)> GuardadoAsync(CancellationToken ct)
    {
        if (usuarioCentral.CentralUserId is { } central && usuarioCentral.ActiveTenantPublicId is { } tenant)
        {
            var propio = await admin.UserSettings.AsNoTracking()
                .Where(s => s.CentralUserId == central && s.TenantPublicId == tenant && s.SettingKey == UserSettingKeys.Tablero)
                .Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
            if (TarjetasDelTablero.Leer(propio) is { } delUsuario) return (OrigenDelTablero.Usuario, null, delUsuario);
        }

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return (OrigenDelTablero.Sugerido, null, null);

        var roles = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == usuario)
            .Join(db.Roles.AsNoTracking().Where(r => r.IsActive && !r.IsDeleted), ur => ur.RoleId, r => r.Id, (ur, r) => new { r.Code, r.Name })
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        if (roles.Count == 0) return (OrigenDelTablero.Sugerido, null, null);

        var claves = roles.Select(r => TableroDelRol.Clave(r.Code)).ToList();
        var guardados = await db.SystemSettings.AsNoTracking()
            .Where(s => claves.Contains(s.SettingKey))
            .Select(s => new { s.SettingKey, s.SettingValue })
            .ToListAsync(ct);
        foreach (var r in roles)
        {
            var fila = guardados.FirstOrDefault(g => g.SettingKey == TableroDelRol.Clave(r.Code));
            if (TarjetasDelTablero.Leer(fila?.SettingValue) is { } delRol) return (OrigenDelTablero.Rol, r.Name, delRol);
        }
        return (OrigenDelTablero.Sugerido, null, null);
    }

    /// <summary>El sugerido: los marcados por defecto que el usuario puede ver, y si no alcanzan, los demás, hasta 8.</summary>
    public static IReadOnlyList<TarjetaDelTablero> Sugerido(IReadOnlyList<IIndicadorDelTablero> visibles) =>
        visibles.OrderByDescending(i => i.Definicion.PorDefecto)
            .Take(TableroDelRol.TarjetasSugeridas)
            .Select(i => new TarjetaDelTablero(i.Definicion.Codigo))
            .ToList();
}

// ------------------------------------------------------------------------------------------------- un indicador --

/// <summary><c>GET /api/dashboard/indicators/{codigo}?soloLoMio=</c>: una cifra. Cada tarjeta pide la suya para cargar por separado.</summary>
public sealed record GetDashboardIndicatorQuery(string Codigo, bool SoloLoMio) : IRequest<Result<ValorDeIndicadorDto>>;

public sealed class GetDashboardIndicatorQueryHandler(CatalogoDelTablero catalogo, ILogger<GetDashboardIndicatorQueryHandler> log)
    : IRequestHandler<GetDashboardIndicatorQuery, Result<ValorDeIndicadorDto>>
{
    public async Task<Result<ValorDeIndicadorDto>> Handle(GetDashboardIndicatorQuery request, CancellationToken ct)
    {
        var indicador = catalogo.Buscar(request.Codigo);
        if (indicador is null || !await catalogo.PuedeVerAsync(indicador.Definicion.Permiso, ct))
            return Result.Failure<ValorDeIndicadorDto>(ErroresDelTablero.IndicadorNoExiste(request.Codigo));

        var d = indicador.Definicion;
        var soloLoMio = request.SoloLoMio && d.AdmiteSoloLoMio;
        var contexto = await catalogo.ContextoAsync(soloLoMio, ct);
        var r = await indicador.CalcularAsync(contexto, ct);
        if (r.IsFailure)
        {
            log.LogWarning("[Tablero] El indicador {Codigo} falló: {Codigo2} {Mensaje}", d.Codigo, r.Error.Code, r.Error.Message);
            return Result.Failure<ValorDeIndicadorDto>(r.Error);
        }

        var v = r.Value;
        return Result.Success(new ValorDeIndicadorDto(d.Codigo, d.Titulo, d.Tipo, d.Formato, soloLoMio, v.Valor, v.Anterior, v.EtiquetaDelAnterior,
            v.Serie ?? [], v.Nota, d.Ruta));
    }
}

// ----------------------------------------------------------------------------------------------------- pendientes --

/// <summary><c>GET /api/dashboard/pending</c>: lo que espera a quien entra (sólo lo que tiene cuenta).</summary>
public sealed record GetDashboardPendingQuery : IRequest<Result<IReadOnlyList<PendienteDto>>>;

public sealed class GetDashboardPendingQueryHandler(CatalogoDelTablero catalogo, ILogger<GetDashboardPendingQueryHandler> log)
    : IRequestHandler<GetDashboardPendingQuery, Result<IReadOnlyList<PendienteDto>>>
{
    public async Task<Result<IReadOnlyList<PendienteDto>>> Handle(GetDashboardPendingQuery request, CancellationToken ct)
    {
        var contexto = await catalogo.ContextoAsync(false, ct);
        var lista = new List<PendienteDto>();
        foreach (var p in await catalogo.PendientesVisiblesAsync(ct))
        {
            int cantidad;
            try
            {
                cantidad = await p.ContarAsync(contexto, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un pendiente que falla no tumba los demás: se omite y queda en el log.
                log.LogWarning(ex, "[Tablero] El pendiente {Codigo} falló", p.Definicion.Codigo);
                continue;
            }
            if (cantidad > 0) lista.Add(new PendienteDto(p.Definicion.Codigo, p.Definicion.Titulo, p.Definicion.Grupo, cantidad, p.Definicion.Ruta));
        }
        return Result.Success<IReadOnlyList<PendienteDto>>(lista);
    }
}
