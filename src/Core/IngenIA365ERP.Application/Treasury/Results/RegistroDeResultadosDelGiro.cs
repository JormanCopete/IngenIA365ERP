using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;
using IngenIA365ERP.Domain.Treasury.Resultados;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Results;

/// <summary>Lo que se registra de un resultado: el resultado, su fecha, la referencia de Tesorería y de dónde salió. (nuevo)</summary>
public sealed record ResultadoARegistrar(
    DisbursementOutcome Outcome,
    DateOnly OccurredOn,
    string TreasuryReference,
    string? CheckNumber,
    string? Reason,
    PaymentResponseSource Source,
    Guid? SupportAttachmentPublicId = null,
    int? PaymentResponseLineId = null,
    Guid? StatementLinePublicId = null);

/// <summary>Quién registra: una persona de Tesorería (con su <c>SEC_Users.Id</c>) o, en el «anulada», la persona del origen que anuló. (nuevo)</summary>
public sealed record QuienRegistra(int? UserId, Guid? CentralUserId, string Name)
{
    public UserRefV1 Ref => new() { CentralUserId = CentralUserId, Name = Name };
}

/// <summary>Lo que dejó un registro: el resultado y si ya estaba (la UK <c>(PaymentOrderId, Outcome)</c> lo hace idempotente). (nuevo)</summary>
public sealed record ResultadoRegistrado(DisbursementResult Resultado, bool Repetido);

/// <summary>
/// El único que registra el resultado del giro de una orden de pago (feature 015, US10 y US11, T0140, T0158; R-18, R-19; FR-115, FR-118 a FR-121, FR-142, FR-215).
/// Valida la transición con <see cref="TransicionesDelResultado"/>, guarda <c>TES_DisbursementResults</c> (repetir el mismo resultado no cambia nada), mueve la orden,
/// su ítem y su lote (parcial o confirmado), y lo informa a su origen <b>a nombre de la persona</b>: a Cartera y Captaciones como <c>ResultadoDelGiro</c> v1 sin cambios
/// (clave <c>Confirmation:{orden:N}</c>, <c>Amount = NetAmount</c>, <c>OrderMessagePublicId</c> = la <c>OrdenDeGiroEmitida</c> que la trajo), y a Recaudo por
/// <see cref="IResultadoDeOrdenesDeRecaudo"/>. Al «girada» de una orden pagada desde una cuenta propia emite <c>PagoDeOrdenGirado</c> y, si la devolución de Recaudo
/// lo causa, <c>GmfDePagoCausado</c>. Ningún proceso lo llama (<c>NingunProcesoAplicaPagosNiInformaResultados</c>): el «anulada» responde a la anulación de una
/// persona del origen y la nombra a ella. Escribe en la unidad de trabajo de quien llama, sin guardar; quien llama toma los cerrojos (los de Recaudo primero, con
/// <see cref="TomarCerrojosDeRecaudoAsync"/>). (nuevo)
/// </summary>
public sealed partial class RegistroDeResultadosDelGiro(
    IApplicationDbContext db,
    IDateTimeService reloj,
    EmisorDeMensajes emisor,
    EmisionDeTesoreria emision,
    ParametrosDeTesoreria parametros,
    IEnumerable<IResultadoDeOrdenesDeRecaudo> recaudo,
    NumeradorDeTesoreria? numerador = null)
{
    /// <summary>La clase del documento relacionado en <c>ResultadoDelGiro</c>: la orden de giro del origen (la misma que usa Cartera).</summary>
    public const string ClaseOrdenDeGiro = "DisbursementOrder";

    /// <summary>
    /// Antes de los cerrojos de Tesorería (orden global Recaudo → Tesorería): si alguna de las órdenes es de Recaudo, toma los suyos por el puerto. Sin
    /// implementación registrada (Recaudo llega con la fase 10), no hace nada.
    /// </summary>
    public async Task TomarCerrojosDeRecaudoAsync(IReadOnlyCollection<PaymentOrder> ordenes, CancellationToken ct)
    {
        var deRecaudo = ordenes.Where(o => o.Origin == PaymentOrderOrigin.Receipts).Select(o => o.PublicId).Distinct().ToList();
        if (deRecaudo.Count == 0) return;
        foreach (var puerto in recaudo) await puerto.TomarCerrojosAsync(deRecaudo, ct);
    }

    /// <summary>
    /// La anulación del origen sobre una orden recibida, pendiente, lista o en un lote no enviado (mensajes.md §5.2): la orden queda anulada y el resultado
    /// «anulada» sale a nombre de la persona del origen que anuló (<paramref name="quien"/>), nunca de un proceso (FR-215).
    /// </summary>
    public async Task AnuladaPorElOrigenAsync(PaymentOrder orden, UserRefV1 quien, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(quien);
        var hoy = reloj.HoyLocal;
        var r = await RegistrarAsync(orden, new ResultadoARegistrar(DisbursementOutcome.Voided, hoy, orden.Number, null, orden.VoidReason, PaymentResponseSource.Manual),
            new QuienRegistra(null, quien.CentralUserId, string.IsNullOrWhiteSpace(quien.Name) ? "Origen" : quien.Name), ct);
        if (r.IsFailure) throw new InvalidOperationException($"La anulación de la orden {orden.Number} no cabe: {r.Error.Message}");
    }

    /// <summary>Registra un resultado de una orden ya bloqueada por quien llama. Sin guardar.</summary>
    public async Task<Result<ResultadoRegistrado>> RegistrarAsync(PaymentOrder orden, ResultadoARegistrar r, QuienRegistra quien, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(quien);
        var previo = db.DisbursementResults.Local.FirstOrDefault(x => x.PaymentOrderId == orden.Id && x.Outcome == r.Outcome)
                     ?? await db.DisbursementResults.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentOrderId == orden.Id && x.Outcome == r.Outcome, ct);
        if (previo is not null) return Result.Success(new ResultadoRegistrado(previo, Repetido: true));

        var transicion = TransicionesDelResultado.Evaluar(orden.Status, orden.Form, orden.PaidByLendingFile, r.Outcome == DisbursementOutcome.Voided, r.Outcome);
        if (!transicion.Admite)
            return Result.Failure<ResultadoRegistrado>(transicion.Codigo == TransicionesDelResultado.CodigoNoEnviada
                ? ErroresDeTesoreria.ResultadoNoEnviada(orden.Number)
                : ErroresDeTesoreria.TransicionInvalida(orden.Number, orden.Status.ToString(), r.Outcome.ToString()));

        // Validación previa contable (pendiente de T0250, 2026-10-24; FR-221): el «girada» de una orden pagada desde una cuenta propia emite PagoDeOrdenGirado y, si
        // grava, GmfDePagoCausado; antes de tocar nada se pregunta si tienen su regla. Con «Bloquear» la pantalla no registra el resultado.
        if (r.Outcome == DisbursementOutcome.Disbursed && orden.Form != PaymentForm.Check)
        {
            var (_, _, cuentaPrevia) = await LoteDeLaOrdenAsync(orden, ct);
            if (cuentaPrevia is not null)
            {
                var tipos = new List<string> { PagoDeOrdenGiradoV1.Type };
                var pg = await parametros.GmfAsync(r.OccurredOn, ct);
                var gmfPrevio = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, orden.BeneficiaryIsDebtor,
                    cuentaPrevia.IsGmfExempt, orden.Origin == PaymentOrderOrigin.DepositsPayout), pg);
                if (gmfPrevio.EmiteHecho && gmfPrevio.Gmf > 0m) tipos.Add(GmfDePagoCausadoV1.Type);
                var previa = await emision.ValidacionPreviaAsync(tipos, cuentaPrevia, r.OccurredOn, ct);
                if (previa.IsFailure) return Result.Failure<ResultadoRegistrado>(previa.Error);
            }
        }

        var resultado = new DisbursementResult
        {
            PaymentOrderId = orden.Id, Outcome = r.Outcome, OccurredOn = r.OccurredOn, Amount = orden.NetAmount,
            TreasuryReference = Recortar(string.IsNullOrWhiteSpace(r.TreasuryReference) ? orden.Number : r.TreasuryReference, 60),
            CheckNumber = string.IsNullOrWhiteSpace(r.CheckNumber) ? null : Recortar(r.CheckNumber, 30),
            Reason = string.IsNullOrWhiteSpace(r.Reason) ? null : Recortar(r.Reason, 500), Source = r.Source, RecordedByUserId = quien.UserId,
            RecordedByCentralUserId = quien.CentralUserId, RecordedByName = Recortar(quien.Name, 150), RecordedAt = reloj.UtcNow,
            SupportAttachmentPublicId = r.SupportAttachmentPublicId, PaymentResponseLineId = r.PaymentResponseLineId, StatementLinePublicId = r.StatementLinePublicId,
        };
        orden.Status = transicion.Hacia!.Value;
        db.DisbursementResults.Add(resultado);

        var (item, lote, cuenta) = await LoteDeLaOrdenAsync(orden, ct);
        if (item is not null && lote is not null)
        {
            item.Status = r.Outcome switch
            {
                DisbursementOutcome.Disbursed => PaymentBatchItemState.Paid,
                DisbursementOutcome.Rejected => PaymentBatchItemState.Rejected,
                _ => item.Status,
            };
            if (lote.Status is PaymentBatchState.Sent or PaymentBatchState.PartiallyConfirmed)
                lote.Status = await TodosConResultadoAsync(lote, item, ct) ? PaymentBatchState.Confirmed : PaymentBatchState.PartiallyConfirmed;
        }

        if (orden.EsDeGiro)
            resultado.EmittedMessagePublicId = await EmitirResultadoAsync(orden, resultado, quien, cuenta, ct);
        else if (orden.Origin == PaymentOrderOrigin.Receipts)
            foreach (var puerto in recaudo) await puerto.InformarAsync(orden.PublicId, r.Outcome.ToString(), r.OccurredOn, resultado.Reason, ct);

        // T1 (fase 35, T0421; FR-117): el «girada» deja el comprobante de pago numerado de la orden, en esta misma unidad de trabajo.
        if (r.Outcome == DisbursementOutcome.Disbursed && numerador is not null)
            await ComprobantesDePago.EmitirAsync(db, numerador, orden, resultado, cuenta, reloj.UtcNow, ct);

        if (r.Outcome == DisbursementOutcome.Disbursed && cuenta is not null && orden.Form != PaymentForm.Check)
        {
            await emision.PagoDeOrdenGiradoAsync(cuenta, orden, resultado, ct);
            var p = await parametros.GmfAsync(r.OccurredOn, ct);
            var gmf = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, orden.BeneficiaryIsDebtor,
                cuenta.IsGmfExempt, orden.Origin == PaymentOrderOrigin.DepositsPayout), p);
            await emision.GmfDePagoCausadoAsync(cuenta, orden, gmf, p.Tarifa, r.OccurredOn, ct);
        }
        return Result.Success(new ResultadoRegistrado(resultado, Repetido: false));
    }

    /// <summary>El ítem vivo de la orden, su lote y la cuenta de origen (nulos si la orden no entró a un lote: pagada por el archivo de Cartera o anulada en la bandeja).</summary>
    private async Task<(PaymentBatchItem? Item, PaymentBatch? Lote, OwnBankAccount? Cuenta)> LoteDeLaOrdenAsync(PaymentOrder orden, CancellationToken ct)
    {
        var item = db.PaymentBatchItems.Local.FirstOrDefault(i => i.PaymentOrderId == orden.Id && i.IsLive)
                   ?? await db.PaymentBatchItems.FirstOrDefaultAsync(i => i.PaymentOrderId == orden.Id && i.IsLive, ct);
        if (item is null) return (null, null, null);
        var lote = db.PaymentBatches.Local.FirstOrDefault(b => b.Id == item.PaymentBatchId) ?? await db.PaymentBatches.FirstAsync(b => b.Id == item.PaymentBatchId, ct);
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == lote.BankAccountId, ct);
        return (item, lote, cuenta);
    }

    private async Task<bool> TodosConResultadoAsync(PaymentBatch lote, PaymentBatchItem actual, CancellationToken ct)
    {
        var items = await db.PaymentBatchItems.Where(i => i.PaymentBatchId == lote.Id && i.IsLive).ToListAsync(ct);
        return items.All(i => (i.Id == actual.Id ? actual.Status : i.Status) is PaymentBatchItemState.Paid or PaymentBatchItemState.Rejected);
    }

    /// <summary><c>ResultadoDelGiro</c> v1 sin cambios, a nombre de la persona, con el resultado como documento de origen y la orden del origen como relacionado.</summary>
    private async Task<Guid> EmitirResultadoAsync(PaymentOrder orden, DisbursementResult resultado, QuienRegistra quien, OwnBankAccount? cuenta, CancellationToken ct)
    {
        var sucursal = orden.BranchPublicId
                       ?? (cuenta is null ? null : await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct))
                       ?? await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => b.PublicId).FirstAsync(ct);
        var beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == orden.BeneficiaryPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var contenido = new ResultadoDelGiroV1
        {
            OrderPublicId = orden.OriginOrderPublicId,
            OrderMessagePublicId = orden.OriginMessagePublicId ?? Guid.Empty,
            Outcome = resultado.Outcome.ToString(),
            OccurredOn = resultado.OccurredOn,
            Amount = orden.NetAmount,
            TreasuryReference = resultado.TreasuryReference,
            CheckNumber = resultado.CheckNumber,
            Reason = resultado.Reason,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, resultado.PublicId, EmisionDeTesoreria.ClaseResultado, EmisionDeTesoreria.TipoDeOrden, orden.Number,
                resultado.OccurredOn, sucursal, PersonPublicId: beneficiario),
            ClavesDeEvento.ConfirmacionPor(orden.OriginOrderPublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: new DocumentoRelacionado(orden.OriginOrderPublicId, ClaseOrdenDeGiro, orden.OriginOrderNumber),
            UsuarioDeOrigen: quien.Ref), ct);
        return mensajes[0].PublicId;
    }

    private static string Recortar(string texto, int largo)
    {
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
