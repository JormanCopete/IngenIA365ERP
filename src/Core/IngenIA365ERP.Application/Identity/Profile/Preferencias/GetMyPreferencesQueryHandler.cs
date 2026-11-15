using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.Preferencias;

public sealed class GetMyPreferencesQueryHandler(
    IAdminDbContext db,
    ICurrentCentralUserContext currentUser,
    ILogger<GetMyPreferencesQueryHandler> logger)
    : IRequestHandler<GetMyPreferencesQuery, Result<PreferenciasUsuarioDto>>
{
    public async Task<Result<PreferenciasUsuarioDto>> Handle(
        GetMyPreferencesQuery request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure<PreferenciasUsuarioDto>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");

        var centralUserId = currentUser.CentralUserId.Value;
        var tenant = currentUser.ActiveTenantPublicId ?? Guid.Empty;

        // Una sola consulta para los dos ámbitos. Guid.Empty es el ámbito
        // global; cuando el usuario todavía no eligió cooperativa, ambos
        // términos coinciden y la comparación sigue siendo válida.
        var filas = await db.UserSettings
            .AsNoTracking()
            .Where(s => s.CentralUserId == centralUserId
                        && (s.TenantPublicId == Guid.Empty || s.TenantPublicId == tenant))
            .Select(s => new { s.SettingKey, s.SettingValue, s.TenantPublicId })
            .ToListAsync(ct);

        // Si por lo que sea existieran las dos filas para una misma clave, manda
        // la de la cooperativa: es la más específica.
        string? Valor(string clave) => filas
            .Where(f => f.SettingKey == clave)
            .OrderByDescending(f => f.TenantPublicId != Guid.Empty)
            .Select(f => f.SettingValue)
            .FirstOrDefault();

        return Result.Success(new PreferenciasUsuarioDto(
            Tema: Valor(UserSettingKeys.Tema),
            Densidad: Valor(UserSettingKeys.Densidad),
            Escala: Valor(UserSettingKeys.Escala),
            Contraste: Valor(UserSettingKeys.Contraste),
            PaginaInicio: Valor(UserSettingKeys.PaginaInicio),
            Favoritos: LeerFavoritos(Valor(UserSettingKeys.Favoritos), centralUserId),
            // Feature 022 (T031): sólo «no» lo apaga; sin fila (o vacía) queda encendido.
            EnterAvanza: Valor(UserSettingKeys.EnterAvanza) != "no",
            Accesos: LeerAccesos(Valor(UserSettingKeys.Accesos), centralUserId),
            // 2026-11-13: las columnas del estado de cuenta (vacía = las de por defecto, que decide la pantalla).
            ColumnasEstadoDeCuenta: LeerFavoritos(Valor(UserSettingKeys.ColumnasEstadoDeCuenta), centralUserId)));
    }

    private IReadOnlyList<AccesoDirectoDto> LeerAccesos(string? json, Guid centralUserId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<AccesoDirectoDto>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch (JsonException ex)
        {
            // Como los favoritos: una fila ilegible no impide entrar; queda el rastro.
            logger.LogWarning(ex, "Accesos directos ilegibles para el usuario central {UsuarioId}; se devuelve lista vacía.", centralUserId);
            return [];
        }
    }

    private IReadOnlyList<string> LeerFavoritos(string? json, Guid centralUserId)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException ex)
        {
            // Devolver la lista vacía y no propagar: un JSON corrupto no puede
            // impedir que el usuario entre a la aplicación. Se registra para
            // que quede rastro de que la fila está mal, no se silencia.
            logger.LogWarning(ex,
                "Favoritos ilegibles para el usuario central {UsuarioId}; se devuelve lista vacía.",
                centralUserId);
            return [];
        }
    }
}
