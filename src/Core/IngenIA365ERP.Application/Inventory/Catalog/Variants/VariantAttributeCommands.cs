using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Catalog.Variants;

/// <summary>Un valor de un atributo de variante: código (<c>CodigoDeCatalogo</c>, 10), nombre y orden en la matriz. (nuevo)</summary>
public sealed record VariantAttributeValueDto(Guid PublicId, string Code, string Name, int SortOrder);

/// <summary>Un atributo de variante —talla, color— con sus valores vivos por orden y cuántas variantes lo usan. (nuevo)</summary>
public sealed record VariantAttributeDto(Guid PublicId, string Code, string Name, bool IsActive, IReadOnlyList<VariantAttributeValueDto> Values, int VariantsUsing);

/// <summary>Un valor pedido para el atributo. (nuevo)</summary>
public sealed record ValorDeAtributoPedido(string Code, string Name, int SortOrder = 0);

/// <summary>
/// Crear o cambiar un atributo de variante con sus valores (feature 012, I6, T919; FR-023, US15-1; data-model §1.11):
/// <see cref="AttributePublicId"/> nulo crea; si viene, cambia ese atributo. El código del atributo y los de sus valores siguen
/// <c>CodigoDeCatalogo</c> (10, mayúsculas, únicos: <c>Catalogo.CodigoDuplicado</c>); un valor repetido en la lista es
/// <c>Inventory.VariantAttribute.ValueDuplicate</c>. Los valores se identifican por código: los que vienen se crean o se
/// renombran y reordenan; los que no vienen se retiran (baja lógica). Un código que usan variantes —el del atributo o el de un
/// valor— no cambia ni se retira, porque está en su <c>VariantKey</c> (<c>Inventory.VariantAttribute.InUse</c>); el nombre sí
/// cambia, y el atributo se puede inactivar (no se ofrece para generar variantes nuevas). (nuevo)
/// </summary>
public sealed record SaveVariantAttributeCommand(
    Guid? AttributePublicId,
    string Code,
    string Name,
    IReadOnlyList<ValorDeAtributoPedido> Values,
    bool IsActive = true)
    : IRequest<Result<VariantAttributeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveVariantAttributeCommandValidator : AbstractValidator<SaveVariantAttributeCommand>
{
    public SaveVariantAttributeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(VariantAttribute.LargoDelCodigo)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(VariantAttribute.LargoDelNombre);
        RuleFor(x => x.Values).NotEmpty().WithMessage("El atributo necesita al menos un valor.");
        RuleForEach(x => x.Values).ChildRules(v =>
        {
            v.RuleFor(x => x.Code).NotEmpty().MaximumLength(VariantAttribute.LargoDelCodigo)
                .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
            v.RuleFor(x => x.Name).NotEmpty().MaximumLength(VariantAttribute.LargoDelNombre);
        });
    }
}

public sealed class SaveVariantAttributeCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<SaveVariantAttributeCommand, Result<VariantAttributeDto>>
{
    public async Task<Result<VariantAttributeDto>> Handle(SaveVariantAttributeCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        VariantAttribute? atributo = null;
        if (request.AttributePublicId is { } id)
        {
            atributo = await db.VariantAttributes.Include(a => a.Values).FirstOrDefaultAsync(a => a.PublicId == id, ct);
            if (atributo is null) return Falla(CatalogErrors.VariantAttributeNotFound());
        }

        var otro = await db.VariantAttributes.Where(a => a.Code == codigo && (atributo == null || a.Id != atributo.Id))
            .Select(a => new { a.PublicId, a.Name }).FirstOrDefaultAsync(ct);
        if (otro is not null) return Falla(CodigoDeCatalogo.Duplicado("un atributo de variante", codigo, otro.Name, otro.PublicId));

        var pedidos = request.Values.Select(v => v with { Code = CodigoDeCatalogo.Normalizar(v.Code)!, Name = v.Name.Trim() }).ToList();
        if (pedidos.GroupBy(v => v.Code, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            return Falla(CatalogErrors.VariantAttributeValueDuplicate(repetido.Key));

        if (atributo is null)
        {
            atributo = new VariantAttribute { Code = codigo };
            db.VariantAttributes.Add(atributo);
        }
        else
        {
            // Lo que usan las variantes vivas: el código del atributo y el de cada valor viajan en su VariantKey.
            var usados = await db.ProductVariantValues.Where(v => v.VariantAttributeId == atributo.Id)
                .GroupBy(v => v.VariantAttributeValueId).Select(g => new { g.Key, Cuantas = g.Count() }).ToListAsync(ct);
            if (atributo.Code != codigo && usados.Count > 0)
                return Falla(CatalogErrors.VariantAttributeInUse(atributo.Code, usados.Sum(u => u.Cuantas)));
            var vivos = atributo.Values.Where(v => !v.IsDeleted).ToList();
            foreach (var retirado in vivos.Where(v => pedidos.All(p => p.Code != v.Code)))
            {
                if (usados.FirstOrDefault(u => u.Key == retirado.Id) is { } uso)
                    return Falla(CatalogErrors.VariantAttributeInUse($"{atributo.Code}={retirado.Code}", uso.Cuantas));
                retirado.IsDeleted = true;
                retirado.DeletedAt = reloj.UtcNow;
            }
            atributo.Code = codigo;
        }

        atributo.Name = request.Name.Trim();
        atributo.IsActive = request.IsActive;
        foreach (var pedido in pedidos)
        {
            var valor = atributo.Values.FirstOrDefault(v => !v.IsDeleted && v.Code == pedido.Code);
            if (valor is null)
            {
                valor = new VariantAttributeValue { VariantAttribute = atributo, Code = pedido.Code };
                atributo.Values.Add(valor);
            }
            valor.Name = pedido.Name;
            valor.SortOrder = pedido.SortOrder;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(await VistaDeAtributos.ArmarAsync(db, atributo.Id, ct));
    }

    private static Result<VariantAttributeDto> Falla(Error error) => Result.Failure<VariantAttributeDto>(error);
}

/// <summary>Los atributos de variante (T919), por código, con sus valores; sólo activos salvo <see cref="IncludeInactive"/>. (nuevo)</summary>
public sealed record ListVariantAttributesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<VariantAttributeDto>>>;

public sealed class ListVariantAttributesQueryValidator : AbstractValidator<ListVariantAttributesQuery>;

public sealed class ListVariantAttributesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListVariantAttributesQuery, Result<IReadOnlyList<VariantAttributeDto>>>
{
    public async Task<Result<IReadOnlyList<VariantAttributeDto>>> Handle(ListVariantAttributesQuery request, CancellationToken ct)
    {
        var atributos = await db.VariantAttributes.AsNoTracking().Include(a => a.Values)
            .Where(a => request.IncludeInactive || a.IsActive).OrderBy(a => a.Code).ToListAsync(ct);
        var ids = atributos.Select(a => a.Id).ToList();
        var usos = await VistaDeAtributos.UsosAsync(db, ids, ct);
        return Result.Success<IReadOnlyList<VariantAttributeDto>>(atributos.Select(a => VistaDeAtributos.Dto(a, usos.GetValueOrDefault(a.Id))).ToList());
    }
}

/// <summary>Cómo se arma el <see cref="VariantAttributeDto"/>. (nuevo)</summary>
public static class VistaDeAtributos
{
    public static async Task<VariantAttributeDto> ArmarAsync(IApplicationDbContext db, int atributoId, CancellationToken ct)
    {
        var atributo = await db.VariantAttributes.AsNoTracking().Include(a => a.Values).FirstAsync(a => a.Id == atributoId, ct);
        var usos = await UsosAsync(db, [atributoId], ct);
        return Dto(atributo, usos.GetValueOrDefault(atributoId));
    }

    public static async Task<Dictionary<int, int>> UsosAsync(IApplicationDbContext db, IReadOnlyCollection<int> atributos, CancellationToken ct) =>
        (await db.ProductVariantValues.Where(v => atributos.Contains(v.VariantAttributeId))
            .GroupBy(v => v.VariantAttributeId).Select(g => new { g.Key, Cuantas = g.Count() }).ToListAsync(ct))
        .ToDictionary(x => x.Key, x => x.Cuantas);

    public static VariantAttributeDto Dto(VariantAttribute a, int variantes) => new(
        a.PublicId, a.Code, a.Name, a.IsActive,
        a.Values.Where(v => !v.IsDeleted).OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal)
            .Select(v => new VariantAttributeValueDto(v.PublicId, v.Code, v.Name, v.SortOrder)).ToList(),
        variantes);
}
