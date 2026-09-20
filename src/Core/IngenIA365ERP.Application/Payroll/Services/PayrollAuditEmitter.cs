using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Evento explícito de auditoría (Principio X; contracts/permissions.md §Auditoría) para
/// los comandos de nómina con efecto contable o sobre datos de personas. El
/// <c>AuditBehavior</c> genérico guarda el comando tal cual; esto guarda además la
/// entidad, y los valores antes y después. Si la escritura falla no tumba el comando,
/// pero lo deja en el log con la acción y el motivo (nunca en silencio, Principio IX).
/// La cooperativa del evento es la de <see cref="ICurrentTenantService"/> (PublicId en formato N,
/// la base que la consola lee), no el Id interno de <see cref="ICurrentUserService.TenantId"/>:
/// con éste, hasta el 2026-09-20, los eventos explícitos de nómina iban a una base que nadie
/// consultaba (ver <c>AccountingAuditEmitter</c>).
/// </summary>
public sealed class PayrollAuditEmitter(
    IAuditAppendOnlyWriter writer,
    ICurrentUserService currentUser,
    ICurrentTenantService tenant,
    IDateTimeService clock,
    ILogger<PayrollAuditEmitter> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task EmitAsync(string action, string entityType, Guid? entityPublicId, object? before, object? after, CancellationToken ct)
    {
        try
        {
            await writer.AppendAsync(new AuditEventDocument(
                TenantId: tenant.TenantId ?? string.Empty,
                UserId: currentUser.UserId?.ToString() ?? string.Empty,
                UserName: currentUser.UserName,
                Action: action,
                EntityType: entityType,
                EntityPublicId: entityPublicId?.ToString(),
                Module: "Payroll",
                OldValuesJson: before is null ? null : JsonSerializer.Serialize(before, Json),
                NewValuesJson: after is null ? null : JsonSerializer.Serialize(after, Json),
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: null,
                HttpMethod: null,
                HttpStatusCode: null,
                DurationMs: null,
                OccurredAt: clock.UtcNow), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "La auditoría explícita de nómina falló para {Action} sobre {EntityType} {EntityPublicId}",
                action, entityType, entityPublicId);
        }
    }
}
