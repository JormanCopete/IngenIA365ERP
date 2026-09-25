using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Common.Approvals.WithdrawApprovalRequest;

/// <summary>
/// El solicitante o el creador retiran una solicitud pendiente (feature 012, T33, T085; contracts/api.md §15.2,
/// <c>POST /api/inventory/approvals/{id}/withdraw</c> → 204): queda <c>Cancelled</c> y lo aprobado vuelve a borrador
/// para corregirlo. Otro usuario: 404; no pendiente: <c>Approvals.Request.NotPending</c>. (nuevo)
/// </summary>
public sealed record WithdrawApprovalRequestCommand(Guid RequestPublicId, string Reason)
    : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class WithdrawApprovalRequestCommandValidator : ValidadorConMotivo<WithdrawApprovalRequestCommand>;

public sealed class WithdrawApprovalRequestCommandHandler(IMotorDeAprobaciones motor)
    : IRequestHandler<WithdrawApprovalRequestCommand, Result>
{
    public Task<Result> Handle(WithdrawApprovalRequestCommand request, CancellationToken ct) =>
        motor.RetirarAsync(request.RequestPublicId, request.Reason, ct);
}
