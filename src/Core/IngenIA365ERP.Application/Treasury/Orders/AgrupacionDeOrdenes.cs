using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Payables;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Orders;

// 2026-11-15 (pedido del dueño): varias cuentas por pagar de la misma persona se pagan en UN giro. Las órdenes (facturas de proveedor o cuentas por pagar
// contables, listas, a la misma cuenta destino y programadas para el mismo banco) se agrupan en una orden agrupadora con la suma de sus netos; ésa es la que
// entra al lote, sale en una sola línea del archivo y recibe un solo resultado. Mientras tanto las agrupadas quedan Grouped. Cuando la agrupadora se gira, cada
// agrupada queda girada con su propio resultado y su propio hecho contable (cada una cancela su cuenta por pagar, con su tercero y su cruce; Comercial recibe
// el aviso de cada factura); si se rechaza o se anula, se liberan y vuelven a quedar listas. Desagrupar, antes de que la agrupadora entre a un lote, las
// libera y anula la agrupadora. (nuevo)

/// <summary>Lo común de la agrupación. (nuevo)</summary>
public static class AgrupacionDeOrdenes
{
    /// <summary>La clase de la orden agrupadora (<c>OrderKind</c>): no es una cuenta por pagar, así que no sale en la lista de cuentas por pagar.</summary>
    public const string ClaseAgrupada = "GroupedPayment";

    public const string EventoAgrupadas = "Treasury.PaymentOrder.Grouped";
    public const string EventoDesagrupadas = "Treasury.PaymentOrder.Ungrouped";

    public static readonly IReadOnlySet<PaymentOrderOrigin> OrigenesAgrupables =
        new HashSet<PaymentOrderOrigin> { PaymentOrderOrigin.SupplierInvoice, PaymentOrderOrigin.AccountingPayable };

    public static bool EsAgrupadora(PaymentOrder o) => o.OrderKind == ClaseAgrupada;

    public static Error NoAgrupables(string detalle) => new("Treasury.Order.NotGroupable",
        $"No se pueden agrupar: {detalle}. Se agrupan dos o más facturas o cuentas por pagar contables listas, de la misma persona, a la misma cuenta destino, " +
        "programadas para el mismo banco y que no estén en un lote.");

    public static Error NoDesagrupable(string estado) => new ErrorConDatos("Treasury.Order.NotUngroupable",
        $"La orden agrupadora está «{estado}»: sólo se desagrupa antes de entrar a un lote (o cuando quedó rechazada o anulada).", new { state = estado });

    /// <summary>Las agrupadas vivas de una agrupadora (con seguimiento).</summary>
    public static Task<List<PaymentOrder>> HijasAsync(IApplicationDbContext db, PaymentOrder agrupadora, CancellationToken ct) =>
        db.PaymentOrders.Where(o => o.ConsolidatedIntoOrderId == agrupadora.Id && o.Status == PaymentOrderState.Grouped).ToListAsync(ct);

    /// <summary>Libera las agrupadas: vuelven a quedar listas y sin agrupadora. Sin guardar.</summary>
    public static async Task<IReadOnlyList<PaymentOrder>> LiberarAsync(IApplicationDbContext db, PaymentOrder agrupadora, CancellationToken ct)
    {
        var hijas = await HijasAsync(db, agrupadora, ct);
        foreach (var h in hijas)
        {
            h.Status = PaymentOrderState.Ready;
            h.ConsolidatedIntoOrderId = null;
            h.ConsolidatedInto = null;
        }
        return hijas;
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-orders/group</c> (<c>Treasury.Orders.Manage</c>, con clave): agrupa en una sola orden las órdenes indicadas. Devuelve la
/// agrupadora. (nuevo)
/// </summary>
public sealed record GroupPaymentOrdersCommand(IReadOnlyList<Guid> OrderPublicIds, string Reason)
    : IRequest<Result<PaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class GroupPaymentOrdersCommandValidator : AbstractValidator<GroupPaymentOrdersCommand>
{
    public GroupPaymentOrdersCommandValidator()
    {
        RuleFor(x => x.OrderPublicIds).NotNull().Must(l => l.Distinct().Count() >= 2).WithMessage("Elija dos o más órdenes para pagarlas juntas.")
            .Must(l => l.Count <= 200).WithMessage("Se agrupan hasta 200 órdenes a la vez.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo de la agrupación.").MaximumLength(500);
    }
}

public sealed class GroupPaymentOrdersCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<GroupPaymentOrdersCommand, Result<PaymentOrderDto>>
{
    public async Task<Result<PaymentOrderDto>> Handle(GroupPaymentOrdersCommand request, CancellationToken ct)
    {
        var pedidas = request.OrderPublicIds.Distinct().ToList();
        var ids = await db.PaymentOrders.AsNoTracking().Where(o => pedidas.Contains(o.PublicId)).Select(o => o.Id).ToListAsync(ct);
        if (ids.Count != pedidas.Count) return Result.Failure<PaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("Una de las órdenes"));
        var actor = await actorActual.ObtenerAsync(ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, ids.OrderBy(i => i).Select(i => (long)i).ToList(), ct: ct);
        var ordenes = await db.PaymentOrders.Where(o => ids.Contains(o.Id)).OrderBy(o => o.Id).ToListAsync(ct);

        var enLote = await db.PaymentBatchItems.AsNoTracking().AnyAsync(i => ids.Contains(i.PaymentOrderId) && i.IsLive, ct);
        string? problema =
            ordenes.Any(o => !AgrupacionDeOrdenes.OrigenesAgrupables.Contains(o.Origin) || AgrupacionDeOrdenes.EsAgrupadora(o))
                ? "sólo se agrupan facturas de proveedor y cuentas por pagar contables"
            : ordenes.Any(o => o.Status != PaymentOrderState.Ready || o.VoidRequested || o.PaidByLendingFile || o.ConsolidatedIntoOrderId is not null) || enLote
                ? "todas tienen que estar listas, fuera de un lote y sin anulación pedida"
            : ordenes.Any(o => o.Form != PaymentForm.Transfer) ? "todas tienen que pagarse por transferencia"
            : ordenes.Select(o => o.BeneficiaryPersonId).Distinct().Count() > 1 ? "son de personas distintas"
            : ordenes.Select(o => (o.DestinationBankCode, o.DestinationAccountNumber)).Distinct().Count() > 1 ? "van a cuentas destino distintas"
            : ordenes.Select(o => o.ScheduledBankAccountId).Distinct().Count() > 1 ? "están programadas para bancos de salida distintos"
            : null;
        if (problema is not null) return Result.Failure<PaymentOrderDto>(AgrupacionDeOrdenes.NoAgrupables(problema));

        var primera = ordenes[0];
        var neto = ordenes.Sum(o => o.NetAmount);
        var documentos = string.Join(", ", ordenes.Select(o => o.OriginOrderNumber));
        var agrupadora = await CuentasPorPagarDeTesoreria.NuevaOrdenAsync(db, numerador, validador, parametros, primera.Origin, AgrupacionDeOrdenes.ClaseAgrupada,
            Guid.NewGuid(), "AGRUPADA", primera.BeneficiaryPersonId, neto, ordenes.Sum(o => o.GrossAmount), ordenes.Max(o => o.PayDate),
            $"Pago agrupado de {ordenes.Count}: {documentos}", null, null, reloj.UtcNow, ct);
        agrupadora.OriginOrderNumber = agrupadora.Number;
        agrupadora.ScheduledBankAccountId = primera.ScheduledBankAccountId;
        // La cuenta destino es la de las agrupadas (la misma para todas), no la más reciente de la persona.
        agrupadora.DestinationBankCode = primera.DestinationBankCode;
        agrupadora.DestinationBankId = primera.DestinationBankId;
        agrupadora.DestinationAccountNumber = primera.DestinationAccountNumber;
        agrupadora.DestinationAccountType = primera.DestinationAccountType;
        agrupadora.PersonBankAccountId = primera.PersonBankAccountId;
        await validador.ValidarAsync(agrupadora, ct);

        foreach (var o in ordenes)
        {
            o.Status = PaymentOrderState.Grouped;
            o.ConsolidatedInto = agrupadora;
        }
        await auditoria.AgregarAsync(AgrupacionDeOrdenes.EventoAgrupadas, "PaymentOrder", agrupadora.PublicId, null,
            new { orders = ordenes.Select(o => o.Number).ToList(), net = neto, reason = request.Reason.Trim(), number = agrupadora.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesDePago.DtoAsync(db, agrupadora, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-orders/{id}/ungroup</c> (<c>Treasury.Orders.Manage</c>, con clave): libera las agrupadas de una agrupadora que todavía no
/// entró a un lote (o que quedó rechazada o anulada con alguna sin liberar) y anula la agrupadora. (nuevo)
/// </summary>
public sealed record UngroupPaymentOrderCommand(Guid PublicId, string Reason) : IRequest<Result<IReadOnlyList<PaymentOrderDto>>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class UngroupPaymentOrderCommandValidator : AbstractValidator<UngroupPaymentOrderCommand>
{
    public UngroupPaymentOrderCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo.").MaximumLength(500);
    }
}

public sealed class UngroupPaymentOrderCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<UngroupPaymentOrderCommand, Result<IReadOnlyList<PaymentOrderDto>>>
{
    private static readonly IReadOnlySet<PaymentOrderState> Desagrupables = new HashSet<PaymentOrderState>
    {
        PaymentOrderState.Received, PaymentOrderState.Pending, PaymentOrderState.Ready, PaymentOrderState.Rejected, PaymentOrderState.Voided,
    };

    public async Task<Result<IReadOnlyList<PaymentOrderDto>>> Handle(UngroupPaymentOrderCommand request, CancellationToken ct)
    {
        var id = await db.PaymentOrders.AsNoTracking().Where(o => o.PublicId == request.PublicId).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
        if (id is null) return Result.Failure<IReadOnlyList<PaymentOrderDto>>(ErroresDeTesoreria.NoEncontrado("La orden agrupadora"));
        var actor = await actorActual.ObtenerAsync(ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [id.Value], ct: ct);
        var agrupadora = await db.PaymentOrders.FirstAsync(o => o.Id == id.Value, ct);
        if (!AgrupacionDeOrdenes.EsAgrupadora(agrupadora))
            return Result.Failure<IReadOnlyList<PaymentOrderDto>>(AgrupacionDeOrdenes.NoAgrupables("la orden no es una agrupadora"));
        var enLote = await db.PaymentBatchItems.AsNoTracking().AnyAsync(i => i.PaymentOrderId == agrupadora.Id && i.IsLive, ct);
        if (!Desagrupables.Contains(agrupadora.Status) || (enLote && agrupadora.Status != PaymentOrderState.Rejected))
            return Result.Failure<IReadOnlyList<PaymentOrderDto>>(AgrupacionDeOrdenes.NoDesagrupable(agrupadora.Status.ToString()));

        var liberadas = await AgrupacionDeOrdenes.LiberarAsync(db, agrupadora, ct);
        var antes = agrupadora.Status;
        if (agrupadora.Status is not (PaymentOrderState.Voided or PaymentOrderState.Rejected))
        {
            agrupadora.Status = PaymentOrderState.Voided;
            agrupadora.VoidReason = CuentasPorPagarDeTesoreria.Recortar($"Desagrupada: {request.Reason}", 500);
            agrupadora.VoidedByCentralUserId = actor.CentralUserId;
            agrupadora.VoidedByName = CuentasPorPagarDeTesoreria.Recortar(actor.Name, 150);
        }
        await auditoria.AgregarAsync(AgrupacionDeOrdenes.EventoDesagrupadas, "PaymentOrder", agrupadora.PublicId, new { state = antes.ToString() },
            new { released = liberadas.Select(o => o.Number).ToList(), reason = request.Reason.Trim(), number = agrupadora.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesDePago.DtosAsync(db, liberadas, ct));
    }
}
