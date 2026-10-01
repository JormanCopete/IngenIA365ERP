using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Cierra el saldo pendiente de recibir de una orden de compra (<c>POST /api/inventory/purchases/orders/{id}/close-balance</c>
/// <c>{ reason }</c>, <c>Inventory.Purchases.Confirm</c> → 204; feature 012, I5, T792; decisión del dueño adoptada; data-model §9.8;
/// contracts/api.md §14.9). (nuevo)
/// <list type="bullet">
/// <item>sólo una orden <c>Confirmed</c> con el saldo abierto; sin confirmar, anulada o ya cerrada:
/// <c>Inventory.PurchaseOrder.NotOpen</c>; fuera del alcance o de otra clase, el 404 del documento;</item>
/// <item>anota <c>BalanceClosedAt</c>, <c>BalanceClosedByUserId</c> y <c>BalanceClosedReason</c> (<c>InventoryDocument.CerrarSaldo</c>,
/// columnas mutables tras confirmar): desde ese momento la orden no admite recepciones y su saldo deja de contar como «por recibir»;</item>
/// <item>no mueve kardex ni emite mensajes; lo recibido y lo facturado no cambian;</item>
/// <item>bloquea la fila de la orden como documento de origen (el mismo cerrojo que toma una recepción contra ella): una recepción en
/// vuelo termina antes o ve el saldo cerrado;</item>
/// <item>queda en la auditoría con su motivo (<see cref="IConMotivo"/>, <c>AuditBehavior</c>).</item>
/// </list>
/// </summary>
public sealed record ClosePurchaseOrderBalanceCommand(Guid OrderPublicId, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ClosePurchaseOrderBalanceCommandValidator : ValidadorConMotivo<ClosePurchaseOrderBalanceCommand>;

public sealed class ClosePurchaseOrderBalanceCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    VistaDeDocumentos vista,
    ICerrojoDeInventario cerrojo)
    : IRequestHandler<ClosePurchaseOrderBalanceCommand, Result>
{
    public Task<Result> Handle(ClosePurchaseOrderBalanceCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => CerrarAsync(request, ct), ct);

    private async Task<Result> CerrarAsync(ClosePurchaseOrderBalanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure(ErroresDelDocumento.SinUsuario());

        var orden = await vista.BuscarAsync(request.OrderPublicId, DocumentClassGroup.Purchases, seguir: true, ct);
        if (orden is null || orden.Class != DocumentClass.PurchaseOrder) return Result.Failure(InventoryErrors.DocumentNotFound());

        await cerrojo.BloquearAsync(new PedidoDeCerrojo { DocumentosDeOrigen = [orden.Id] }, ct);
        // Con la fila bloqueada, lo que otra operación ya dejó (el mismo cierre pedido dos veces a la vez).
        var vigente = await db.InventoryDocuments.AsNoTracking().Where(d => d.Id == orden.Id)
            .Select(d => new { d.Status, d.BalanceClosedAt }).FirstAsync(ct);
        if (!orden.OrdenAbierta || vigente.Status != DocumentStatus.Confirmed || vigente.BalanceClosedAt is not null)
            return Result.Failure(ErroresDeCompras.OrderNotOpen(orden.PublicId, VistaDeDocumentos.NumeroVisible(orden.Prefix, orden.Number),
                vigente.Status, vigente.BalanceClosedAt ?? orden.BalanceClosedAt));

        orden.CerrarSaldo(usuario, reloj.UtcNow, request.Reason);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
