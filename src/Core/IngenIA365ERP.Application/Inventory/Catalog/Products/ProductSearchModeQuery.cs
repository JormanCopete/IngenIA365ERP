using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// 2026-11-12 (decisión del dueño): cómo se busca un producto en las pantallas que lo registran, el parámetro
/// <c>Comercial.BusquedaDeProductos</c> vigente hoy (<c>GET /api/inventory/products/search-mode</c>). Es la misma pregunta para
/// quien vende en el POS que para quien compra: la responde cualquiera que pueda buscar productos, sin el permiso de ver
/// parámetros. Se lee por el único lector (<see cref="ILectorDeParametros"/>), con su defecto seguro «VentanaF2». (nuevo)
/// </summary>
public sealed record GetProductSearchModeQuery : IRequest<Result<ProductSearchModeDto>>;

/// <summary>El modo: <c>VentanaF2</c> o <c>EnElCampo</c>.</summary>
public sealed record ProductSearchModeDto(string Mode);

/// <summary>No hay campos que validar; existe porque todo pedido del catálogo nace con su validador (Principio VIII).</summary>
public sealed class GetProductSearchModeQueryValidator : AbstractValidator<GetProductSearchModeQuery>
{
}

public sealed class GetProductSearchModeQueryHandler(ILectorDeParametros lector, IDateTimeService reloj)
    : IRequestHandler<GetProductSearchModeQuery, Result<ProductSearchModeDto>>
{
    public async Task<Result<ProductSearchModeDto>> Handle(GetProductSearchModeQuery request, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComercialBusquedaDeProductos, reloj.HoyLocal, ct: ct);
        return leido.IsFailure
            ? Result.Failure<ProductSearchModeDto>(leido.Error)
            : Result.Success(new ProductSearchModeDto(leido.Value.Texto));
    }
}
