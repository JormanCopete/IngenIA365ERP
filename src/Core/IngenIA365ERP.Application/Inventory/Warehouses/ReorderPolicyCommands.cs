using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Inventory.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

/// <summary>
/// Fijar el mínimo, el máximo y el punto de reorden de un producto en una bodega (feature 012, T225; contracts/api.md §4.4,
/// <c>PUT /api/inventory/reorder-policies</c>; FR-035): crea o cambia la fila viva de (producto, bodega), sin duplicar.
/// Exige <c>0 ≤ mínimo ≤ punto ≤ máximo</c> (<c>Inventory.ReorderPolicy.Invalid</c>), una bodega operativa del alcance
/// (tránsito: <c>.TransitNotAllowed</c>; fuera: 404) y un producto con existencias (<c>Inventory.Product.NotInventoriable</c>).
/// (nuevo)
/// </summary>
public sealed record SetReorderPolicyCommand(Guid ProductPublicId, Guid WarehousePublicId, decimal Minimum, decimal Maximum, decimal ReorderPoint)
    : IRequest<Result<ReorderPolicyDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetReorderPolicyCommandValidator : AbstractValidator<SetReorderPolicyCommand>
{
    public SetReorderPolicyCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.Minimum).Must(Cuatro).WithMessage("El mínimo admite hasta 4 decimales.");
        RuleFor(x => x.Maximum).Must(Cuatro).WithMessage("El máximo admite hasta 4 decimales.");
        RuleFor(x => x.ReorderPoint).Must(Cuatro).WithMessage("El punto de reorden admite hasta 4 decimales.");
    }

    private static bool Cuatro(decimal v) => ConversionDeUnidades.Decimales(v) <= 4;
}

public sealed class SetReorderPolicyCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcance)
    : IRequestHandler<SetReorderPolicyCommand, Result<ReorderPolicyDto>>
{
    public async Task<Result<ReorderPolicyDto>> Handle(SetReorderPolicyCommand request, CancellationToken ct)
    {
        var bodega = await BodegasDelAlcance.BuscarAsync(db, alcance, request.WarehousePublicId, ct);
        if (bodega is null) return Falla(WarehouseErrors.WarehouseNotFound());
        if (bodega.EsTransito) return Falla(WarehouseErrors.ReorderPolicyTransitNotAllowed());
        var producto = await db.Products.FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Falla(CatalogErrors.ProductNotFound());
        if (!producto.EsInventariable) return Falla(CatalogErrors.ProductNotInventoriable(producto.Code));
        if (!ReorderPolicy.EsCoherente(request.Minimum, request.ReorderPoint, request.Maximum))
            return Falla(WarehouseErrors.ReorderPolicyInvalid(request.Minimum, request.ReorderPoint, request.Maximum));

        var politica = await db.ReorderPolicies.FirstOrDefaultAsync(r => r.ProductId == producto.Id && r.WarehouseId == bodega.Id, ct);
        if (politica is null)
        {
            politica = new ReorderPolicy { ProductId = producto.Id, WarehouseId = bodega.Id };
            db.ReorderPolicies.Add(politica);
        }
        politica.Fijar(request.Minimum, request.ReorderPoint, request.Maximum);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReorderPolicyDto(politica.PublicId, new CatalogRefDto(producto.PublicId, producto.Code, producto.Name),
            new WarehouseRefDto(bodega.PublicId, bodega.Code, bodega.Name), politica.MinimumQuantity, politica.MaximumQuantity, politica.ReorderPoint,
            null, null));
    }

    private static Result<ReorderPolicyDto> Falla(Error e) => Result.Failure<ReorderPolicyDto>(e);
}

/// <summary>
/// Retirar una política de reorden (T225; §4.4, <c>DELETE /{id}</c>): baja lógica; un <c>PUT</c> posterior crea una fila
/// nueva (el único es filtrado a las vivas). Fuera del alcance o inexistente: <c>Inventory.ReorderPolicy.NotFound</c>. (nuevo)
/// </summary>
public sealed record DeleteReorderPolicyCommand(Guid ReorderPolicyPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteReorderPolicyCommandValidator : AbstractValidator<DeleteReorderPolicyCommand>
{
    public DeleteReorderPolicyCommandValidator() => RuleFor(x => x.ReorderPolicyPublicId).NotEmpty();
}

public sealed class DeleteReorderPolicyCommandHandler(IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<DeleteReorderPolicyCommand, Result>
{
    public async Task<Result> Handle(DeleteReorderPolicyCommand request, CancellationToken ct)
    {
        var politica = await db.ReorderPolicies.FirstOrDefaultAsync(r => r.PublicId == request.ReorderPolicyPublicId, ct);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (politica is null || !alcance.IncluyeBodega(politica.WarehouseId)) return Result.Failure(WarehouseErrors.ReorderPolicyNotFound());
        politica.IsDeleted = true;
        politica.DeletedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// Las políticas de reorden del alcance (T225; §4.4, <c>GET /reorder-policies</c>), paginadas, por bodega y producto.
/// <c>position</c> y <c>available</c> sólo con <c>Inventory.Stock.View</c>: los lee <c>PosicionDeReposicion</c> (US2, T257), el
/// único lector de la posición (disponible + en tránsito hacia la bodega + por recibir), y <see cref="BelowReorderPoint"/> deja
/// las que están en o bajo su punto de reorden. Filtra por <see cref="IAlcanceDeInventario"/>. (nuevo)
/// </summary>
public sealed record ListReorderPoliciesQuery(
    Guid? WarehousePublicId = null, Guid? ProductPublicId = null, bool? BelowReorderPoint = null, PageRequest? Pagina = null)
    : IRequest<Result<PagedResult<ReorderPolicyDto>>>;

public sealed class ListReorderPoliciesQueryValidator : AbstractValidator<ListReorderPoliciesQuery>;

public sealed class ListReorderPoliciesQueryHandler(
    IApplicationDbContext db, IAlcanceDeInventario alcanceDeLaPeticion, IPermissionChecker permisos, Replenishment.PosicionDeReposicion posiciones)
    : IRequestHandler<ListReorderPoliciesQuery, Result<PagedResult<ReorderPolicyDto>>>
{
    public async Task<Result<PagedResult<ReorderPolicyDto>>> Handle(ListReorderPoliciesQuery request, CancellationToken ct)
    {
        var pagina = request.Pagina ?? new PageRequest();
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.ReorderPolicies.AsNoTracking().PorBodega(alcance, r => r.WarehouseId);
        if (request.WarehousePublicId is { } wp) consulta = consulta.Where(r => r.Warehouse!.PublicId == wp);
        if (request.ProductPublicId is { } pp) consulta = consulta.Where(r => r.Product!.PublicId == pp);

        var filas = await consulta.Include(r => r.Product).Include(r => r.Warehouse)
            .OrderBy(r => r.Warehouse!.Code).ThenBy(r => r.Product!.Code).ToListAsync(ct);

        var conExistencias = await permisos.HasPermissionAsync(Catalog.Products.SearchProductsQueryHandler.VerExistencias, ct);
        var posicion = conExistencias
            ? await posiciones.LeerAsync(filas.Select(f => (f.ProductId, f.WarehouseId)).ToList(), ct)
            : new Dictionary<(int ProductId, int WarehouseId), Replenishment.Posicion>();

        var vistas = filas
            .Select(r => new ReorderPolicyDto(r.PublicId, new CatalogRefDto(r.Product!.PublicId, r.Product.Code, r.Product.Name),
                new WarehouseRefDto(r.Warehouse!.PublicId, r.Warehouse.Code, r.Warehouse.Name), r.MinimumQuantity, r.MaximumQuantity, r.ReorderPoint,
                conExistencias ? (posicion.GetValueOrDefault((r.ProductId, r.WarehouseId)) ?? Replenishment.Posicion.Cero).Valor : null,
                conExistencias ? (posicion.GetValueOrDefault((r.ProductId, r.WarehouseId)) ?? Replenishment.Posicion.Cero).Disponible : null))
            .Where(v => request.BelowReorderPoint != true || v.Position is { } p && p <= v.ReorderPoint)
            .ToList();

        var items = vistas.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        return Result.Success(new PagedResult<ReorderPolicyDto>(items, pagina.SafePage, pagina.SafePageSize, vistas.Count));
    }
}
