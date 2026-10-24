using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// El emisor de auditoría de Recaudo (feature 015, T0062; US12; R-32; FR-014; TR37), en el molde de <c>LendingAuditEmitter</c>. Recaudo es un módulo
/// encadenado (<see cref="ModuloDeAuditoria.Receipts"/>): el evento va por <c>COR_AuditOutbox</c> <b>dentro de la transacción</b> de la operación al flujo
/// de diez años <c>{tenantPublicId:N}:10y</c>, nombrado por el <b>PublicId</b> de la cooperativa de <c>ICurrentTenantService</c> (lo resuelve
/// <see cref="AuditoriaEncadenada.ContextoAsync"/>), nunca por el Id interno de <c>ICurrentUserService.TenantId</c> —el defecto que tuvieron
/// <c>AccountingAuditEmitter</c> y <c>PayrollAuditEmitter</c>—. <c>AuditBehavior</c> ya deja el evento de cada comando y el interceptor las diferencias de
/// cada entidad; aquí van:
/// <list type="bullet">
/// <item>las <b>lecturas sensibles</b> de la lista cerrada (<see cref="LecturasSensibles"/>, R-32): lo que puede pagar una persona, el reporte de
/// efectivo, las exportaciones;</item>
/// <item>los eventos de negocio con antes y después que una operación quiere sellar junto con su documento (<see cref="AgregarAsync"/>, sin guardar: lo
/// guarda el <c>SaveChanges</c> único de la operación);</item>
/// <item>el pedido de algo fuera del alcance (<see cref="FueraDeAlcanceAsync"/>), que sobrevive al 404 por un contexto aparte.</item>
/// </list>
/// Una lectura <b>nunca lanza</b> hacia quien lee: un fallo queda en el log (Principio IX). (nuevo)
/// </summary>
public sealed class AuditoriaDeRecaudo(IServiceProvider servicios, ILogger<AuditoriaDeRecaudo> logger)
{
    public const string Modulo = ModuloDeAuditoria.Receipts;

    public const string EventoLoQuePuedePagar = "Receipts.Person.PayablesRead";
    public const string EventoReporteDeEfectivoLeido = "Receipts.Sarlaft.ReportRead";
    public const string EventoInformeExportado = "Receipts.Report.Exported";
    public const string EventoFueraDeAlcance = "Receipts.Access.OutOfScope";

    /// <summary>La lista cerrada de lecturas sensibles de Recaudo (R-32).</summary>
    public static IReadOnlyList<string> LecturasSensibles { get; } = [EventoLoQuePuedePagar, EventoReporteDeEfectivoLeido, EventoInformeExportado];

    /// <summary>Lo que puede pagar una persona (FR-037): la persona, cuántas obligaciones y cuentas se mostraron.</summary>
    public Task<Guid?> LoQuePuedePagarAsync(Guid personPublicId, int obligaciones, int cuentas, CancellationToken ct = default) =>
        LecturaAsync(EventoLoQuePuedePagar, "Person", personPublicId, new { obligations = obligaciones, accounts = cuentas }, ct);

    /// <summary>Una exportación de un informe de Recaudo.</summary>
    public Task<Guid?> InformeExportadoAsync(string vista, string formato, int filas, object? filtros, CancellationToken ct = default) =>
        LecturaAsync(EventoInformeExportado, "ReceiptsReport", null, new { view = vista, format = formato, rows = filas, filters = filtros }, ct);

    /// <summary>El pedido de un recurso fuera del alcance; va por un contexto aparte para sobrevivir al 404.</summary>
    public Task<Guid?> FueraDeAlcanceAsync(string recurso, Guid? recursoPublicId, CancellationToken ct = default) =>
        LecturaAsync(EventoFueraDeAlcance, recurso, recursoPublicId, new { resource = recurso }, ct, aparte: true);

    /// <summary>
    /// Un evento de negocio de Recaudo con su antes y su después, agregado a la unidad de trabajo <b>sin guardar</b>: nace en el mismo guardado que el
    /// documento. Sin cooperativa activa (no encadenado) va directo al servicio de auditoría.
    /// </summary>
    public async Task AgregarAsync(string accion, string entidad, Guid? entidadPublicId, object? antes, object? despues, CancellationToken ct = default)
    {
        var evento = new AuditLogCommand
        {
            Action = accion,
            EntityType = entidad,
            EntityId = entidadPublicId?.ToString(),
            Module = Modulo,
            OldValues = antes,
            NewValues = despues,
        };
        var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct, Modulo);
        var flujo = AuditoriaEncadenada.Flujo(contexto.TenantId);
        var db = flujo is null ? null : servicios.GetService<IApplicationDbContext>();
        if (db is null)
        {
            await servicios.GetRequiredService<IAuditService>().LogAsync(AuditoriaEncadenada.ParaMongo(contexto, evento), ct);
            return;
        }
        db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo!, AuditoriaEncadenada.Evento(contexto, evento)));
    }

    private async Task<Guid?> LecturaAsync(string accion, string entidad, Guid? entidadPublicId, object? detalle, CancellationToken ct, bool aparte = false)
    {
        try
        {
            return await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
            {
                Action = accion,
                EntityType = entidad,
                EntityId = entidadPublicId?.ToString(),
                Module = Modulo,
                NewValues = detalle,
            }, ct, aparte);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Recaudo.LecturaSinAuditar] La auditoría de {Action} sobre {EntityType} {EntityPublicId} falló.", accion, entidad, entidadPublicId);
            return null;
        }
    }
}
