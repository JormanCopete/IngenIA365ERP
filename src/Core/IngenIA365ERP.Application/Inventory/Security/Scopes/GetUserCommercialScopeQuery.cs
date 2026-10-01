using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Security.Scopes;

/// <summary>
/// El alcance comercial de un usuario (feature 012, T35, T090; contracts/api.md §16.3,
/// <c>GET /api/inventory/scopes/users/{userPublicId}</c>, <c>Inventory.Scopes.Manage</c>): si tiene alcance total por
/// permiso y las bodegas y puntos asignados que caen dentro del <see cref="IAlcanceDeInventario"/> de quien administra.
/// Usuario inexistente: 404. Lee por los puertos (<c>IAsignacionesDeBodega</c>, <c>IAsignacionesDePuntoDeVenta</c>).
/// (nuevo)
/// </summary>
public sealed record GetUserCommercialScopeQuery(Guid UserPublicId) : IRequest<Result<UserCommercialScopeDto>>;

public sealed class GetUserCommercialScopeQueryHandler(VistaDeAlcanceComercial vista)
    : IRequestHandler<GetUserCommercialScopeQuery, Result<UserCommercialScopeDto>>
{
    public Task<Result<UserCommercialScopeDto>> Handle(GetUserCommercialScopeQuery request, CancellationToken ct) =>
        vista.ArmarAsync(request.UserPublicId, ct);
}
