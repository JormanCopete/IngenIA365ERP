using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;

/// <summary>
/// Descarta el borrador de una prima (contracts/api.md §3.1 <c>POST /{runId}/discard</c>, mismo permiso
/// que calcular): queda <c>Superseded</c> con <c>DiscardedAt/By/Reason</c>, sin contabilidad; se puede
/// calcular de nuevo.
/// </summary>
public sealed record DiscardServiceBonusCommand(Guid RunPublicId, string Reason) : IRequest<Result<SettlementDiscardedDto>>;

public sealed class DiscardServiceBonusCommandValidator : AbstractValidator<DiscardServiceBonusCommand>
{
    public DiscardServiceBonusCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty().WithMessage("La corrida es obligatoria.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique por qué se descarta el borrador.").MaximumLength(300);
    }
}

public sealed class DiscardServiceBonusCommandHandler(SettlementRunWorkflow workflow)
    : IRequestHandler<DiscardServiceBonusCommand, Result<SettlementDiscardedDto>>
{
    public Task<Result<SettlementDiscardedDto>> Handle(DiscardServiceBonusCommand request, CancellationToken ct) =>
        workflow.DiscardAsync(request.RunPublicId, PayrollRunKind.ServiceBonus, request.Reason, antesDeGuardar: null, ct);
}
