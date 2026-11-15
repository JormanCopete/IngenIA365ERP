using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Catalog.Products;

/// <summary>
/// Cómo trabaja la cooperativa las unidades de sus productos (<c>GET /api/inventory/products/unit-settings</c>; 2026-11-15): si
/// permite unidades alternas (<c>Productos.VariasUnidades</c>) y la unidad base que propone el alta
/// (<c>Productos.UnidadBasePorDefecto</c>; nula si su código no existe). Lo piden la ficha y el alta del producto para mostrar u
/// ocultar las alternas y preseleccionar la unidad, sin exigir el permiso de parámetros. (nuevo)
/// </summary>
public sealed record GetProductUnitSettingsQuery : IRequest<Result<ProductUnitSettingsDto>>;

/// <summary>No hay nada que validar: la consulta no lleva datos (Principio VIII, validador hermano).</summary>
public sealed class GetProductUnitSettingsQueryValidator : AbstractValidator<GetProductUnitSettingsQuery>;

public sealed record ProductUnitSettingsDto(bool MultipleUnitsAllowed, Guid? DefaultBaseUnitPublicId, string? DefaultBaseUnitCode);

public sealed class GetProductUnitSettingsQueryHandler(UnidadesDelProducto unidades, IDateTimeService reloj)
    : IRequestHandler<GetProductUnitSettingsQuery, Result<ProductUnitSettingsDto>>
{
    public async Task<Result<ProductUnitSettingsDto>> Handle(GetProductUnitSettingsQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var porDefecto = await unidades.UnidadBasePorDefectoAsync(hoy, ct);
        return Result.Success(new ProductUnitSettingsDto(await unidades.PermiteVariasAsync(hoy, ct), porDefecto?.PublicId, porDefecto?.Code));
    }
}
