using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Assembly;
using IngenIA365ERP.Application.Lending.Deposits.Deceased;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

// Respuestas 92 y 96 del dueño (feature 014, E258): el giro rechazado de un excedente de la asamblea o de la entrega por fallecimiento se REGIRA. Mientras
// Tesorería no lo paga, el valor no vuelve a ninguna cuenta: sigue respaldado en la cuenta por pagar a favor del destinatario (los retornos por pagar de
// su tercero, o la devolución por pagar de la entrega) y la orden rechazada queda «pendiente de regiro». (nuevo)

/// <summary>La orden nueva de un regiro y la que reemplaza. (nuevo)</summary>
public sealed record PayoutOrderReissueDto(
    Guid OrderPublicId,
    string OrderNumber,
    decimal Amount,
    DisbursementOrderStatus Status,
    DateOnly ResultDueOn,
    Guid ReissuesOrderPublicId,
    string ReissuesOrderNumber);

/// <summary>
/// <c>POST /api/lending/assembly-distributions/{id}/orders/{orderId}/reissue</c> (<c>ReissueSurplusPayoutOrderCommand</c>, <c>Lending.Assembly.Approve</c>):
/// vuelve a girar la orden de un pago de excedentes que Tesorería rechazó, anuló o no se cobró, al mismo beneficiario y cuenta, o a los que se indiquen
/// (banco, tipo y número de cuenta). Una sola orden nueva por cada orden pendiente. (nuevo)
/// </summary>
public sealed record ReissueSurplusPayoutOrderCommand(
    Guid DistributionPublicId,
    Guid OrderPublicId,
    Guid? BankPublicId,
    byte? AccountType,
    string? AccountNumber,
    string Reason) : IRequest<Result<PayoutOrderReissueDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// <c>POST /api/lending/deceased-cases/{id}/orders/{orderId}/reissue</c> (<c>ReissueDeceasedPayoutOrderCommand</c>, <c>Lending.DeceasedCases.Approve</c>): lo
/// mismo para la orden de una entrega por fallecimiento. El GMF retenido en la entrega no se devuelve ni se vuelve a cobrar. (nuevo)
/// </summary>
public sealed record ReissueDeceasedPayoutOrderCommand(
    Guid CasePublicId,
    Guid OrderPublicId,
    Guid? BankPublicId,
    byte? AccountType,
    string? AccountNumber,
    string Reason) : IRequest<Result<PayoutOrderReissueDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReissueSurplusPayoutOrderCommandValidator : ValidadorConMotivo<ReissueSurplusPayoutOrderCommand>
{
    public ReissueSurplusPayoutOrderCommandValidator()
    {
        RuleFor(x => x.DistributionPublicId).NotEmpty();
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.AccountNumber).NotEmpty().When(x => x.BankPublicId.HasValue).WithMessage("La cuenta de destino lleva su número.");
        RuleFor(x => x.AccountNumber).MaximumLength(25);
    }
}

public sealed class ReissueDeceasedPayoutOrderCommandValidator : ValidadorConMotivo<ReissueDeceasedPayoutOrderCommand>
{
    public ReissueDeceasedPayoutOrderCommandValidator()
    {
        RuleFor(x => x.CasePublicId).NotEmpty();
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.AccountNumber).NotEmpty().When(x => x.BankPublicId.HasValue).WithMessage("La cuenta de destino lleva su número.");
        RuleFor(x => x.AccountNumber).MaximumLength(25);
    }
}

/// <summary>Lo común de los dos comandos: bloquea persona y cuenta (en ese orden), vuelve a girar y guarda. (nuevo)</summary>
public sealed class RegiroDeOrdenesDeCuentas(IApplicationDbContext db, OrdenesDeGiroDeCuentas ordenes, ICerrojoDeCartera cerrojo)
{
    public async Task<Result<PayoutOrderReissueDto>> RegirarAsync(int ordenId, Guid? banco, byte? tipo, string? numero, Func<AccountPayoutOrder, Task<(Guid Fuente, string Concepto)>> origen,
        CancellationToken ct) =>
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var previa = await db.AccountPayoutOrders.AsNoTracking().Where(o => o.Id == ordenId).Select(o => o.MemberAccountId).FirstAsync(ct);
            var titular = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == previa).Select(a => a.HolderPersonId).FirstAsync(ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([titular], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync([previa], ModoDeEspera.Esperar, ct);

            var original = await db.AccountPayoutOrders.FirstAsync(o => o.Id == ordenId, ct);
            BeneficiarioDeOrden? destino = null;
            if (banco is not null)
            {
                var resuelto = await ordenes.BeneficiarioAsync(original.BeneficiaryPersonId, banco, tipo, numero, ct);
                if (resuelto.IsFailure) return Result.Failure<PayoutOrderReissueDto>(resuelto.Error);
                destino = resuelto.Value;
            }
            var (fuente, concepto) = await origen(original);
            var nueva = await ordenes.RegirarAsync(original, destino, fuente, concepto, ct);
            if (nueva.IsFailure) return Result.Failure<PayoutOrderReissueDto>(nueva.Error);
            await db.SaveChangesAsync(ct);
            return Result.Success(new PayoutOrderReissueDto(nueva.Value.PublicId, nueva.Value.Number, nueva.Value.Amount, nueva.Value.Status, nueva.Value.ResultDueOn,
                original.PublicId, original.Number));
        }, ct);
}

public sealed class ReissueSurplusPayoutOrderCommandHandler(IApplicationDbContext db, DistribucionesDeAsamblea distribuciones, RegiroDeOrdenesDeCuentas regiro)
    : IRequestHandler<ReissueSurplusPayoutOrderCommand, Result<PayoutOrderReissueDto>>
{
    public async Task<Result<PayoutOrderReissueDto>> Handle(ReissueSurplusPayoutOrderCommand request, CancellationToken ct)
    {
        var leida = await distribuciones.LeerAsync(request.DistributionPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<PayoutOrderReissueDto>(leida.Error);
        var d = leida.Value;
        // La orden es de esta decisión: comparte la operación de alguna de sus líneas (un regiro comparte la de la orden que vuelve a girar).
        var ordenId = await (from o in db.AccountPayoutOrders.AsNoTracking()
                             join l in db.AssemblyDistributionLines.AsNoTracking() on o.OperationId equals l.OperationId
                             where o.PublicId == request.OrderPublicId && o.Purpose == PayoutPurpose.SurplusPayment && !o.IsDeleted
                                   && l.AssemblyDistributionId == d.Id && l.SimulationVersion == d.SimulationVersion
                             select (int?)o.Id).FirstOrDefaultAsync(ct);
        if (ordenId is not { } id) return Result.Failure<PayoutOrderReissueDto>(ErroresDeAsamblea.Inexistente());
        return await regiro.RegirarAsync(id, request.BankPublicId, request.AccountType, request.AccountNumber,
            original => Task.FromResult((d.PublicId, $"Excedentes {d.FiscalYear} {d.Number} (regiro de {original.Number})")), ct);
    }
}

public sealed class ReissueDeceasedPayoutOrderCommandHandler(IApplicationDbContext db, VistaDeFallecimientos vista, RegiroDeOrdenesDeCuentas regiro)
    : IRequestHandler<ReissueDeceasedPayoutOrderCommand, Result<PayoutOrderReissueDto>>
{
    public async Task<Result<PayoutOrderReissueDto>> Handle(ReissueDeceasedPayoutOrderCommand request, CancellationToken ct)
    {
        var leido = await vista.LeerAsync(request.CasePublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<PayoutOrderReissueDto>(leido.Error);
        var caso = leido.Value;
        var ordenId = await db.AccountPayoutOrders.AsNoTracking()
            .Where(o => o.PublicId == request.OrderPublicId && o.Purpose == PayoutPurpose.DeceasedDelivery && o.DeceasedHolderCaseId == caso.Id && !o.IsDeleted)
            .Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
        if (ordenId is not { } id) return Result.Failure<PayoutOrderReissueDto>(ErroresDeFallecimiento.Inexistente());
        return await regiro.RegirarAsync(id, request.BankPublicId, request.AccountType, request.AccountNumber, async original =>
        {
            var operacion = await db.MemberAccountOperations.AsNoTracking().Where(o => o.Id == original.OperationId).Select(o => o.PublicId).FirstAsync(ct);
            return (operacion, $"Entrega por fallecimiento, caso {caso.Number} (regiro de {original.Number})");
        }, ct);
    }
}
