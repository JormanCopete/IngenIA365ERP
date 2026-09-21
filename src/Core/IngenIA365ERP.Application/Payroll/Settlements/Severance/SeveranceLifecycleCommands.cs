using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.Settlements.Severance;

/// <summary>
/// Aprueba la liquidación de cesantías e intereses (contracts/api.md §3.2 <c>approve</c>) por el
/// ciclo común (<see cref="SettlementRunWorkflow"/>) con <c>Kind = Severance</c>: el comprobante
/// <c>SeveranceRun</c> cancela las provisiones de cesantías e intereses y deja la cuenta por pagar
/// <b>al fondo</b> por <c>CESANTIAS</c> (tercero: la persona del fondo, FR-088) y <b>al empleado</b>
/// por <c>INT_CESANTIAS</c> (FR-011); eso lo decide <c>SettlementAccountingPoster.TerceroPara</c>.
/// <see cref="PayDate"/> es la fecha en que se pagan los intereses al empleado (la ley da hasta el
/// 31 de enero siguiente); por defecto la del comprobante. Las cesantías no se «pagan» aquí: se
/// consignan al fondo y eso se registra por fondo con <c>mark-deposited</c>. Reintentable ante concurrencia
/// como la prima (feature 009, R3): el consecutivo <c>NM</c> se toma en la misma escritura y una carrera con
/// otra aprobación se repite entera en vez de salir 409.
/// </summary>
public sealed record ApproveSeveranceCommand(
    Guid RunPublicId,
    bool Confirm,
    DateOnly? PostingDate = null,
    DateOnly? PayDate = null,
    bool ConfirmEmpty = false,
    bool ConfirmWithoutSegregation = false) : IRequest<Result<SettlementApprovedDto>>, IReintentableAnteConcurrencia;

public sealed class ApproveSeveranceCommandValidator : AbstractValidator<ApproveSeveranceCommand>
{
    public ApproveSeveranceCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ApproveSeveranceCommandHandler(SettlementRunWorkflow workflow)
    : IRequestHandler<ApproveSeveranceCommand, Result<SettlementApprovedDto>>
{
    public Task<Result<SettlementApprovedDto>> Handle(ApproveSeveranceCommand request, CancellationToken ct)
    {
        var peticion = new SettlementApprovalRequest(request.RunPublicId, PayrollRunKind.Severance, request.Confirm,
            request.PostingDate, request.ConfirmEmpty, request.ConfirmWithoutSegregation);

        // Lo propio de las cesantías, en la misma transacción: la fecha de pago de los intereses.
        SettlementRunWorkflow.Gancho? fechaDePago = request.PayDate is null ? null : (run, _, _) =>
        {
            var corte = run.CutoffDate!.Value;
            if (request.PayDate.Value < corte)
                return Task.FromResult(Result.Failure(SeveranceErrors.PayDateBeforeCutoff(request.PayDate.Value, corte)));
            run.PayDate = request.PayDate;
            return Task.FromResult(Result.Success());
        };

        return workflow.ApproveAsync(peticion, fechaDePago, ct);
    }
}

/// <summary>Reversa la liquidación aprobada con asiento espejo (FR-032); la consignación ya marcada a un fondo se conserva como historial (data-model §2.7a).</summary>
public sealed record ReverseSeveranceCommand(Guid RunPublicId, string Reason) : IRequest<Result<SettlementReversedDto>>, IReintentableAnteConcurrencia;

public sealed class ReverseSeveranceCommandValidator : AbstractValidator<ReverseSeveranceCommand>
{
    public ReverseSeveranceCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo de la reversión.").MaximumLength(300);
    }
}

public sealed class ReverseSeveranceCommandHandler(SettlementRunWorkflow workflow)
    : IRequestHandler<ReverseSeveranceCommand, Result<SettlementReversedDto>>
{
    public Task<Result<SettlementReversedDto>> Handle(ReverseSeveranceCommand request, CancellationToken ct) =>
        workflow.ReverseAsync(request.RunPublicId, PayrollRunKind.Severance, request.Reason, null, ct);
}

/// <summary>Descarta el borrador: queda <c>Superseded</c> con quién, cuándo y por qué; sin contabilidad.</summary>
public sealed record DiscardSeveranceCommand(Guid RunPublicId, string Reason) : IRequest<Result<SettlementDiscardedDto>>;

public sealed class DiscardSeveranceCommandValidator : AbstractValidator<DiscardSeveranceCommand>
{
    public DiscardSeveranceCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique por qué se descarta el borrador.").MaximumLength(300);
    }
}

public sealed class DiscardSeveranceCommandHandler(SettlementRunWorkflow workflow)
    : IRequestHandler<DiscardSeveranceCommand, Result<SettlementDiscardedDto>>
{
    public Task<Result<SettlementDiscardedDto>> Handle(DiscardSeveranceCommand request, CancellationToken ct) =>
        workflow.DiscardAsync(request.RunPublicId, PayrollRunKind.Severance, request.Reason, null, ct);
}
