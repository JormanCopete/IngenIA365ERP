using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;

/// <summary>
/// Reversa una prima aprobada (contracts/api.md §3.1 <c>POST /{runId}/reverse</c>): asiento espejo por el
/// contrato contable, el saldo inicial que consumió vuelve a ser editable y la corrida queda
/// <c>Reversed</c> con quién, cuándo y por qué; se puede liquidar de nuevo. Con una marca de pago
/// vigente no se reversa (<c>Payroll.PaymentBlocksReversal</c>): primero se retira la marca, con motivo.
/// </summary>
public sealed record ReverseServiceBonusCommand(Guid RunPublicId, string Reason)
    : IRequest<Result<SettlementReversedDto>>, IReintentableAnteConcurrencia;

public sealed class ReverseServiceBonusCommandValidator : AbstractValidator<ReverseServiceBonusCommand>
{
    public ReverseServiceBonusCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty().WithMessage("La corrida es obligatoria.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo de la reversión.").MaximumLength(500);
    }
}

public sealed class ReverseServiceBonusCommandHandler(SettlementRunWorkflow workflow)
    : IRequestHandler<ReverseServiceBonusCommand, Result<SettlementReversedDto>>
{
    public Task<Result<SettlementReversedDto>> Handle(ReverseServiceBonusCommand request, CancellationToken ct) =>
        workflow.ReverseAsync(request.RunPublicId, PayrollRunKind.ServiceBonus, request.Reason, antesDeGuardar: null, ct);
}
