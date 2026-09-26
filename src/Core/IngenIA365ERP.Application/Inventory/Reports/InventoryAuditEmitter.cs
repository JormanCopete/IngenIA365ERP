using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// Evento explícito de auditoría del módulo de Inventario (feature 012, T44, T181; contracts/api.md §1.6 y §27;
/// Principio X), en el molde de <c>AccountingAuditEmitter</c>. El <c>AuditBehavior</c> sólo ve comandos; la exportación
/// de un informe y la descarga de un catálogo con datos son lecturas, así que se registran aquí. (nuevo)
///
/// <para>
/// A diferencia del molde contable, que escribe directo a Mongo, Inventario es un módulo <b>encadenado</b>
/// (<see cref="AuditoriaEncadenada.Modulos"/>): el evento va por la bandeja de auditoría (<c>COR_AuditOutbox</c>) al
/// flujo <c>{tenantPublicId:N}:10y</c> y <c>AuditOutboxForwarder</c> lo sella y lo lleva a Mongo. El flujo y el
/// <c>TenantId</c> del evento salen del <b>PublicId</b> de la cooperativa (<see cref="ICurrentTenantService"/>), nunca de
/// <see cref="ICurrentUserService.TenantId"/> —el Id interno—, que fue el defecto de la 009 (2026-09-21). Sin cooperativa
/// activa no se inventa un flujo: el evento va por <see cref="IAuditService"/> como el de cualquier módulo.
/// </para>
///
/// <para>
/// Guarda su propia fila (<see cref="AuditoriaEncadenada.RegistrarAsync"/>), así que se llama donde el contexto no tiene
/// otros cambios pendientes: después de leer, o después del <c>SaveChanges</c> del comando. Si la escritura falla no
/// tumba la operación, pero lo deja en el log con la acción (Principio IX).
/// </para>
/// </summary>
public sealed class InventoryAuditEmitter(IServiceProvider servicios, ILogger<InventoryAuditEmitter> logger)
{
    public const string Modulo = ModuloDeAuditoria.Inventory;

    /// <summary>Un evento explícito del módulo: acción, entidad y los valores antes y después.</summary>
    public async Task EmitAsync(string action, string entityType, Guid? entityPublicId, object? before, object? after, CancellationToken ct)
    {
        try
        {
            await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityPublicId?.ToString(),
                Module = Modulo,
                OldValues = before,
                NewValues = after,
            }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "La auditoría explícita de inventario falló para {Action} sobre {EntityType} {EntityPublicId}",
                action, entityType, entityPublicId);
        }
    }

    /// <summary>La exportación de una vista de <c>/api/reports/inventory</c>: cuál, con qué filtros, en qué formato y cuántas filas.</summary>
    public Task EmitirExportacionAsync(string vista, object filtros, string formato, int filas, CancellationToken ct) =>
        EmitAsync(AuditEventTypes.InventoryReportExported, "InventoryReport", null, null, new { vista, filtros, formato, filas }, ct);

    /// <summary>La descarga de la plantilla de un catálogo con datos (<c>?withData=true</c>, contracts/plantillas.md §0.6).</summary>
    public Task EmitirExportacionDeCatalogoAsync(string catalogo, int filas, CancellationToken ct) =>
        EmitAsync(AuditEventTypes.InventoryCatalogExported, catalogo, null, null, new { catalog = catalogo, rows = filas }, ct);
}
