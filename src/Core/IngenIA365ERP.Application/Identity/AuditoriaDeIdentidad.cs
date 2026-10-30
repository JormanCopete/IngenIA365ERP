using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity;

/// <summary>
/// Los eventos explícitos de la identidad anclada al documento (feature 017, research R13, FR-044).
/// Lo que no es de una cooperativa —documento, correo, fusión, rescate— va a la auditoría global
/// (<c>TenantId</c> vacío → <c>{prefijo}_Global</c>); lo que sí lo es —invitación, revisión,
/// corrección del administrador, enlace de recuperación— va a la de esa cooperativa, por su
/// <c>PublicId</c> en formato «N», como los demás eventos de identidad.
/// <para>
/// Nunca tumba el flujo: si Mongo falla queda el aviso en el registro y el <c>AuditBehavior</c>
/// genérico deja el nombre del comando.
/// </para>
/// </summary>
public sealed class AuditoriaDeIdentidad(IAuditAppendOnlyWriter escritor, ILogger<AuditoriaDeIdentidad> logger)
{
    /// <summary>Un evento que no pertenece a ninguna cooperativa.</summary>
    public Task GlobalAsync(
        string accion, Guid actorId, string? actorNombre, string tipoDeEntidad, Guid entidad,
        object? anterior, object? nuevo, DateTime cuando, CancellationToken ct) =>
        EscribirAsync(string.Empty, accion, actorId, actorNombre, tipoDeEntidad, entidad, anterior, nuevo, cuando, ct);

    /// <summary>Un evento de una cooperativa.</summary>
    public Task EnCooperativaAsync(
        Guid cooperativa, string accion, Guid actorId, string? actorNombre, string tipoDeEntidad, Guid entidad,
        object? anterior, object? nuevo, DateTime cuando, CancellationToken ct) =>
        EscribirAsync(cooperativa.ToString("N"), accion, actorId, actorNombre, tipoDeEntidad, entidad, anterior, nuevo, cuando, ct);

    private async Task EscribirAsync(
        string tenantId, string accion, Guid actorId, string? actorNombre, string tipoDeEntidad, Guid entidad,
        object? anterior, object? nuevo, DateTime cuando, CancellationToken ct)
    {
        try
        {
            await escritor.AppendAsync(new AuditEventDocument(
                TenantId: tenantId,
                UserId: actorId.ToString("N"),
                UserName: actorNombre ?? string.Empty,
                Action: accion,
                EntityType: tipoDeEntidad,
                EntityPublicId: entidad.ToString("N"),
                Module: "Identity",
                OldValuesJson: anterior is null ? null : JsonSerializer.Serialize(anterior),
                NewValuesJson: nuevo is null ? null : JsonSerializer.Serialize(nuevo),
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: null,
                HttpMethod: null,
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: cuando), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditAppendOnlyWriter falló para la acción de identidad {Accion}", accion);
        }
    }
}
