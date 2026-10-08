using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// Las lecturas sensibles de Cartera (feature 013, T67, C89, T0289; research §13 y R136; api.md §1.12), en el molde de
/// <c>InventoryAuditEmitter</c>. <c>AuditBehavior</c> sólo ve comandos; ver una liquidación, un informe de centrales, un acta o los
/// vinculados, reimprimir o verificar un documento, exportar, consultar el cupo desde Comercial, liberar una coincidencia en listas o
/// pedir algo fuera del alcance son lecturas o rechazos que se registran aquí, con la <b>lista fija</b> de <see cref="LecturasSensibles"/>
/// (<c>LasLecturasSensiblesDeCarteraSeAuditan</c>). (nuevo)
///
/// <para>
/// Cartera es un módulo encadenado: el evento va por <c>COR_AuditOutbox</c> al flujo <c>{tenantPublicId:N}:10y</c>, nombrado por el
/// <b>PublicId</b> de la cooperativa (<see cref="ICurrentTenantService"/>, nunca el Id interno de <see cref="ICurrentUserService.TenantId"/>;
/// <c>LaAuditoriaSeNombraPorElPublicIdDelTenant</c>). Cada método devuelve el <c>EventId</c> de la fila —la consulta de cupo de Comercial lo
/// usa como <c>EvidenceId</c>—, o nulo si el evento fue por Mongo (sin cooperativa activa) o no se pudo escribir. <b>Nunca lanza</b> hacia
/// quien lee: un fallo queda en el log (Principio IX). Guarda su propia fila, así que se llama sin otros cambios pendientes; el de
/// «fuera de alcance» va por un contexto aparte si hay una transacción en curso, para sobrevivir al 404 del comando.
/// </para>
/// </summary>
public sealed class LendingAuditEmitter(IServiceProvider servicios, ILogger<LendingAuditEmitter> logger)
{
    public const string Modulo = ModuloDeAuditoria.Lending;

    /// <summary>La lista cerrada de eventos de lectura sensible de Cartera (research §13, C89).</summary>
    public static IReadOnlyList<string> LecturasSensibles { get; } =
    [
        AuditEventTypes.LendingPayoffViewed,
        AuditEventTypes.LendingCreditBureauReportViewed,
        AuditEventTypes.LendingDocumentReprinted,
        AuditEventTypes.LendingDocumentVerified,
        AuditEventTypes.LendingReportExported,
        AuditEventTypes.LendingSessionViewed,
        AuditEventTypes.LendingRelatedPartyViewed,
        AuditEventTypes.LendingCreditStatusQueried,
        AuditEventTypes.LendingAccessOutOfScope,
        AuditEventTypes.LendingScreeningMatchReleased,
    ];

    /// <summary>La liquidación de un crédito a la fecha D con la información hasta R, y la huella del resultado (T24).</summary>
    public Task<Guid?> EmitirLiquidacionVistaAsync(Guid loanPublicId, DateOnly fecha, DateTime? informacionHasta, string huella, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingPayoffViewed, "Loan", loanPublicId,
            new { date = fecha.ToString("yyyy-MM-dd"), recordedAsOf = informacionHasta, sha256 = huella }, ct);

    /// <summary>El informe de una consulta a centrales de riesgo.</summary>
    public Task<Guid?> EmitirInformeDeCentralesVistoAsync(Guid bureauQueryPublicId, string central, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingCreditBureauReportViewed, "BureauQuery", bureauQueryPublicId, new { bureau = central }, ct);

    /// <summary>La reimpresión o la descarga de un documento emitido.</summary>
    public Task<Guid?> EmitirDocumentoReimpresoAsync(Guid documentPublicId, string clase, string formato, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingDocumentReprinted, "IssuedLendingDocument", documentPublicId, new { kind = clase, format = formato }, ct);

    /// <summary>La verificación de un documento emitido (por su código de verificación) y su resultado.</summary>
    public Task<Guid?> EmitirDocumentoVerificadoAsync(Guid documentPublicId, bool valido, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingDocumentVerified, "IssuedLendingDocument", documentPublicId, new { valid = valido }, ct);

    /// <summary>La exportación de una vista de <c>/api/reports/lending</c>: cuál, con qué filtros, en qué formato y cuántas filas.</summary>
    public Task<Guid?> EmitirExportacionAsync(string vista, object filtros, string formato, int filas, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingReportExported, "LendingReport", null, new { vista, filtros, formato, filas }, ct);

    /// <summary>La lectura de un acta (en pantalla o en PDF).</summary>
    public Task<Guid?> EmitirActaVistaAsync(Guid sessionPublicId, string? numero, bool comoPdf, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingSessionViewed, "Session", sessionPublicId, new { number = numero, pdf = comoPdf }, ct);

    /// <summary>La lista de personas vinculadas: con qué filtros y cuántas filas.</summary>
    public Task<Guid?> EmitirVinculadosVistosAsync(object filtros, int filas, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingRelatedPartyViewed, "RelatedParty", null, new { filtros, filas }, ct);

    /// <summary>La consulta de cupo de Comercial; su <c>EventId</c> es el <c>EvidenceId</c> de la respuesta (T67).</summary>
    public Task<Guid?> EmitirConsultaDeCupoAsync(Guid personPublicId, object detalle, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingCreditStatusQueried, "Person", personPublicId, detalle, ct);

    /// <summary>Un recurso pedido fuera del alcance de oficina o línea (la respuesta fue el 404 del recurso).</summary>
    public Task<Guid?> EmitirFueraDeAlcanceAsync(string recurso, Guid? recursoPublicId, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingAccessOutOfScope, recurso, recursoPublicId, new { resource = recurso }, ct, aparte: true);

    /// <summary>La liberación de una coincidencia en listas restrictivas, con su motivo.</summary>
    public Task<Guid?> EmitirCoincidenciaLiberadaAsync(Guid screeningResultPublicId, string motivo, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingScreeningMatchReleased, "ScreeningResult", screeningResultPublicId, new { reason = motivo }, ct);

    private async Task<Guid?> EmitAsync(string action, string entityType, Guid? entityPublicId, object? after, CancellationToken ct, bool aparte = false)
    {
        try
        {
            return await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityPublicId?.ToString(),
                Module = Modulo,
                NewValues = after,
            }, ct, aparte);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Cartera.LecturaSinAuditar] La auditoría de {Action} sobre {EntityType} {EntityPublicId} falló.",
                action, entityType, entityPublicId);
            return null;
        }
    }
}
