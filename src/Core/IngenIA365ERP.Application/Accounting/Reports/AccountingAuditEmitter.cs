using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Accounting.Reports;

/// <summary>
/// Evento explícito de auditoría del módulo contable (feature 009, FR-050; Principio X), molde
/// de <c>PayrollAuditEmitter</c>. El <c>AuditBehavior</c> genérico sólo ve comandos; las
/// exportaciones de informes son consultas, así que su handler emite aquí
/// <c>Accounting.Report.Exported</c> con informe, filtros y formato. También lo usan los envíos
/// de certificados, la inicialización y la validación de catálogos. Si la escritura falla no
/// tumba la operación, pero lo deja en el log con la acción y el motivo (Principio IX).
///
/// <para>
/// La base de auditoría de cada cooperativa se nombra por el <c>PublicId</c> del tenant
/// (<see cref="ICurrentTenantService.TenantId"/>), que es lo que la consola consulta. Hasta el
/// 2026-09-21 este emisor escribía con <see cref="ICurrentUserService.TenantId"/> —el Id interno— y
/// los eventos contables explícitos (cuentas, períodos, tipos de comprobante, catálogos, inicio de la
/// contabilidad) caían en una base que nadie leía: el mismo defecto que <c>PayrollAuditEmitter</c>
/// corrigió ese día y que la revisión de la feature 010 encontró aquí. El servicio de tenant es
/// opcional para que las pruebas que construyen el emisor a mano sigan compilando; sin cooperativa
/// activa el evento va vacío —la base global—, nunca al Id interno, que sería una base fantasma.
/// </para>
/// </summary>
public sealed class AccountingAuditEmitter(
    IAuditAppendOnlyWriter writer,
    ICurrentUserService currentUser,
    IDateTimeService clock,
    ILogger<AccountingAuditEmitter> logger,
    ICurrentTenantService? tenant = null)
{
    public const string Modulo = "Accounting";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task EmitAsync(string action, string entityType, Guid? entityPublicId, object? before, object? after, CancellationToken ct)
    {
        try
        {
            await writer.AppendAsync(new AuditEventDocument(
                TenantId: tenant?.TenantId ?? string.Empty,
                UserId: currentUser.UserId?.ToString() ?? string.Empty,
                UserName: currentUser.UserName,
                Action: action,
                EntityType: entityType,
                EntityPublicId: entityPublicId?.ToString(),
                Module: Modulo,
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
            logger.LogWarning(ex, "La auditoría explícita de contabilidad falló para {Action} sobre {EntityType} {EntityPublicId}",
                action, entityType, entityPublicId);
        }
    }

    /// <summary>Exportación de un informe: qué informe, con qué filtros, en qué formato.</summary>
    public Task EmitirExportacionAsync(string informe, object filtros, string formato, int filas, CancellationToken ct) =>
        EmitAsync(Common.Audit.AuditEventTypes.AccountingReportExported, "AccountingReport", null, null,
            new { informe, filtros, formato, filas }, ct);
}
