using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;

/// <summary>
/// Lo que comparten el consumidor por documento (<c>PostInventoryMessagesCommand</c>) y el resumido
/// (<c>PostInventorySummaryGroupCommand</c>): los pasos 2 a 5 de contracts/contabilidad.md §3.1 (versión, moneda, recibo
/// existente, original sin contabilizar), los recibos de <c>ACC_InventoryPostings</c>, la traducción de un fallo a un rechazo con
/// las líneas del documento (FR-074) y la colisión del índice único del recibo. No guarda: guarda el comando, en un solo
/// <c>SaveChangesAsync</c>. Lee las entregas de la plataforma sólo para saber si un original sigue pendiente; nunca las escribe
/// (T11). (nuevo)
/// </summary>
public sealed class ConsumoDeInventario(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj, AccountingAuditEmitter auditoria)
{
    /// <summary>El índice único del recibo (<c>InventoryPostingConfiguration.IndiceUnicoDelMensaje</c>): otra réplica ganó.</summary>
    public const string IndiceUnicoDelRecibo = "UK_ACC_InventoryPostings_MessagePublicId";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>¿El choque es contra el recibo único de un mensaje? (molde <c>PersonFactory.EsColisionDeDocumento</c>).</summary>
    public static bool EsColisionDelRecibo(DbUpdateException ex)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
            if (actual.Message.Contains(IndiceUnicoDelRecibo, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Pasos 2 a 5: un resultado si la unidad no sigue (versión no aceptada o moneda distinta → <c>Rejected</c>; recibo existente
    /// → <c>AlreadyProcessed</c>; original pendiente → <c>Retry</c>), o nulo si sigue.
    /// </summary>
    public async Task<ResultadoDeConsumo?> RevisarAsync(IReadOnlyList<MensajeEntrante> unidad, CancellationToken ct)
    {
        foreach (var m in unidad)
        {
            if (!VersionesAceptadas.Acepta(m.Type, m.Version))
                return Rechazo(ErroresDeIntegracion.VersionNoAceptada(m.Type, m.Version));
            if (!string.Equals(m.Envelope.Currency, "COP", StringComparison.Ordinal) || m.Envelope.ExchangeRate != 1m)
                return Rechazo(AccountingErrors.InventoryMessageCurrencyNotSupported(m.Envelope.Currency, m.Envelope.ExchangeRate));
        }

        if (await YaProcesadoAsync(unidad.Select(m => m.MessageId).ToList(), ct) is { } ya) return ya;

        var originales = Originales(unidad).Distinct().ToList();
        if (originales.Count > 0)
        {
            var contabilizados = await db.InventoryPostings.AsNoTracking()
                .Where(p => originales.Contains(p.SourcePublicId)).Select(p => p.SourcePublicId).Distinct().ToListAsync(ct);
            var faltan = originales.Except(contabilizados).ToList();
            if (faltan.Count > 0)
            {
                var pendiente = await (
                        from d in db.IntegrationMessageDeliveries.AsNoTracking()
                        join mm in db.IntegrationMessages.AsNoTracking() on d.MessageId equals mm.Id
                        where faltan.Contains(mm.OriginPublicId) && d.Destination == IntegrationDestinations.Accounting
                              && (d.Status == DeliveryStatus.Pending || d.Status == DeliveryStatus.InBatch || d.Status == DeliveryStatus.Rejected)
                        orderby mm.Id
                        select (Guid?)mm.PublicId)
                    .FirstOrDefaultAsync(ct);
                if (pendiente is { } original)
                {
                    var error = AccountingErrors.InventoryMessageWaitingForOriginal(original);
                    return new ResultadoDeConsumo.Retry(error.Message, error.Code);
                }
            }
        }
        return null;
    }

    /// <summary>El resultado de un mensaje ya procesado: la misma referencia (recibo único, SC-002), o nulo.</summary>
    public async Task<ResultadoDeConsumo?> YaProcesadoAsync(IReadOnlyList<Guid> mensajes, CancellationToken ct)
    {
        var recibo = await db.InventoryPostings.AsNoTracking().Where(p => mensajes.Contains(p.MessagePublicId))
            .OrderBy(p => p.Id)
            .Select(p => new { p.AccountingDocumentId, p.NoVoucherReason })
            .FirstOrDefaultAsync(ct);
        if (recibo is null) return null;
        if (recibo.AccountingDocumentId is not { } documentoId)
            return new ResultadoDeConsumo.AlreadyProcessed(null, null, null, MotivoDe(recibo.NoVoucherReason));
        var documento = await db.AccountingDocuments.AsNoTracking().Where(d => d.Id == documentoId)
            .Select(d => new { d.PublicId, Codigo = d.VoucherType!.Code, d.Number }).FirstAsync(ct);
        return new ResultadoDeConsumo.AlreadyProcessed(documento.PublicId, documento.Codigo, documento.Number?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Paso 10: una fila de <c>ACC_InventoryPostings</c> por mensaje (tipo, versión, documento y relacionado, fecha, comprobante o
    /// motivo sin comprobante, lote, usuario de origen como dato y el actor de <see cref="IActorActual"/>). No guarda.
    /// </summary>
    public async Task AgregarRecibosAsync(
        IReadOnlyList<MensajeEntrante> unidad, AccountingDocument? comprobante, string? sinComprobante, Guid? lote, DateOnly fecha, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var m in unidad)
        {
            var sobre = m.Envelope;
            db.InventoryPostings.Add(new InventoryPosting
            {
                MessagePublicId = sobre.MessageId,
                MessageType = sobre.Type,
                MessageVersion = (short)sobre.Version,
                MessageKind = sobre.Kind,
                SourceModule = ModuloContable.Inventario,
                SourcePublicId = sobre.Origin.PublicId,
                SourceDocumentClass = sobre.Origin.DocumentClass?.ToString(),
                SourceDocumentTypeCode = sobre.Origin.DocumentTypeCode,
                SourceDocumentNumber = string.IsNullOrWhiteSpace(sobre.Origin.Number) ? null : sobre.Origin.Number,
                RelatedDocumentPublicId = sobre.Related?.PublicId,
                OperationDate = fecha,
                AccountingDocument = comprobante,
                NoVoucherReason = comprobante is null ? sinComprobante : null,
                BatchPublicId = lote ?? m.Entrega.BatchPublicId,
                OriginUserCentralId = sobre.OriginUser.CentralUserId ?? Guid.Empty,
                OriginUserName = sobre.OriginUser.Name,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                ActorName = actor.Name,
                ProcessedAt = ahora,
                CreatedAt = ahora,
                CreatedBy = actor.Name,
            });
        }
    }

    /// <summary>El rechazo de una unidad que no se pudo armar: el primer error y todos con su mensaje y sus líneas del documento.</summary>
    public static ResultadoDeConsumo.Rejected RechazoDe(ConstruccionDeUnidad construccion)
    {
        var primero = construccion.Fallos[0];
        var datos = new
        {
            errors = construccion.Fallos.Select(f => new
            {
                messageType = f.MessageType, documentLines = f.DocumentLines, accountCode = f.AccountCode, rule = f.Error.Code,
                message = f.Error.Message, data = (f.Error as ErrorConDatos)?.Data,
            }).ToList(),
        };
        return new ResultadoDeConsumo.Rejected(primero.Error.Code, primero.Error.Message, JsonSerializer.Serialize(datos, Json));
    }

    /// <summary>
    /// El rechazo del contrato de la 009 (reglas 1 a 11) con los errores de línea traducidos a las líneas del documento y al mensaje
    /// de donde salieron (FR-074).
    /// </summary>
    public static ResultadoDeConsumo.Rejected RechazoDe(ValidacionDeComprobante validacion, IReadOnlyList<LineaDeLaUnidad> mapa, Error? error = null)
    {
        var errores = validacion.Errores;
        var codigo = errores.Count == 1 ? errores[0].Code : error?.Code ?? (errores.Count > 0 ? AccountingErrors.DocumentInvalid(errores).Code : "Accounting.Document.Invalid");
        var mensaje = errores.Count == 1 ? errores[0].Message : error?.Message ?? (errores.Count > 0 ? AccountingErrors.DocumentInvalid(errores).Message : string.Empty);
        var datos = new { errors = errores.Select(e => Traducido(e, mapa)).ToList() };
        return new ResultadoDeConsumo.Rejected(codigo, mensaje, JsonSerializer.Serialize(datos, Json));
    }

    /// <summary>Un error de línea del contrato con el mensaje, las líneas del documento y la cuenta de su línea contable.</summary>
    public static object Traducido(ErrorDeLinea e, IReadOnlyList<LineaDeLaUnidad> mapa)
    {
        var linea = e.LineNumber >= 1 && e.LineNumber <= mapa.Count ? mapa[e.LineNumber - 1] : null;
        return new
        {
            voucherLine = e.LineNumber, messageType = linea?.MessageType, documentLines = linea?.DocumentLines ?? [],
            accountCode = e.AccountCode ?? linea?.AccountCode, field = e.Field, rule = e.Code, message = e.Message,
        };
    }

    /// <summary>Evento <c>Accounting.Inventory.Posted</c> del comprobante, por la cooperativa (T495).</summary>
    public Task AuditarContabilizacionAsync(AccountingDocument comprobante, IReadOnlyList<MensajeEntrante> mensajes, Guid? lote, CancellationToken ct) =>
        auditoria.EmitirContabilizacionDeInventarioAsync(comprobante.PublicId, new
        {
            voucherTypeCode = comprobante.VoucherType?.Code, number = comprobante.Number, date = comprobante.Date,
            messages = mensajes.Select(m => m.MessageId).ToList(), documents = mensajes.Select(m => m.Envelope.Origin.PublicId).Distinct().ToList(),
            batchPublicId = lote,
        }, ct);

    /// <summary>Evento <c>Accounting.Inventory.Rejected</c> de cada mensaje rechazado.</summary>
    public async Task AuditarRechazoAsync(IReadOnlyList<MensajeEntrante> mensajes, ResultadoDeConsumo.Rejected rechazo, CancellationToken ct)
    {
        foreach (var m in mensajes)
            await auditoria.EmitirRechazoDeInventarioAsync(m.MessageId, new { code = rechazo.Code, reason = rechazo.Reason, document = m.Envelope.Origin.PublicId }, ct);
    }

    /// <summary>Los documentos de los que depende la unidad: el relacionado, los orígenes de un derivado y el afectado de un ajuste de costo.</summary>
    public static IEnumerable<Guid> Originales(IReadOnlyList<MensajeEntrante> unidad)
    {
        foreach (var m in unidad)
        {
            if (m.Envelope.Related is { } relacionado) yield return relacionado.PublicId;
            var derivados = m.Payload switch
            {
                VentaFacturadaV1 v => v.DerivedFrom,
                FacturaProveedorRegistradaV1 f => f.DerivedFrom,
                TrasladoRecibidoV1 t => t.DerivedFrom,
                _ => [],
            };
            foreach (var d in derivados) yield return d.PublicId;
            if (m.Payload is AjusteDeCostoReconocidoV1 ajuste && ajuste.AffectedDocument.PublicId != Guid.Empty) yield return ajuste.AffectedDocument.PublicId;
        }
    }

    public static ResultadoDeConsumo.Rejected Rechazo(Error error) =>
        new(error.Code, error.Message, error is ErrorConDatos conDatos ? JsonSerializer.Serialize(conDatos.Data, Json) : null);

    private static MotivoSinComprobante? MotivoDe(string? motivo) => motivo switch
    {
        InventoryPosting.SinComprobanteValorCero => MotivoSinComprobante.ZeroValue,
        InventoryPosting.SinComprobanteInformativo => MotivoSinComprobante.Informational,
        _ => null,
    };
}
