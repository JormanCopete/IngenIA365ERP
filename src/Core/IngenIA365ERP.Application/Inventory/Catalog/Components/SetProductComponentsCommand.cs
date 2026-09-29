using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Components;

/// <summary>Un componente pedido: el producto y la cantidad por unidad del combo o del kit, en la unidad base del componente. (nuevo)</summary>
public sealed record ComponentePedido(Guid ComponentProductPublicId, decimal Quantity);

/// <summary>Un componente de un combo o kit. (nuevo)</summary>
public sealed record ProductComponentDto(Guid PublicId, CatalogRefDto Component, ProductKind Kind, UnitRefDto BaseUnit, decimal Quantity);

/// <summary>Los componentes vigentes de un combo o kit (<c>GET/PUT /api/inventory/products/{id}/components</c>, T934). (nuevo)</summary>
public sealed record ProductComponentsDto(Guid ProductPublicId, string Code, ProductKind Kind, IReadOnlyList<ProductComponentDto> Components);

/// <summary>
/// Reemplazar los componentes de un combo o kit (feature 012, I6, T920; FR-023, US15-2, US15-3; data-model §1.11): la lista que
/// llega es la nueva; lo que ya estaba y sigue cambia su cantidad, lo que no viene queda de baja lógica y lo nuevo se agrega.
/// <see cref="ValidadorDeComponentes"/> revisa todo a la vez sobre el grafo de <c>INV_ProductComponents</c>: sólo un combo o un kit
/// lleva componentes (<c>Inventory.Component.NotAComboOrKit</c>) y al menos uno (<c>.Required</c>); un componente es inventariable
/// o variante (<c>.InvalidKind</c>), no se repite (<c>.Duplicate</c>), no forma un ciclo (<c>.Cycle</c>) y su cantidad es mayor
/// que cero con los decimales de su unidad base (<c>.InvalidQuantity</c>); el error lleva en <c>data.errors[]</c> todos los
/// hallados. El cambio rige para lo que se venda o se ensamble después (lo confirmado ya guardó sus líneas) y queda en la
/// auditoría como cambio de las filas de <c>INV_ProductComponents</c>. (nuevo)
/// </summary>
public sealed record SetProductComponentsCommand(Guid ProductPublicId, IReadOnlyList<ComponentePedido> Components)
    : IRequest<Result<ProductComponentsDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetProductComponentsCommandValidator : AbstractValidator<SetProductComponentsCommand>
{
    public SetProductComponentsCommandValidator()
    {
        RuleFor(x => x.ProductPublicId).NotEmpty();
        RuleFor(x => x.Components).NotNull();
        RuleForEach(x => x.Components).ChildRules(c => c.RuleFor(x => x.ComponentProductPublicId).NotEmpty());
    }
}

public sealed class SetProductComponentsCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<SetProductComponentsCommand, Result<ProductComponentsDto>>
{
    public async Task<Result<ProductComponentsDto>> Handle(SetProductComponentsCommand request, CancellationToken ct)
    {
        var producto = await db.Products.Include(p => p.BaseUnit).FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        if (producto is null) return Falla(CatalogErrors.ProductNotFound());

        var idsPedidos = request.Components.Select(c => c.ComponentProductPublicId).Distinct().ToList();
        var componentes = await db.Products.AsNoTracking().Include(p => p.BaseUnit)
            .Where(p => idsPedidos.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, ct);
        if (idsPedidos.FirstOrDefault(id => !componentes.ContainsKey(id)) is var faltante && faltante != Guid.Empty)
            return Falla(CatalogErrors.ProductNotFound());

        var resultado = ValidadorDeComponentes.Validar(await PedidoAsync(db, producto, request.Components, componentes, ct));
        if (!resultado.Valido)
        {
            var primero = resultado.Errores[0];
            return Falla(new ErrorConDatos(primero.Codigo, primero.Mensaje, new
            {
                errors = resultado.Errores.Select(e => new
                {
                    code = e.Codigo,
                    message = e.Mensaje,
                    componentProductPublicId = e.ComponentProductId is int id ? componentes.Values.FirstOrDefault(p => p.Id == id)?.PublicId : null,
                }).ToList(),
            }));
        }

        var vigentes = await db.ProductComponents.Where(c => c.ProductId == producto.Id).ToListAsync(ct);
        foreach (var retirado in vigentes.Where(v => request.Components.All(c => componentes[c.ComponentProductPublicId].Id != v.ComponentProductId)))
        {
            retirado.IsDeleted = true;
            retirado.DeletedAt = reloj.UtcNow;
        }
        foreach (var pedido in request.Components)
        {
            var componente = componentes[pedido.ComponentProductPublicId];
            var fila = vigentes.FirstOrDefault(v => v.ComponentProductId == componente.Id);
            if (fila is null) db.ProductComponents.Add(new ProductComponent { ProductId = producto.Id, ComponentProductId = componente.Id, Quantity = pedido.Quantity });
            else if (fila.Quantity != pedido.Quantity) fila.Quantity = pedido.Quantity;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeComponentes.ArmarAsync(db, producto, ct));
    }

    /// <summary>
    /// El pedido del validador: el producto, los componentes pedidos y el grafo vivo de combos y kits (sin las filas del propio
    /// producto, que la lista nueva reemplaza). Los Id del grafo son los de la base.
    /// </summary>
    public static async Task<PedidoDeComponentes> PedidoAsync(
        IApplicationDbContext db, Product producto, IReadOnlyList<ComponentePedido> pedidos, IReadOnlyDictionary<Guid, Product> componentes, CancellationToken ct)
    {
        var aristas = await db.ProductComponents.AsNoTracking().Where(c => c.ProductId != producto.Id)
            .Select(c => new { c.ProductId, c.ComponentProductId }).ToListAsync(ct);
        var grafo = aristas.GroupBy(a => a.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(a => a.ComponentProductId).ToList());
        var productos = new Dictionary<int, ProductoDelGrafo> { [producto.Id] = Nodo(producto) };
        foreach (var c in componentes.Values) productos[c.Id] = Nodo(c);
        return new PedidoDeComponentes(producto.Id,
            pedidos.Select(p => new ComponentePropuesto(componentes[p.ComponentProductPublicId].Id, p.Quantity)).ToList(), productos, grafo);
    }

    public static ProductoDelGrafo Nodo(Product p) => new(p.Id, p.Code, p.Kind, p.BaseUnit?.AllowedDecimals ?? 0);

    private static Result<ProductComponentsDto> Falla(Error error) => Result.Failure<ProductComponentsDto>(error);
}

/// <summary>Los componentes vigentes de un combo o kit (T920; <c>GET /products/{id}/components</c>). (nuevo)</summary>
public sealed record GetProductComponentsQuery(Guid ProductPublicId) : IRequest<Result<ProductComponentsDto>>;

public sealed class GetProductComponentsQueryValidator : AbstractValidator<GetProductComponentsQuery>
{
    public GetProductComponentsQueryValidator() => RuleFor(x => x.ProductPublicId).NotEmpty();
}

public sealed class GetProductComponentsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductComponentsQuery, Result<ProductComponentsDto>>
{
    public async Task<Result<ProductComponentsDto>> Handle(GetProductComponentsQuery request, CancellationToken ct)
    {
        var producto = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId, ct);
        return producto is null
            ? Result.Failure<ProductComponentsDto>(CatalogErrors.ProductNotFound())
            : Result.Success(await VistaDeComponentes.ArmarAsync(db, producto, ct));
    }
}

/// <summary>Cómo se arma el <see cref="ProductComponentsDto"/>: los componentes vivos por código. (nuevo)</summary>
public static class VistaDeComponentes
{
    public static async Task<ProductComponentsDto> ArmarAsync(IApplicationDbContext db, Product producto, CancellationToken ct)
    {
        var filas = await db.ProductComponents.AsNoTracking()
            .Include(c => c.ComponentProduct).ThenInclude(p => p!.BaseUnit)
            .Where(c => c.ProductId == producto.Id).ToListAsync(ct);
        return new ProductComponentsDto(producto.PublicId, producto.Code, producto.Kind,
            filas.OrderBy(f => f.ComponentProduct!.Code, StringComparer.Ordinal).Select(f => new ProductComponentDto(
                f.PublicId,
                new CatalogRefDto(f.ComponentProduct!.PublicId, f.ComponentProduct.Code, f.ComponentProduct.Name),
                f.ComponentProduct.Kind,
                Products.VistaDeProductos.Unidad(f.ComponentProduct.BaseUnit!),
                f.Quantity)).ToList());
    }
}
