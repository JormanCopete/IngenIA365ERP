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
/// La cooperativa del evento sale de <see cref="ICurrentTenantService"/> —el PublicId en formato N,
/// el mismo con el que <c>MongoAuditService</c> escribe los comandos y con el que la consola lee—
/// y <b>no</b> de <see cref="ICurrentUserService.TenantId"/>, que es el Id interno («3»). Hasta el
/// 2026-09-20 se usaba éste: el rastro se escribía en <c>…_Audit_3</c>, una base que nadie consulta,
/// y <c>GET /api/audit/logs</c> nunca mostró una exportación de informes. Lo fija la e2e de
/// informes (<c>Accounting.Report.Exported</c> visible en la consola tras exportar).
/// </para>
/// </summary>
public sealed class AccountingAuditEmitter(
    IAuditAppendOnlyWriter writer,
    ICurrentUserService currentUser,
    ICurrentTenantService tenant,
    IDateTimeService clock,
    ILogger<AccountingAuditEmitter> logger)
{
    public const string Modulo = "Accounting";

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
