using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Orders;

// 2026-11-15 (pedido del dueño): programar el pago de lo causado. La orden nace con la fecha del origen (el vencimiento de la factura, la que eligió quien
// convirtió la cuenta por pagar) y hasta hoy no había cómo moverla: el calendario la mostraba, pero no la cambiaba. Ahora Tesorería la reprograma mientras la orden
// no esté en un lote, con motivo, clave de operación, cerrojo de la orden y su evento de auditoría con la fecha de antes y la de después. El banco de salida sigue
// siendo el del lote en que entre (un lote, una cuenta propia); desde la misma fecha se puede programar también la cuenta propia de salida
// (ScheduledBankAccountId) y un lote de otra cuenta ya no la admite. (nuevo)

/// <summary>
/// <c>POST /api/treasury/payment-orders/{id}/reschedule</c> (<c>Treasury.Orders.Manage</c>, con clave): cambia la fecha programada de pago de una orden recibida,
/// pendiente o lista que no está en un lote. La fecha no puede ser anterior a hoy. <paramref name="BankAccountPublicId"/> es la cuenta propia de salida:
/// nula la deja como está, <see cref="Guid.Empty"/> la quita (sale la del lote) y otra la programa (activa en la fecha). (nuevo)
/// </summary>
public sealed record ReschedulePaymentOrderCommand(Guid PublicId, DateOnly PayDate, string Reason, Guid? BankAccountPublicId = null)
    : IRequest<Result<PaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ReschedulePaymentOrderCommandValidator : AbstractValidator<ReschedulePaymentOrderCommand>
{
    public ReschedulePaymentOrderCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del cambio de fecha.").MaximumLength(500);
    }
}

/// <summary>Lo común de la reprogramación. (nuevo)</summary>
public static class ReprogramacionDeOrdenes
{
    public const string Evento = "Treasury.PaymentOrder.Rescheduled";

    /// <summary>Los estados en que la fecha todavía se puede mover: antes de entrar a un lote.</summary>
    public static readonly IReadOnlySet<PaymentOrderState> Reprogramables =
        new HashSet<PaymentOrderState> { PaymentOrderState.Received, PaymentOrderState.Pending, PaymentOrderState.Ready };

    public static Error NoReprogramable(string estado) => new ErrorConDatos("Treasury.Order.NotReschedulable",
        $"La orden está «{estado}»: sólo se reprograma una orden recibida, pendiente o lista que no esté en un lote (ni la pague un archivo de Cartera, ni su origen haya pedido anularla). Sáquela del lote primero.",
        new { state = estado });

    public static Error CuentaNoValida() => new("Treasury.Order.ScheduledBankAccountInvalid",
        "La cuenta de salida no es una cuenta bancaria propia activa en la fecha de pago.");

    public static Error FechaPasada(DateOnly hoy) => new ErrorConDatos("Treasury.Order.PayDateInPast",
        $"La fecha programada no puede ser anterior a hoy ({hoy:dd/MM/yyyy}).", new { today = hoy });
}

public sealed class ReschedulePaymentOrderCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ReschedulePaymentOrderCommand, Result<PaymentOrderDto>>
{
    public async Task<Result<PaymentOrderDto>> Handle(ReschedulePaymentOrderCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if (request.PayDate < hoy) return Result.Failure<PaymentOrderDto>(ReprogramacionDeOrdenes.FechaPasada(hoy));

        var id = await db.PaymentOrders.AsNoTracking().Where(o => o.PublicId == request.PublicId).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
        if (id is null) return Result.Failure<PaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("La orden de pago"));
        var actor = await actorActual.ObtenerAsync(ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [id.Value], ct: ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == id.Value, ct);
        if (!ReprogramacionDeOrdenes.Reprogramables.Contains(orden.Status) || orden.PaidByLendingFile || orden.VoidRequested)
            return Result.Failure<PaymentOrderDto>(ReprogramacionDeOrdenes.NoReprogramable(orden.Status.ToString()));

        int? banco = orden.ScheduledBankAccountId;
        if (request.BankAccountPublicId is { } pedida)
        {
            if (pedida == Guid.Empty) banco = null;
            else
            {
                var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == pedida, ct);
                if (cuenta is null || !cuenta.Activa(request.PayDate)) return Result.Failure<PaymentOrderDto>(ReprogramacionDeOrdenes.CuentaNoValida());
                banco = cuenta.Id;
            }
        }

        var antes = new { payDate = orden.PayDate, bankAccountId = orden.ScheduledBankAccountId };
        if (antes.payDate != request.PayDate || antes.bankAccountId != banco)
        {
            orden.PayDate = request.PayDate;
            orden.ScheduledBankAccountId = banco;
            await auditoria.AgregarAsync(ReprogramacionDeOrdenes.Evento, "PaymentOrder", orden.PublicId, antes,
                new { payDate = request.PayDate, bankAccountId = banco, reason = request.Reason.Trim(), number = orden.Number, by = actor.Name }, ct);
            await db.SaveChangesAsync(ct);
        }
        return Result.Success(await OrdenesDePago.DtoAsync(db, orden, ct));
    }
}
