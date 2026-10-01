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
/// opcional para que las pruebas que construyen el emisor a mano sigan compilando.
/// </para>
///
/// <para>
/// Sin cooperativa resuelta <b>lanza</b> (feature 012, T495; T5, FR-083): hasta I2 el evento iba vacío a la base
/// global. Con el procesador de mensajes de Inventario escribiendo en segundo plano, eso escondería un trabajo que
/// corrió fuera de <c>IEjecutorEnCooperativa</c>; y nunca va al Id interno, que sería una base fantasma. La
/// comprobación va fuera del <c>try</c>: es un defecto del programa, no una falla de Mongo que se tolera.
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
        var cooperativa = tenant?.TenantId;
        if (string.IsNullOrWhiteSpace(cooperativa))
            throw new InvalidOperationException(
                $"La auditoría contable de {action} no tiene cooperativa resuelta: un evento contable nunca va a la base global (FR-083). "
                + "Si corre en segundo plano, debe ir dentro de IEjecutorEnCooperativa.");
        try
        {
            await writer.AppendAsync(new AuditEventDocument(
                TenantId: cooperativa,
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

    /// <summary>Un comprobante nacido de mensajes de Inventario (por documento o resumido), con sus mensajes y su lote.</summary>
    public Task EmitirContabilizacionDeInventarioAsync(Guid accountingDocumentPublicId, object detalle, CancellationToken ct) =>
        EmitAsync(Common.Audit.AuditEventTypes.AccountingInventoryPosted, "AccountingDocument", accountingDocumentPublicId, null, detalle, ct);

    /// <summary>Un mensaje de Inventario que Contabilidad rechazó a la bandeja, con el código y el motivo.</summary>
    public Task EmitirRechazoDeInventarioAsync(Guid messagePublicId, object detalle, CancellationToken ct) =>
        EmitAsync(Common.Audit.AuditEventTypes.AccountingInventoryRejected, "IntegrationMessage", messagePublicId, null, detalle, ct);

    /// <summary>Un lote de integración cerrado, con su estado y sus totales.</summary>
    public Task EmitirLoteDeInventarioProcesadoAsync(Guid batchPublicId, object detalle, CancellationToken ct) =>
        EmitAsync(Common.Audit.AuditEventTypes.AccountingInventoryBatchProcessed, "IntegrationBatch", batchPublicId, null, detalle, ct);

    /// <summary>Exportación de un informe: qué informe, con qué filtros, en qué formato.</summary>
    public Task EmitirExportacionAsync(string informe, object filtros, string formato, int filas, CancellationToken ct) =>
        EmitAsync(Common.Audit.AuditEventTypes.AccountingReportExported, "AccountingReport", null, null,
            new { informe, filtros, formato, filas }, ct);
}
