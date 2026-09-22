using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;

/// <summary>
/// Aprueba la prima de un semestre (feature 010, US1; contracts/api.md §3.1 <c>POST /{runId}/approve</c>)
/// por el ciclo común <see cref="SettlementRunWorkflow"/> con <c>Kind = ServiceBonus</c>: el comprobante
/// <c>NM</c> con <c>SourceType = ServiceBonusRun</c> cancela la provisión acumulada del empleado y lleva
/// la diferencia al gasto (<c>PRIMA_AJUSTE_PROV</c>), con la cuenta por pagar al empleado; se fecha al
/// corte del semestre salvo <c>postingDate</c> entre el corte y hoy (D-04, D-21). La prima no tiene nada
/// propio que dejar en la transacción: el gancho va vacío.
/// </summary>
public sealed record ApproveServiceBonusCommand(
    Guid RunPublicId,
    bool Confirm,
    DateOnly? PostingDate = null,
    bool ConfirmEmpty = false,
    bool ConfirmWithoutSegregation = false)
    : IRequest<Result<SettlementApprovedDto>>, IReintentableAnteConcurrencia;

public sealed class ApproveServiceBonusCommandValidator : AbstractValidator<ApproveServiceBonusCommand>
{
    public ApproveServiceBonusCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty().WithMessage("La corrida es obligatoria.");
    }
}

public sealed class ApproveServiceBonusCommandHandler(SettlementRunWorkflow workflow)
    : IRequestHandler<ApproveServiceBonusCommand, Result<SettlementApprovedDto>>
{
    public Task<Result<SettlementApprovedDto>> Handle(ApproveServiceBonusCommand request, CancellationToken ct) =>
        workflow.ApproveAsync(
            new SettlementApprovalRequest(request.RunPublicId, PayrollRunKind.ServiceBonus, request.Confirm, request.PostingDate, request.ConfirmEmpty, request.ConfirmWithoutSegregation),
            antesDeGuardar: null, ct);
}
