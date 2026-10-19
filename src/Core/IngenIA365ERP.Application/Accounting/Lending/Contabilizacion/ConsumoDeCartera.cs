using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Accounting.Integration;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;

/// <summary>
/// Lo que comparten el consumidor por unidad (<c>PostLendingFactsCommand</c>) y el resumido (<c>PostLendingSummaryGroupCommand</c>) de los
/// hechos de Cartera (feature 013, T0647; contracts/contabilidad.md §7.1, §7.2 y §10.4; molde <c>ConsumoDeInventario</c>): los pasos 2 a 5
/// (versión, moneda, recibo existente, original sin contabilizar; comunes en <see cref="RecibosDeContabilidad"/>), los recibos de
/// <c>ACC_LendingPostings</c>, la traducción de un fallo a un rechazo con el hecho, el concepto y el crédito de cada línea, la alerta del
/// período inexistente y la colisión del índice único del recibo. No guarda: guarda el comando, en un solo <c>SaveChangesAsync</c>. Lee las
/// entregas de la plataforma sólo para saber si un original sigue pendiente; nunca las escribe (T11). Nunca lee <c>LND_</c>. (nuevo)
/// </summary>
public sealed class ConsumoDeCartera(
    IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj, AccountingAuditEmitter auditoria, IAlertas? alertas = null)
{
    /// <summary>El índice único del recibo (<c>LendingPostingConfiguration.IndiceUnicoDelMensaje</c>): otra réplica ganó.</summary>
    public const string IndiceUnicoDelRecibo = "UK_ACC_LendingPostings_MessagePublicId";

    /// <summary>El código que el poster da a una fecha sin período: para Cartera es reintento, no rechazo (§8).</summary>
    public const string CodigoPeriodoInexistente = "Accounting.Period.NotFound";

    /// <summary>El permiso de quien abre ejercicios: recibe <c>Contabilidad.PeriodoInexistente</c>.</summary>
    private const string PermisoDePeriodos = "Accounting.Periods.CloseYear";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool EsColisionDelRecibo(DbUpdateException ex) => RecibosDeContabilidad.EsColision(ex, IndiceUnicoDelRecibo);

    /// <summary>
    /// Pasos 2 a 5: un resultado si la unidad no sigue (versión no aceptada o moneda distinta → <c>Rejected</c>; recibo existente →
    /// <c>AlreadyProcessed</c>; original pendiente → <c>Retry</c> con <c>Accounting.LendingFact.WaitingForOriginal</c>), o nulo si sigue.
    /// </summary>
    public async Task<ResultadoDeConsumo?> RevisarAsync(IReadOnlyList<MensajeEntrante> unidad, CancellationToken ct)
    {
        if (RecibosDeContabilidad.RevisarVersionYMoneda(unidad, ModulosDeOrigen.Cartera, AccountingErrors.LendingFactCurrencyNotSupported) is { } rechazo)
            return rechazo;

        if (await YaProcesadoAsync(unidad.Select(m => m.MessageId).ToList(), ct) is { } ya) return ya;

        var originales = Originales(unidad).Distinct().ToList();
        if (originales.Count > 0)
        {
            var contabilizados = await db.LendingPostings.AsNoTracking()
                .Where(p => originales.Contains(p.SourcePublicId)).Select(p => p.SourcePublicId).Distinct().ToListAsync(ct);
            var faltan = originales.Except(contabilizados).ToList();
            if (faltan.Count > 0 && await RecibosDeContabilidad.OriginalPendienteAsync(db, faltan, ct) is { } original)
            {
                var error = AccountingErrors.LendingFactWaitingForOriginal(original);
                return new ResultadoDeConsumo.Retry(error.Message, error.Code);
            }
        }
        return null;
    }

    /// <summary>Lo que origina cada hecho de la unidad: el original del sobre (el pago de un reverso, el desembolso de un ajuste, la parte del cierre).</summary>
    public static IEnumerable<Guid> Originales(IReadOnlyList<MensajeEntrante> unidad) =>
        unidad.Select(m => m.Envelope.Related?.PublicId).Where(g => g is { } x && x != Guid.Empty).Select(g => g!.Value);

    /// <summary>El resultado de un hecho ya procesado: la misma referencia (recibo único), o nulo.</summary>
    public async Task<ResultadoDeConsumo?> YaProcesadoAsync(IReadOnlyList<Guid> mensajes, CancellationToken ct)
    {
        var recibo = await db.LendingPostings.AsNoTracking().Where(p => mensajes.Contains(p.MessagePublicId))
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
    /// Paso 10: una fila de <c>ACC_LendingPostings</c> por hecho (tipo, versión, origen, crédito, original, fecha contable, comprobante o motivo
    /// sin comprobante, lote, quién lo originó como dato y el actor de <see cref="IActorActual"/>). No guarda.
    /// </summary>
    public async Task AgregarRecibosAsync(
        IReadOnlyList<MensajeEntrante> unidad, AccountingDocument? comprobante, string? sinComprobante, Guid? lote, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        foreach (var m in unidad)
        {
            var sobre = m.Envelope;
            var credito = ConstructorDeLineasDeCartera.Credito(m.Payload);
            var proceso = sobre.OriginUser.CentralUserId is null;
            db.LendingPostings.Add(new LendingPosting
            {
                MessagePublicId = sobre.MessageId,
                MessageType = sobre.Type,
                MessageVersion = (short)sobre.Version,
                MessageKind = sobre.Kind,
                SourceModule = ModuloContable.Cartera,
                SourcePublicId = sobre.Origin.PublicId,
                SourceDocumentClass = m.OriginDocumentClassText,
                SourceDocumentTypeCode = sobre.Origin.DocumentTypeCode,
                SourceDocumentNumber = string.IsNullOrWhiteSpace(sobre.Origin.Number) ? null : sobre.Origin.Number,
                LoanPublicId = credito is { LoanPublicId: var lp } && lp != Guid.Empty ? lp : null,
                LoanNumber = string.IsNullOrWhiteSpace(credito?.LoanNumber) ? null : credito.LoanNumber,
                RelatedMessagePublicId = ConstructorDeLineasDeCartera.MensajeOriginal(m.Payload) ?? await RelacionadoAsync(m, ct),
                OperationDate = sobre.Origin.OperationDate,
                AccountingDocument = comprobante,
                NoVoucherReason = comprobante is null ? sinComprobante : null,
                BatchPublicId = lote ?? m.Entrega.BatchPublicId,
                OriginActorKind = proceso ? ActorKind.Process : ActorKind.Person,
                OriginUserCentralId = sobre.OriginUser.CentralUserId,
                OriginUserName = string.IsNullOrWhiteSpace(sobre.OriginUser.Name) ? Actor.NombreDelProceso : sobre.OriginUser.Name,
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                ActorName = actor.Name,
                ProcessedAt = ahora,
                CreatedAt = ahora,
                CreatedBy = actor.Name,
            });
        }
    }

    /// <summary>
    /// El <c>CierreDeCarteraRealizado</c> que reabre un <c>CierreDeCarteraReabierto</c> (§7.4, §12): los dos nombran la misma corrida como
    /// origen. Lo lee el cierre contable para saber si el cierre de Cartera de un mes sigue vigente.
    /// </summary>
    private async Task<Guid?> RelacionadoAsync(MensajeEntrante m, CancellationToken ct)
    {
        if (m.Type != CierreDeCarteraReabiertoV1.Type) return null;
        var origen = m.Envelope.Origin.PublicId;
        var local = db.LendingPostings.Local.Where(p => p.MessageType == CierreDeCarteraRealizadoV1.Type && p.SourcePublicId == origen)
            .OrderByDescending(p => p.OperationDate).Select(p => (Guid?)p.MessagePublicId).FirstOrDefault();
        return local ?? await db.LendingPostings.AsNoTracking()
            .Where(p => p.MessageType == CierreDeCarteraRealizadoV1.Type && p.SourcePublicId == origen)
            .OrderByDescending(p => p.Id).Select(p => (Guid?)p.MessagePublicId).FirstOrDefaultAsync(ct);
    }

    /// <summary>El rechazo de una unidad que no se pudo armar: el primer error y todos con su hecho, rol, combinación y créditos.</summary>
    public static ResultadoDeConsumo.Rejected RechazoDe(ConstruccionDeCartera construccion)
    {
        var primero = construccion.Fallos[0];
        var datos = new
        {
            errors = construccion.Fallos.Select(f => new
            {
                factType = f.FactType, role = f.Role, combination = f.Combination, accountCode = f.AccountCode, loans = f.Loans,
                rule = f.Error.Code, message = f.Error.Message, data = (f.Error as ErrorConDatos)?.Data,
            }).ToList(),
        };
        return new ResultadoDeConsumo.Rejected(primero.Error.Code, primero.Error.Message, JsonSerializer.Serialize(datos, Json));
    }

    /// <summary>
    /// El rechazo del contrato de la 009 (reglas 1 a 11) con los errores de línea traducidos al hecho, el rol, el concepto y los créditos de
    /// donde salió cada línea (§7.1, paso 9).
    /// </summary>
    public static ResultadoDeConsumo.Rejected RechazoDe(ValidacionDeComprobante validacion, IReadOnlyList<LineaDeCartera> mapa, Error? error = null)
    {
        var errores = validacion.Errores;
        var codigo = errores.Count == 1 ? errores[0].Code : error?.Code ?? (errores.Count > 0 ? AccountingErrors.DocumentInvalid(errores).Code : "Accounting.Document.Invalid");
        var mensaje = errores.Count == 1 ? errores[0].Message : error?.Message ?? (errores.Count > 0 ? AccountingErrors.DocumentInvalid(errores).Message : string.Empty);
        var datos = new { errors = errores.Select(e => Traducido(e, mapa)).ToList() };
        return new ResultadoDeConsumo.Rejected(codigo, mensaje, JsonSerializer.Serialize(datos, Json));
    }

    /// <summary>Un error de línea del contrato con los hechos, el rol, el concepto, los créditos y la cuenta de su línea contable.</summary>
    public static object Traducido(ErrorDeLinea e, IReadOnlyList<LineaDeCartera> mapa)
    {
        var linea = e.LineNumber >= 1 && e.LineNumber <= mapa.Count ? mapa[e.LineNumber - 1] : null;
        return new
        {
            voucherLine = e.LineNumber, factTypes = linea?.Hechos ?? [], role = linea?.Rol, concept = linea?.Concepto, loans = linea?.Creditos ?? [], accounts = linea?.CuentasDeCaptacion ?? [],
            accountCode = e.AccountCode ?? linea?.AccountCode, field = e.Field, rule = e.Code, message = e.Message,
        };
    }

    /// <summary>
    /// Paso 9, la excepción de §8: un mes que no existe en Contabilidad es <c>Retry</c> (no rechazo) y levanta <c>Contabilidad.PeriodoInexistente</c>
    /// para quien abre ejercicios, una por mes. Nulo si el error es otro.
    /// </summary>
    public async Task<ResultadoDeConsumo.Retry?> PeriodoInexistenteAsync(Error error, DateOnly fecha, CancellationToken ct)
    {
        if (error.Code != CodigoPeriodoInexistente) return null;
        if (alertas is not null)
        {
            var mes = fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            await alertas.LevantarAsync(new AlertaALevantar(
                TiposDeAlerta.PeriodoInexistente,
                $"Llegó un hecho de Cartera para {mes}, que no existe en Contabilidad",
                $"Cartera fechó un hecho en {mes} y ese período no existe en Contabilidad. Ábralo para que el hecho se contabilice: hasta entonces queda en reintento.",
                DedupKey: $"{TiposDeAlerta.PeriodoInexistente}:{mes}",
                RecipientPermissions: [PermisoDePeriodos],
                ModuloDeOrigen: "Accounting"), ct);
        }
        return new ResultadoDeConsumo.Retry(error.Message, error.Code);
    }

    /// <summary>Evento <c>Accounting.Lending.Posted</c> del comprobante, por la cooperativa (§22).</summary>
    public Task AuditarContabilizacionAsync(AccountingDocument comprobante, IReadOnlyList<MensajeEntrante> hechos, Guid? lote, CancellationToken ct) =>
        auditoria.EmitirContabilizacionDeCarteraAsync(comprobante.PublicId, new
        {
            voucherTypeCode = comprobante.VoucherType?.Code, number = comprobante.Number, date = comprobante.Date,
            facts = hechos.Select(m => m.MessageId).ToList(), origins = hechos.Select(m => m.Envelope.Origin.PublicId).Distinct().ToList(),
            loans = hechos.Select(m => ConstructorDeLineasDeCartera.Credito(m.Payload)?.LoanNumber).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList(),
            batchPublicId = lote,
        }, ct);

    /// <summary>Evento <c>Accounting.Lending.Rejected</c> de cada hecho rechazado.</summary>
    public async Task AuditarRechazoAsync(IReadOnlyList<MensajeEntrante> hechos, ResultadoDeConsumo.Rejected rechazo, CancellationToken ct)
    {
        foreach (var m in hechos)
            await auditoria.EmitirRechazoDeCarteraAsync(m.MessageId, new { code = rechazo.Code, reason = rechazo.Reason, type = m.Type, origin = m.Envelope.Origin.PublicId }, ct);
    }

    public static ResultadoDeConsumo.Rejected Rechazo(Error error) => RecibosDeContabilidad.Rechazo(error);

    /// <summary>El motivo sin comprobante de un recibo como lo entiende la plataforma.</summary>
    public static MotivoSinComprobante? MotivoDe(string? motivo) => motivo switch
    {
        LendingPosting.SinComprobanteValorCero => MotivoSinComprobante.ZeroValue,
        LendingPosting.SinComprobanteInformativo => MotivoSinComprobante.Informational,
        LendingPosting.SinComprobanteMismasCuentas => MotivoSinComprobante.SameAccounts,
        _ => null,
    };
}
