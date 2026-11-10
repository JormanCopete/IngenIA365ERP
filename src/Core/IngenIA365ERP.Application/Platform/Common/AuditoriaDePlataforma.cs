using System.Text.Json;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Platform.Common;

/// <summary>
/// Quién del lado del maestro hace un cambio de plataforma (feature 020, R1, R15): sólo el administrador maestro con su sesión.
/// </summary>
public sealed record MaestroDePlataforma(Guid CentralUserId, string Nombre)
{
    public static Result<MaestroDePlataforma> De(ICurrentCentralUserContext usuario)
    {
        if (!usuario.IsAuthenticated || usuario.CentralUserId is null)
            return Result.Failure<MaestroDePlataforma>("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (!usuario.IsGlobalMasterAdmin)
            return Result.Failure<MaestroDePlataforma>("Saas.MasterOnly", "Sólo el administrador de la plataforma hace este cambio.");
        var nombre = string.IsNullOrWhiteSpace(usuario.Email) ? usuario.CentralUserId.Value.ToString("N") : usuario.Email!;
        return Result.Success(new MaestroDePlataforma(usuario.CentralUserId.Value, nombre.Length > 150 ? nombre[..150] : nombre));
    }
}

/// <summary>
/// Los eventos de auditoría de la plataforma de la 020 (módulos por cooperativa y conectores del maestro), en el flujo de
/// administración (<see cref="ModuloDeAuditoria.Admin"/>). No llevan credenciales: un conector se audita por su definición, que no
/// las tiene (R16).
/// </summary>
public static class AuditoriaDePlataforma
{
    public const string ModuloHabilitado = "Platform.Module.Enabled";
    public const string ModuloDeshabilitado = "Platform.Module.Disabled";
    public const string ConectorCreado = "Platform.Connector.Created";
    public const string ConectorActualizado = "Platform.Connector.Updated";
    public const string VersionPublicada = "Platform.Connector.VersionPublished";
    public const string ConectorHabilitado = "Platform.Connector.TenantEnabled";
    public const string ConectorDeshabilitado = "Platform.Connector.TenantDisabled";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task RegistrarAsync(
        IAuditAppendOnlyWriter? escritor, MaestroDePlataforma maestro, string accion, string entidad, Guid? publicId, Guid? cooperativa,
        object? datos, DateTime cuando, ILogger? logger, CancellationToken ct)
    {
        if (escritor is null) return;
        try
        {
            await escritor.AppendAsync(new AuditEventDocument(
                TenantId: cooperativa?.ToString() ?? string.Empty,
                UserId: maestro.CentralUserId.ToString("N"),
                UserName: maestro.Nombre,
                Action: accion,
                EntityType: entidad,
                EntityPublicId: publicId?.ToString("N"),
                Module: ModuloDeAuditoria.Admin,
                OldValuesJson: null,
                NewValuesJson: datos is null ? null : JsonSerializer.Serialize(datos, Json),
                ChangedFields: null,
                IpAddress: null, UserAgent: null, Endpoint: null, HttpMethod: null,
                HttpStatusCode: 200, DurationMs: null,
                OccurredAt: cuando), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "[Plataforma.AuditoriaFallida] {Accion}", accion);
        }
    }
}
