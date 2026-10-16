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

    /// <summary>
    /// US13 (T0784): la emisión de un documento al deudor, con su clase, número, entrega y el SHA-256 del PDF guardado. La emisión muestra los datos
    /// del titular y sus saldos, así que además de la auditoría del comando cuenta como lectura (nota 10 de PG); no va en la lista cerrada de
    /// <see cref="LecturasSensibles"/> de research §13, que es de las lecturas sin escritura.
    /// </summary>
    public Task<Guid?> EmitirDocumentoEmitidoAsync(Guid documentPublicId, string clase, string numero, string entrega, string sha256, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingDocumentIssued, "IssuedLendingDocument", documentPublicId,
            new { kind = clase, number = numero, delivery = entrega, sha256 }, ct);

    /// <summary>US13 (T0785): la anulación manual de un documento, con su motivo. La automática la deja el reverso que la causa.</summary>
    public Task<Guid?> EmitirDocumentoAnuladoAsync(Guid documentPublicId, string numero, string motivo, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingDocumentVoided, "IssuedLendingDocument", documentPublicId, new { number = numero, reason = motivo }, ct);

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

    /// <summary>
    /// US3 (T0444, T23): la distribución completa por cuota y concepto de una operación del libro, en la transacción de la operación (se llama
    /// después de su guardado). No es una lectura sensible: no va en <see cref="LecturasSensibles"/>.
    /// </summary>
    public Task<Guid?> EmitirOperacionRegistradaAsync(Guid operationPublicId, string numero, string clase, object distribucion, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingOperationRegistered, "LoanOperation", operationPublicId, new { number = numero, kind = clase, distribution = distribucion }, ct);

    /// <summary>
    /// US3 (T0454): el enlace de descarga de un archivo de desembolsos de crédito (cuentas y montos de los beneficiarios). Se registra como el de los
    /// demás archivos de módulo; no va en <see cref="LecturasSensibles"/>, que es la lista cerrada de research §13.
    /// </summary>
    public Task<Guid?> EmitirArchivoDeDesembolsosDescargadoAsync(Guid filePublicId, string numero, string sha256, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingDisbursementFileDownloaded, "DisbursementFile", filePublicId, new { number = numero, sha256 }, ct);

    /// <summary>
    /// K2 (T0836; C46): el evento resumen de un archivo de descuentos —número, convenio, período, versión, conteos por novedad, total y huella—, en
    /// la transacción de la generación (se llama después de su guardado). Sus líneas van con <c>[SinDiffDeAuditoria]</c>: no es una lectura sensible.
    /// </summary>
    public Task<Guid?> EmitirArchivoDeDescuentosGeneradoAsync(Guid filePublicId, object resumen, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingPayrollDeductionFileGenerated, "PayrollDeductionFile", filePublicId, resumen, ct);

    /// <summary>
    /// K2 (T0836): el enlace de descarga de un archivo de descuentos (documentos, créditos y cuotas de los deudores). Se registra como el de los
    /// demás archivos de módulo; no va en <see cref="LecturasSensibles"/>, que es la lista cerrada de research §13.
    /// </summary>
    public Task<Guid?> EmitirArchivoDeDescuentosDescargadoAsync(Guid filePublicId, string numero, string sha256, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingPayrollDeductionFileDownloaded, "PayrollDeductionFile", filePublicId, new { number = numero, sha256 }, ct);

    /// <summary>
    /// US8 (T0752): la activación de Cartera, en su transacción, con los requisitos evaluados, lo que nació y las huellas del cuadre, la conciliación y
    /// los cobros provisionales. No es una lectura sensible: no va en <see cref="LecturasSensibles"/>.
    /// </summary>
    public Task<Guid?> EmitirActivacionRegistradaAsync(Guid activationPublicId, object detalle, CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingActivationRegistered, "LendingActivation", activationPublicId, detalle, ct);

    /// <summary>
    /// K3, bloque D: el crédito que se excluye de un lote de castigo al aplicarlo porque ya no cumple, con lo que le falta y el motivo, en la transacción del
    /// lote (se llama después de su guardado). No es una lectura sensible: no va en <see cref="LecturasSensibles"/>.
    /// </summary>
    public Task<Guid?> EmitirCreditoExcluidoDelCastigoAsync(Guid batchPublicId, string lote, string credito, IReadOnlyList<string> faltan, string motivo,
        CancellationToken ct = default) =>
        EmitAsync(AuditEventTypes.LendingWriteOffItemExcluded, "WriteOffBatch", batchPublicId,
            new { batch = lote, loanNumber = credito, missing = faltan, reason = motivo }, ct);

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
