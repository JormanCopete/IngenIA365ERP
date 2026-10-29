using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;
using IngenIA365ERP.Domain.Treasury.Lotes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Orders;

// Feature 015, D-a (decisión del dueño del 2026-10-27; FR-229; contracts/api.md §10): una orden de transferencia que llegó sin cuenta destino
// (Treasury.Order.DestinationMissing) —típicamente el giro de Captaciones a un asociado sin cuenta de depósito— la resuelve Tesorería sin esperar un regiro del
// origen: le completa la cuenta destino con una cuenta registrada y activa del beneficiario, o la pasa a cheque y sale por el flujo de cheques de siempre. Las dos
// llevan motivo, su permiso (Treasury.Orders.ResolveDestination), clave de operación, cerrojo de la orden y su evento de auditoría con antes y después, y terminan
// volviendo a validar la orden como cualquiera de la bandeja (cuenta registrada, listas, marcas). Lo demás que trajo el origen (beneficiario, neto) no cambia. (nuevo)

/// <summary>Lo común de la resolución del destino. (nuevo)</summary>
public static class ResolucionDeDestinoDeOrdenes
{
    public const string Permiso = "Treasury.Orders.ResolveDestination";

    public const string EventoCuentaCompletada = "Treasury.PaymentOrder.DestinationCompleted";
    public const string EventoPasadaACheque = "Treasury.PaymentOrder.SwitchedToCheck";

    public static Error NoResoluble(string estado) => new ErrorConDatos(ResolucionDeDestino.NoResoluble,
        $"La orden está «{estado}» (o la paga un archivo de Cartera, o el origen pidió anularla): Tesorería sólo resuelve el destino de una orden recibida, pendiente o lista.",
        new { state = estado });

    public static Error NoLeFaltaDestino() => new(ResolucionDeDestino.NoLeFaltaDestino,
        "La orden no está pendiente por falta de cuenta destino: si la cuenta no está registrada o está en espera, corríjala en las cuentas del beneficiario y vuelva a validarla.");

    public static Error CuentaNoValida() => new("Treasury.Order.DestinationAccountInvalid",
        "La cuenta elegida no es una cuenta registrada y activa del beneficiario de la orden: regístrela (con su doble control) y vuelva a intentarlo.");

    /// <summary>Lee la orden bajo su cerrojo y comprueba que se pueda resolver. (nuevo)</summary>
    internal static async Task<Result<PaymentOrder>> OrdenResolubleAsync(IApplicationDbContext db, ICerrojoDeTesoreria cerrojo, Guid publicId, CancellationToken ct)
    {
        var id = await db.PaymentOrders.AsNoTracking().Where(o => o.PublicId == publicId).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
        if (id is null) return Result.Failure<PaymentOrder>(ErroresDeTesoreria.NoEncontrado("La orden de pago"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [id.Value], ct: ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == id.Value, ct);
        return ResolucionDeDestino.Evaluar(orden.Status, orden.Form, orden.PaidByLendingFile, orden.VoidRequested, ValidadorDeOrdenes.Motivos(orden)) switch
        {
            null => Result.Success(orden),
            ResolucionDeDestino.NoResoluble => Result.Failure<PaymentOrder>(NoResoluble(orden.Status.ToString())),
            _ => Result.Failure<PaymentOrder>(NoLeFaltaDestino()),
        };
    }

    internal static object Foto(PaymentOrder o) => new
    {
        form = o.Form.ToString(), bank = o.DestinationBankCode, account = o.DestinationAccountNumber is null ? null : CuentasBancariasPropias.Enmascarar(o.DestinationAccountNumber),
        status = o.Status.ToString(), reasons = ValidadorDeOrdenes.Motivos(o),
    };
}

/// <summary>
/// <c>POST /api/treasury/payment-orders/{id}/destination</c> (<c>Treasury.Orders.ResolveDestination</c>, con clave; D-a, FR-229): completa la cuenta destino de una
/// orden que llegó sin ella con una cuenta <b>registrada y activa</b> del beneficiario (la que aprobó otra persona con su doble control y certificación), con motivo y
/// auditada, y la vuelve a validar. (nuevo)
/// </summary>
public sealed record CompletePaymentOrderDestinationCommand(Guid PublicId, Guid PersonBankAccountPublicId, string Reason)
    : IRequest<Result<PaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class CompletePaymentOrderDestinationCommandValidator : AbstractValidator<CompletePaymentOrderDestinationCommand>
{
    public CompletePaymentOrderDestinationCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.PersonBankAccountPublicId).NotEmpty().WithMessage("Elija la cuenta registrada del beneficiario a la que se gira.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo (por ejemplo, el soporte con el que el beneficiario indicó su cuenta).").MaximumLength(500);
    }
}

public sealed class CompletePaymentOrderDestinationCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    ValidadorDeOrdenes validador,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<CompletePaymentOrderDestinationCommand, Result<PaymentOrderDto>>
{
    public async Task<Result<PaymentOrderDto>> Handle(CompletePaymentOrderDestinationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var leida = await ResolucionDeDestinoDeOrdenes.OrdenResolubleAsync(db, cerrojo, request.PublicId, ct);
        if (leida.IsFailure) return Result.Failure<PaymentOrderDto>(leida.Error);
        var orden = leida.Value;
        var cuenta = await (from a in db.PersonBankAccounts.AsNoTracking()
                            join b in db.Banks.AsNoTracking() on a.BankId equals b.Id
                            where a.PublicId == request.PersonBankAccountPublicId && a.PersonId == orden.BeneficiaryPersonId && a.Status == PersonBankAccountState.Active
                            select new { a.Id, a.AccountNumber, a.AccountType, b.TransferCode, BankId = b.Id }).FirstOrDefaultAsync(ct);
        if (cuenta is null) return Result.Failure<PaymentOrderDto>(ResolucionDeDestinoDeOrdenes.CuentaNoValida());

        var antes = ResolucionDeDestinoDeOrdenes.Foto(orden);
        orden.DestinationBankCode = cuenta.TransferCode;
        orden.DestinationBankId = cuenta.BankId;
        orden.DestinationAccountNumber = cuenta.AccountNumber;
        orden.DestinationAccountType = cuenta.AccountType;
        orden.PersonBankAccountId = cuenta.Id;
        await validador.ValidarAsync(orden, ct);
        await auditoria.AgregarAsync(ResolucionDeDestinoDeOrdenes.EventoCuentaCompletada, "PaymentOrder", orden.PublicId, antes,
            new { after = ResolucionDeDestinoDeOrdenes.Foto(orden), reason = request.Reason.Trim(), number = orden.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesDePago.DtoAsync(db, orden, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-orders/{id}/pay-by-check</c> (<c>Treasury.Orders.ResolveDestination</c>, con clave; D-a, FR-229): pasa a cheque una orden de
/// transferencia que llegó sin cuenta destino, con motivo y auditada; se vuelve a validar (un cheque no necesita cuenta destino), se recalcula su GMF estimado y queda
/// lista para un lote de forma «cheques», donde sigue el flujo de cheques de siempre (emisión, entrega con constancia, resultado). (nuevo)
/// </summary>
public sealed record PayPaymentOrderByCheckCommand(Guid PublicId, string Reason) : IRequest<Result<PaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class PayPaymentOrderByCheckCommandValidator : AbstractValidator<PayPaymentOrderByCheckCommand>
{
    public PayPaymentOrderByCheckCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo por el que la orden se gira por cheque.").MaximumLength(500);
    }
}

public sealed class PayPaymentOrderByCheckCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<PayPaymentOrderByCheckCommand, Result<PaymentOrderDto>>
{
    public async Task<Result<PaymentOrderDto>> Handle(PayPaymentOrderByCheckCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var leida = await ResolucionDeDestinoDeOrdenes.OrdenResolubleAsync(db, cerrojo, request.PublicId, ct);
        if (leida.IsFailure) return Result.Failure<PaymentOrderDto>(leida.Error);
        var orden = leida.Value;

        var antes = ResolucionDeDestinoDeOrdenes.Foto(orden);
        orden.Form = PaymentForm.Check;
        orden.DestinationBankCode = null;
        orden.DestinationBankId = null;
        orden.DestinationAccountNumber = null;
        orden.DestinationAccountType = null;
        orden.PersonBankAccountId = null;
        await validador.ValidarAsync(orden, ct);
        var gmf = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, orden.BeneficiaryIsDebtor, false,
            orden.Origin == PaymentOrderOrigin.DepositsPayout),
            await parametros.GmfAsync(orden.PayDate, ct));
        orden.GmfExempt = gmf.Exento;
        orden.GmfExemptionReason = gmf.Evaluado ? gmf.Motivo : null;
        orden.GmfEstimate = gmf.Gravado ? gmf.Gmf : 0m;
        await auditoria.AgregarAsync(ResolucionDeDestinoDeOrdenes.EventoPasadaACheque, "PaymentOrder", orden.PublicId, antes,
            new { after = ResolucionDeDestinoDeOrdenes.Foto(orden), reason = request.Reason.Trim(), number = orden.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesDePago.DtoAsync(db, orden, ct));
    }
}
