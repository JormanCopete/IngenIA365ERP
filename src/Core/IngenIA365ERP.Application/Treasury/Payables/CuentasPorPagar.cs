using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Treasury.Batches;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Integration;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Payables;

// Feature 015, T2 (fase 42, T0455, T0456; FR-001 parte T2, FR-107, FR-124 a FR-127, FR-166, FR-167, FR-209 fin; US18): los pagos a proveedores y de cuentas por pagar. Las
// facturas de proveedor a crédito que Comercial confirma llegan a la bandeja de órdenes con su vencimiento y su neto (FacturaDeProveedorPorPagar); su anulación la saca del
// lote no enviado. Las cuentas por pagar contables se consultan a Contabilidad por tercero y documento cruce (IContabilidadParaTesoreria.CuentasPorPagarAsync) y se
// convierten en órdenes, también por una parte. Tesorería no registra facturas ni causa gastos: paga lo causado. Al girar, cada una tiene su hecho con tercero y cruce y la
// factura avisa a Comercial (PagoDeFacturaDeProveedor). El calendario de pagos agrupa lo abierto por su fecha de pago.

/// <summary>Una cuenta por pagar que Tesorería puede pagar: una orden de un proveedor o una cuenta por pagar contable (con lo que ya tiene en órdenes). (nuevo)</summary>
public sealed record PayableDto(string Source, Guid? PaymentOrderPublicId, string? OrderNumber, Guid? DocumentPublicId, string Document, Guid PersonPublicId, string PersonName,
    DateOnly DueDate, decimal Pending, decimal InOrders, PaymentOrderState? OrderStatus);

/// <summary>Un día del calendario de pagos: cuántas órdenes abiertas y por cuánto. (nuevo)</summary>
public sealed record DiaDelCalendarioDto(DateOnly Date, int Orders, decimal Total, IReadOnlyList<string> OrderNumbers);

/// <summary>Lo común de las cuentas por pagar. (nuevo)</summary>
public static class CuentasPorPagarDeTesoreria
{
    public const string ClaseProveedor = "SupplierInvoice";
    public const string ClaseContable = "AccountingPayable";

    /// <summary>Los estados de una orden que todavía no se giró ni se anuló.</summary>
    public static readonly IReadOnlySet<PaymentOrderState> Abiertas = new HashSet<PaymentOrderState>
    {
        PaymentOrderState.Received, PaymentOrderState.Pending, PaymentOrderState.Ready, PaymentOrderState.InBatch, PaymentOrderState.Approved, PaymentOrderState.Sent,
    };

    /// <summary>La cuenta destino activa más reciente de la persona (la que el validador verifica después).</summary>
    public static async Task<(string? Banco, string? Numero, string? Tipo, int? Id)> CuentaDestinoAsync(IApplicationDbContext db, int persona, CancellationToken ct)
    {
        var c = await (from a in db.PersonBankAccounts.AsNoTracking()
                       join b in db.Banks.AsNoTracking() on a.BankId equals b.Id
                       where a.PersonId == persona && a.Status == PersonBankAccountState.Active
                       orderby a.Id descending
                       select new { b.TransferCode, a.AccountNumber, a.AccountType, a.Id }).FirstOrDefaultAsync(ct);
        return c is null ? (null, null, null, null) : (c.TransferCode, c.AccountNumber, c.AccountType, c.Id);
    }

    /// <summary>Una orden nueva de la bandeja para pagar a una persona; el validador la deja lista o pendiente con sus motivos. Sin guardar.</summary>
    public static async Task<PaymentOrder> NuevaOrdenAsync(IApplicationDbContext db, NumeradorDeTesoreria numerador, ValidadorDeOrdenes validador, ParametrosDeTesoreria parametros,
        PaymentOrderOrigin origen, string clase, Guid origenPublicId, string numeroDelOrigen, int persona, decimal neto, decimal bruto, DateOnly fechaDePago, string concepto,
        Guid? documento, Guid? mensaje, DateTime ahora, CancellationToken ct)
    {
        var p = await db.People.AsNoTracking().FirstAsync(x => x.Id == persona, ct);
        var (banco, numero, tipo, cuentaId) = await CuentaDestinoAsync(db, persona, ct);
        var orden = new PaymentOrder
        {
            Origin = origen, OriginOrderPublicId = origenPublicId, OriginOrderNumber = Recortar(numeroDelOrigen, 40), OriginMessagePublicId = mensaje,
            OriginMessageVersion = mensaje is null ? null : (short)1, OrderKind = clase, Form = PaymentForm.Transfer, BeneficiaryPersonId = persona,
            BeneficiaryName = Recortar(Receipts.CashDesk.OperacionesDeCaja.Nombre(p), 200), BeneficiaryTaxIdType = p.IdType, BeneficiaryTaxId = Recortar(p.TaxId ?? string.Empty, 20),
            DestinationBankCode = banco, DestinationAccountNumber = numero, DestinationAccountType = tipo, PersonBankAccountId = cuentaId, GrossAmount = bruto, NetAmount = neto,
            PayDate = fechaDePago, Concept = Recortar(concepto, 120), SourceDocumentPublicId = documento, ReceivedAt = ahora, Status = PaymentOrderState.Received,
        };
        var gmf = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, false, false, false),
            await parametros.GmfAsync(orden.PayDate, ct));
        (orden.GmfExempt, orden.GmfExemptionReason, orden.GmfEstimate) = (gmf.Exento, gmf.Evaluado ? gmf.Motivo : null, gmf.Gravado ? gmf.Gmf : 0m);
        await numerador.NumerarAsync(orden, ConsumePaymentOrderCommandHandler.CodigoDeOrden, ConsumePaymentOrderCommandHandler.CodigoDeOrden, ct);
        await validador.ValidarAsync(orden, ct);
        db.PaymentOrders.Add(orden);
        return orden;
    }

    public static string Recortar(string texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }

    /// <summary>
    /// Revisión comercial (2026-11-12): una nota del proveedor (o su anulación) cambia lo que se le paga en una orden que todavía no salió al banco: el neto, el
    /// bruto (nunca menor que el neto), el GMF estimado con el valor nuevo y la validación, que la deja lista o pendiente. Sin guardar.
    /// </summary>
    public static async Task ReajustarAsync(PaymentOrder orden, decimal neto, decimal bruto, ValidadorDeOrdenes validador, ParametrosDeTesoreria parametros,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        orden.NetAmount = neto;
        orden.GrossAmount = Math.Max(bruto, neto);
        var gmf = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, false, false, false),
            await parametros.GmfAsync(orden.PayDate, ct));
        (orden.GmfExempt, orden.GmfExemptionReason, orden.GmfEstimate) = (gmf.Exento, gmf.Evaluado ? gmf.Motivo : null, gmf.Gravado ? gmf.Gmf : 0m);
        await validador.ValidarAsync(orden, ct);
    }
}

// ------------------------------------------------------------------------------------------------- facturas de Comercial --

/// <summary>
/// Consume en Tesorería <c>FacturaDeProveedorPorPagar</c> v1 y <c>AnulacionDeFacturaDeProveedor</c> v1 (feature 015, T2, fase 42, T0455; E-03; FR-124, FR-166): la factura
/// nace como orden de la bandeja (<c>Origin = SupplierInvoice</c>) con su vencimiento como fecha de pago y su neto; la anulación la saca del lote no enviado y la anula (si ya
/// salió en un archivo enviado o se giró, se rechaza: Comercial lo resuelve con su nota). Sin ruta; deja su recibo en <c>TES_InboundMessages</c>. (nuevo)
/// </summary>
public sealed record ConsumeSupplierInvoiceMessageCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeSupplierInvoiceMessageCommandValidator : AbstractValidator<ConsumeSupplierInvoiceMessageCommand>
{
    public ConsumeSupplierInvoiceMessageCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeSupplierInvoiceMessageCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    NumeradorDeTesoreria numerador,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    IMotorDeAprobaciones motor,
    IDateTimeService reloj) : IRequestHandler<ConsumeSupplierInvoiceMessageCommand, Result<ResultadoDeConsumo>>
{
    public const string CodigoYaEnviada = "Treasury.Payable.AlreadySent";
    public const string CodigoProveedorInexistente = "Treasury.Payable.SupplierNotFound";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeSupplierInvoiceMessageCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Treasury, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await RecibosDeTesoreria.PrevioAsync(db, mensaje.MessageId, ct) is { } previo) return Result.Success(RecibosDeTesoreria.Repetido(previo));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            switch (mensaje.Payload)
            {
                case FacturaDeProveedorPorPagarV1 f when mensaje.Version == 1:
                {
                    if (await db.PaymentOrders.AnyAsync(o => o.Origin == PaymentOrderOrigin.SupplierInvoice && o.OriginOrderPublicId == f.InvoicePublicId, ct))
                        return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, "La factura ya estaba en la bandeja.");
                    var proveedor = await db.People.AsNoTracking().Where(p => p.PublicId == f.SupplierPersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
                    if (proveedor is null)
                        return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Rejected, CodigoProveedorInexistente, $"El proveedor de la factura {f.InvoiceNumber} no existe.");
                    var orden = await CuentasPorPagarDeTesoreria.NuevaOrdenAsync(db, numerador, validador, parametros, PaymentOrderOrigin.SupplierInvoice,
                        CuentasPorPagarDeTesoreria.ClaseProveedor, f.InvoicePublicId, f.InvoiceNumber, proveedor.Value, f.AmountDue, f.Total, f.DueDate,
                        $"Factura {f.SupplierDocumentNumber}", f.InvoicePublicId, mensaje.MessageId, ahora, ct);
                    return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
                        string.Create(CultureInfo.GetCultureInfo("es-CO"), $"Factura {f.InvoiceNumber} por {f.AmountDue:N2}: orden {orden.Number} ({orden.Status})."));
                }
                case AnulacionDeFacturaDeProveedorV1 a when mensaje.Version == 1:
                {
                    var orden = await db.PaymentOrders.FirstOrDefaultAsync(o => o.Origin == PaymentOrderOrigin.SupplierInvoice && o.OriginOrderPublicId == a.InvoicePublicId, ct);
                    if (orden is null) return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, "La factura no tenía orden en la bandeja.");
                    if (orden.Status is PaymentOrderState.Sent or PaymentOrderState.Disbursed)
                        return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Rejected, CodigoYaEnviada,
                            $"La orden {orden.Number} de la factura {a.InvoiceNumber} ya salió al banco: no se anula desde Comercial.");
                    if (orden.Status is PaymentOrderState.InBatch or PaymentOrderState.Approved
                        && await db.PaymentBatchItems.FirstOrDefaultAsync(i => i.PaymentOrderId == orden.Id && i.IsLive, ct) is { } item)
                        await LotesDePago.SacarAsync(db, motor, item, $"Factura {a.InvoiceNumber} anulada en Comercial", ahora, ct);
                    (orden.Status, orden.VoidRequested, orden.VoidReason) = (PaymentOrderState.Voided, true, CuentasPorPagarDeTesoreria.Recortar(a.Reason ?? "Factura anulada", 500));
                    return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, $"Factura {a.InvoiceNumber} anulada: la orden {orden.Number} queda anulada.");
                }
                // Revisión comercial (2026-11-12): las notas del proveedor ajustan lo que se le debe, y su anulación lo revierte.
                case NotaDeProveedorPorPagarV1 n when mensaje.Version == 1:
                    return await NotaAsync(mensaje, n, ahora, ct);
                case AnulacionDeNotaDeProveedorV1 a when mensaje.Version == 1:
                    return await AnulacionDeNotaAsync(mensaje, a, ahora, ct);
                default:
                    return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Tesorería no acepta {mensaje.Type} v{mensaje.Version}."));
            }
        }, ct);
    }

    /// <summary>El texto del motivo con que una nota crédito deja en cero la orden de su factura (la anulación de la nota la reabre).</summary>
    public const string MotivoNotaEnCero = "Nota crédito del proveedor";

    /// <summary>
    /// Revisión comercial (2026-11-12): la nota <b>débito</b> nace como orden propia del proveedor (enlazada a la factura por <c>SourceDocumentPublicId</c>); la
    /// nota <b>crédito</b> disminuye la orden de la factura —sacándola de su lote si no salió al banco— y, si la deja en cero, la anula. Si la orden ya salió al
    /// banco, la nota crédito se rechaza con su recibo: el saldo a favor se cruza con el proveedor. Sin orden de la factura (de contado o anulada), no cambia nada.
    /// El recibo guarda la orden tocada: su anulación revierte exactamente eso.
    /// </summary>
    private async Task<Result<ResultadoDeConsumo>> NotaAsync(MensajeEntrante mensaje, NotaDeProveedorPorPagarV1 n, DateTime ahora, CancellationToken ct)
    {
        var es = CultureInfo.GetCultureInfo("es-CO");
        if (n.AmountDue <= 0m) return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, $"La nota {n.NoteNumber} no cambia lo que se le debe al proveedor.");
        if (n.Kind == ClasesDeNotaPorPagar.Debito)
        {
            if (await db.PaymentOrders.AnyAsync(o => o.Origin == PaymentOrderOrigin.SupplierInvoice && o.OriginOrderPublicId == n.NotePublicId, ct))
                return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, "La nota débito ya estaba en la bandeja.");
            var proveedor = await db.People.AsNoTracking().Where(p => p.PublicId == n.SupplierPersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (proveedor is null)
                return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Rejected, CodigoProveedorInexistente, $"El proveedor de la nota {n.NoteNumber} no existe.");
            var nueva = await CuentasPorPagarDeTesoreria.NuevaOrdenAsync(db, numerador, validador, parametros, PaymentOrderOrigin.SupplierInvoice,
                CuentasPorPagarDeTesoreria.ClaseProveedor, n.NotePublicId, n.NoteNumber, proveedor.Value, n.AmountDue, n.Total, n.DueDate,
                $"Nota débito {n.SupplierDocumentNumber} de la factura {n.InvoiceNumber}", n.InvoicePublicId, mensaje.MessageId, ahora, ct);
            await db.SaveChangesAsync(ct);
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
                string.Create(es, $"Nota débito {n.NoteNumber} por {n.AmountDue:N2}: orden {nueva.Number} ({nueva.Status})."), nueva.Id);
        }

        var orden = await db.PaymentOrders.FirstOrDefaultAsync(o => o.Origin == PaymentOrderOrigin.SupplierInvoice && o.OriginOrderPublicId == n.InvoicePublicId, ct);
        if (orden is null || orden.Status == PaymentOrderState.Voided)
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
                $"La factura {n.InvoiceNumber} no tiene orden abierta en Tesorería: la nota crédito {n.NoteNumber} no cambia nada aquí.");
        if (orden.Status is PaymentOrderState.Sent or PaymentOrderState.Disbursed)
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Rejected, CodigoYaEnviada,
                $"La orden {orden.Number} de la factura {n.InvoiceNumber} ya salió al banco: el saldo a favor de la nota crédito {n.NoteNumber} se cruza con el proveedor.");
        await SacarDelLoteAsync(orden, $"Nota crédito {n.NoteNumber} de la factura {n.InvoiceNumber}", ahora, ct);
        var neto = orden.NetAmount - n.AmountDue;
        if (neto <= 0m)
        {
            (orden.Status, orden.VoidRequested, orden.VoidReason) = (PaymentOrderState.Voided, false,
                CuentasPorPagarDeTesoreria.Recortar($"{MotivoNotaEnCero} {n.NoteNumber}: la factura {n.InvoiceNumber} queda sin saldo.", 500));
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
                $"Nota crédito {n.NoteNumber}: la orden {orden.Number} de la factura {n.InvoiceNumber} queda sin saldo y anulada.", orden.Id);
        }
        await CuentasPorPagarDeTesoreria.ReajustarAsync(orden, neto, orden.GrossAmount - n.Total, validador, parametros, ct);
        return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
            string.Create(es, $"Nota crédito {n.NoteNumber} por {n.AmountDue:N2}: la orden {orden.Number} queda en {orden.NetAmount:N2} ({orden.Status})."), orden.Id);
    }

    /// <summary>
    /// La anulación de una nota revierte lo que su recibo dice que hizo: la orden de la nota débito se anula (si no salió al banco); a la orden de la factura que la
    /// nota crédito disminuyó le vuelve el valor (y si la nota la había dejado en cero, se reabre); si esa orden ya salió al banco, lo que la nota había quitado
    /// nace como una orden nueva. Sin recibo procesado (la nota no había cambiado nada), no hace nada.
    /// </summary>
    private async Task<Result<ResultadoDeConsumo>> AnulacionDeNotaAsync(MensajeEntrante mensaje, AnulacionDeNotaDeProveedorV1 a, DateTime ahora, CancellationToken ct)
    {
        var es = CultureInfo.GetCultureInfo("es-CO");
        var mensajesDeLaNota = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == a.NotePublicId && m.Type == NotaDeProveedorPorPagarV1.Type).Select(m => m.PublicId).ToListAsync(ct);
        var aplicada = await db.TreasuryInboundMessages.AsNoTracking()
            .Where(r => mensajesDeLaNota.Contains(r.MessagePublicId) && r.Outcome == InboundMessageOutcome.Processed && r.PaymentOrderId != null)
            .Select(r => r.PaymentOrderId).FirstOrDefaultAsync(ct);
        if (aplicada is not { } ordenId)
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, $"La nota {a.NoteNumber} no había cambiado nada en Tesorería.");
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == ordenId, ct);

        if (a.Kind == ClasesDeNotaPorPagar.Debito)
        {
            if (orden.Status is PaymentOrderState.Sent or PaymentOrderState.Disbursed)
                return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Rejected, CodigoYaEnviada,
                    $"La orden {orden.Number} de la nota débito {a.NoteNumber} ya salió al banco: no se anula desde Comercial.");
            if (orden.Status == PaymentOrderState.Voided)
                return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, $"La orden {orden.Number} de la nota débito ya estaba anulada.", orden.Id);
            await SacarDelLoteAsync(orden, $"Nota débito {a.NoteNumber} anulada en Comercial", ahora, ct);
            (orden.Status, orden.VoidRequested, orden.VoidReason) = (PaymentOrderState.Voided, true, CuentasPorPagarDeTesoreria.Recortar(a.Reason ?? "Nota débito anulada", 500));
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, $"Nota débito {a.NoteNumber} anulada: la orden {orden.Number} queda anulada.", orden.Id);
        }

        if (orden.Status is PaymentOrderState.Sent or PaymentOrderState.Disbursed)
        {
            // La factura ya se pagó por lo que dejó la nota: lo que la nota había quitado se vuelve a deber, en una orden propia.
            if (await db.PaymentOrders.AnyAsync(o => o.Origin == PaymentOrderOrigin.SupplierInvoice && o.OriginOrderPublicId == mensaje.Envelope.Origin.PublicId, ct))
                return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null, "La devolución de la nota ya estaba en la bandeja.");
            var nueva = await CuentasPorPagarDeTesoreria.NuevaOrdenAsync(db, numerador, validador, parametros, PaymentOrderOrigin.SupplierInvoice,
                CuentasPorPagarDeTesoreria.ClaseProveedor, mensaje.Envelope.Origin.PublicId, a.NoteNumber, orden.BeneficiaryPersonId, a.AmountDue, a.Total,
                reloj.HoyLocal, $"Anulación de la nota crédito {a.NoteNumber} de la factura {a.InvoiceNumber}", a.InvoicePublicId, mensaje.MessageId, ahora, ct);
            await db.SaveChangesAsync(ct);
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
                string.Create(es, $"Nota crédito {a.NoteNumber} anulada después del pago: orden {nueva.Number} por {a.AmountDue:N2}."), nueva.Id);
        }
        if (orden.Status == PaymentOrderState.Voided && (orden.VoidRequested || orden.VoidReason?.StartsWith(MotivoNotaEnCero, StringComparison.Ordinal) != true))
            return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
                $"La orden {orden.Number} de la factura {a.InvoiceNumber} está anulada: no hay nada que devolverle.", orden.Id);

        await SacarDelLoteAsync(orden, $"Nota crédito {a.NoteNumber} anulada en Comercial", ahora, ct);
        var neto = orden.Status == PaymentOrderState.Voided ? a.AmountDue : orden.NetAmount + a.AmountDue;
        var bruto = orden.Status == PaymentOrderState.Voided ? a.Total : orden.GrossAmount + a.Total;
        if (orden.Status == PaymentOrderState.Voided) (orden.VoidRequested, orden.VoidReason) = (false, null);
        await CuentasPorPagarDeTesoreria.ReajustarAsync(orden, neto, bruto, validador, parametros, ct);
        return await ReciboAsync(mensaje, ahora, InboundMessageOutcome.Processed, null,
            string.Create(es, $"Nota crédito {a.NoteNumber} anulada: la orden {orden.Number} vuelve a {orden.NetAmount:N2} ({orden.Status})."), orden.Id);
    }

    /// <summary>Una orden en un lote no enviado sale de él antes de cambiar su valor (el lote se recalcula y su aprobación se invalida).</summary>
    private async Task SacarDelLoteAsync(PaymentOrder orden, string motivo, DateTime ahora, CancellationToken ct)
    {
        if (orden.Status is PaymentOrderState.InBatch or PaymentOrderState.Approved
            && await db.PaymentBatchItems.FirstOrDefaultAsync(i => i.PaymentOrderId == orden.Id && i.IsLive, ct) is { } item)
            await LotesDePago.SacarAsync(db, motor, item, motivo, ahora, ct);
    }

    private async Task<Result<ResultadoDeConsumo>> ReciboAsync(MensajeEntrante mensaje, DateTime ahora, InboundMessageOutcome resultado, string? codigo, string detalle,
        int? orden = null)
    {
        db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, resultado, orden, codigo, detalle));
        await db.SaveChangesAsync();
        return resultado == InboundMessageOutcome.Rejected
            ? Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(codigo!, detalle))
            : Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
    }
}

// ------------------------------------------------------------------------------------------------------ consulta y conversión --

/// <summary>
/// <c>GET /api/treasury/payables?person=&amp;dueUntil=</c> (<c>Treasury.Payables.View</c>; FR-124, FR-125): lo que Tesorería puede pagar: las órdenes abiertas de facturas de
/// proveedor y las cuentas por pagar contables por tercero y documento cruce, con lo que ya está en órdenes vivas. (nuevo)
/// </summary>
public sealed record ListPayablesQuery(Guid? PersonPublicId = null, DateOnly? DueUntil = null) : IRequest<Result<IReadOnlyList<PayableDto>>>;

public sealed class ListPayablesQueryHandler(IApplicationDbContext db, IContabilidadParaTesoreria contabilidad)
    : IRequestHandler<ListPayablesQuery, Result<IReadOnlyList<PayableDto>>>
{
    public async Task<Result<IReadOnlyList<PayableDto>>> Handle(ListPayablesQuery request, CancellationToken ct)
    {
        var lista = new List<PayableDto>();
        int? persona = request.PersonPublicId is { } pp
            ? await db.People.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct)
            : null;
        var abiertas = CuentasPorPagarDeTesoreria.Abiertas.ToList();
        var ordenes = await db.PaymentOrders.AsNoTracking()
            .Where(o => (o.Origin == PaymentOrderOrigin.SupplierInvoice || o.Origin == PaymentOrderOrigin.AccountingPayable) && abiertas.Contains(o.Status)
                        && (persona == null || o.BeneficiaryPersonId == persona) && (request.DueUntil == null || o.PayDate <= request.DueUntil))
            .ToListAsync(ct);
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ordenes.Select(o => o.BeneficiaryPersonId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        lista.AddRange(ordenes.Where(o => o.Origin == PaymentOrderOrigin.SupplierInvoice).Select(o => new PayableDto(CuentasPorPagarDeTesoreria.ClaseProveedor, o.PublicId, o.Number,
            o.OriginOrderPublicId, o.OriginOrderNumber, personas[o.BeneficiaryPersonId], o.BeneficiaryName, o.PayDate, o.NetAmount, o.NetAmount, o.Status)));

        var contables = await contabilidad.CuentasPorPagarAsync(new FiltroDeCuentasPorPagar(request.PersonPublicId, request.DueUntil), ct);
        var nombres = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => contables.Select(c => c.PersonPublicId).Contains(p.PublicId))
            .ToDictionaryAsync(p => p.PublicId, p => Receipts.CashDesk.OperacionesDeCaja.Nombre(p), ct);
        foreach (var c in contables)
        {
            var enOrdenes = ordenes.Where(o => o.Origin == PaymentOrderOrigin.AccountingPayable && o.OriginOrderNumber == c.Number && personas[o.BeneficiaryPersonId] == c.PersonPublicId)
                .Sum(o => o.NetAmount);
            lista.Add(new PayableDto(CuentasPorPagarDeTesoreria.ClaseContable, null, null, c.DocumentPublicId, c.Number, c.PersonPublicId, nombres.GetValueOrDefault(c.PersonPublicId, ""),
                c.DueDate, c.Pending, enOrdenes, null));
        }
        return Result.Success<IReadOnlyList<PayableDto>>(lista.OrderBy(x => x.DueDate).ThenBy(x => x.Document).ToList());
    }
}

/// <summary>
/// <c>POST /api/treasury/payables/orders</c> (<c>Treasury.Payables.Manage</c>, con clave; FR-125, FR-127): convierte una cuenta por pagar contable —por su tercero y su
/// documento cruce— en una orden de la bandeja por todo o por una parte de lo pendiente (sin pasar de lo pendiente menos lo que ya está en órdenes). (nuevo)
/// </summary>
public sealed record ConvertAccountingPayableCommand(Guid PersonPublicId, string CrossDocument, decimal Amount, DateOnly? PayDate = null, string? Concept = null)
    : IRequest<Result<PayableDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ConvertAccountingPayableCommandValidator : AbstractValidator<ConvertAccountingPayableCommand>
{
    public ConvertAccountingPayableCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.CrossDocument).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Concept).MaximumLength(120);
    }
}

public sealed class ConvertAccountingPayableCommandHandler(
    IApplicationDbContext db,
    IContabilidadParaTesoreria contabilidad,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ConvertAccountingPayableCommand, Result<PayableDto>>
{
    public async Task<Result<PayableDto>> Handle(ConvertAccountingPayableCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<PayableDto>(ErroresDeTesoreria.NoEncontrado("El tercero"));
        var cruce = request.CrossDocument.Trim();
        var cuenta = (await contabilidad.CuentasPorPagarAsync(new FiltroDeCuentasPorPagar(request.PersonPublicId, null), ct)).FirstOrDefault(c => c.Number == cruce);
        if (cuenta is null) return Result.Failure<PayableDto>(ErroresDeTesoreriaT2Pagos.CuentaPorPagarInexistente(cruce));
        await cerrojo.TomarSetupAsync(ct: ct);
        var abiertas = CuentasPorPagarDeTesoreria.Abiertas.ToList();
        var enOrdenes = await db.PaymentOrders.Where(o => o.Origin == PaymentOrderOrigin.AccountingPayable && o.BeneficiaryPersonId == persona && o.OriginOrderNumber == cruce
                                                          && abiertas.Contains(o.Status)).SumAsync(o => o.NetAmount, ct);
        var disponible = cuenta.Pending - enOrdenes;
        if (request.Amount > disponible) return Result.Failure<PayableDto>(ErroresDeTesoreriaT2Pagos.SobreLoPendiente(disponible));
        var hoy = reloj.HoyLocal;
        var orden = await CuentasPorPagarDeTesoreria.NuevaOrdenAsync(db, numerador, validador, parametros, PaymentOrderOrigin.AccountingPayable,
            CuentasPorPagarDeTesoreria.ClaseContable, cuenta.DocumentPublicId, cruce, persona.Value, request.Amount, request.Amount, request.PayDate ?? hoy,
            request.Concept ?? $"Cuenta por pagar {cruce}", cuenta.DocumentPublicId, null, reloj.UtcNow, ct);
        await auditoria.AgregarAsync("Treasury.Payable.OrderCreated", "PaymentOrder", orden.PublicId, null,
            new { number = orden.Number, cross = cruce, amount = request.Amount, pending = cuenta.Pending, partial = request.Amount < disponible, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new PayableDto(CuentasPorPagarDeTesoreria.ClaseContable, orden.PublicId, orden.Number, cuenta.DocumentPublicId, cruce, request.PersonPublicId,
            orden.BeneficiaryName, orden.PayDate, cuenta.Pending, enOrdenes + request.Amount, orden.Status));
    }
}

/// <summary>
/// <c>GET /api/treasury/payables/calendar?from=&amp;to=</c> (<c>Treasury.Payables.View</c>; FR-126): las órdenes abiertas de la bandeja agrupadas por su fecha de pago (la
/// programación por vencimiento). (nuevo)
/// </summary>
public sealed record GetPaymentCalendarQuery(DateOnly? From = null, DateOnly? To = null) : IRequest<Result<IReadOnlyList<DiaDelCalendarioDto>>>;

public sealed class GetPaymentCalendarQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetPaymentCalendarQuery, Result<IReadOnlyList<DiaDelCalendarioDto>>>
{
    public async Task<Result<IReadOnlyList<DiaDelCalendarioDto>>> Handle(GetPaymentCalendarQuery request, CancellationToken ct)
    {
        var desde = request.From ?? reloj.HoyLocal;
        var hasta = request.To ?? desde.AddDays(30);
        var abiertas = CuentasPorPagarDeTesoreria.Abiertas.Where(s => s != PaymentOrderState.Sent).ToList();
        var ordenes = await db.PaymentOrders.AsNoTracking().Where(o => abiertas.Contains(o.Status) && o.PayDate <= hasta).ToListAsync(ct);
        var dias = ordenes.GroupBy(o => o.PayDate < desde ? desde : o.PayDate).OrderBy(g => g.Key)
            .Select(g => new DiaDelCalendarioDto(g.Key, g.Count(), g.Sum(o => o.NetAmount), g.Select(o => o.Number).OrderBy(n => n).ToList())).ToList();
        return Result.Success<IReadOnlyList<DiaDelCalendarioDto>>(dias);
    }
}

/// <summary>Los errores de las cuentas por pagar (feature 015, T2, fase 42; nuevos, DT §2.7). (nuevo)</summary>
public static class ErroresDeTesoreriaT2Pagos
{
    public static Error CuentaPorPagarInexistente(string cruce) => new("Treasury.Payable.NotFound",
        $"No hay una cuenta por pagar contable pendiente con el documento {cruce} para ese tercero.");

    public static Error SobreLoPendiente(decimal disponible) => new ErrorConDatos("Treasury.Payable.OverPending",
        string.Create(CultureInfo.GetCultureInfo("es-CO"), $"Lo que se puede pagar de esa cuenta es {disponible:N2} (lo pendiente menos lo que ya está en órdenes)."),
        new { available = disponible });
}
