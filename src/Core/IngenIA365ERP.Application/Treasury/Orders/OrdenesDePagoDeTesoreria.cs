using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Orders;

/// <summary>
/// La orden de pago que Recaudo pide a Tesorería (feature 015, US5, T0177; R-05; FR-053, FR-054; contracts/interfaces.md §2): la devolución de un saldo a favor del
/// pagador, de un saldo a favor de Cartera por el recaudador o de una partida por identificar. Corre en la unidad de trabajo de Recaudo (misma base, misma
/// transacción) y después de sus cerrojos: toma los de Tesorería en su orden (configuración), numera con <c>OP</c> y deja la orden en la bandeja única, validada
/// como las de giro (<see cref="ValidadorDeOrdenes"/>). Idempotente por <see cref="SolicitudDeOrdenDePago.IdempotencyKey"/> (UK <c>(Origin, OriginOrderPublicId)</c>).
/// No guarda: quien llama guarda todo junto. El resultado vuelve a Recaudo por <c>IResultadoDeOrdenesDeRecaudo</c> (fase 8). (nuevo)
/// </summary>
public sealed class OrdenesDePagoDeTesoreria(
    IApplicationDbContext db,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IOrdenesDePagoDeTesoreria
{
    /// <summary>Las clases de orden que Recaudo pide (interfaces.md §2).</summary>
    public static readonly IReadOnlySet<string> ClasesDeRecaudo = new HashSet<string>(StringComparer.Ordinal)
    {
        "PayerCreditBalanceRefund", "LendingCreditBalanceRefund", "UnidentifiedItemRefund",
        // Feature 015, US14 (T0240): la devolución del dinero retenido en custodia por cumplimiento.
        "WithheldMoneyRefund",
    };

    public async Task<OrdenDePagoEmitidaDto> EmitirAsync(SolicitudDeOrdenDePago solicitud, CancellationToken ct)
    {
        var forma = ValidarSolicitud(solicitud);

        await cerrojo.TomarSetupAsync(ct: ct);
        var ya = db.PaymentOrders.Local.FirstOrDefault(o => o.Origin == PaymentOrderOrigin.Receipts && o.OriginOrderPublicId == solicitud.IdempotencyKey)
                 ?? await db.PaymentOrders.FirstOrDefaultAsync(o => o.Origin == PaymentOrderOrigin.Receipts && o.OriginOrderPublicId == solicitud.IdempotencyKey, ct);
        if (ya is not null) return new OrdenDePagoEmitidaDto(ya.PublicId, ya.Number, ya.Status.ToString());

        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == solicitud.BeneficiaryPersonPublicId)
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.TaxId, p.IdType }).FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("El beneficiario no existe en el maestro de personas.", nameof(solicitud));
        var cuenta = solicitud.PersonBankAccountPublicId is { } c
            ? await (from a in db.PersonBankAccounts.AsNoTracking()
                     join b in db.Banks.AsNoTracking() on a.BankId equals b.Id
                     where a.PublicId == c && a.PersonId == persona.Id
                     select new { a.Id, a.AccountNumber, a.AccountType, b.TransferCode, BankId = b.Id }).FirstOrDefaultAsync(ct)
            : null;

        var orden = new PaymentOrder
        {
            Origin = PaymentOrderOrigin.Receipts, OriginOrderPublicId = solicitud.IdempotencyKey, OriginOrderNumber = Recortar(solicitud.Concept, 30),
            OrderKind = solicitud.OrderKind, Form = forma, BeneficiaryPersonId = persona.Id,
            BeneficiaryName = Recortar($"{persona.FirstName} {persona.LastName}", 200), BeneficiaryTaxIdType = persona.IdType, BeneficiaryTaxId = Recortar(persona.TaxId ?? string.Empty, 20),
            DestinationBankCode = cuenta?.TransferCode, DestinationBankId = cuenta?.BankId, DestinationAccountNumber = cuenta?.AccountNumber,
            DestinationAccountType = cuenta?.AccountType, PersonBankAccountId = cuenta?.Id, GrossAmount = solicitud.NetAmount, NetAmount = solicitud.NetAmount,
            PayDate = solicitud.PayDate, Concept = Recortar(solicitud.Concept, 120), SourceDocumentPublicId = solicitud.SourceDocumentPublicId,
            BeneficiaryIsDebtor = false, ReceivedAt = reloj.UtcNow,
        };
        await validador.ValidarAsync(orden, ct);
        var gmf = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, false, false, false),
            await parametros.GmfAsync(orden.PayDate, ct));
        orden.GmfExempt = gmf.Exento;
        orden.GmfExemptionReason = gmf.Evaluado ? gmf.Motivo : null;
        orden.GmfEstimate = gmf.Gravado ? gmf.Gmf : 0m;
        orden.Number = await numerador.AsignarAsync(Integration.ConsumePaymentOrderCommandHandler.CodigoDeOrden, Integration.ConsumePaymentOrderCommandHandler.CodigoDeOrden, ct);
        db.PaymentOrders.Add(orden);
        await AuditarAsync(orden, ct);
        return new OrdenDePagoEmitidaDto(orden.PublicId, orden.Number, orden.Status.ToString());
    }

    /// <summary>El validador del puerto (desviación III): la clase de Recaudo, el valor positivo y una forma de pago de Tesorería.</summary>
    public static PaymentForm ValidarSolicitud(SolicitudDeOrdenDePago solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!ClasesDeRecaudo.Contains(solicitud.OrderKind))
            throw new ArgumentException($"«{solicitud.OrderKind}» no es una clase de orden de Recaudo.", nameof(solicitud));
        if (solicitud.NetAmount <= 0m) throw new ArgumentException("El valor de la orden tiene que ser positivo.", nameof(solicitud));
        if (solicitud.IdempotencyKey == Guid.Empty) throw new ArgumentException("La orden lleva su clave.", nameof(solicitud));
        if (!Enum.TryParse<PaymentForm>(solicitud.Form, ignoreCase: false, out var forma) || !Enum.IsDefined(forma))
            throw new ArgumentException($"«{solicitud.Form}» no es una forma de pago de Tesorería.", nameof(solicitud));
        return forma;
    }

    private Task AuditarAsync(PaymentOrder orden, CancellationToken ct) =>
        auditoria.AgregarAsync("Treasury.PaymentOrder.ReceivedFromReceipts", "PaymentOrder", orden.PublicId, null,
            new { number = orden.Number, kind = orden.OrderKind, amount = orden.NetAmount, status = orden.Status.ToString() }, ct);

    public async Task<EstadoDeOrdenDto> EstadoAsync(Guid paymentOrderPublicId, CancellationToken ct)
    {
        var orden = await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == paymentOrderPublicId, ct)
                    ?? throw new ArgumentException("La orden de pago no existe.", nameof(paymentOrderPublicId));
        var ultimo = await db.DisbursementResults.AsNoTracking().Where(r => r.PaymentOrderId == orden.Id).OrderByDescending(r => r.Id)
            .Select(r => new { r.OccurredOn, r.Reason }).FirstOrDefaultAsync(ct);
        return new EstadoDeOrdenDto(orden.PublicId, orden.Number, orden.Status.ToString(), ultimo?.OccurredOn, ultimo?.Reason);
    }

    private static string Recortar(string texto, int largo)
    {
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
