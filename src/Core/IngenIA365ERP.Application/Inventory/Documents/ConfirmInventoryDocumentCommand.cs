using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Confirma un borrador desde la ruta de su grupo (feature 012, T146; contracts/api.md §9.3, <c>POST /{id}/confirm</c>
/// con <c>{ rowVersion }</c>): el flujo canónico de <see cref="ConfirmacionDeDocumento"/>. Con niveles de aprobación
/// responde <c>PendingApproval</c> sin número. (nuevo)
/// </summary>
public sealed record ConfirmInventoryDocumentCommand(Guid DocumentPublicId, DocumentClassGroup ExpectedGroup)
    : IRequest<Result<ConfirmationResultDto>>, IOperacionIdempotente
{
    public byte[]? RowVersion { get; init; }

    public Guid OperationKey { get; init; }
}

public sealed class ConfirmInventoryDocumentCommandValidator : AbstractValidator<ConfirmInventoryDocumentCommand>
{
    public ConfirmInventoryDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.ExpectedGroup).IsInEnum();
    }
}

/// <summary>Todo en una transacción (la de <c>IdempotencyBehavior</c> si la abrió; si no, una propia).</summary>
public sealed class ConfirmInventoryDocumentCommandHandler(IApplicationDbContext db, ConfirmacionDeDocumento confirmacion)
    : IRequestHandler<ConfirmInventoryDocumentCommand, Result<ConfirmationResultDto>>
{
    public Task<Result<ConfirmationResultDto>> Handle(ConfirmInventoryDocumentCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db,
            () => confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(request.DocumentPublicId, request.ExpectedGroup, request.RowVersion), ct),
            ct);
}
