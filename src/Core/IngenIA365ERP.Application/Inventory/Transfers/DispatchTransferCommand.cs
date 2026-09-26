using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Transfers;

/// <summary>
/// Despacha un traslado (feature 012, US10, T369; FR-039, US10-1; contracts/api.md §11, <c>POST /transfers/{id}/dispatch</c> con
/// <c>{ rowVersion }</c>, permiso <c>Inventory.Transfers.Dispatch</c>): el flujo canónico de <see cref="ConfirmacionDeDocumento"/>
/// con el grupo <c>Transfers</c> sobre un borrador de clase <c>TransferDispatch</c> —las reglas del despacho las pone
/// <c>EfectoDespachoDeTraslado</c>—. Con niveles de aprobación responde <c>PendingApproval</c> sin número. (nuevo)
/// </summary>
public sealed record DispatchTransferCommand(Guid TransferPublicId) : IRequest<Result<ConfirmationResultDto>>, IOperacionIdempotente
{
    public byte[]? RowVersion { get; init; }

    public Guid OperationKey { get; init; }
}

public sealed class DispatchTransferCommandValidator : AbstractValidator<DispatchTransferCommand>
{
    public DispatchTransferCommandValidator() => RuleFor(x => x.TransferPublicId).NotEmpty();
}

/// <summary>Una recepción no se despacha: sólo un borrador de despacho (si no, el 404 del documento). Todo en una transacción.</summary>
public sealed class DispatchTransferCommandHandler(IApplicationDbContext db, ConfirmacionDeDocumento confirmacion)
    : IRequestHandler<DispatchTransferCommand, Result<ConfirmationResultDto>>
{
    public Task<Result<ConfirmationResultDto>> Handle(DispatchTransferCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var clase = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == request.TransferPublicId)
                .Select(d => (DocumentClass?)d.Class).FirstOrDefaultAsync(ct);
            if (clase != DocumentClass.TransferDispatch) return Result.Failure<ConfirmationResultDto>(InventoryErrors.DocumentNotFound());
            return await confirmacion.ConfirmarAsync(
                new PedidoDeConfirmacion(request.TransferPublicId, DocumentClassGroup.Transfers, request.RowVersion), ct);
        }, ct);
}
