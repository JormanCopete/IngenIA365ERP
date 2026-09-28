using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Cash;

/// <summary>
/// El tipo de movimiento de caja que usa el sistema cuando el cuerpo de <c>POST /api/inventory/cash-movements</c> no trae
/// <c>documentTypePublicId</c> (contracts/api.md §21.3; feature 012, I3, T631): el primero activo de la clase <c>CashMovement</c>
/// (<see cref="TiposDeCaja.PorDefectoAsync"/>). Nulo si la cooperativa no tiene ninguno; el guardado responde entonces el error del tipo.
/// Así la ruta sólo reenvía al <see cref="ISender"/> y no conoce la base. (nuevo)
/// </summary>
public sealed record GetDefaultCashMovementTypeQuery : IRequest<Result<Guid?>>;

public sealed class GetDefaultCashMovementTypeQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDefaultCashMovementTypeQuery, Result<Guid?>>
{
    public async Task<Result<Guid?>> Handle(GetDefaultCashMovementTypeQuery request, CancellationToken ct) =>
        Result.Success((await TiposDeCaja.PorDefectoAsync(db, DocumentClass.CashMovement, ct))?.PublicId);
}
