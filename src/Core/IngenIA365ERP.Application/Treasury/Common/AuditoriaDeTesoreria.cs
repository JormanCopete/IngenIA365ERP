using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>
/// El emisor de auditoría de la Tesorería nueva (feature 015, T0062; US12; R-32; FR-014), el mismo molde que <c>AuditoriaDeRecaudo</c>: módulo encadenado
/// <see cref="ModuloDeAuditoria.Treasury"/>, por <c>COR_AuditOutbox</c> en la transacción y el flujo de diez años nombrado por el <b>PublicId</b> de la
/// cooperativa de <c>ICurrentTenantService</c> (nunca el Id interno de <c>ICurrentUserService.TenantId</c>). Lecturas sensibles de la lista cerrada
/// (<see cref="LecturasSensibles"/>): los saldos bancarios y las exportaciones. Una lectura nunca lanza hacia quien lee. (nuevo)
/// </summary>
public sealed class AuditoriaDeTesoreria(IServiceProvider servicios, ILogger<AuditoriaDeTesoreria> logger)
{
    public const string Modulo = ModuloDeAuditoria.Treasury;

    public const string EventoSaldosBancariosLeidos = "Treasury.BankBalances.Read";
    public const string EventoInformeExportado = "Treasury.Report.Exported";
    public const string EventoFueraDeAlcance = "Treasury.Access.OutOfScope";

    /// <summary>La lista cerrada de lecturas sensibles de Tesorería (R-32).</summary>
    public static IReadOnlyList<string> LecturasSensibles { get; } = [EventoSaldosBancariosLeidos, EventoInformeExportado];

    /// <summary>Los saldos de una o varias cuentas propias (FR-103).</summary>
    public Task<Guid?> SaldosBancariosLeidosAsync(IReadOnlyCollection<Guid> cuentas, DateOnly fecha, CancellationToken ct = default) =>
        LecturaAsync(EventoSaldosBancariosLeidos, "BankAccount", cuentas.Count == 1 ? cuentas.First() : null,
            new { accounts = cuentas, date = fecha.ToString("yyyy-MM-dd") }, ct);

    /// <summary>Una exportación de un informe de Tesorería.</summary>
    public Task<Guid?> InformeExportadoAsync(string vista, string formato, int filas, object? filtros, CancellationToken ct = default) =>
        LecturaAsync(EventoInformeExportado, "TreasuryReport", null, new { view = vista, format = formato, rows = filas, filters = filtros }, ct);

    /// <summary>El pedido de un recurso fuera del alcance; va por un contexto aparte para sobrevivir al 404.</summary>
    public Task<Guid?> FueraDeAlcanceAsync(string recurso, Guid? recursoPublicId, CancellationToken ct = default) =>
        LecturaAsync(EventoFueraDeAlcance, recurso, recursoPublicId, new { resource = recurso }, ct, aparte: true);

    /// <summary>Un evento de negocio con antes y después, agregado a la unidad de trabajo sin guardar.</summary>
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
            logger.LogWarning(ex, "[Tesoreria.LecturaSinAuditar] La auditoría de {Action} sobre {EntityType} {EntityPublicId} falló.", accion, entidad, entidadPublicId);
            return null;
        }
    }
}
